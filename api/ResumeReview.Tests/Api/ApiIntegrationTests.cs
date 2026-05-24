using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ResumeReview.Infrastructure.Persistence;
using ResumeReview.Tests.TestHelpers;

namespace ResumeReview.Tests.Api;

public class ApiIntegrationTests : IClassFixture<ApiIntegrationTests.SqliteFactory>
{
    private readonly SqliteFactory _factory;

    public ApiIntegrationTests(SqliteFactory factory) => _factory = factory;

    [Fact]
    public async Task Health_endpoint_returns_ok()
    {
        var client = _factory.CreateClient();
        var resp = await client.GetAsync("/health");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Upload_then_list_then_review_then_report_round_trip()
    {
        var client = _factory.CreateClient();

        using var docx = TestDocxBuilder.BuildDocx();
        using var content = new MultipartFormDataContent();
        using var fileContent = new StreamContent(docx);
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/vnd.openxmlformats-officedocument.wordprocessingml.document");
        content.Add(fileContent, "file", "ada.docx");

        var upload = await client.PostAsync("/api/resumes/upload", content);
        upload.StatusCode.Should().Be(HttpStatusCode.Accepted);

        var listing = await client.GetFromJsonAsync<List<CandidateRow>>("/api/candidates");
        listing!.Should().HaveCount(1);
        var candidateId = listing[0].id;

        // Wait briefly for background enrichment to complete (stub is fast).
        for (var i = 0; i < 20; i++)
        {
            var detail = await client.GetFromJsonAsync<DetailRow>($"/api/candidates/{candidateId}");
            if (detail?.aiFields is { } ai && ai.enrichmentStatus == "Completed")
            {
                ai.aiSummary.Should().NotBeNullOrEmpty();
                break;
            }
            await Task.Delay(100);
        }

        // Mark reviewed
        var review = await client.PostAsJsonAsync(
            $"/api/candidates/{candidateId}/review", new { status = "Reviewed", updatedBy = "tester" });
        review.StatusCode.Should().Be(HttpStatusCode.OK);

        // Regenerate one AI field
        var regen = await client.PostAsJsonAsync(
            $"/api/candidates/{candidateId}/ai-fields/AiSummary/regenerate",
            new { extraInstructions = "make it concise", requestedBy = "tester" });
        regen.StatusCode.Should().Be(HttpStatusCode.OK);

        // Report includes the reviewed candidate
        var report = await client.GetFromJsonAsync<List<DetailRow>>("/api/report");
        report!.Should().ContainSingle(r => r.id == candidateId);
    }

    public record CandidateRow(Guid id, string fullName, string email);
    public record AiSummaryRow(string? aiSummary, string enrichmentStatus);
    public record DetailRow(Guid id, AiSummaryRow? aiFields);

    public class SqliteFactory : WebApplicationFactory<Program>
    {
        private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"resumereview-tests-{Guid.NewGuid():N}.db");

        protected override IHost CreateHost(IHostBuilder builder)
        {
            var connStr = $"DataSource={_dbPath}";
            builder.ConfigureHostConfiguration(c => c.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:ResumeReviewDb"] = connStr,
                ["AzureAi:UseStub"] = "true",
                ["Worker:MaxConcurrentEnrichments"] = "2",
                ["Worker:RetryMaxAttempts"] = "1",
                ["Worker:RetryBaseDelaySeconds"] = "0"
            }));
            return base.CreateHost(builder);
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureServices(services =>
            {
                var sp = services.BuildServiceProvider();
                using var scope = sp.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<ResumeReviewDbContext>();
                db.Database.EnsureCreated();
            });
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (disposing && File.Exists(_dbPath))
            {
                try { File.Delete(_dbPath); } catch { /* ignore */ }
            }
        }
    }
}

internal static class ServiceCollectionRemoveExt
{
    public static IServiceCollection RemoveAll<T>(this IServiceCollection services)
    {
        for (var i = services.Count - 1; i >= 0; i--)
            if (services[i].ServiceType == typeof(T)) services.RemoveAt(i);
        return services;
    }
}

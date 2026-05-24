using DotNetEnv;
using ResumeReview.Api;
using ResumeReview.Api.Endpoints;
using ResumeReview.Api.Infrastructure;
using ResumeReview.Infrastructure;

// Load .env file (works for `dotnet run` and tests).
var envFile = Path.Combine(AppContext.BaseDirectory, ".env");
if (!File.Exists(envFile))
{
    var local = Path.Combine(Directory.GetCurrentDirectory(), ".env");
    if (File.Exists(local)) envFile = local;
    else
    {
        // walk up to find /api/.env
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            var probe = Path.Combine(dir.FullName, ".env");
            if (File.Exists(probe)) { envFile = probe; break; }
            dir = dir.Parent;
        }
    }
}
if (File.Exists(envFile))
{
    Env.Load(envFile);
}

if (args.Length > 0 && args[0].Equals("seed", StringComparison.OrdinalIgnoreCase))
{
    await SeedRunner.RunAsync(args);
    return;
}

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddEnvironmentVariables();

EnvValidation.RequireKeys(builder.Configuration,
    "ConnectionStrings:ResumeReviewDb");

builder.Services.AddResumeReviewInfrastructure(builder.Configuration);
builder.Services.AddHostedService<ResumeReview.Worker.EnrichmentWorker>();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi();

builder.Services.AddCors(o => o.AddDefaultPolicy(p =>
    p.AllowAnyHeader().AllowAnyMethod().AllowAnyOrigin()));

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseCors();
app.UseErrorHandling();

app.MapCandidateEndpoints();
app.MapIngestionEndpoints();
app.MapReportEndpoints();
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

// Apply migrations on startup in Development to make first-run smooth.
if (!builder.Environment.IsEnvironment("Testing"))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<ResumeReview.Infrastructure.Persistence.ResumeReviewDbContext>();
    try { db.Database.EnsureCreated(); } catch { /* ignore in environments where DB isn't reachable */ }
}

app.Run();

public partial class Program { }

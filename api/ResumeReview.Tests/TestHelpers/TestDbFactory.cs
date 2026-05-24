using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using ResumeReview.Infrastructure.Persistence;

namespace ResumeReview.Tests.TestHelpers;

public sealed class TestDbContext : IDisposable
{
    private readonly SqliteConnection _connection;
    public ResumeReviewDbContext Db { get; }

    public TestDbContext()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        var options = new DbContextOptionsBuilder<ResumeReviewDbContext>()
            .UseSqlite(_connection)
            .AddInterceptors(new AuditInterceptor())
            .Options;
        Db = new ResumeReviewDbContext(options);
        Db.Database.EnsureCreated();
    }

    public void Dispose()
    {
        Db.Dispose();
        _connection.Dispose();
    }
}

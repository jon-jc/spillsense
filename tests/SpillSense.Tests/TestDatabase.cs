using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SpillSense.Infrastructure.Persistence;

namespace SpillSense.Tests;

/// <summary>
/// An isolated in-memory SQLite database that runs the real migrations,
/// so tests exercise the actual schema including indexes and constraints.
/// </summary>
public sealed class TestDatabase : IDisposable
{
    private readonly SqliteConnection _connection;

    public TestDatabase()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<SpillSenseDbContext>()
            .UseSqlite(_connection)
            .Options;

        Context = new SpillSenseDbContext(options);
        Context.Database.Migrate();
    }

    public SpillSenseDbContext Context { get; }

    public void Dispose()
    {
        Context.Dispose();
        _connection.Dispose();
    }
}

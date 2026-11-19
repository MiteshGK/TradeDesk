using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using TradeDesk.Core.Data;

namespace TradeDesk.Tests;

public sealed class FixedClock(DateTimeOffset now) : TimeProvider
{
    public DateTimeOffset Now { get; set; } = now;
    public override DateTimeOffset GetUtcNow() => Now;
    public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
}

/// <summary>A fresh in-memory SQLite database per test (real SQL, no mocks).</summary>
public sealed class TestDb : IDisposable
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    public TradeDbContext Context { get; }

    public TestDb()
    {
        _connection.Open();
        Context = new TradeDbContext(new DbContextOptionsBuilder<TradeDbContext>().UseSqlite(_connection).Options);
        Context.Database.EnsureCreated();
    }

    public void Dispose()
    {
        Context.Dispose();
        _connection.Dispose();
    }
}

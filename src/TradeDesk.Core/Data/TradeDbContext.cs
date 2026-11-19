using Microsoft.EntityFrameworkCore;
using TradeDesk.Core.Domain;

namespace TradeDesk.Core.Data;

public class TradeDbContext(DbContextOptions<TradeDbContext> options) : DbContext(options)
{
    public DbSet<Trade> Trades => Set<Trade>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Trade>(e =>
        {
            e.HasIndex(t => t.TradeRef).IsUnique();
            e.Property(t => t.TradeRef).HasMaxLength(20).IsRequired();
            e.Property(t => t.Counterparty).HasMaxLength(100).IsRequired();
            e.Property(t => t.Principal).HasPrecision(18, 2);
            e.Property(t => t.RatePercent).HasPrecision(9, 4);
            e.Property(t => t.Side).HasConversion<string>().HasMaxLength(10);
            e.Property(t => t.Status).HasConversion<string>().HasMaxLength(10);
            e.Ignore(t => t.TenorDays);
            e.Ignore(t => t.Interest);
            e.Ignore(t => t.MaturityAmount);
        });
    }
}

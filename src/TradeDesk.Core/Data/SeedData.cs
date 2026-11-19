using TradeDesk.Core.Domain;

namespace TradeDesk.Core.Data;

public static class SeedData
{
    public static void Ensure(TradeDbContext db, DateOnly today)
    {
        if (db.Trades.Any()) return;

        var rows = new (string Cpty, TradeSide Side, decimal Principal, decimal Rate, int StartOffset, int Tenor, TradeStatus Status)[]
        {
            ("Alpha Bank",        TradeSide.Lend,   250_000_000m, 6.45m, -3, 7,  TradeStatus.Settled),
            ("Beacon Mutual Fund",TradeSide.Lend,   120_000_000m, 6.52m,  0, 1,  TradeStatus.Booked),
            ("Crest Insurance",   TradeSide.Borrow, 300_000_000m, 6.38m, -1, 14, TradeStatus.Settled),
            ("Delta Securities",  TradeSide.Lend,    75_000_000m, 6.60m,  1, 30, TradeStatus.Booked),
            ("Alpha Bank",        TradeSide.Borrow,  50_000_000m, 6.40m, -10, 3, TradeStatus.Matured),
            ("Evergreen Co-op Bank", TradeSide.Lend, 40_000_000m, 6.70m,  0, 7,  TradeStatus.Cancelled),
        };

        var seq = 1;
        foreach (var r in rows)
        {
            var start = today.AddDays(r.StartOffset);
            db.Trades.Add(new Trade
            {
                TradeRef = $"TD-{today:yyyyMMdd}-{seq++:D4}",
                Counterparty = r.Cpty,
                Side = r.Side,
                Principal = r.Principal,
                RatePercent = r.Rate,
                TradeDate = r.StartOffset > 0 ? today : start,
                StartDate = start,
                MaturityDate = start.AddDays(r.Tenor),
                Status = r.Status,
            });
        }
        db.SaveChanges();
    }
}

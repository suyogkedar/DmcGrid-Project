using DmcGrid.Core.Interfaces;
using DmcGrid.Core.Models;
using Microsoft.Data.SqlClient;
using static DmcGrid.Data.Repositories.QuoteRepository;

namespace DmcGrid.Data.Repositories;

public class RfqRepository(Db db) : IRfqRepository
{
    private const string Submitted = "q.Status = 'Submitted'";

    public async Task<Rfq?> GetAsync(int rfqId) =>
        (await db.QueryAsync("SELECT r.* FROM dbo.Rfqs r WHERE r.RfqId = @id", r => Map(r, new Rfq()), Db.P("@id", rfqId)))
        .FirstOrDefault();

    /// <summary>Inserts a new RFQ, or updates the agent's own draft. Returns the RFQ id, or 0 when the RFQ is not an editable draft.</summary>
    public async Task<int> SaveAsync(Rfq q)
    {
        SqlParameter[] Params() =>
        [
            Db.P("@id", q.RfqId), Db.P("@agent", q.AgentUserId), Db.P("@dest", Cut(q.Destination, 100)), Db.P("@year", q.TravelYear),
            Db.P("@month", Cut(q.TravelMonth, 20)), Db.P("@start", q.StartDate), Db.P("@end", q.EndDate), Db.P("@nights", q.EffectiveNights),
            Db.P("@adults", q.Adults), Db.P("@children", q.Children), Db.P("@flight", Cut(q.FlightTime, 40)), Db.P("@stops", Cut(q.Stops, 40)),
            Db.P("@cabin", Cut(q.Cabin, 40)), Db.P("@hotel", Cut(q.HotelCategory, 40)), Db.P("@room", Cut(q.RoomType, 40)),
            Db.P("@meal", Cut(q.MealPlan, 40)), Db.P("@cities", Cut(q.Cities, 300)), Db.P("@vehicle", Cut(q.Vehicle, 40)),
            Db.P("@food", Cut(q.FoodPrefs, 200)), Db.P("@allergies", Cut(q.Allergies, 300)), Db.P("@budget", Cut(q.Budget, 40)),
            Db.P("@priorities", Cut(q.Priorities, 1000)), Db.P("@natural", Cut(q.NaturalText, 2000)),
            Db.P("@ai", Cut(q.AiFields, 300)), Db.P("@status", q.Status)
        ];

        if (q.RfqId == 0)
        {
            return await db.ScalarAsync<int>("""
                INSERT INTO dbo.Rfqs (AgentUserId, Destination, TravelYear, TravelMonth, StartDate, EndDate, Nights, Adults, Children,
                    FlightTime, Stops, Cabin, HotelCategory, RoomType, MealPlan, Cities, Vehicle, FoodPrefs, Allergies, Budget,
                    Priorities, NaturalText, AiFields, Status)
                VALUES (@agent, @dest, @year, @month, @start, @end, @nights, @adults, @children,
                    @flight, @stops, @cabin, @hotel, @room, @meal, @cities, @vehicle, @food, @allergies, @budget,
                    @priorities, @natural, @ai, @status);
                SELECT CAST(SCOPE_IDENTITY() AS INT);
                """, Params());
        }

        var rows = await db.ExecuteAsync("""
            UPDATE dbo.Rfqs SET Destination = @dest, TravelYear = @year, TravelMonth = @month, StartDate = @start, EndDate = @end,
                Nights = @nights, Adults = @adults, Children = @children, FlightTime = @flight, Stops = @stops, Cabin = @cabin,
                HotelCategory = @hotel, RoomType = @room, MealPlan = @meal, Cities = @cities, Vehicle = @vehicle, FoodPrefs = @food,
                Allergies = @allergies, Budget = @budget, Priorities = @priorities, NaturalText = @natural, AiFields = @ai,
                Status = @status, UpdatedAt = SYSUTCDATETIME()
            WHERE RfqId = @id AND AgentUserId = @agent AND Status = 'Draft'
            """, Params());
        return rows == 1 ? q.RfqId : 0;
    }

    public Task SetStatusAsync(int rfqId, int agentUserId, string status) =>
        db.ExecuteAsync(
            "UPDATE dbo.Rfqs SET Status = @s, UpdatedAt = SYSUTCDATETIME() WHERE RfqId = @id AND AgentUserId = @a AND Status <> 'Draft'",
            Db.P("@s", status), Db.P("@id", rfqId), Db.P("@a", agentUserId));

    public Task<List<AgentRfqRow>> ListForAgentAsync(int agentUserId) =>
        db.QueryAsync($"""
            SELECT r.*,
                (SELECT COUNT(*) FROM dbo.QuotePackages p JOIN dbo.Quotes q ON q.QuoteId = p.QuoteId
                  WHERE q.RfqId = r.RfqId AND {Submitted}) AS OptionCount,
                (SELECT COUNT(*) FROM dbo.Quotes q WHERE q.RfqId = r.RfqId AND {Submitted}) AS DmcCount,
                (SELECT COUNT(*) FROM dbo.RfqConnections c WHERE c.RfqId = r.RfqId) AS ConnectionCount
            FROM dbo.Rfqs r
            WHERE r.AgentUserId = @a
            ORDER BY r.RfqId DESC
            """,
            r =>
            {
                var row = Map(r, new AgentRfqRow());
                row.OptionCount = r.Int("OptionCount");
                row.DmcCount = r.Int("DmcCount");
                row.ConnectionCount = r.Int("ConnectionCount");
                return row;
            },
            Db.P("@a", agentUserId));

    public Task<List<DmcRfqRow>> ListForDmcAsync(int dmcUserId) =>
        db.QueryAsync("""
            SELECT r.*,
                CASE WHEN q.QuoteId IS NULL THEN 0 ELSE 1 END AS HasQuote,
                CASE WHEN c.ConnectionId IS NULL THEN 0 ELSE 1 END AS Connected
            FROM dbo.Rfqs r
            JOIN dbo.Users u ON u.UserId = @d
            LEFT JOIN dbo.Quotes q ON q.RfqId = r.RfqId AND q.DmcUserId = @d
            LEFT JOIN dbo.RfqConnections c ON c.RfqId = r.RfqId AND c.DmcUserId = @d
            WHERE q.QuoteId IS NOT NULL
               OR (r.Status = 'Published'
                   AND (u.Destinations = '' OR CHARINDEX(',' + r.Destination + ',', ',' + u.Destinations + ',') > 0))
            ORDER BY r.RfqId DESC
            """,
            r =>
            {
                var row = Map(r, new DmcRfqRow());
                row.HasQuote = r.Int("HasQuote") == 1;
                row.Connected = r.Int("Connected") == 1;
                return row;
            },
            Db.P("@d", dmcUserId));

    public async Task<DashboardStats> AgentStatsAsync(int agentUserId) =>
        (await db.QueryAsync($"""
            SELECT
                (SELECT LeadsRemaining FROM dbo.Users WHERE UserId = @a) AS A,
                (SELECT COUNT(*) FROM dbo.Rfqs WHERE AgentUserId = @a AND Status = 'Published') AS B,
                (SELECT COUNT(*) FROM dbo.QuotePackages p JOIN dbo.Quotes q ON q.QuoteId = p.QuoteId
                    JOIN dbo.Rfqs r ON r.RfqId = q.RfqId WHERE r.AgentUserId = @a AND {Submitted}) AS C,
                (SELECT COUNT(*) FROM dbo.Rfqs r WHERE r.AgentUserId = @a AND r.Status = 'Published'
                    AND EXISTS (SELECT 1 FROM dbo.Quotes q WHERE q.RfqId = r.RfqId AND {Submitted})
                    AND NOT EXISTS (SELECT 1 FROM dbo.RfqConnections c WHERE c.RfqId = r.RfqId)) AS D
            """, MapStats, Db.P("@a", agentUserId))).First();

    public async Task<DashboardStats> DmcStatsAsync(int dmcUserId) =>
        (await db.QueryAsync($"""
            SELECT
                (SELECT COUNT(*) FROM dbo.Rfqs r JOIN dbo.Users u ON u.UserId = @d
                  WHERE r.Status = 'Published'
                    AND (u.Destinations = '' OR CHARINDEX(',' + r.Destination + ',', ',' + u.Destinations + ',') > 0)
                    AND NOT EXISTS (SELECT 1 FROM dbo.Quotes q WHERE q.RfqId = r.RfqId AND q.DmcUserId = @d)) AS A,
                (SELECT COUNT(*) FROM dbo.QuotePackages p JOIN dbo.Quotes q ON q.QuoteId = p.QuoteId
                  WHERE q.DmcUserId = @d AND {Submitted}) AS B,
                (SELECT COUNT(*) FROM dbo.RfqConnections WHERE DmcUserId = @d) AS C,
                (SELECT QuoteCredits FROM dbo.Users WHERE UserId = @d) AS D
            """, MapStats, Db.P("@d", dmcUserId))).First();

    public async Task<MarketPulse> MarketPulseAsync()
    {
        await using var conn = await db.OpenAsync();
        var pulse = (await Db.QueryAsync(conn, null, $"""
            SELECT
                (SELECT COUNT(*) FROM dbo.Rfqs WHERE Status <> 'Draft') AS Rfqs,
                (SELECT COUNT(*) FROM dbo.QuotePackages p JOIN dbo.Quotes q ON q.QuoteId = p.QuoteId WHERE {Submitted}) AS Quotes,
                (SELECT COUNT(DISTINCT RfqId) FROM dbo.RfqConnections) AS Connections,
                (SELECT COUNT(*) FROM dbo.Users WHERE Role = 'DMC' AND IsActive = 1) AS Dmcs,
                (SELECT COUNT(DISTINCT Destination) FROM dbo.Rfqs WHERE Status <> 'Draft') AS Destinations
            """,
            r => new MarketPulse
            {
                Rfqs = r.Int("Rfqs"), Quotes = r.Int("Quotes"), Connections = r.Int("Connections"),
                Dmcs = r.Int("Dmcs"), Destinations = r.Int("Destinations")
            })).First();

        pulse.Coverage = await Db.QueryAsync(conn, null, $"""
            SELECT TOP 4 r.Destination, COUNT(DISTINCT q.DmcUserId) AS Dmcs
            FROM dbo.Rfqs r JOIN dbo.Quotes q ON q.RfqId = r.RfqId AND {Submitted}
            GROUP BY r.Destination
            ORDER BY Dmcs DESC, r.Destination
            """, r => (r.Str("Destination"), r.Int("Dmcs")));
        return pulse;
    }

    private static DashboardStats MapStats(SqlDataReader r) =>
        new() { A = r.Int("A"), B = r.Int("B"), C = r.Int("C"), D = r.Int("D") };

    private static T Map<T>(SqlDataReader r, T q) where T : Rfq
    {
        q.RfqId = r.Int("RfqId");
        q.RfqNo = r.Str("RfqNo");
        q.AgentUserId = r.Int("AgentUserId");
        q.Destination = r.Str("Destination");
        q.TravelYear = r.Int("TravelYear");
        q.TravelMonth = r.Str("TravelMonth");
        q.StartDate = r.NDate("StartDate");
        q.EndDate = r.NDate("EndDate");
        q.Nights = r.NInt("Nights");
        q.Adults = r.Int("Adults");
        q.Children = r.Int("Children");
        q.FlightTime = r.Str("FlightTime");
        q.Stops = r.Str("Stops");
        q.Cabin = r.Str("Cabin");
        q.HotelCategory = r.Str("HotelCategory");
        q.RoomType = r.Str("RoomType");
        q.MealPlan = r.Str("MealPlan");
        q.Cities = r.Str("Cities");
        q.Vehicle = r.Str("Vehicle");
        q.FoodPrefs = r.Str("FoodPrefs");
        q.Allergies = r.Str("Allergies");
        q.Budget = r.Str("Budget");
        q.Priorities = r.Str("Priorities");
        q.NaturalText = r.Str("NaturalText");
        q.AiFields = r.Str("AiFields");
        q.Status = r.Str("Status");
        q.CreatedAt = r.Date("CreatedAt");
        return q;
    }
}

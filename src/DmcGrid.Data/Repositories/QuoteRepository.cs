using DmcGrid.Core.Interfaces;
using DmcGrid.Core.Models;
using Microsoft.Data.SqlClient;

namespace DmcGrid.Data.Repositories;

public class QuoteRepository(Db db) : IQuoteRepository
{
    public const decimal ConnectionFee = 999m;
    public const int MaxDmcsPerRfq = 2;

    // DmcSeq gives each DMC a stable anonymous alias (DMC1, DMC2…) per RFQ, in order of first quote.
    private const string PackageSelect = """
        SELECT p.*, q.DmcUserId, u.OrgName,
            (SELECT COUNT(*) FROM dbo.Quotes q2 WHERE q2.RfqId = q.RfqId AND q2.QuoteId <= q.QuoteId) AS DmcSeq
        FROM dbo.QuotePackages p
        JOIN dbo.Quotes q ON q.QuoteId = p.QuoteId
        JOIN dbo.Users u ON u.UserId = q.DmcUserId
        """;

    public Task<List<QuotePackage>> GetForDmcAsync(int rfqId, int dmcUserId) =>
        LoadAsync("WHERE q.RfqId = @rfq AND q.DmcUserId = @dmc ORDER BY p.SortOrder",
            Db.P("@rfq", rfqId), Db.P("@dmc", dmcUserId));

    public Task<List<QuotePackage>> GetSubmittedAsync(int rfqId) =>
        LoadAsync("WHERE q.RfqId = @rfq AND q.Status = 'Submitted' ORDER BY q.QuoteId, p.SortOrder", Db.P("@rfq", rfqId));

    private async Task<List<QuotePackage>> LoadAsync(string where, params SqlParameter[] ps)
    {
        await using var conn = await db.OpenAsync();
        var rows = await Db.QueryAsync(conn, null, $"{PackageSelect} {where}", r => (Package: new QuotePackage
        {
            PackageId = r.Int("PackageId"),
            QuoteId = r.Int("QuoteId"),
            DmcUserId = r.Int("DmcUserId"),
            DmcAlias = "DMC" + r.Int("DmcSeq"),
            DmcOrg = r.Str("OrgName"),
            Name = r.Str("Name"),
            NetPrice = r.Dec("NetPrice"),
            Hotel = r.Str("Hotel"),
            Room = r.Str("Room"),
            Meal = r.Str("Meal"),
            Vehicle = r.Str("Vehicle"),
            Food = r.Str("Food"),
            VersionNo = r.Int("VersionNo"),
            ParentPackageId = r.NInt("ParentPackageId"),
            AfterConnection = r.Bool("AfterConnection"),
            StatusNote = r.Str("StatusNote"),
            CreatedAt = r.Date("CreatedAt")
        }, DayCount: r.Int("DayCount")), ps);
        if (rows.Count == 0) return [];

        var byId = new Dictionary<int, QuotePackage>();
        foreach (var (package, dayCount) in rows)
        {
            for (var d = 0; d < dayCount; d++) package.Days.Add([]);
            byId[package.PackageId] = package;
        }

        // Ids come straight from the query above, so inlining them is safe.
        var activities = await Db.QueryAsync(conn, null, $"""
            SELECT PackageId, DayNo, FromTime, ToTime, City, Description, Transfer, Included, Notes
            FROM dbo.PackageActivities
            WHERE PackageId IN ({string.Join(",", byId.Keys)})
            ORDER BY PackageId, DayNo, SortOrder
            """,
            r => (PackageId: r.Int("PackageId"), DayNo: r.Int("DayNo"), Activity: new PackageActivity
            {
                From = r.Str("FromTime"),
                To = r.Str("ToTime"),
                City = r.Str("City"),
                Desc = r.Str("Description"),
                Transfer = r.Str("Transfer"),
                Inc = r.Bool("Included") ? "Yes" : "No",
                Notes = r.Str("Notes")
            }));
        foreach (var (packageId, dayNo, activity) in activities)
        {
            var days = byId[packageId].Days;
            while (days.Count < dayNo) days.Add([]);
            days[dayNo - 1].Add(activity);
        }
        return rows.Select(x => x.Package).ToList();
    }

    public async Task<string?> SaveAsync(int rfqId, int dmcUserId, List<QuotePackage> packages, bool submit)
    {
        await using var conn = await db.OpenAsync();
        await using var tx = (SqlTransaction)await conn.BeginTransactionAsync();

        var rfqStatus = await Db.ScalarAsync<string>(conn, tx,
            "SELECT Status FROM dbo.Rfqs WITH (UPDLOCK, ROWLOCK) WHERE RfqId = @id", Db.P("@id", rfqId));
        if (rfqStatus is null or RfqStatus.Draft) return "RFQ not found";
        if (rfqStatus == RfqStatus.Closed) return "RFQ is closed · further DMC quotes are blocked";

        var quote = (await Db.QueryAsync(conn, tx,
            "SELECT QuoteId, Status FROM dbo.Quotes WHERE RfqId = @r AND DmcUserId = @d",
            r => (QuoteId: r.Int("QuoteId"), Status: r.Str("Status")), Db.P("@r", rfqId), Db.P("@d", dmcUserId))).FirstOrDefault();
        var quoteId = quote.QuoteId;
        if (quoteId == 0)
        {
            quoteId = await Db.ScalarAsync<int>(conn, tx, """
                INSERT INTO dbo.Quotes (RfqId, DmcUserId, Status) VALUES (@r, @d, 'Draft');
                SELECT CAST(SCOPE_IDENTITY() AS INT);
                """, Db.P("@r", rfqId), Db.P("@d", dmcUserId));
        }

        if (submit && quote.Status != "Submitted")
        {
            var debited = await Db.ExecuteAsync(conn, tx,
                "UPDATE dbo.Users SET QuoteCredits = QuoteCredits - 1 WHERE UserId = @d AND QuoteCredits > 0", Db.P("@d", dmcUserId));
            if (debited == 0) return "No quote credits remaining";
            await Db.ExecuteAsync(conn, tx,
                "UPDATE dbo.Quotes SET Status = 'Submitted', SubmittedAt = SYSUTCDATETIME() WHERE QuoteId = @q", Db.P("@q", quoteId));
            await AddTxnAsync(conn, tx, dmcUserId, rfqId, "QuoteCredit", 0, -1, "Quote submitted");
        }

        var connected = await Db.ScalarAsync<int>(conn, tx,
            "SELECT COUNT(*) FROM dbo.RfqConnections WHERE RfqId = @r AND DmcUserId = @d",
            Db.P("@r", rfqId), Db.P("@d", dmcUserId)) > 0;

        var existing = (await Db.QueryAsync(conn, tx,
            "SELECT PackageId, VersionNo FROM dbo.QuotePackages WHERE QuoteId = @q",
            r => (Id: r.Int("PackageId"), Version: r.Int("VersionNo")), Db.P("@q", quoteId))).ToDictionary(x => x.Id, x => x.Version);

        var kept = new HashSet<int>();
        for (var i = 0; i < packages.Count; i++)
        {
            var p = packages[i];
            SqlParameter[] Fields() =>
            [
                Db.P("@name", Cut(p.Name, 150)), Db.P("@price", Math.Max(0, p.NetPrice)), Db.P("@hotel", Cut(p.Hotel, 200)),
                Db.P("@room", Cut(p.Room, 100)), Db.P("@meal", Cut(p.Meal, 100)), Db.P("@vehicle", Cut(p.Vehicle, 100)),
                Db.P("@food", Cut(p.Food, 200)), Db.P("@days", p.Days.Count), Db.P("@note", Cut(p.StatusNote, 300)), Db.P("@sort", i)
            ];

            int packageId;
            if (p.PackageId > 0 && existing.ContainsKey(p.PackageId))
            {
                packageId = p.PackageId;
                await Db.ExecuteAsync(conn, tx, """
                    UPDATE dbo.QuotePackages SET Name = @name, NetPrice = @price, Hotel = @hotel, Room = @room, Meal = @meal,
                        Vehicle = @vehicle, Food = @food, DayCount = @days, StatusNote = @note, SortOrder = @sort,
                        UpdatedAt = SYSUTCDATETIME()
                    WHERE PackageId = @id
                    """, [.. Fields(), Db.P("@id", packageId)]);
                await Db.ExecuteAsync(conn, tx, "DELETE FROM dbo.PackageActivities WHERE PackageId = @id", Db.P("@id", packageId));
            }
            else
            {
                int? parent = null;
                var version = 1;
                if (p.ParentPackageId is int parentId && existing.TryGetValue(parentId, out var parentVersion))
                {
                    parent = parentId;
                    version = parentVersion + 1;
                }
                packageId = await Db.ScalarAsync<int>(conn, tx, """
                    INSERT INTO dbo.QuotePackages (QuoteId, Name, NetPrice, Hotel, Room, Meal, Vehicle, Food, DayCount,
                        VersionNo, ParentPackageId, AfterConnection, StatusNote, SortOrder)
                    VALUES (@quote, @name, @price, @hotel, @room, @meal, @vehicle, @food, @days,
                        @version, @parent, @after, @note, @sort);
                    SELECT CAST(SCOPE_IDENTITY() AS INT);
                    """, [.. Fields(), Db.P("@quote", quoteId), Db.P("@version", version), Db.P("@parent", parent),
                          Db.P("@after", connected && parent is null)]);
            }
            kept.Add(packageId);

            for (var d = 0; d < p.Days.Count; d++)
            for (var a = 0; a < p.Days[d].Count; a++)
            {
                var act = p.Days[d][a];
                await Db.ExecuteAsync(conn, tx, """
                    INSERT INTO dbo.PackageActivities (PackageId, DayNo, SortOrder, FromTime, ToTime, City, Description, Transfer, Included, Notes)
                    VALUES (@p, @day, @sort, @from, @to, @city, @desc, @transfer, @inc, @notes)
                    """,
                    Db.P("@p", packageId), Db.P("@day", d + 1), Db.P("@sort", a), Db.P("@from", Cut(act.From, 10)),
                    Db.P("@to", Cut(act.To, 10)), Db.P("@city", Cut(act.City, 100)), Db.P("@desc", Cut(act.Desc, 500)),
                    Db.P("@transfer", Cut(act.Transfer, 100)), Db.P("@inc", act.Inc != "No"), Db.P("@notes", Cut(act.Notes, 300)));
            }
        }

        // Once the agent has connected, earlier versions are part of the deal history and are never removed.
        if (!connected)
        {
            foreach (var id in existing.Keys.Where(id => !kept.Contains(id)))
                await Db.ExecuteAsync(conn, tx, "DELETE FROM dbo.QuotePackages WHERE PackageId = @id", Db.P("@id", id));
        }

        await tx.CommitAsync();
        return null;
    }

    public Task<List<int>> ConnectedDmcIdsAsync(int rfqId) =>
        db.QueryAsync("SELECT DmcUserId FROM dbo.RfqConnections WHERE RfqId = @r", r => r.Int("DmcUserId"), Db.P("@r", rfqId));

    public async Task<ConnectResult> ConnectAsync(int rfqId, int agentUserId, List<int> packageIds, string billing)
    {
        if (packageIds.Count == 0) return Fail("Select at least one package option");

        await using var conn = await db.OpenAsync();
        await using var tx = (SqlTransaction)await conn.BeginTransactionAsync();

        var status = await Db.ScalarAsync<string>(conn, tx,
            "SELECT Status FROM dbo.Rfqs WITH (UPDLOCK, ROWLOCK) WHERE RfqId = @id AND AgentUserId = @a",
            Db.P("@id", rfqId), Db.P("@a", agentUserId));
        if (status is null or RfqStatus.Draft) return Fail("RFQ not found");

        var selected = await Db.QueryAsync(conn, tx, $"""
            SELECT DISTINCT q.DmcUserId
            FROM dbo.QuotePackages p JOIN dbo.Quotes q ON q.QuoteId = p.QuoteId
            WHERE q.RfqId = @r AND q.Status = 'Submitted' AND p.PackageId IN ({string.Join(",", packageIds)})
            """, r => r.Int("DmcUserId"), Db.P("@r", rfqId));
        var already = await Db.QueryAsync(conn, tx,
            "SELECT DmcUserId FROM dbo.RfqConnections WHERE RfqId = @r", r => r.Int("DmcUserId"), Db.P("@r", rfqId));

        var toAdd = selected.Except(already).ToList();
        if (toAdd.Count == 0) return Fail(selected.Count == 0 ? "Selected options are no longer available" : "Already connected to the selected DMCs");
        if (already.Count + toAdd.Count > MaxDmcsPerRfq) return Fail($"Maximum {MaxDmcsPerRfq} distinct DMCs per RFQ");

        string message;
        if (already.Count > 0)
        {
            message = "Connected ✓ no additional lead charge";
        }
        else if (billing == "wallet")
        {
            var debited = await Db.ExecuteAsync(conn, tx,
                "UPDATE dbo.Users SET WalletBalance = WalletBalance - @fee WHERE UserId = @a AND WalletBalance >= @fee",
                Db.P("@fee", ConnectionFee), Db.P("@a", agentUserId));
            if (debited == 0) return Fail("Insufficient wallet balance");
            await AddTxnAsync(conn, tx, agentUserId, rfqId, "WalletDebit", -ConnectionFee, 0, "RFQ connection fee");
            var balance = await Db.ScalarAsync<decimal>(conn, tx,
                "SELECT WalletBalance FROM dbo.Users WHERE UserId = @a", Db.P("@a", agentUserId));
            message = $"Connected ✓ ₹{ConnectionFee:0} debited once · balance ₹{balance:#,##0}";
        }
        else
        {
            var (planLeads, planPrice) = billing switch { "plan5" => (5, 4999m), "plan8" => (8, 7999m), _ => (0, 0m) };
            if (planLeads > 0)
            {
                await Db.ExecuteAsync(conn, tx, "UPDATE dbo.Users SET LeadsRemaining = LeadsRemaining + @n WHERE UserId = @a",
                    Db.P("@n", planLeads), Db.P("@a", agentUserId));
                await AddTxnAsync(conn, tx, agentUserId, rfqId, "PlanPurchase", planPrice, planLeads, $"{planLeads}-lead plan · payment to be collected");
            }
            var consumed = await Db.ExecuteAsync(conn, tx,
                "UPDATE dbo.Users SET LeadsRemaining = LeadsRemaining - 1 WHERE UserId = @a AND LeadsRemaining > 0", Db.P("@a", agentUserId));
            if (consumed == 0) return Fail("No leads remaining · choose a plan or pay from wallet");
            await AddTxnAsync(conn, tx, agentUserId, rfqId, "LeadConsumed", 0, -1, "RFQ connection");
            message = "Connected ✓ 1 lead consumed once";
        }

        foreach (var dmcId in toAdd)
            await Db.ExecuteAsync(conn, tx, "INSERT INTO dbo.RfqConnections (RfqId, DmcUserId) VALUES (@r, @d)",
                Db.P("@r", rfqId), Db.P("@d", dmcId));

        await tx.CommitAsync();
        return new ConnectResult { Ok = true, Message = message, ConnectedDmcs = already.Count + toAdd.Count };

        static ConnectResult Fail(string message) => new() { Message = message };
    }

    private static Task AddTxnAsync(SqlConnection conn, SqlTransaction tx, int userId, int rfqId, string type, decimal amount, int leads, string note) =>
        Db.ExecuteAsync(conn, tx,
            "INSERT INTO dbo.WalletTransactions (UserId, RfqId, TxnType, Amount, Leads, Note) VALUES (@u, @r, @t, @amt, @leads, @note)",
            Db.P("@u", userId), Db.P("@r", rfqId), Db.P("@t", type), Db.P("@amt", amount), Db.P("@leads", leads), Db.P("@note", note));

    internal static string Cut(string? value, int max)
    {
        value = (value ?? "").Trim();
        return value.Length > max ? value[..max] : value;
    }
}

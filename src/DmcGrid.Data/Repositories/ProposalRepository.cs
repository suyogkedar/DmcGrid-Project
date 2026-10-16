using DmcGrid.Core.Interfaces;
using DmcGrid.Core.Models;
using Microsoft.Data.SqlClient;
using static DmcGrid.Data.Repositories.QuoteRepository;

namespace DmcGrid.Data.Repositories;

public class ProposalRepository(Db db) : IProposalRepository
{
    public async Task<Proposal?> GetByRfqAsync(int rfqId)
    {
        await using var conn = await db.OpenAsync();
        var proposal = (await Db.QueryAsync(conn, null, "SELECT * FROM dbo.Proposals WHERE RfqId = @r", r => new Proposal
        {
            ProposalId = r.Int("ProposalId"),
            RfqId = r.Int("RfqId"),
            AgentUserId = r.Int("AgentUserId"),
            PackageId = r.NInt("PackageId"),
            SourceLabel = r.Str("SourceLabel"),
            NetCost = r.Dec("NetCost"),
            MarginType = r.Str("MarginType"),
            Margin = r.Dec("Margin"),
            GstPct = r.Dec("GstPct"),
            TcsPct = r.Dec("TcsPct"),
            Travellers = r.Int("Travellers"),
            Validity = r.Str("Validity"),
            BrandName = r.Str("BrandName"),
            Title = r.Str("Title"),
            GstNumber = r.Str("GstNumber"),
            Contact = r.Str("Contact"),
            Inclusions = r.Str("Inclusions"),
            Exclusions = r.Str("Exclusions"),
            TdsPaid = r.Dec("TdsPaid"),
            TdsPayable = r.Dec("TdsPayable"),
            ApprovalStatus = r.Str("ApprovalStatus")
        }, Db.P("@r", rfqId))).FirstOrDefault();
        if (proposal is null) return null;

        var id = proposal.ProposalId;
        var days = await Db.QueryAsync(conn, null,
            "SELECT DayNo, City FROM dbo.ProposalDays WHERE ProposalId = @p ORDER BY DayNo",
            r => (DayNo: r.Int("DayNo"), Day: new ProposalDay { City = r.Str("City") }), Db.P("@p", id));
        var byNo = days.ToDictionary(d => d.DayNo, d => d.Day);
        var items = await Db.QueryAsync(conn, null,
            "SELECT DayNo, TimeText, ItemType, Details, Reference FROM dbo.ProposalItems WHERE ProposalId = @p ORDER BY DayNo, SortOrder",
            r => (DayNo: r.Int("DayNo"), Item: new ProposalItem
            {
                Time = r.Str("TimeText"), Type = r.Str("ItemType"), Details = r.Str("Details"), Reference = r.Str("Reference")
            }), Db.P("@p", id));
        foreach (var (dayNo, item) in items) byNo[dayNo].Items.Add(item);
        proposal.Days = days.Select(d => d.Day).ToList();

        proposal.Costs = await Db.QueryAsync(conn, null,
            "SELECT CostType, Description, AgentCost, CustomerCharge FROM dbo.DealCosts WHERE ProposalId = @p ORDER BY SortOrder",
            r => new DealCost { Type = r.Str("CostType"), Desc = r.Str("Description"), Cost = r.Dec("AgentCost"), Charge = r.Dec("CustomerCharge") },
            Db.P("@p", id));
        return proposal;
    }

    /// <summary>Upserts the proposal header and replaces its itinerary and deal costs in one transaction.</summary>
    public async Task<int> SaveAsync(Proposal p)
    {
        await using var conn = await db.OpenAsync();
        await using var tx = (SqlTransaction)await conn.BeginTransactionAsync();

        SqlParameter[] header =
        [
            Db.P("@id", p.ProposalId), Db.P("@rfq", p.RfqId), Db.P("@agent", p.AgentUserId), Db.P("@package", p.PackageId),
            Db.P("@source", Cut(p.SourceLabel, 200)), Db.P("@net", p.NetCost), Db.P("@mtype", p.MarginType == "Fixed" ? "Fixed" : "Percentage"),
            Db.P("@margin", p.Margin), Db.P("@gst", p.GstPct), Db.P("@tcs", p.TcsPct), Db.P("@trav", p.Travellers),
            Db.P("@validity", Cut(p.Validity, 50)), Db.P("@brand", Cut(p.BrandName, 200)), Db.P("@title", Cut(p.Title, 200)),
            Db.P("@gstno", Cut(p.GstNumber, 30)), Db.P("@contact", Cut(p.Contact, 200)), Db.P("@inc", Cut(p.Inclusions, 2000)),
            Db.P("@exc", Cut(p.Exclusions, 2000)), Db.P("@tdsPaid", p.TdsPaid), Db.P("@tdsPayable", p.TdsPayable),
            Db.P("@approval", p.ApprovalStatus)
        ];

        var id = p.ProposalId;
        if (id == 0)
        {
            id = await Db.ScalarAsync<int>(conn, tx, """
                INSERT INTO dbo.Proposals (RfqId, AgentUserId, PackageId, SourceLabel, NetCost, MarginType, Margin, GstPct, TcsPct,
                    Travellers, Validity, BrandName, Title, GstNumber, Contact, Inclusions, Exclusions, TdsPaid, TdsPayable, ApprovalStatus)
                VALUES (@rfq, @agent, @package, @source, @net, @mtype, @margin, @gst, @tcs,
                    @trav, @validity, @brand, @title, @gstno, @contact, @inc, @exc, @tdsPaid, @tdsPayable, @approval);
                SELECT CAST(SCOPE_IDENTITY() AS INT);
                """, header);
        }
        else
        {
            await Db.ExecuteAsync(conn, tx, """
                UPDATE dbo.Proposals SET PackageId = @package, SourceLabel = @source, NetCost = @net, MarginType = @mtype, Margin = @margin,
                    GstPct = @gst, TcsPct = @tcs, Travellers = @trav, Validity = @validity, BrandName = @brand, Title = @title,
                    GstNumber = @gstno, Contact = @contact, Inclusions = @inc, Exclusions = @exc, TdsPaid = @tdsPaid,
                    TdsPayable = @tdsPayable, ApprovalStatus = @approval, UpdatedAt = SYSUTCDATETIME()
                WHERE ProposalId = @id AND AgentUserId = @agent
                """, header);
            await Db.ExecuteAsync(conn, tx, "DELETE FROM dbo.ProposalDays WHERE ProposalId = @p", Db.P("@p", id));
            await Db.ExecuteAsync(conn, tx, "DELETE FROM dbo.DealCosts WHERE ProposalId = @p", Db.P("@p", id));
        }

        for (var d = 0; d < p.Days.Count; d++)
        {
            await Db.ExecuteAsync(conn, tx, "INSERT INTO dbo.ProposalDays (ProposalId, DayNo, City) VALUES (@p, @day, @city)",
                Db.P("@p", id), Db.P("@day", d + 1), Db.P("@city", Cut(p.Days[d].City, 100)));
            for (var i = 0; i < p.Days[d].Items.Count; i++)
            {
                var item = p.Days[d].Items[i];
                await Db.ExecuteAsync(conn, tx, """
                    INSERT INTO dbo.ProposalItems (ProposalId, DayNo, SortOrder, TimeText, ItemType, Details, Reference)
                    VALUES (@p, @day, @sort, @time, @type, @details, @ref)
                    """,
                    Db.P("@p", id), Db.P("@day", d + 1), Db.P("@sort", i), Db.P("@time", Cut(item.Time, 10)),
                    Db.P("@type", Cut(item.Type, 30)), Db.P("@details", Cut(item.Details, 500)), Db.P("@ref", Cut(item.Reference, 300)));
            }
        }

        for (var i = 0; i < p.Costs.Count; i++)
        {
            var c = p.Costs[i];
            await Db.ExecuteAsync(conn, tx, """
                INSERT INTO dbo.DealCosts (ProposalId, SortOrder, CostType, Description, AgentCost, CustomerCharge)
                VALUES (@p, @sort, @type, @desc, @cost, @charge)
                """,
                Db.P("@p", id), Db.P("@sort", i), Db.P("@type", Cut(c.Type, 50)), Db.P("@desc", Cut(c.Desc, 300)),
                Db.P("@cost", c.Cost), Db.P("@charge", c.Charge));
        }

        await tx.CommitAsync();
        return id;
    }

    public Task SetApprovalAsync(int proposalId, string status, int byUserId) =>
        db.ExecuteAsync("""
            UPDATE dbo.Proposals
            SET ApprovalStatus = @s,
                ApprovedBy = CASE WHEN @s = 'Approved' THEN @u ELSE NULL END,
                ApprovedAt = CASE WHEN @s = 'Approved' THEN SYSUTCDATETIME() ELSE NULL END
            WHERE ProposalId = @id
            """, Db.P("@s", status), Db.P("@u", byUserId), Db.P("@id", proposalId));
}

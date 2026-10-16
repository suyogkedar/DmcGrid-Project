using DmcGrid.Core.Models;

namespace DmcGrid.Core.Interfaces;

public interface IUserRepository
{
    Task<AppUser?> GetByEmailAsync(string email);
    Task<AppUser?> GetByIdAsync(int userId);
}

public interface IRfqRepository
{
    Task<Rfq?> GetAsync(int rfqId);
    Task<int> SaveAsync(Rfq rfq);
    Task SetStatusAsync(int rfqId, int agentUserId, string status);
    Task<List<AgentRfqRow>> ListForAgentAsync(int agentUserId);
    Task<List<DmcRfqRow>> ListForDmcAsync(int dmcUserId);
    Task<DashboardStats> AgentStatsAsync(int agentUserId);
    Task<DashboardStats> DmcStatsAsync(int dmcUserId);
    Task<MarketPulse> MarketPulseAsync();
}

public interface IQuoteRepository
{
    /// <summary>All packages a DMC holds against an RFQ (draft or submitted).</summary>
    Task<List<QuotePackage>> GetForDmcAsync(int rfqId, int dmcUserId);
    /// <summary>Submitted packages from every DMC, as seen by the agent.</summary>
    Task<List<QuotePackage>> GetSubmittedAsync(int rfqId);
    /// <summary>Upserts the DMC's package set. Returns an error message, or null on success.</summary>
    Task<string?> SaveAsync(int rfqId, int dmcUserId, List<QuotePackage> packages, bool submit);
    Task<List<int>> ConnectedDmcIdsAsync(int rfqId);
    Task<ConnectResult> ConnectAsync(int rfqId, int agentUserId, List<int> packageIds, string billing);
}

public interface IProposalRepository
{
    Task<Proposal?> GetByRfqAsync(int rfqId);
    Task<int> SaveAsync(Proposal proposal);
    Task SetApprovalAsync(int proposalId, string status, int byUserId);
}

public interface IAuditRepository
{
    Task LogAsync(int? userId, string action, string entity, int? entityId, string details = "");
}

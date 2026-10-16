using DmcGrid.Core.Interfaces;
using DmcGrid.Core.Models;
using Microsoft.Data.SqlClient;

namespace DmcGrid.Data.Repositories;

public class UserRepository(Db db) : IUserRepository
{
    private const string Columns =
        "UserId, Email, PasswordHash, FullName, Role, OrgName, GstNumber, Phone, Destinations, LeadsRemaining, WalletBalance, QuoteCredits, IsActive";

    public async Task<AppUser?> GetByEmailAsync(string email) =>
        (await db.QueryAsync($"SELECT {Columns} FROM dbo.Users WHERE Email = @email", Map, Db.P("@email", email.Trim())))
        .FirstOrDefault();

    public async Task<AppUser?> GetByIdAsync(int userId) =>
        (await db.QueryAsync($"SELECT {Columns} FROM dbo.Users WHERE UserId = @id", Map, Db.P("@id", userId)))
        .FirstOrDefault();

    private static AppUser Map(SqlDataReader r) => new()
    {
        UserId = r.Int("UserId"),
        Email = r.Str("Email"),
        PasswordHash = r.Str("PasswordHash"),
        FullName = r.Str("FullName"),
        Role = r.Str("Role"),
        OrgName = r.Str("OrgName"),
        GstNumber = r.Str("GstNumber"),
        Phone = r.Str("Phone"),
        Destinations = r.Str("Destinations"),
        LeadsRemaining = r.Int("LeadsRemaining"),
        WalletBalance = r.Dec("WalletBalance"),
        QuoteCredits = r.Int("QuoteCredits"),
        IsActive = r.Bool("IsActive")
    };
}

public class AuditRepository(Db db) : IAuditRepository
{
    public Task LogAsync(int? userId, string action, string entity, int? entityId, string details = "") =>
        db.ExecuteAsync(
            "INSERT INTO dbo.AuditLog (UserId, Action, Entity, EntityId, Details) VALUES (@u, @a, @e, @id, @d)",
            Db.P("@u", userId), Db.P("@a", action), Db.P("@e", entity), Db.P("@id", entityId),
            Db.P("@d", details.Length > 1000 ? details[..1000] : details));
}

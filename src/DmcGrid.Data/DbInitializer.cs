using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;

namespace DmcGrid.Data;

/// <summary>Creates the application database if missing, applies the idempotent schema script and seeds demo data once.</summary>
public class DbInitializer(string masterConnectionString, string defaultConnectionString, Db db, DemoSeeder seeder)
{
    public async Task InitializeAsync(bool seedDemoData)
    {
        var database = new SqlConnectionStringBuilder(defaultConnectionString).InitialCatalog;
        if (string.IsNullOrWhiteSpace(database))
            throw new InvalidOperationException("DefaultConnection must specify a Database.");

        await using (var master = new SqlConnection(masterConnectionString))
        {
            await master.OpenAsync();
            await Db.ExecuteAsync(master, null,
                "IF DB_ID(@name) IS NULL BEGIN DECLARE @sql NVARCHAR(400) = N'CREATE DATABASE ' + QUOTENAME(@name); EXEC(@sql); END",
                Db.P("@name", database));
        }

        // A freshly created LocalDB database can take a moment to accept logins.
        SqlConnection.ClearAllPools();
        await using (var conn = await OpenWithRetryAsync())
        {
            foreach (var batch in Regex.Split(ReadScript("001_schema.sql"), @"^\s*GO\s*$", RegexOptions.Multiline | RegexOptions.IgnoreCase))
            {
                if (!string.IsNullOrWhiteSpace(batch)) await Db.ExecuteAsync(conn, null, batch);
            }
        }

        if (seedDemoData) await seeder.SeedAsync();
    }

    private async Task<SqlConnection> OpenWithRetryAsync()
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return await db.OpenAsync();
            }
            catch (SqlException) when (attempt < 5)
            {
                await Task.Delay(1000 * attempt);
            }
        }
    }

    private static string ReadScript(string name)
    {
        var assembly = Assembly.GetExecutingAssembly();
        var resource = assembly.GetManifestResourceNames().Single(n => n.EndsWith(name, StringComparison.OrdinalIgnoreCase));
        using var reader = new StreamReader(assembly.GetManifestResourceStream(resource)!);
        return reader.ReadToEnd();
    }
}

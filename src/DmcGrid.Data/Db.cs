using Microsoft.Data.SqlClient;

namespace DmcGrid.Data;

/// <summary>Thin ADO.NET helper: opens connections and runs parameterised commands.</summary>
public sealed class Db(string connectionString)
{
    public async Task<SqlConnection> OpenAsync()
    {
        var conn = new SqlConnection(connectionString);
        await conn.OpenAsync();
        return conn;
    }

    public static SqlParameter P(string name, object? value) => new(name, value ?? DBNull.Value);

    public async Task<List<T>> QueryAsync<T>(string sql, Func<SqlDataReader, T> map, params SqlParameter[] ps)
    {
        await using var conn = await OpenAsync();
        return await QueryAsync(conn, null, sql, map, ps);
    }

    public async Task<int> ExecuteAsync(string sql, params SqlParameter[] ps)
    {
        await using var conn = await OpenAsync();
        return await ExecuteAsync(conn, null, sql, ps);
    }

    public async Task<T?> ScalarAsync<T>(string sql, params SqlParameter[] ps)
    {
        await using var conn = await OpenAsync();
        return await ScalarAsync<T>(conn, null, sql, ps);
    }

    public static async Task<List<T>> QueryAsync<T>(SqlConnection conn, SqlTransaction? tx, string sql,
        Func<SqlDataReader, T> map, params SqlParameter[] ps)
    {
        await using var cmd = new SqlCommand(sql, conn, tx);
        cmd.Parameters.AddRange(ps);
        var list = new List<T>();
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync()) list.Add(map(reader));
        return list;
    }

    public static async Task<int> ExecuteAsync(SqlConnection conn, SqlTransaction? tx, string sql, params SqlParameter[] ps)
    {
        await using var cmd = new SqlCommand(sql, conn, tx);
        cmd.Parameters.AddRange(ps);
        return await cmd.ExecuteNonQueryAsync();
    }

    public static async Task<T?> ScalarAsync<T>(SqlConnection conn, SqlTransaction? tx, string sql, params SqlParameter[] ps)
    {
        await using var cmd = new SqlCommand(sql, conn, tx);
        cmd.Parameters.AddRange(ps);
        var value = await cmd.ExecuteScalarAsync();
        return value is null or DBNull ? default : (T)Convert.ChangeType(value, Nullable.GetUnderlyingType(typeof(T)) ?? typeof(T));
    }
}

internal static class ReaderExtensions
{
    public static string Str(this SqlDataReader r, string col) => r[col] as string ?? "";
    public static int Int(this SqlDataReader r, string col) => Convert.ToInt32(r[col]);
    public static int? NInt(this SqlDataReader r, string col) => r[col] is DBNull ? null : Convert.ToInt32(r[col]);
    public static decimal Dec(this SqlDataReader r, string col) => Convert.ToDecimal(r[col]);
    public static bool Bool(this SqlDataReader r, string col) => Convert.ToBoolean(r[col]);
    public static DateTime Date(this SqlDataReader r, string col) => Convert.ToDateTime(r[col]);
    public static DateTime? NDate(this SqlDataReader r, string col) => r[col] is DBNull ? null : Convert.ToDateTime(r[col]);
}

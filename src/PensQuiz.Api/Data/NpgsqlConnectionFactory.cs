using System.Data.Common;
using Npgsql;

namespace PensQuiz.Api.Data;

public sealed class NpgsqlConnectionFactory(IConfiguration configuration) : IDbConnectionFactory
{
    public DbConnection CreateConnection()
    {
        static string? NonEmpty(params string?[] values) =>
            values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));

        var connectionString = NonEmpty(
            configuration.GetConnectionString("SupabasePostgres"),
            configuration.GetConnectionString("DefaultConnection"),
            configuration["Supabase:ConnectionString"],
            configuration["SUPABASE_CONNECTION_STRING"]
        );

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "Missing Supabase Postgres connection string. Set ConnectionStrings:SupabasePostgres (or Supabase:ConnectionString / SUPABASE_CONNECTION_STRING).");
        }

        return new NpgsqlConnection(NormalizeConnectionString(connectionString));
    }

    private static string NormalizeConnectionString(string value)
    {
        var trimmed = value.Trim();
        NpgsqlConnectionStringBuilder builder;

        if (trimmed.StartsWith("postgres", StringComparison.OrdinalIgnoreCase) && 
            Uri.TryCreate(trimmed, UriKind.Absolute, out var uri) && 
            !string.IsNullOrWhiteSpace(uri.Host))
        {
            var userInfo = uri.UserInfo.Split(':', 2);
            var username = userInfo.Length > 0 ? Uri.UnescapeDataString(userInfo[0]) : "";
            var password = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : "";
            var database = uri.AbsolutePath.Trim('/');

            builder = new NpgsqlConnectionStringBuilder
            {
                Host = uri.Host,
                Port = uri.Port > 0 ? uri.Port : 5432,
                Database = string.IsNullOrWhiteSpace(database) ? "postgres" : database,
                Username = username,
                Password = password,
                SslMode = SslMode.Require
            };
        }
        else
        {
            builder = new NpgsqlConnectionStringBuilder(trimmed);
        }

        // CRITICAL FIX: Disable client-side pooling when using Supabase pooler (Supavisor).
        // Npgsql's internal pool holds onto TCP connections, but Supavisor aggressively closes
        // idle connections. Reusing a closed connection causes SocketException (10054).
        builder.Pooling = false;

        return builder.ConnectionString;
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.Data.Sqlite;
using Npgsql;

namespace RevoltaLetrinhas.Api.Data;

/// <summary>
/// Informações sobre o banco de dados em uso (exibidas em /api/status e no log de inicialização).
/// </summary>
public record InfoBanco(string Provedor, string Local, bool Persistente, string Aviso);

/// <summary>
/// Decide qual banco de dados usar, nesta ordem:
///   1. ConnectionStrings:PostgresConnection (appsettings ou variável ConnectionStrings__PostgresConnection)
///   2. Variável de ambiente DATABASE_URL (formato postgres://usuario:senha@host:porta/banco — padrão do Render)
///   3. SQLite no arquivo definido em ConnectionStrings:SqliteConnection ou SQLITE_PATH (padrão: revoltaletrinhas.db)
/// </summary>
public static class ConfiguracaoBanco
{
    public static InfoBanco Registrar(WebApplicationBuilder builder)
    {
        var cfg = builder.Configuration;

        string? postgres = cfg.GetConnectionString("PostgresConnection");
        if (string.IsNullOrWhiteSpace(postgres))
        {
            postgres = ConverterDatabaseUrl(Environment.GetEnvironmentVariable("DATABASE_URL"));
        }

        if (!string.IsNullOrWhiteSpace(postgres))
        {
            builder.Services.AddDbContext<AppDbContext>(opt => opt.UseNpgsql(postgres));
            var csb = new NpgsqlConnectionStringBuilder(postgres);
            return new InfoBanco("PostgreSQL", $"{csb.Host}/{csb.Database}", true, "");
        }

        string caminho = cfg.GetConnectionString("SqliteConnection")
                         ?? Environment.GetEnvironmentVariable("SQLITE_PATH")
                         ?? "revoltaletrinhas.db";

        // Aceita tanto um caminho simples ("dados/banco.db") quanto "Data Source=..."
        string connSqlite = caminho.Contains('=') ? caminho : $"Data Source={caminho}";
        string arquivo = new SqliteConnectionStringBuilder(connSqlite).DataSource;
        string pasta = Path.GetDirectoryName(Path.GetFullPath(arquivo)) ?? "";
        if (!string.IsNullOrEmpty(pasta) && !Directory.Exists(pasta)) Directory.CreateDirectory(pasta);

        builder.Services.AddDbContext<AppDbContext>(opt => opt.UseSqlite(connSqlite));

        bool emContainer = string.Equals(Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER"), "true", StringComparison.OrdinalIgnoreCase);
        bool caminhoConfigurado = !string.IsNullOrWhiteSpace(cfg.GetConnectionString("SqliteConnection"))
                                  || !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("SQLITE_PATH"));
        bool persistente = !emContainer || caminhoConfigurado;

        string aviso = persistente
            ? ""
            : "ATENÇÃO: SQLite dentro de um container sem disco persistente. Os dados são APAGADOS a cada deploy/reinício. " +
              "Configure DATABASE_URL (PostgreSQL) ou SQLITE_PATH apontando para um disco persistente.";

        return new InfoBanco("SQLite", Path.GetFullPath(arquivo), persistente, aviso);
    }

    /// <summary>
    /// Converte "postgres://usuario:senha@host:porta/banco?sslmode=require" para o formato do Npgsql.
    /// </summary>
    public static string? ConverterDatabaseUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return null;
        url = url.Trim();
        if (!url.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase) &&
            !url.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase))
        {
            // Já está no formato "Host=...;Database=..."
            return url;
        }

        var uri = new Uri(url);
        string[] userInfo = uri.UserInfo.Split(':', 2);

        var csb = new NpgsqlConnectionStringBuilder
        {
            Host = uri.Host,
            Port = uri.Port > 0 ? uri.Port : 5432,
            Database = Uri.UnescapeDataString(uri.AbsolutePath.TrimStart('/')),
            Username = Uri.UnescapeDataString(userInfo[0]),
            Password = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : "",
            SslMode = SslMode.Prefer
        };

        if (uri.Query.Contains("sslmode=require", StringComparison.OrdinalIgnoreCase))
        {
            csb.SslMode = SslMode.Require;
        }

        return csb.ConnectionString;
    }
}

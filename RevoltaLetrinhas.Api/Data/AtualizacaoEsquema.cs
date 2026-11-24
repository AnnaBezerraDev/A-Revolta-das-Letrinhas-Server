using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;

namespace RevoltaLetrinhas.Api.Data;

/// <summary>
/// Atualiza um banco JÁ EXISTENTE para o modelo atual (o EnsureCreated só cria bancos novos).
/// Cada passo é idempotente: pode rodar em toda inicialização.
/// Se algo falhar, a exceção sobe e o servidor não inicia — assim, no Render, a versão anterior
/// continua no ar em vez de rodar com o banco pela metade.
/// </summary>
public static class AtualizacaoEsquema
{
    public static void Aplicar(AppDbContext db, ILogger logger)
    {
        bool postgres = (db.Database.ProviderName ?? "").Contains("Npgsql", StringComparison.OrdinalIgnoreCase);

        // v1.0.4 — limites da licença na escola
        AdicionarColuna(db, postgres, logger, "Escolas", "LimiteAlunos", "integer NULL", "INTEGER NULL");
        AdicionarColuna(db, postgres, logger, "Escolas", "LimiteProfessores", "integer NULL", "INTEGER NULL");
        AdicionarColuna(db, postgres, logger, "Escolas", "LimiteMaquinas", "integer NOT NULL DEFAULT 100", "INTEGER NOT NULL DEFAULT 100");

        // v1.0.4 — tabela de máquinas (o SQL é gerado pelo próprio EF para o banco em uso)
        CriarTabelaSeNaoExistir(db, logger, "Maquinas");

        // v1.0.8 — senha da coordenação da escola (substitui o acesso fixo "professor / 1234")
        AdicionarColuna(db, postgres, logger, "Escolas", "SenhaCoordenacaoHash", "character varying(200) NULL", "TEXT NULL");

        // v1.0.8 — personagens de perfil únicos por escola e acertos/erros por habilidade da BNCC
        CriarTabelaSeNaoExistir(db, logger, "PersonagensResgatados");
        CriarTabelaSeNaoExistir(db, logger, "AlunoHabilidades");
    }

    private static void AdicionarColuna(AppDbContext db, bool postgres, ILogger logger,
                                        string tabela, string coluna, string tipoPostgres, string tipoSqlite)
    {
        if (postgres)
        {
            string sql = "ALTER TABLE \"" + tabela + "\" ADD COLUMN IF NOT EXISTS \"" + coluna + "\" " + tipoPostgres + ";";
            db.Database.ExecuteSqlRaw(sql);
            return;
        }

        string consulta = "SELECT COUNT(*) AS \"Value\" FROM pragma_table_info('" + tabela + "') WHERE name = '" + coluna + "'";
        bool existe = db.Database.SqlQueryRaw<int>(consulta).AsEnumerable().First() > 0;
        if (!existe)
        {
            string sql = "ALTER TABLE \"" + tabela + "\" ADD COLUMN \"" + coluna + "\" " + tipoSqlite + ";";
            db.Database.ExecuteSqlRaw(sql);
            logger.LogInformation("Banco atualizado: coluna {Tabela}.{Coluna} criada.", tabela, coluna);
        }
    }

    private static void CriarTabelaSeNaoExistir(AppDbContext db, ILogger logger, string tabela)
    {
        string script = db.Database.GenerateCreateScript();
        string marcador = "\"" + tabela + "\"";

        foreach (string comando in Regex.Split(script, @";\s*(?:\r?\n|$)"))
        {
            string c = comando.Trim();
            if (c.Length == 0 || !c.Contains(marcador)) continue;
            bool criaTabela = c.StartsWith("CREATE TABLE " + marcador, StringComparison.Ordinal);
            bool criaIndice = Regex.IsMatch(c, @"^CREATE (UNIQUE )?INDEX ") && c.Contains(" ON " + marcador);
            if (!criaTabela && !criaIndice) continue;

            string seguro = c
                .Replace("CREATE TABLE " + marcador, "CREATE TABLE IF NOT EXISTS " + marcador)
                .Replace("CREATE UNIQUE INDEX \"", "CREATE UNIQUE INDEX IF NOT EXISTS \"")
                .Replace("CREATE INDEX \"", "CREATE INDEX IF NOT EXISTS \"");
            db.Database.ExecuteSqlRaw(seguro + ";");
        }
        logger.LogInformation("Banco conferido: tabela {Tabela} pronta.", tabela);
    }
}

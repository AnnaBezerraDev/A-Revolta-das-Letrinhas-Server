namespace RevoltaLetrinhas.Api.Servicos;

/// <summary>Monta o relatório por habilidade da BNCC (usado pelo jogo, pelo painel e pelo CSV).</summary>
public static class RelatorioHabilidades
{
    public class Linha
    {
        public string Codigo { get; set; } = "";
        public string Componente { get; set; } = "";
        public string Ano { get; set; } = "";
        public string Resumo { get; set; } = "";
        public string Descricao { get; set; } = "";
        public int Acertos { get; set; }
        public int Erros { get; set; }
        public int Respostas { get; set; }
        /// <summary>Percentual de acerto (0 a 100).</summary>
        public int Taxa { get; set; }
        public int AlunosAvaliados { get; set; }
        /// <summary>Alunos com pelo menos RespostasMinimas respostas e acerto abaixo de TaxaAtencao.</summary>
        public List<string> AlunosAtencao { get; set; } = new();
    }

    /// <param name="dados">(código, nome do aluno, acertos, erros) — uma entrada por aluno e habilidade.</param>
    public static List<Linha> Montar(IEnumerable<(string Codigo, string Aluno, int Acertos, int Erros)> dados)
    {
        var linhas = new List<Linha>();
        foreach (var grupo in dados.GroupBy(d => d.Codigo, StringComparer.OrdinalIgnoreCase))
        {
            if (!HabilidadesBncc.Todas.TryGetValue(grupo.Key, out var h)) continue;
            int acertos = grupo.Sum(g => g.Acertos), erros = grupo.Sum(g => g.Erros);
            var atencao = grupo
                .Where(g => g.Acertos + g.Erros >= HabilidadesBncc.RespostasMinimas &&
                            g.Acertos / (double)(g.Acertos + g.Erros) < HabilidadesBncc.TaxaAtencao)
                .OrderBy(g => g.Acertos / (double)(g.Acertos + g.Erros))
                .Select(g => g.Aluno)
                .ToList();
            linhas.Add(new Linha
            {
                Codigo = h.Codigo,
                Componente = h.Componente,
                Ano = h.Ano,
                Resumo = h.Resumo,
                Descricao = h.Descricao,
                Acertos = acertos,
                Erros = erros,
                Respostas = acertos + erros,
                Taxa = acertos + erros == 0 ? 0 : (int)Math.Round(100.0 * acertos / (acertos + erros)),
                AlunosAvaliados = grupo.Select(g => g.Aluno).Distinct().Count(),
                AlunosAtencao = atencao
            });
        }
        // Português antes de Matemática; dentro de cada um, pela ordem do código (ano)
        return linhas.OrderBy(l => l.Componente == "Matemática" ? 1 : 0).ThenBy(l => l.Codigo).ToList();
    }
}

using RevoltaLetrinhas.Api.DTOs;
using RevoltaLetrinhas.Api.Models;

namespace RevoltaLetrinhas.Api.Servicos;

/// <summary>
/// Cálculo único dos indicadores de desempenho (usado pelo painel web e pela Área do Professor do jogo).
/// </summary>
public static class CalculoDesempenho
{
    /// <summary>Cada matéria tem 3 níveis (Fácil, Médio, Difícil) com 30 fases cada.</summary>
    public const int FasesPorMateria = 90;

    /// <summary>
    /// Classifica o aluno pela quantidade de fases e pela taxa de erros (erros por fase concluída).
    /// No jogo cada fase permite no máximo 1 erro (2 vidas), então a taxa vai de 0 a 1.
    /// </summary>
    public static string Status(int totalFases, int erros)
    {
        if (totalFases <= 0) return "Sem Atividade";
        double taxaErro = (double)erros / totalFases;
        if (totalFases >= 10 && taxaErro <= 0.10) return "Excelente";
        if (totalFases >= 10 && taxaErro <= 0.30) return "Bom";
        if (taxaErro > 0.50) return "Precisa de Apoio";
        return "Em Progresso";
    }

    public static AlunoSalaDetalheDto ResumirAluno(Aluno aluno)
    {
        var concluidas = aluno.Progressos.Where(p => p.Concluida).ToList();
        int fasesPort = concluidas.Count(p => p.Materia == "Portugues");
        int fasesMath = concluidas.Count(p => p.Materia == "Matematica");
        int erros = concluidas.Sum(p => p.QuantidadeErros);

        return new AlunoSalaDetalheDto
        {
            Id = aluno.Id,
            NomeCompleto = aluno.NomeCompleto,
            NomeJogo = string.IsNullOrWhiteSpace(aluno.NomeJogo) ? aluno.NomeCompleto : aluno.NomeJogo,
            FasesPort = fasesPort,
            FasesMath = fasesMath,
            TotalFases = concluidas.Count,
            PontuacaoTotal = concluidas.Sum(p => p.Pontuacao),
            MediaEstrelas = concluidas.Count > 0 ? Math.Round(concluidas.Average(p => p.Estrelas), 1) : 0,
            ErrosTotais = erros,
            TempoTotalSegundos = concluidas.Sum(p => p.TempoGastoSegundos),
            StatusDesempenho = Status(concluidas.Count, erros),
            UltimaAtividade = aluno.Progressos.Count > 0 ? aluno.Progressos.Max(p => p.UltimaAtualizacao) : null
        };
    }

    public static ResumoSalaDto ResumirSala(Turma turma, bool incluirLoginProfessor)
    {
        var alunos = turma.Alunos
            .Where(a => a.Ativo)
            .Select(ResumirAluno)
            .OrderBy(a => a.NomeJogo)
            .ToList();

        int totalAlunos = alunos.Count;
        int somaFases = alunos.Sum(a => a.TotalFases);
        var comAtividade = alunos.Where(a => a.TotalFases > 0).ToList();

        double Taxa(int fases) => totalAlunos > 0
            ? Math.Min(100, Math.Round((double)fases / (totalAlunos * FasesPorMateria) * 100, 1))
            : 0;

        return new ResumoSalaDto
        {
            TurmaId = turma.Id,
            NomeTurma = turma.Nome,
            AnoLetivo = turma.AnoLetivo,
            NomeProfessor = turma.NomeProfessor,
            LoginProfessor = incluirLoginProfessor ? turma.LoginProfessor : null,
            TotalAlunos = totalAlunos,
            TotalFasesConcluidas = somaFases,
            MediaFasesPorAluno = totalAlunos > 0 ? Math.Round((double)somaFases / totalAlunos, 1) : 0,
            MediaEstrelas = comAtividade.Count > 0 ? Math.Round(comAtividade.Average(a => a.MediaEstrelas), 1) : 0,
            TotalErros = alunos.Sum(a => a.ErrosTotais),
            TaxaConclusaoPort = Taxa(alunos.Sum(a => a.FasesPort)),
            TaxaConclusaoMath = Taxa(alunos.Sum(a => a.FasesMath)),
            AlunosAtivos = comAtividade.Count,
            UltimaAtividade = alunos.Where(a => a.UltimaAtividade.HasValue).Select(a => a.UltimaAtividade).DefaultIfEmpty(null).Max(),
            Alunos = alunos
        };
    }
}

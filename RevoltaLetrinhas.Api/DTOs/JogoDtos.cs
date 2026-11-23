namespace RevoltaLetrinhas.Api.DTOs;

public class RegistrarProgressoDto
{
    public Guid AlunoId { get; set; }
    public string Materia { get; set; } = "Portugues"; // "Portugues" ou "Matematica"
    public string Nivel { get; set; } = "Facil";       // "Facil", "Medio", "Dificil"
    public int NumeroFase { get; set; }               // 1 a 30
    public bool Concluida { get; set; }
    public int Estrelas { get; set; }                 // 1 a 3
    public int Pontuacao { get; set; }
    public int QuantidadeErros { get; set; }
    public int TempoGastoSegundos { get; set; }
}

public class CriarAlunoDto
{
    // string (e não Guid) de propósito: o jogo envia "turmaId":"" quando não sabe o Id da sala,
    // e um Guid vazio fazia o servidor recusar TODO cadastro de aluno vindo do jogo (erro 400).
    public string? TurmaId { get; set; }
    public string? NomeTurma { get; set; }
    public string NomeCompleto { get; set; } = string.Empty;
    public string? NomeJogo { get; set; }
}

public class SinalMaquinaDto
{
    public string Identificador { get; set; } = string.Empty;
    public string? Nome { get; set; }
    public string? VersaoJogo { get; set; }
}

public class ProgressoLoteDto
{
    public string NomeAluno { get; set; } = string.Empty;
    public string? NomeTurma { get; set; }
    public List<ProgressoItemDto> Itens { get; set; } = new();
}

/// <summary>Fase do lote. Sem AlunoId de propósito: o jogo envia "alunoId":"" e um Guid recusaria o JSON.</summary>
public class ProgressoItemDto
{
    public string Materia { get; set; } = "Portugues";
    public string Nivel { get; set; } = "Facil";
    public int NumeroFase { get; set; }
    public bool Concluida { get; set; }
    public int Estrelas { get; set; }
    public int Pontuacao { get; set; }
    public int QuantidadeErros { get; set; }
    public int TempoGastoSegundos { get; set; }
}

public class CriarTurmaJogoDto
{
    public string NomeTurma { get; set; } = string.Empty;
}

public class AtualizarTurmaAlunoDto
{
    public string NomeAluno { get; set; } = string.Empty;
    public string NomeTurma { get; set; } = string.Empty;
}

public class ExcluirAlunoDto
{
    public string NomeAluno { get; set; } = string.Empty;
}

public class AlunoResumoDto
{
    public Guid Id { get; set; }
    public string NomeCompleto { get; set; } = string.Empty;
    public string NomeJogo { get; set; } = string.Empty;
    public Guid TurmaId { get; set; }
    public string TurmaNome { get; set; } = string.Empty;
}

public class TurmaComAlunosDto
{
    public Guid TurmaId { get; set; }
    public string Nome { get; set; } = string.Empty;
    public string? NomeProfessor { get; set; }
    public string? LoginProfessor { get; set; }
    public List<AlunoResumoDto> Alunos { get; set; } = new();
}

public class InfoEscolaDto
{
    public Guid EscolaId { get; set; }
    public string NomeEscola { get; set; } = string.Empty;
    public string Municipio { get; set; } = string.Empty;
    public string UF { get; set; } = string.Empty;
    public DateTime? DataExpiracaoContrato { get; set; }
    public string? NumeroConvenio { get; set; }
    public int? DiasRestantesContrato { get; set; }
    public bool ContratoValido { get; set; } = true;
    public string TokenAcesso { get; set; } = string.Empty;
    public bool TemLimiteAlunos { get; set; }
    public int LimiteAlunos { get; set; }
    public int TotalAlunos { get; set; }
    public int LimiteMaquinas { get; set; }
    public List<TurmaComAlunosDto> Turmas { get; set; } = new();
}

public class EscolaDisponivelDto
{
    public string Token { get; set; } = string.Empty;
    public string Nome { get; set; } = string.Empty;
    public string Municipio { get; set; } = string.Empty;
    public string UF { get; set; } = string.Empty;
    public DateTime? DataExpiracaoContrato { get; set; }
    public int TotalAlunos { get; set; }
}

public class LoginProfessorDto
{
    public string Login { get; set; } = string.Empty;
    public string Senha { get; set; } = string.Empty;
}

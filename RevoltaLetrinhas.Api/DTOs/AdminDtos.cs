namespace RevoltaLetrinhas.Api.DTOs;

public class CriarMunicipioDto
{
    public string Nome { get; set; } = string.Empty;
    public string UF { get; set; } = "SP";
    public DateTime? DataExpiracaoContrato { get; set; }
    public string? NumeroConvenio { get; set; }
}

public class AtualizarMunicipioDto
{
    public string? Nome { get; set; }
    public string? UF { get; set; }
    public DateTime? DataExpiracaoContrato { get; set; }
    public string? NumeroConvenio { get; set; }
}

public class CriarEscolaDto
{
    public Guid MunicipioId { get; set; }
    public string Nome { get; set; } = string.Empty;
    public string? CodigoInep { get; set; }
    public string? TokenAcesso { get; set; }
}

public class CriarTurmaDto
{
    public Guid EscolaId { get; set; }
    public string Nome { get; set; } = string.Empty;
    public int AnoLetivo { get; set; } = DateTime.UtcNow.Year;
    public string? NomeProfessor { get; set; }
    public string? LoginProfessor { get; set; }
    public string? SenhaProfessor { get; set; }
}

public class AtualizarTurmaDto
{
    public string? Nome { get; set; }
    public int? AnoLetivo { get; set; }
    public string? NomeProfessor { get; set; }
    public string? LoginProfessor { get; set; }
    public string? SenhaProfessor { get; set; }
}

public class CadastrarAlunoAdminDto
{
    public Guid TurmaId { get; set; }
    public string NomeCompleto { get; set; } = string.Empty;
    public string? NomeJogo { get; set; }
}

public class ResumoSalaDto
{
    public Guid TurmaId { get; set; }
    public string NomeTurma { get; set; } = string.Empty;
    public int AnoLetivo { get; set; }
    public string? NomeProfessor { get; set; }
    public string? LoginProfessor { get; set; }
    public int TotalAlunos { get; set; }
    public int TotalFasesConcluidas { get; set; }
    public double MediaFasesPorAluno { get; set; }
    public double MediaEstrelas { get; set; }
    public int TotalErros { get; set; }
    public double TaxaConclusaoPort { get; set; }
    public double TaxaConclusaoMath { get; set; }
    public int AlunosAtivos { get; set; }
    public DateTime? UltimaAtividade { get; set; }
    public List<AlunoSalaDetalheDto> Alunos { get; set; } = new();
}

public class AlunoSalaDetalheDto
{
    public Guid Id { get; set; }
    public string NomeCompleto { get; set; } = string.Empty;
    public string NomeJogo { get; set; } = string.Empty;
    public int FasesPort { get; set; }
    public int FasesMath { get; set; }
    public int TotalFases { get; set; }
    public int PontuacaoTotal { get; set; }
    public double MediaEstrelas { get; set; }
    public int ErrosTotais { get; set; }
    public int TempoTotalSegundos { get; set; }
    public string StatusDesempenho { get; set; } = "Regular";
    public DateTime? UltimaAtividade { get; set; }
}

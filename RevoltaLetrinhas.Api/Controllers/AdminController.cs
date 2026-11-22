using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RevoltaLetrinhas.Api.Data;
using RevoltaLetrinhas.Api.DTOs;
using RevoltaLetrinhas.Api.Models;

namespace RevoltaLetrinhas.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AdminController : ControllerBase
{
    private readonly AppDbContext _db;

    public AdminController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet("visao-geral")]
    public async Task<IActionResult> ObterVisaoGeral()
    {
        var totalMunicipios = await _db.Municipios.CountAsync();
        var totalEscolas = await _db.Escolas.CountAsync();
        var totalTurmas = await _db.Turmas.CountAsync();
        var totalAlunos = await _db.Alunos.CountAsync();
        var totalFasesConcluidas = await _db.ProgressoFases.CountAsync(p => p.Concluida);

        var escolas = await _db.Escolas
            .Include(e => e.Municipio)
            .Include(e => e.Turmas)
                .ThenInclude(t => t.Alunos)
                    .ThenInclude(a => a.Progressos)
            .Select(e => new
            {
                e.Id,
                e.Nome,
                e.TokenAcesso,
                MunicipioId = e.MunicipioId,
                Municipio = e.Municipio != null ? $"{e.Municipio.Nome}/{e.Municipio.UF}" : "Geral",
                TotalTurmas = e.Turmas.Count,
                TotalAlunos = e.Turmas.SelectMany(t => t.Alunos).Count(),
                FasesConcluidas = e.Turmas.SelectMany(t => t.Alunos).SelectMany(a => a.Progressos).Count(p => p.Concluida)
            })
            .ToListAsync();

        var municipios = await _db.Municipios
            .Select(m => new
            {
                m.Id,
                m.Nome,
                m.UF,
                m.DataExpiracaoContrato,
                m.NumeroConvenio,
                TotalEscolas = m.Escolas.Count,
                DiasRestantes = m.DataExpiracaoContrato.HasValue
                    ? (int)Math.Ceiling((m.DataExpiracaoContrato.Value - DateTime.UtcNow).TotalDays)
                    : (int?)null
            })
            .ToListAsync();

        return Ok(new
        {
            Metricas = new
            {
                TotalMunicipios = totalMunicipios,
                TotalEscolas = totalEscolas,
                TotalTurmas = totalTurmas,
                TotalAlunos = totalAlunos,
                TotalFasesConcluidas = totalFasesConcluidas
            },
            Escolas = escolas,
            Municipios = municipios
        });
    }

    /// <summary>
    /// Resumo detalhado por sala/turma para os professores avaliarem o desempenho coletivo.
    /// </summary>
    [HttpGet("escola/{escolaId:guid}/salas-resumo")]
    public async Task<IActionResult> ObterDesempenhoSalas(Guid escolaId)
    {
        var escola = await _db.Escolas
            .Include(e => e.Municipio)
            .Include(e => e.Turmas)
                .ThenInclude(t => t.Alunos)
                    .ThenInclude(a => a.Progressos)
            .FirstOrDefaultAsync(e => e.Id == escolaId);

        if (escola == null) return NotFound(new { erro = "Escola não encontrada." });

        var salasResumo = new List<ResumoSalaDto>();

        foreach (var turma in escola.Turmas.OrderBy(t => t.Nome))
        {
            var alunosDto = new List<AlunoSalaDetalheDto>();
            int somaFases = 0;
            int somaErros = 0;
            double somaEstrelas = 0;
            int alunosComFases = 0;

            foreach (var aluno in turma.Alunos.Where(a => a.Ativo).OrderBy(a => a.NomeJogo))
            {
                var fasesPort = aluno.Progressos.Count(p => p.Concluida && p.Materia == "Portugues");
                var fasesMath = aluno.Progressos.Count(p => p.Concluida && p.Materia == "Matematica");
                var totalFases = aluno.Progressos.Count(p => p.Concluida);
                var pontos = aluno.Progressos.Sum(p => p.Pontuacao);
                var erros = aluno.Progressos.Sum(p => p.QuantidadeErros);
                var mediaEstrelas = aluno.Progressos.Any() ? Math.Round(aluno.Progressos.Average(p => p.Estrelas), 1) : 0;

                string status = "Sem Atividade";
                if (totalFases >= 10) status = erros <= 3 ? "Excelente" : "Bom";
                else if (totalFases > 0) status = erros > 5 ? "Precisa de Apoio" : "Em Progresso";

                somaFases += totalFases;
                somaErros += erros;
                if (totalFases > 0)
                {
                    somaEstrelas += mediaEstrelas;
                    alunosComFases++;
                }

                alunosDto.Add(new AlunoSalaDetalheDto
                {
                    Id = aluno.Id,
                    NomeCompleto = aluno.NomeCompleto,
                    NomeJogo = string.IsNullOrWhiteSpace(aluno.NomeJogo) ? aluno.NomeCompleto : aluno.NomeJogo,
                    FasesPort = fasesPort,
                    FasesMath = fasesMath,
                    TotalFases = totalFases,
                    PontuacaoTotal = pontos,
                    MediaEstrelas = mediaEstrelas,
                    ErrosTotais = erros,
                    StatusDesempenho = status
                });
            }

            int totalAlunos = turma.Alunos.Count;
            double taxaPort = totalAlunos > 0 ? Math.Round((double)alunosDto.Sum(a => a.FasesPort) / (totalAlunos * 30) * 100, 1) : 0;
            double taxaMath = totalAlunos > 0 ? Math.Round((double)alunosDto.Sum(a => a.FasesMath) / (totalAlunos * 30) * 100, 1) : 0;

            salasResumo.Add(new ResumoSalaDto
            {
                TurmaId = turma.Id,
                NomeTurma = turma.Nome,
                AnoLetivo = turma.AnoLetivo,
                NomeProfessor = turma.NomeProfessor,
                LoginProfessor = turma.LoginProfessor,
                TotalAlunos = totalAlunos,
                TotalFasesConcluidas = somaFases,
                MediaFasesPorAluno = totalAlunos > 0 ? Math.Round((double)somaFases / totalAlunos, 1) : 0,
                MediaEstrelas = alunosComFases > 0 ? Math.Round(somaEstrelas / alunosComFases, 1) : 0,
                TotalErros = somaErros,
                TaxaConclusaoPort = Math.Min(100, taxaPort),
                TaxaConclusaoMath = Math.Min(100, taxaMath),
                Alunos = alunosDto
            });
        }

        return Ok(new
        {
            EscolaId = escola.Id,
            NomeEscola = escola.Nome,
            Municipio = escola.Municipio != null ? $"{escola.Municipio.Nome}/{escola.Municipio.UF}" : "Geral",
            DataExpiracao = escola.Municipio?.DataExpiracaoContrato,
            TokenAcesso = escola.TokenAcesso,
            Salas = salasResumo
        });
    }

    /// <summary>
    /// Exportação e download oficial dos dados pedagógicos da escola em formato CSV (compatível com Excel)
    /// </summary>
    [HttpGet("escola/{escolaId:guid}/exportar-csv")]
    public async Task<IActionResult> ExportarDadosEscolaCsv(Guid escolaId)
    {
        var escola = await _db.Escolas
            .Include(e => e.Municipio)
            .Include(e => e.Turmas)
                .ThenInclude(t => t.Alunos)
                    .ThenInclude(a => a.Progressos)
            .FirstOrDefaultAsync(e => e.Id == escolaId);

        if (escola == null) return NotFound("Escola não encontrada.");

        var sb = new StringBuilder();
        // Cabeçalho CSV
        sb.AppendLine("Municipio;Escola;TokenEscola;Turma;ProfessorResponsavel;AlunoNomeCompleto;NomeNoJogo;FasesPortugues;FasesMatematica;TotalFases;Pontuacao;MediaEstrelas;ErrosTotais;StatusDesempenho;UltimaAtividade");

        string munNome = escola.Municipio != null ? $"{escola.Municipio.Nome}-{escola.Municipio.UF}" : "Geral";

        foreach (var turma in escola.Turmas.OrderBy(t => t.Nome))
        {
            foreach (var aluno in turma.Alunos.Where(a => a.Ativo).OrderBy(a => a.NomeCompleto))
            {
                int port = aluno.Progressos.Count(p => p.Concluida && p.Materia == "Portugues");
                int math = aluno.Progressos.Count(p => p.Concluida && p.Materia == "Matematica");
                int total = aluno.Progressos.Count(p => p.Concluida);
                int pontos = aluno.Progressos.Sum(p => p.Pontuacao);
                int erros = aluno.Progressos.Sum(p => p.QuantidadeErros);
                double estrelas = aluno.Progressos.Any() ? Math.Round(aluno.Progressos.Average(p => p.Estrelas), 1) : 0;
                string status = total >= 10 ? (erros <= 3 ? "Excelente" : "Bom") : (total > 0 ? (erros > 5 ? "Precisa de Apoio" : "Em Progresso") : "Sem Atividade");
                string ultimaAtiv = aluno.Progressos.Any() ? aluno.Progressos.Max(p => p.UltimaAtualizacao).ToString("dd/MM/yyyy HH:mm") : "-";

                sb.AppendLine($"{munNome};{escola.Nome};{escola.TokenAcesso};{turma.Nome};{turma.NomeProfessor ?? "Nenhum"};{aluno.NomeCompleto};{aluno.NomeJogo};{port};{math};{total};{pontos};{estrelas};{erros};{status};{ultimaAtiv}");
            }
        }

        byte[] buffer = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
        string filename = $"Relatorio_Desempenho_{escola.Nome.Replace(" ", "_")}_{DateTime.Now:yyyyMMdd}.csv";

        return File(buffer, "text/csv; charset=utf-8", filename);
    }

    [HttpPost("municipios")]
    public async Task<IActionResult> CriarMunicipio([FromBody] CriarMunicipioDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Nome)) return BadRequest("Nome do município é obrigatório.");
        var mun = new Municipio
        {
            Id = Guid.NewGuid(),
            Nome = dto.Nome.Trim(),
            UF = (dto.UF ?? "SP").Trim().ToUpper(),
            DataExpiracaoContrato = dto.DataExpiracaoContrato,
            NumeroConvenio = dto.NumeroConvenio?.Trim(),
            DataCriacao = DateTime.UtcNow
        };
        _db.Municipios.Add(mun);
        await _db.SaveChangesAsync();
        return Ok(mun);
    }

    [HttpPut("municipios/{municipioId:guid}/contrato")]
    public async Task<IActionResult> AtualizarContratoMunicipio(Guid municipioId, [FromBody] AtualizarMunicipioDto dto)
    {
        var mun = await _db.Municipios.FindAsync(municipioId);
        if (mun == null) return NotFound("Município não encontrado.");

        if (dto.DataExpiracaoContrato.HasValue) mun.DataExpiracaoContrato = dto.DataExpiracaoContrato.Value;
        if (!string.IsNullOrWhiteSpace(dto.NumeroConvenio)) mun.NumeroConvenio = dto.NumeroConvenio.Trim();
        if (!string.IsNullOrWhiteSpace(dto.Nome)) mun.Nome = dto.Nome.Trim();
        if (!string.IsNullOrWhiteSpace(dto.UF)) mun.UF = dto.UF.Trim().ToUpper();

        await _db.SaveChangesAsync();
        return Ok(mun);
    }

    [HttpDelete("municipios/{municipioId:guid}")]
    public async Task<IActionResult> ExcluirMunicipio(Guid municipioId)
    {
        var mun = await _db.Municipios
            .Include(m => m.Escolas)
                .ThenInclude(e => e.Turmas)
                    .ThenInclude(t => t.Alunos)
                        .ThenInclude(a => a.Progressos)
            .FirstOrDefaultAsync(m => m.Id == municipioId);

        if (mun == null) return NotFound("Município não encontrado.");

        foreach (var escola in mun.Escolas)
        {
            foreach (var turma in escola.Turmas)
            {
                foreach (var aluno in turma.Alunos)
                {
                    _db.ProgressoFases.RemoveRange(aluno.Progressos);
                }
                _db.Alunos.RemoveRange(turma.Alunos);
            }
            _db.Turmas.RemoveRange(escola.Turmas);
        }
        _db.Escolas.RemoveRange(mun.Escolas);
        _db.Municipios.Remove(mun);

        await _db.SaveChangesAsync();
        return Ok(new { mensagem = "Município e todos os seus vínculos excluídos com sucesso." });
    }

    [HttpPost("escolas")]
    public async Task<IActionResult> CriarEscola([FromBody] CriarEscolaDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Nome)) return BadRequest("Nome da escola é obrigatório.");
        var mun = await _db.Municipios.FindAsync(dto.MunicipioId);
        if (mun == null) return NotFound("Município não encontrado.");

        string token = !string.IsNullOrWhiteSpace(dto.TokenAcesso)
            ? dto.TokenAcesso.Trim().ToUpper()
            : $"ESC-{mun.UF}-{Guid.NewGuid().ToString("N")[..6].ToUpper()}";

        var escola = new Escola
        {
            Id = Guid.NewGuid(),
            MunicipioId = mun.Id,
            Nome = dto.Nome.Trim(),
            CodigoInep = dto.CodigoInep?.Trim(),
            TokenAcesso = token,
            Ativa = true,
            DataCriacao = DateTime.UtcNow
        };

        _db.Escolas.Add(escola);
        await _db.SaveChangesAsync();

        return Ok(escola);
    }

    /// <summary>
    /// Exclusão de Escola e cascata de suas turmas e alunos
    /// </summary>
    [HttpDelete("escolas/{escolaId:guid}")]
    public async Task<IActionResult> ExcluirEscola(Guid escolaId)
    {
        var escola = await _db.Escolas
            .Include(e => e.Turmas)
                .ThenInclude(t => t.Alunos)
                    .ThenInclude(a => a.Progressos)
            .FirstOrDefaultAsync(e => e.Id == escolaId);

        if (escola == null) return NotFound("Escola não encontrada.");

        foreach (var turma in escola.Turmas)
        {
            foreach (var aluno in turma.Alunos)
            {
                _db.ProgressoFases.RemoveRange(aluno.Progressos);
            }
            _db.Alunos.RemoveRange(turma.Alunos);
        }
        _db.Turmas.RemoveRange(escola.Turmas);
        _db.Escolas.Remove(escola);

        await _db.SaveChangesAsync();
        return Ok(new { mensagem = $"Escola '{escola.Nome}' excluída com sucesso." });
    }

    [HttpPost("turmas")]
    public async Task<IActionResult> CriarTurma([FromBody] CriarTurmaDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Nome)) return BadRequest("Nome da turma/sala é obrigatório.");
        var escola = await _db.Escolas.FindAsync(dto.EscolaId);
        if (escola == null) return NotFound("Escola não encontrada.");

        var turma = new Turma
        {
            Id = Guid.NewGuid(),
            EscolaId = escola.Id,
            Nome = dto.Nome.Trim(),
            AnoLetivo = dto.AnoLetivo > 0 ? dto.AnoLetivo : DateTime.UtcNow.Year,
            NomeProfessor = dto.NomeProfessor?.Trim(),
            LoginProfessor = dto.LoginProfessor?.Trim().ToLower(),
            SenhaProfessor = dto.SenhaProfessor?.Trim()
        };

        _db.Turmas.Add(turma);
        await _db.SaveChangesAsync();
        return Ok(turma);
    }

    /// <summary>
    /// Vincula ou atualiza o professor exclusivo responsável por uma turma
    /// </summary>
    [HttpPut("turmas/{turmaId:guid}/professor")]
    public async Task<IActionResult> AtualizarProfessorTurma(Guid turmaId, [FromBody] AtualizarTurmaDto dto)
    {
        var turma = await _db.Turmas.FindAsync(turmaId);
        if (turma == null) return NotFound("Turma/Sala não encontrada.");

        if (!string.IsNullOrWhiteSpace(dto.NomeProfessor)) turma.NomeProfessor = dto.NomeProfessor.Trim();
        if (!string.IsNullOrWhiteSpace(dto.LoginProfessor)) turma.LoginProfessor = dto.LoginProfessor.Trim().ToLower();
        if (!string.IsNullOrWhiteSpace(dto.SenhaProfessor)) turma.SenhaProfessor = dto.SenhaProfessor.Trim();

        await _db.SaveChangesAsync();
        return Ok(turma);
    }

    /// <summary>
    /// Exclusão de Turma/Sala e seus respectivos alunos
    /// </summary>
    [HttpDelete("turmas/{turmaId:guid}")]
    public async Task<IActionResult> ExcluirTurma(Guid turmaId)
    {
        var turma = await _db.Turmas
            .Include(t => t.Alunos)
                .ThenInclude(a => a.Progressos)
            .FirstOrDefaultAsync(t => t.Id == turmaId);

        if (turma == null) return NotFound("Turma/Sala não encontrada.");

        foreach (var aluno in turma.Alunos)
        {
            _db.ProgressoFases.RemoveRange(aluno.Progressos);
        }
        _db.Alunos.RemoveRange(turma.Alunos);
        _db.Turmas.Remove(turma);

        await _db.SaveChangesAsync();
        return Ok(new { mensagem = $"Turma/Sala '{turma.Nome}' excluída com sucesso." });
    }

    [HttpPost("alunos")]
    public async Task<IActionResult> CadastrarAluno([FromBody] CadastrarAlunoAdminDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.NomeCompleto)) return BadRequest("Nome do aluno é obrigatório.");
        var turma = await _db.Turmas.FindAsync(dto.TurmaId);
        if (turma == null) return NotFound("Turma não encontrada.");

        var aluno = new Aluno
        {
            Id = Guid.NewGuid(),
            TurmaId = turma.Id,
            NomeCompleto = dto.NomeCompleto.Trim(),
            NomeJogo = string.IsNullOrWhiteSpace(dto.NomeJogo) ? dto.NomeCompleto.Trim().Split(' ')[0] : dto.NomeJogo.Trim(),
            Ativo = true,
            DataCriacao = DateTime.UtcNow
        };

        _db.Alunos.Add(aluno);
        await _db.SaveChangesAsync();
        return Ok(aluno);
    }

    [HttpDelete("alunos/{alunoId:guid}")]
    public async Task<IActionResult> ExcluirAlunoAdmin(Guid alunoId)
    {
        var aluno = await _db.Alunos
            .Include(a => a.Progressos)
            .FirstOrDefaultAsync(a => a.Id == alunoId);

        if (aluno == null) return NotFound("Aluno não encontrado.");

        _db.ProgressoFases.RemoveRange(aluno.Progressos);
        _db.Alunos.Remove(aluno);
        await _db.SaveChangesAsync();
        return Ok(new { mensagem = "Aluno excluído com sucesso." });
    }
}

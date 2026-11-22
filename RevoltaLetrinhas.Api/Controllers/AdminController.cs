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
            .Select(m => new { m.Id, m.Nome, m.UF, TotalEscolas = m.Escolas.Count })
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
            // Cada matéria tem 30 fases (total 60)
            double taxaPort = totalAlunos > 0 ? Math.Round((double)alunosDto.Sum(a => a.FasesPort) / (totalAlunos * 30) * 100, 1) : 0;
            double taxaMath = totalAlunos > 0 ? Math.Round((double)alunosDto.Sum(a => a.FasesMath) / (totalAlunos * 30) * 100, 1) : 0;

            salasResumo.Add(new ResumoSalaDto
            {
                TurmaId = turma.Id,
                NomeTurma = turma.Nome,
                AnoLetivo = turma.AnoLetivo,
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
            TokenAcesso = escola.TokenAcesso,
            Salas = salasResumo
        });
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
            DataCriacao = DateTime.UtcNow
        };
        _db.Municipios.Add(mun);
        await _db.SaveChangesAsync();
        return Ok(mun);
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
            AnoLetivo = dto.AnoLetivo > 0 ? dto.AnoLetivo : DateTime.UtcNow.Year
        };

        _db.Turmas.Add(turma);
        await _db.SaveChangesAsync();
        return Ok(turma);
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

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RevoltaLetrinhas.Api.Data;
using RevoltaLetrinhas.Api.DTOs;
using RevoltaLetrinhas.Api.Models;

namespace RevoltaLetrinhas.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class JogoController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ILogger<JogoController> _logger;

    public JogoController(AppDbContext db, ILogger<JogoController> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// Consulta a versão mais recente do jogo e se há atualizações para as escolas.
    /// </summary>
    [HttpGet("versao")]
    public IActionResult ObterVersaoJogo()
    {
        var patchPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "patch", "Assembly-CSharp.dll");
        bool temPatch = System.IO.File.Exists(patchPath);
        long tamanho = temPatch ? new FileInfo(patchPath).Length : 0;
        DateTime ultimaModificacao = temPatch ? System.IO.File.GetLastWriteTimeUtc(patchPath) : DateTime.UtcNow;

        return Ok(new
        {
            versao = "1.0.1",
            versaoCodigo = 101,
            dataPublicacao = ultimaModificacao.ToString("yyyy-MM-ddTHH:mm:ssZ"),
            temPatch = temPatch,
            tamanhoBytes = tamanho,
            urlDownloadPatch = "/api/jogo/download-patch",
            novidades = "Atualizações pedagógicas da BNCC, acesso individual para professores e fechamento seguro."
        });
    }

    /// <summary>
    /// Baixa a DLL atualizada do jogo para aplicação direta sem precisar baixar o jogo inteiro de novo.
    /// </summary>
    [HttpGet("download-patch")]
    public IActionResult DownloadPatch()
    {
        var patchPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "patch", "Assembly-CSharp.dll");
        if (!System.IO.File.Exists(patchPath))
        {
            return NotFound("Nenhum arquivo de atualização disponível no momento.");
        }
        return PhysicalFile(patchPath, "application/octet-stream", "Assembly-CSharp.dll");
    }

    /// <summary>
    /// Permite que a desenvolvedora envie um novo patch Assembly-CSharp.dll para distribuir a todas as escolas.
    /// </summary>
    [HttpPost("upload-patch")]
    public async Task<IActionResult> UploadPatch(IFormFile arquivoPatch)
    {
        if (arquivoPatch == null || arquivoPatch.Length == 0) return BadRequest("Arquivo do patch não enviado.");
        var dir = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "patch");
        if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
        var dest = Path.Combine(dir, "Assembly-CSharp.dll");
        using (var stream = new FileStream(dest, FileMode.Create))
        {
            await arquivoPatch.CopyToAsync(stream);
        }
        _logger.LogInformation("Novo patch publicado com sucesso! Tamanho: {Bytes} bytes", arquivoPatch.Length);
        return Ok(new { mensagem = "Patch publicado com sucesso! Todas as escolas atualizarão automaticamente ao abrir o jogo." });
    }

    /// <summary>
    /// Retorna todas as escolas cadastradas para que o professor possa selecionar no jogo se necessário.
    /// </summary>
    [HttpGet("escolas-disponiveis")]
    public async Task<IActionResult> ListarEscolasDisponiveis()
    {
        var escolas = await _db.Escolas
            .Include(e => e.Municipio)
            .Include(e => e.Turmas)
                .ThenInclude(t => t.Alunos)
            .Where(e => e.Ativa)
            .OrderBy(e => e.Nome)
            .Select(e => new EscolaDisponivelDto
            {
                Token = e.TokenAcesso,
                Nome = e.Nome,
                Municipio = e.Municipio != null ? e.Municipio.Nome : "Geral",
                UF = e.Municipio != null ? e.Municipio.UF : "SP",
                DataExpiracaoContrato = e.Municipio != null ? e.Municipio.DataExpiracaoContrato : null,
                TotalAlunos = e.Turmas.SelectMany(t => t.Alunos).Count()
            })
            .ToListAsync();

        return Ok(escolas);
    }

    /// <summary>
    /// Chamado pelo jogo .exe ao iniciar no laboratório para identificar a escola e listar APENAS as turmas/alunos DESTA escola.
    /// </summary>
    [HttpGet("conectar")]
    public async Task<IActionResult> ConectarEscola([FromQuery] string? token, [FromHeader(Name = "X-Escola-Token")] string? tokenHeader)
    {
        var tokenEfetivo = !string.IsNullOrWhiteSpace(tokenHeader) ? tokenHeader : token;
        if (string.IsNullOrWhiteSpace(tokenEfetivo))
        {
            return BadRequest(new { erro = "Token da escola não informado." });
        }

        var escola = await _db.Escolas
            .Include(e => e.Municipio)
            .Include(e => e.Turmas)
                .ThenInclude(t => t.Alunos.Where(a => a.Ativo))
            .FirstOrDefaultAsync(e => e.TokenAcesso == tokenEfetivo && e.Ativa);

        if (escola == null)
        {
            return NotFound(new { erro = $"Escola com token '{tokenEfetivo}' não encontrada ou inativa." });
        }

        DateTime? expContrato = escola.Municipio?.DataExpiracaoContrato;
        int? diasRestantes = expContrato.HasValue
            ? (int)Math.Ceiling((expContrato.Value - DateTime.UtcNow).TotalDays)
            : null;
        bool contratoValido = !diasRestantes.HasValue || diasRestantes.Value >= 0;

        var info = new InfoEscolaDto
        {
            EscolaId = escola.Id,
            NomeEscola = escola.Nome,
            Municipio = escola.Municipio?.Nome ?? "Geral",
            UF = escola.Municipio?.UF ?? "BR",
            DataExpiracaoContrato = expContrato,
            NumeroConvenio = escola.Municipio?.NumeroConvenio,
            DiasRestantesContrato = diasRestantes,
            ContratoValido = contratoValido,
            TokenAcesso = escola.TokenAcesso,
            Turmas = escola.Turmas.Select(t => new TurmaComAlunosDto
            {
                TurmaId = t.Id,
                Nome = t.Nome,
                NomeProfessor = t.NomeProfessor,
                LoginProfessor = t.LoginProfessor,
                Alunos = t.Alunos.Select(a => new AlunoResumoDto
                {
                    Id = a.Id,
                    NomeCompleto = a.NomeCompleto,
                    NomeJogo = string.IsNullOrWhiteSpace(a.NomeJogo) ? a.NomeCompleto : a.NomeJogo,
                    TurmaId = t.Id,
                    TurmaNome = t.Nome
                }).OrderBy(a => a.NomeJogo).ToList()
            }).ToList()
        };

        return Ok(info);
    }

    /// <summary>
    /// Validação de login de professor (Geral ou Específico de Turma/Sala)
    /// </summary>
    [HttpPost("login-professor")]
    public async Task<IActionResult> AutenticarProfessor(
        [FromHeader(Name = "X-Escola-Token")] string? tokenHeader,
        [FromQuery] string? token,
        [FromBody] LoginProfessorDto dto)
    {
        if (dto == null || string.IsNullOrWhiteSpace(dto.Login) || string.IsNullOrWhiteSpace(dto.Senha))
        {
            return BadRequest(new { sucesso = false, erro = "Login e senha obrigatórios." });
        }

        string rawUser = dto.Login.Trim();
        string user = rawUser.ToLower();
        string userNorm = user.Replace(".", "").Replace("_", "").Replace("-", "").Replace(" ", "");
        string pass = dto.Senha.Trim();

        // 1. Verifica se é o admin padrão da escola
        if ((user == "professor" || userNorm == "professor") && pass == "1234")
        {
            return Ok(new
            {
                sucesso = true,
                tipo = "AdministradorEscola",
                nome = "Professor(a) Geral",
                turmaId = (Guid?)null,
                turmaNome = "Todas as Salas"
            });
        }

        var tokenEfetivo = !string.IsNullOrWhiteSpace(tokenHeader) ? tokenHeader.Trim() : token?.Trim();
        Escola? escola = null;
        if (!string.IsNullOrWhiteSpace(tokenEfetivo))
        {
            escola = await _db.Escolas
                .Include(e => e.Turmas)
                    .ThenInclude(t => t.Alunos)
                .FirstOrDefaultAsync(e => e.TokenAcesso == tokenEfetivo && e.Ativa);
        }

        // 2. Verifica se é um professor exclusivo cadastrado na escola informada
        if (escola != null && escola.Turmas != null)
        {
            var turmaDoProf = escola.Turmas.FirstOrDefault(t =>
                !string.IsNullOrWhiteSpace(t.LoginProfessor) &&
                (string.Equals(t.LoginProfessor.Trim(), user, StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(t.LoginProfessor.Trim().ToLower().Replace(".", "").Replace("_", "").Replace("-", "").Replace(" ", ""), userNorm, StringComparison.OrdinalIgnoreCase)) &&
                string.Equals(t.SenhaProfessor?.Trim(), pass));

            if (turmaDoProf != null)
            {
                return Ok(new
                {
                    sucesso = true,
                    tipo = "ProfessorSala",
                    nome = turmaDoProf.NomeProfessor ?? "Professor da Sala",
                    turmaId = (Guid?)turmaDoProf.Id,
                    turmaNome = turmaDoProf.Nome,
                    escolaNome = escola.Nome,
                    tokenEscola = escola.TokenAcesso
                });
            }
        }

        // 3. Fallback inteligente: Procura em QUALQUER escola ativa do servidor se o professor existir
        var todasTurmas = await _db.Turmas
            .Include(t => t.Escola)
            .Where(t => t.Escola != null && t.Escola.Ativa && !string.IsNullOrWhiteSpace(t.LoginProfessor))
            .ToListAsync();

        var turmaGlobal = todasTurmas.FirstOrDefault(t =>
            (string.Equals(t.LoginProfessor!.Trim(), user, StringComparison.OrdinalIgnoreCase) ||
             string.Equals(t.LoginProfessor.Trim().ToLower().Replace(".", "").Replace("_", "").Replace("-", "").Replace(" ", ""), userNorm, StringComparison.OrdinalIgnoreCase)) &&
            string.Equals(t.SenhaProfessor?.Trim(), pass));

        if (turmaGlobal != null)
        {
            return Ok(new
            {
                sucesso = true,
                tipo = "ProfessorSala",
                nome = turmaGlobal.NomeProfessor ?? "Professor da Sala",
                turmaId = (Guid?)turmaGlobal.Id,
                turmaNome = turmaGlobal.Nome,
                escolaNome = turmaGlobal.Escola?.Nome,
                tokenEscola = turmaGlobal.Escola?.TokenAcesso
            });
        }

        return Unauthorized(new { sucesso = false, erro = "Usuário ou senha do professor incorretos." });
    }

    /// <summary>
    /// Chamado quando o aluno ou professor exclui um perfil no jogo .exe.
    /// Exclui o aluno e seu progresso do banco de dados no servidor!
    /// </summary>
    [HttpPost("excluir-aluno")]
    public async Task<IActionResult> ExcluirAluno(
        [FromHeader(Name = "X-Escola-Token")] string? tokenHeader,
        [FromQuery] string? token,
        [FromBody] ExcluirAlunoDto dto)
    {
        var tokenEfetivo = !string.IsNullOrWhiteSpace(tokenHeader) ? tokenHeader : token;
        if (string.IsNullOrWhiteSpace(tokenEfetivo)) return BadRequest("Token obrigatório.");

        var escola = await _db.Escolas
            .Include(e => e.Turmas)
                .ThenInclude(t => t.Alunos)
                    .ThenInclude(a => a.Progressos)
            .FirstOrDefaultAsync(e => e.TokenAcesso == tokenEfetivo && e.Ativa);

        if (escola == null) return Unauthorized("Escola não encontrada.");

        var aluno = escola.Turmas
            .SelectMany(t => t.Alunos)
            .FirstOrDefault(a => string.Equals(a.NomeJogo, dto.NomeAluno, StringComparison.OrdinalIgnoreCase) ||
                                 string.Equals(a.NomeCompleto, dto.NomeAluno, StringComparison.OrdinalIgnoreCase));

        if (aluno != null)
        {
            _db.ProgressoFases.RemoveRange(aluno.Progressos);
            _db.Alunos.Remove(aluno);
            await _db.SaveChangesAsync();
            _logger.LogInformation("Aluno {Nome} excluído da escola {Escola}", dto.NomeAluno, escola.Nome);
            return Ok(new { mensagem = $"Aluno '{dto.NomeAluno}' excluído do servidor com sucesso!" });
        }

        return NotFound(new { erro = $"Aluno '{dto.NomeAluno}' não encontrado nesta escola." });
    }

    /// <summary>
    /// Chamado pelo jogo .exe quando o aluno completa uma fase.
    /// </summary>
    [HttpPost("progresso")]
    public async Task<IActionResult> SalvarProgresso(
        [FromHeader(Name = "X-Escola-Token")] string? tokenHeader,
        [FromQuery] string? token,
        [FromBody] RegistrarProgressoDto dto)
    {
        var tokenEfetivo = !string.IsNullOrWhiteSpace(tokenHeader) ? tokenHeader : token;
        if (string.IsNullOrWhiteSpace(tokenEfetivo)) return BadRequest("Token obrigatório.");

        var escola = await _db.Escolas.FirstOrDefaultAsync(e => e.TokenAcesso == tokenEfetivo && e.Ativa);
        if (escola == null) return Unauthorized("Token da escola inválido.");

        var aluno = await _db.Alunos
            .Include(a => a.Turma)
            .FirstOrDefaultAsync(a => a.Id == dto.AlunoId && a.Turma!.EscolaId == escola.Id);

        if (aluno == null) return NotFound("Aluno não encontrado nesta escola.");

        var progresso = await _db.ProgressoFases.FirstOrDefaultAsync(p =>
            p.AlunoId == dto.AlunoId &&
            p.Materia == dto.Materia &&
            p.Nivel == dto.Nivel &&
            p.NumeroFase == dto.NumeroFase);

        if (progresso == null)
        {
            progresso = new ProgressoFase
            {
                Id = Guid.NewGuid(),
                AlunoId = dto.AlunoId,
                Materia = dto.Materia,
                Nivel = dto.Nivel,
                NumeroFase = dto.NumeroFase,
                Concluida = dto.Concluida,
                Estrelas = dto.Estrelas,
                Pontuacao = dto.Pontuacao,
                QuantidadeErros = dto.QuantidadeErros,
                TempoGastoSegundos = dto.TempoGastoSegundos,
                UltimaAtualizacao = DateTime.UtcNow
            };
            _db.ProgressoFases.Add(progresso);
        }
        else
        {
            progresso.Concluida = progresso.Concluida || dto.Concluida;
            progresso.Estrelas = Math.Max(progresso.Estrelas, dto.Estrelas);
            if (dto.Pontuacao > progresso.Pontuacao) progresso.Pontuacao = dto.Pontuacao;
            progresso.QuantidadeErros = dto.QuantidadeErros;
            progresso.TempoGastoSegundos = dto.TempoGastoSegundos;
            progresso.UltimaAtualizacao = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync();
        return Ok(new { mensagem = "Progresso salvo com sucesso!" });
    }

    /// <summary>
    /// Cadastra um novo aluno diretamente do jogo (quando cria perfil).
    /// </summary>
    [HttpPost("criar-aluno")]
    public async Task<IActionResult> CriarAluno(
        [FromHeader(Name = "X-Escola-Token")] string? tokenHeader,
        [FromQuery] string? token,
        [FromBody] CriarAlunoDto dto)
    {
        var tokenEfetivo = !string.IsNullOrWhiteSpace(tokenHeader) ? tokenHeader : token;
        var escola = await _db.Escolas.Include(e => e.Turmas).FirstOrDefaultAsync(e => e.TokenAcesso == tokenEfetivo && e.Ativa);
        if (escola == null) return Unauthorized("Token da escola inválido.");

        var turma = (dto.TurmaId != Guid.Empty ? escola.Turmas.FirstOrDefault(t => t.Id == dto.TurmaId) : null)
                    ?? escola.Turmas.FirstOrDefault();

        if (turma == null)
        {
            turma = new Turma { Id = Guid.NewGuid(), EscolaId = escola.Id, Nome = "1º Ano A", AnoLetivo = DateTime.UtcNow.Year };
            _db.Turmas.Add(turma);
            await _db.SaveChangesAsync();
        }

        var novoAluno = new Aluno
        {
            Id = Guid.NewGuid(),
            TurmaId = turma.Id,
            NomeCompleto = dto.NomeCompleto.Trim(),
            NomeJogo = string.IsNullOrWhiteSpace(dto.NomeJogo) ? dto.NomeCompleto.Trim() : dto.NomeJogo.Trim(),
            Ativo = true,
            DataCriacao = DateTime.UtcNow
        };

        _db.Alunos.Add(novoAluno);
        await _db.SaveChangesAsync();

        return Ok(new AlunoResumoDto
        {
            Id = novoAluno.Id,
            NomeCompleto = novoAluno.NomeCompleto,
            NomeJogo = novoAluno.NomeJogo,
            TurmaId = turma.Id,
            TurmaNome = turma.Nome
        });
    }

    /// <summary>
    /// Chamado pelo jogo .exe na Área do Professor para carregar o resumo de todas as salas/turmas e alunos daquela escola.
    /// </summary>
    [HttpGet("salas-resumo")]
    public async Task<IActionResult> ObterResumoSalasProfessor(
        [FromQuery] string? token,
        [FromHeader(Name = "X-Escola-Token")] string? tokenHeader)
    {
        var tokenEfetivo = !string.IsNullOrWhiteSpace(tokenHeader) ? tokenHeader : token;
        if (string.IsNullOrWhiteSpace(tokenEfetivo)) return BadRequest("Token obrigatório.");

        var escola = await _db.Escolas
            .Include(e => e.Municipio)
            .Include(e => e.Turmas)
                .ThenInclude(t => t.Alunos)
                    .ThenInclude(a => a.Progressos)
            .FirstOrDefaultAsync(e => e.TokenAcesso == tokenEfetivo && e.Ativa);

        if (escola == null) return NotFound("Escola não encontrada com este token.");

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
            escolaId = escola.Id,
            nomeEscola = escola.Nome,
            municipio = escola.Municipio != null ? $"{escola.Municipio.Nome}/{escola.Municipio.UF}" : "Geral",
            tokenAcesso = escola.TokenAcesso,
            salas = salasResumo
        });
    }

    [HttpGet("progresso-aluno/{alunoId:guid}")]
    public async Task<IActionResult> ObterProgressoAluno(Guid alunoId)
    {
        var progressos = await _db.ProgressoFases
            .Where(p => p.AlunoId == alunoId)
            .Select(p => new
            {
                p.Materia,
                p.Nivel,
                p.NumeroFase,
                p.Concluida,
                p.Estrelas,
                p.Pontuacao
            })
            .ToListAsync();

        return Ok(progressos);
    }
}

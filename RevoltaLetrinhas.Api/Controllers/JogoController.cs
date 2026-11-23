using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RevoltaLetrinhas.Api.Data;
using RevoltaLetrinhas.Api.DTOs;
using RevoltaLetrinhas.Api.Models;
using RevoltaLetrinhas.Api.Seguranca;

namespace RevoltaLetrinhas.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class JogoController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ILogger<JogoController> _logger;

    // Serializa cadastros de alunos/salas vindos do jogo para que duas requisições simultâneas
    // (ex.: "criar aluno" + "definir sala") não criem o mesmo aluno duas vezes.
    private static readonly SemaphoreSlim _travaCadastro = new(1, 1);

    public JogoController(AppDbContext db, ILogger<JogoController> logger)
    {
        _db = db;
        _logger = logger;
    }

    // =====================================================================
    // AUXILIARES
    // =====================================================================

    private static string? NormalizarToken(string? tokenHeader, string? tokenQuery)
    {
        var t = !string.IsNullOrWhiteSpace(tokenHeader) ? tokenHeader : tokenQuery;
        return string.IsNullOrWhiteSpace(t) ? null : t.Trim().ToUpperInvariant();
    }

    private static bool MesmoNome(string? a, string? b) =>
        string.Equals(a?.Trim(), b?.Trim(), StringComparison.OrdinalIgnoreCase);

    private static Aluno? AcharAlunoPorNome(Escola escola, string nome)
    {
        var candidatos = escola.Turmas
            .SelectMany(t => t.Alunos)
            .Where(a => MesmoNome(a.NomeJogo, nome) || MesmoNome(a.NomeCompleto, nome))
            .ToList();

        // Prefere um aluno ativo, se houver mais de um com o mesmo nome
        return candidatos.FirstOrDefault(a => a.Ativo) ?? candidatos.FirstOrDefault();
    }

    private Turma ObterOuCriarTurma(Escola escola, string nomeTurma)
    {
        var turma = escola.Turmas.FirstOrDefault(t => MesmoNome(t.Nome, nomeTurma));
        if (turma == null)
        {
            turma = new Turma
            {
                Id = Guid.NewGuid(),
                EscolaId = escola.Id,
                Nome = nomeTurma.Trim(),
                AnoLetivo = DateTime.UtcNow.Year
            };
            _db.Turmas.Add(turma);
            escola.Turmas.Add(turma);
        }
        return turma;
    }

    private Task<Escola?> BuscarEscolaComAlunos(string token) =>
        _db.Escolas
            .Include(e => e.Turmas)
                .ThenInclude(t => t.Alunos)
            .FirstOrDefaultAsync(e => e.TokenAcesso.ToUpper() == token && e.Ativa);

    private static AlunoResumoDto ParaResumo(Aluno aluno, Turma turma) => new()
    {
        Id = aluno.Id,
        NomeCompleto = aluno.NomeCompleto,
        NomeJogo = string.IsNullOrWhiteSpace(aluno.NomeJogo) ? aluno.NomeCompleto : aluno.NomeJogo,
        TurmaId = turma.Id,
        TurmaNome = turma.Nome
    };

    // =====================================================================
    // ATUALIZAÇÃO DO JOGO (PATCH)
    // =====================================================================

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
            versao = "1.0.2",
            versaoCodigo = 102,
            dataPublicacao = ultimaModificacao.ToString("yyyy-MM-ddTHH:mm:ssZ"),
            temPatch = temPatch,
            tamanhoBytes = tamanho,
            urlDownloadPatch = "/api/jogo/download-patch",
            novidades = "Botão e modal de vinculação por Token da Escola, login individual de professores sem falhas, fechamento imediato do jogo e limpeza visual de textos."
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
    /// Exige a senha de administrador (cabeçalho X-Admin-Senha = ADMIN_SENHA).
    /// </summary>
    [HttpPost("upload-patch")]
    [ExigirSenhaAdmin]
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

    // =====================================================================
    // ESCOLA
    // =====================================================================

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
        var tokenEfetivo = NormalizarToken(tokenHeader, token);
        if (tokenEfetivo == null)
        {
            return BadRequest(new { erro = "Token da escola não informado." });
        }

        var escola = await _db.Escolas
            .Include(e => e.Municipio)
            .Include(e => e.Turmas)
                .ThenInclude(t => t.Alunos.Where(a => a.Ativo))
            .FirstOrDefaultAsync(e => e.TokenAcesso.ToUpper() == tokenEfetivo && e.Ativa);

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
            Turmas = escola.Turmas.OrderBy(t => t.Nome).Select(t => new TurmaComAlunosDto
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
        var tokenEfetivo = NormalizarToken(tokenHeader, token);
        if (tokenEfetivo == null) return BadRequest("Token da escola não informado.");

        var escola = await BuscarEscolaComAlunos(tokenEfetivo);
        if (escola == null) return NotFound("Escola não encontrada.");

        string user = (dto.Login ?? "").Trim().ToLower();
        string pass = (dto.Senha ?? "").Trim();

        // 1. Verifica se é o admin padrão da escola
        if (user == "professor" && pass == "1234")
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

        // 2. Verifica se é um professor exclusivo cadastrado em alguma sala
        var turmaDoProf = escola.Turmas.FirstOrDefault(t =>
            string.Equals(t.LoginProfessor, user, StringComparison.OrdinalIgnoreCase) &&
            t.SenhaProfessor == pass);

        if (turmaDoProf != null)
        {
            return Ok(new
            {
                sucesso = true,
                tipo = "ProfessorSala",
                nome = turmaDoProf.NomeProfessor ?? "Professor da Sala",
                turmaId = (Guid?)turmaDoProf.Id,
                turmaNome = turmaDoProf.Nome
            });
        }

        return Unauthorized(new { sucesso = false, erro = "Usuário ou senha do professor incorretos." });
    }

    // =====================================================================
    // SALAS E ALUNOS (CADASTROS FEITOS DENTRO DO JOGO)
    // =====================================================================

    /// <summary>
    /// Cadastra uma sala/turma criada dentro do jogo. Se já existir uma sala com o mesmo nome, apenas a devolve.
    /// </summary>
    [HttpPost("criar-turma")]
    public async Task<IActionResult> CriarTurma(
        [FromHeader(Name = "X-Escola-Token")] string? tokenHeader,
        [FromQuery] string? token,
        [FromBody] CriarTurmaJogoDto dto)
    {
        var tokenEfetivo = NormalizarToken(tokenHeader, token);
        if (tokenEfetivo == null) return BadRequest("Token obrigatório.");
        if (string.IsNullOrWhiteSpace(dto.NomeTurma)) return BadRequest("Nome da sala é obrigatório.");

        await _travaCadastro.WaitAsync();
        try
        {
            var escola = await BuscarEscolaComAlunos(tokenEfetivo);
            if (escola == null) return Unauthorized("Token da escola inválido.");

            bool jaExistia = escola.Turmas.Any(t => MesmoNome(t.Nome, dto.NomeTurma));
            var turma = ObterOuCriarTurma(escola, dto.NomeTurma);
            await _db.SaveChangesAsync();

            if (!jaExistia) _logger.LogInformation("Sala '{Sala}' criada pelo jogo na escola {Escola}", turma.Nome, escola.Nome);

            return Ok(new { turmaId = turma.Id, nome = turma.Nome, criada = !jaExistia });
        }
        finally
        {
            _travaCadastro.Release();
        }
    }

    /// <summary>
    /// Cadastra um aluno criado no jogo. Se já existir um aluno com o mesmo nome nesta escola,
    /// devolve o aluno existente em vez de criar uma cópia duplicada.
    /// </summary>
    [HttpPost("criar-aluno")]
    public async Task<IActionResult> CriarAluno(
        [FromHeader(Name = "X-Escola-Token")] string? tokenHeader,
        [FromQuery] string? token,
        [FromBody] CriarAlunoDto dto)
    {
        var tokenEfetivo = NormalizarToken(tokenHeader, token);
        if (tokenEfetivo == null) return BadRequest("Token obrigatório.");
        if (string.IsNullOrWhiteSpace(dto.NomeCompleto)) return BadRequest("Nome do aluno é obrigatório.");

        await _travaCadastro.WaitAsync();
        try
        {
            var escola = await BuscarEscolaComAlunos(tokenEfetivo);
            if (escola == null) return Unauthorized("Token da escola inválido.");

            string nomeJogo = string.IsNullOrWhiteSpace(dto.NomeJogo) ? dto.NomeCompleto.Trim() : dto.NomeJogo.Trim();

            // 1. Já existe? Devolve o mesmo aluno (evita duplicados quando o jogo reenvia o cadastro)
            var existente = AcharAlunoPorNome(escola, nomeJogo) ?? AcharAlunoPorNome(escola, dto.NomeCompleto);
            if (existente != null)
            {
                if (!existente.Ativo)
                {
                    existente.Ativo = true;
                    await _db.SaveChangesAsync();
                }
                var turmaExistente = escola.Turmas.First(t => t.Id == existente.TurmaId);
                return Ok(ParaResumo(existente, turmaExistente));
            }

            // 2. Descobre a turma: por Id, por nome (criando se preciso) ou a primeira da escola
            Turma? turma = null;
            if (Guid.TryParse(dto.TurmaId, out var turmaIdInformado) && turmaIdInformado != Guid.Empty)
            {
                turma = escola.Turmas.FirstOrDefault(t => t.Id == turmaIdInformado);
            }
            if (turma == null && !string.IsNullOrWhiteSpace(dto.NomeTurma))
            {
                turma = ObterOuCriarTurma(escola, dto.NomeTurma);
            }
            turma ??= escola.Turmas.OrderBy(t => t.Nome).FirstOrDefault() ?? ObterOuCriarTurma(escola, "1º Ano A");

            var novoAluno = new Aluno
            {
                Id = Guid.NewGuid(),
                TurmaId = turma.Id,
                NomeCompleto = dto.NomeCompleto.Trim(),
                NomeJogo = nomeJogo,
                Ativo = true,
                DataCriacao = DateTime.UtcNow
            };
            _db.Alunos.Add(novoAluno);
            await _db.SaveChangesAsync();

            _logger.LogInformation("Aluno '{Aluno}' criado pelo jogo na sala '{Sala}' da escola {Escola}", nomeJogo, turma.Nome, escola.Nome);
            return Ok(ParaResumo(novoAluno, turma));
        }
        finally
        {
            _travaCadastro.Release();
        }
    }

    /// <summary>
    /// Define a sala/turma de um aluno a partir do jogo.
    /// Se o aluno ainda não existir no servidor, ele é cadastrado já nessa sala.
    /// </summary>
    [HttpPost("atualizar-turma-aluno")]
    public async Task<IActionResult> AtualizarTurmaAluno(
        [FromHeader(Name = "X-Escola-Token")] string? tokenHeader,
        [FromQuery] string? token,
        [FromBody] AtualizarTurmaAlunoDto dto)
    {
        var tokenEfetivo = NormalizarToken(tokenHeader, token);
        if (tokenEfetivo == null) return BadRequest("Token obrigatório.");
        if (string.IsNullOrWhiteSpace(dto.NomeAluno) || string.IsNullOrWhiteSpace(dto.NomeTurma))
            return BadRequest("Nome do aluno e da sala são obrigatórios.");

        await _travaCadastro.WaitAsync();
        try
        {
            var escola = await BuscarEscolaComAlunos(tokenEfetivo);
            if (escola == null) return Unauthorized("Token da escola inválido.");

            var turmaDestino = ObterOuCriarTurma(escola, dto.NomeTurma);
            var aluno = AcharAlunoPorNome(escola, dto.NomeAluno);

            if (aluno == null)
            {
                aluno = new Aluno
                {
                    Id = Guid.NewGuid(),
                    TurmaId = turmaDestino.Id,
                    NomeCompleto = dto.NomeAluno.Trim(),
                    NomeJogo = dto.NomeAluno.Trim(),
                    Ativo = true,
                    DataCriacao = DateTime.UtcNow
                };
                _db.Alunos.Add(aluno);
            }
            else
            {
                aluno.TurmaId = turmaDestino.Id;
                aluno.Ativo = true;
            }

            await _db.SaveChangesAsync();

            var resumo = ParaResumo(aluno, turmaDestino);
            return Ok(new
            {
                id = resumo.Id,
                nomeCompleto = resumo.NomeCompleto,
                nomeJogo = resumo.NomeJogo,
                turmaId = resumo.TurmaId,
                turmaNome = resumo.TurmaNome,
                mensagem = $"Aluno '{resumo.NomeJogo}' está na sala '{turmaDestino.Nome}'."
            });
        }
        finally
        {
            _travaCadastro.Release();
        }
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
        var tokenEfetivo = NormalizarToken(tokenHeader, token);
        if (tokenEfetivo == null) return BadRequest("Token obrigatório.");

        await _travaCadastro.WaitAsync();
        try
        {
            var escola = await _db.Escolas
                .Include(e => e.Turmas)
                    .ThenInclude(t => t.Alunos)
                        .ThenInclude(a => a.Progressos)
                .FirstOrDefaultAsync(e => e.TokenAcesso.ToUpper() == tokenEfetivo && e.Ativa);

            if (escola == null) return Unauthorized("Escola não encontrada.");

            var aluno = AcharAlunoPorNome(escola, dto.NomeAluno);
            if (aluno == null)
            {
                return NotFound(new { erro = $"Aluno '{dto.NomeAluno}' não encontrado nesta escola." });
            }

            _db.ProgressoFases.RemoveRange(aluno.Progressos);
            _db.Alunos.Remove(aluno);
            await _db.SaveChangesAsync();
            _logger.LogInformation("Aluno {Nome} excluído da escola {Escola}", dto.NomeAluno, escola.Nome);
            return Ok(new { mensagem = $"Aluno '{dto.NomeAluno}' excluído do servidor com sucesso!" });
        }
        finally
        {
            _travaCadastro.Release();
        }
    }

    // =====================================================================
    // PROGRESSO
    // =====================================================================

    /// <summary>
    /// Chamado pelo jogo .exe quando o aluno completa uma fase.
    /// </summary>
    [HttpPost("progresso")]
    public async Task<IActionResult> SalvarProgresso(
        [FromHeader(Name = "X-Escola-Token")] string? tokenHeader,
        [FromQuery] string? token,
        [FromBody] RegistrarProgressoDto dto)
    {
        var tokenEfetivo = NormalizarToken(tokenHeader, token);
        if (tokenEfetivo == null) return BadRequest("Token obrigatório.");

        var escola = await _db.Escolas.FirstOrDefaultAsync(e => e.TokenAcesso.ToUpper() == tokenEfetivo && e.Ativa);
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
    /// Chamado pelo jogo .exe na Área do Professor para carregar o resumo de todas as salas/turmas e alunos daquela escola.
    /// </summary>
    [HttpGet("salas-resumo")]
    public async Task<IActionResult> ObterResumoSalasProfessor(
        [FromQuery] string? token,
        [FromHeader(Name = "X-Escola-Token")] string? tokenHeader)
    {
        var tokenEfetivo = NormalizarToken(tokenHeader, token);
        if (tokenEfetivo == null) return BadRequest("Token obrigatório.");

        var escola = await _db.Escolas
            .Include(e => e.Municipio)
            .Include(e => e.Turmas)
                .ThenInclude(t => t.Alunos)
                    .ThenInclude(a => a.Progressos)
            .FirstOrDefaultAsync(e => e.TokenAcesso.ToUpper() == tokenEfetivo && e.Ativa);

        if (escola == null) return NotFound("Escola não encontrada com este token.");

        var salasResumo = new List<ResumoSalaDto>();

        foreach (var turma in escola.Turmas.OrderBy(t => t.Nome))
        {
            var alunosDto = new List<AlunoSalaDetalheDto>();
            int somaFases = 0;
            int somaErros = 0;
            double somaEstrelas = 0;
            int alunosComFases = 0;

            var alunosAtivos = turma.Alunos.Where(a => a.Ativo).OrderBy(a => a.NomeJogo).ToList();
            foreach (var aluno in alunosAtivos)
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

            int totalAlunos = alunosAtivos.Count;
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

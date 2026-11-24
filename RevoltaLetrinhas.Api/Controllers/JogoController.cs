using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RevoltaLetrinhas.Api.Data;
using RevoltaLetrinhas.Api.DTOs;
using RevoltaLetrinhas.Api.Models;
using RevoltaLetrinhas.Api.Seguranca;
using RevoltaLetrinhas.Api.Servicos;

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

    /// <summary>
    /// Sala da escola pelo nome. O jogo NÃO cria salas: elas são cadastradas só pelo administrador no painel
    /// (antes, qualquer nome digitado no jogo virava uma sala nova no banco).
    /// </summary>
    private static Turma? AcharTurma(Escola escola, string? nomeTurma) =>
        string.IsNullOrWhiteSpace(nomeTurma) ? null : escola.Turmas.FirstOrDefault(t => MesmoNome(t.Nome, nomeTurma));

    private IActionResult SalaInexistente(string? nomeTurma) => Conflict(new
    {
        codigo = "sala_inexistente",
        erro = $"A sala '{nomeTurma}' não existe nesta escola. Novas salas são cadastradas pela administração no painel da escola."
    });

    private Task<Escola?> BuscarEscolaComAlunos(string token) =>
        _db.Escolas
            .Include(e => e.Municipio)
            .Include(e => e.Turmas)
                .ThenInclude(t => t.Alunos)
            .FirstOrDefaultAsync(e => e.TokenAcesso.ToUpper() == token);

    /// <summary>403 com o motivo, se a licença da escola não permite usar o jogo (escola inativa ou contrato vencido).</summary>
    private IActionResult? BloqueioLicenca(Escola escola)
    {
        var status = Licenca.Avaliar(escola);
        return status.Valida ? null : StatusCode(StatusCodes.Status403Forbidden, Licenca.Resposta(status));
    }

    /// <summary>409 se a escola já tem o máximo de alunos da licença.</summary>
    private IActionResult? BloqueioLimiteAlunos(Escola escola)
    {
        if (!escola.LimiteAlunos.HasValue) return null;
        int ativos = escola.Turmas.SelectMany(t => t.Alunos).Count(a => a.Ativo);
        if (ativos < escola.LimiteAlunos.Value) return null;
        return StatusCode(StatusCodes.Status409Conflict, new
        {
            codigo = "limite_alunos",
            erro = $"A escola atingiu o limite de {escola.LimiteAlunos.Value} alunos da licença. Para cadastrar mais alunos, fale com a coordenação.",
            limiteAlunos = escola.LimiteAlunos.Value,
            totalAlunos = ativos
        });
    }

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
            // Ao publicar um novo Assembly-CSharp.dll em wwwroot/patch, aumente versaoCodigo
            // (e o VersaoCodigoDoBuild do ServicoNuvem.cs no build correspondente).
            versao = "1.0.8",
            versaoCodigo = 108,
            dataPublicacao = ultimaModificacao.ToString("yyyy-MM-ddTHH:mm:ssZ"),
            temPatch = temPatch,
            tamanhoBytes = tamanho,
            urlDownloadPatch = "/api/jogo/download-patch",
            novidades = "Resgate das Letrinhas e personagens de perfil; desenhos nas palavras; botão Ouvir e dicas; conteúdo do 2º e do 5º ano; relatório por habilidade da BNCC; acesso da coordenação com senha própria."
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

    // A antiga rota POST upload-patch foi removida: ela aceitava, sem senha, um arquivo que TODOS os
    // computadores das escolas baixam e executam. O patch novo é publicado pelo GitHub (wwwroot/patch).

    // =====================================================================
    // ESCOLA
    // =====================================================================

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
            .FirstOrDefaultAsync(e => e.TokenAcesso.ToUpper() == tokenEfetivo);

        if (escola == null)
        {
            return NotFound(new { codigo = "escola_nao_encontrada", erro = $"Escola com token '{tokenEfetivo}' não encontrada." });
        }
        if (BloqueioLicenca(escola) is IActionResult bloqueio) return bloqueio;

        var licenca = Licenca.Avaliar(escola);
        DateTime? expContrato = licenca.DataExpiracao;
        int? diasRestantes = licenca.DiasRestantes;
        bool contratoValido = licenca.Valida;

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
            TemLimiteAlunos = escola.LimiteAlunos.HasValue,
            LimiteAlunos = escola.LimiteAlunos ?? 0,
            TotalAlunos = escola.Turmas.Sum(t => t.Alunos.Count),
            LimiteMaquinas = escola.LimiteMaquinas,
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
        if (BloqueioLicenca(escola) is IActionResult bloqueio) return bloqueio;

        string user = (dto.Login ?? "").Trim().ToLower();
        string pass = (dto.Senha ?? "").Trim();

        // 1. Professor exclusivo cadastrado em alguma sala
        var turmaDoProf = escola.Turmas.FirstOrDefault(t =>
            string.Equals(t.LoginProfessor, user, StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrEmpty(t.SenhaProfessor) &&
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

        // 2. Coordenação (todas as salas). A senha é definida por escola no painel de administração
        //    e guardada só como hash. Não existe mais senha padrão: antes "professor / 1234" valia em todas as escolas.
        if (user is "coordenacao" or "coordenação" or "professor")
        {
            if (string.IsNullOrEmpty(escola.SenhaCoordenacaoHash))
            {
                await Task.Delay(500);
                return Unauthorized(new
                {
                    sucesso = false,
                    codigo = "coordenacao_sem_senha",
                    erro = "A senha da coordenação desta escola ainda não foi definida. Peça à administração para defini-la no painel (Licença da escola)."
                });
            }

            if (SenhaHash.Confere(pass, escola.SenhaCoordenacaoHash))
            {
                return Ok(new
                {
                    sucesso = true,
                    tipo = "AdministradorEscola",
                    nome = "Coordenação",
                    turmaId = (Guid?)null,
                    turmaNome = "Todas as Salas"
                });
            }
        }

        _logger.LogWarning("Login de professor recusado na escola {Escola} (usuário '{Usuario}') vindo de {Ip}.",
            escola.Nome, user, HttpContext.Connection.RemoteIpAddress);
        await Task.Delay(500); // atrasa tentativas de adivinhar a senha
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
            if (BloqueioLicenca(escola) is IActionResult bloqueio) return bloqueio;

            // Desde a 1.0.8 só o administrador cria salas (painel). Versões antigas do jogo ainda chamam esta rota:
            // se a sala já existe, devolve a mesma; se não existe, recusa.
            var turma = AcharTurma(escola, dto.NomeTurma);
            if (turma == null) return SalaInexistente(dto.NomeTurma);
            return Ok(new { turmaId = turma.Id, nome = turma.Nome, criada = false });
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
            if (BloqueioLicenca(escola) is IActionResult bloqueio) return bloqueio;

            string nomeJogo = string.IsNullOrWhiteSpace(dto.NomeJogo) ? dto.NomeCompleto.Trim() : dto.NomeJogo.Trim();

            // 1. Já existe? Devolve o mesmo aluno (evita duplicados quando o jogo reenvia o cadastro)
            var existente = AcharAlunoPorNome(escola, nomeJogo) ?? AcharAlunoPorNome(escola, dto.NomeCompleto);
            if (existente != null)
            {
                if (!existente.Ativo)
                {
                    if (BloqueioLimiteAlunos(escola) is IActionResult limite) return limite;
                    existente.Ativo = true;
                    await _db.SaveChangesAsync();
                }
                var turmaExistente = escola.Turmas.First(t => t.Id == existente.TurmaId);
                return Ok(ParaResumo(existente, turmaExistente));
            }

            // 2. Aluno novo: respeita o limite de alunos da licença
            if (BloqueioLimiteAlunos(escola) is IActionResult limiteAlunos) return limiteAlunos;

            // 3. Descobre a turma: por Id, por nome ou a primeira da escola (salas são criadas só pelo administrador)
            Turma? turma = null;
            if (Guid.TryParse(dto.TurmaId, out var turmaIdInformado) && turmaIdInformado != Guid.Empty)
            {
                turma = escola.Turmas.FirstOrDefault(t => t.Id == turmaIdInformado);
            }
            if (turma == null && !string.IsNullOrWhiteSpace(dto.NomeTurma))
            {
                turma = AcharTurma(escola, dto.NomeTurma);
                if (turma == null) return SalaInexistente(dto.NomeTurma);
            }
            turma ??= escola.Turmas.OrderBy(t => t.Nome).FirstOrDefault();
            if (turma == null)
                return Conflict(new { codigo = "sala_inexistente", erro = "A escola ainda não tem salas. As salas são cadastradas pela administração no painel da escola." });

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
            if (BloqueioLicenca(escola) is IActionResult bloqueio) return bloqueio;

            var aluno = AcharAlunoPorNome(escola, dto.NomeAluno);
            if (aluno == null && BloqueioLimiteAlunos(escola) is IActionResult limite) return limite;
            var turmaDestino = AcharTurma(escola, dto.NomeTurma);
            if (turmaDestino == null) return SalaInexistente(dto.NomeTurma);

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
                .Include(e => e.Municipio)
                .Include(e => e.Turmas)
                    .ThenInclude(t => t.Alunos)
                        .ThenInclude(a => a.Progressos)
                .FirstOrDefaultAsync(e => e.TokenAcesso.ToUpper() == tokenEfetivo);

            if (escola == null) return Unauthorized("Escola não encontrada.");
            if (BloqueioLicenca(escola) is IActionResult bloqueio) return bloqueio;

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

        var escola = await _db.Escolas.Include(e => e.Municipio).FirstOrDefaultAsync(e => e.TokenAcesso.ToUpper() == tokenEfetivo);
        if (escola == null) return Unauthorized("Token da escola inválido.");
        if (BloqueioLicenca(escola) is IActionResult bloqueio) return bloqueio;

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

    // =====================================================================
    // PERSONAGENS DE PERFIL (únicos por escola)
    // =====================================================================

    /// <summary>Troféus para resgatar um personagem (troféu = as 30 fases de um nível concluídas).</summary>
    public const int TrofeusParaPersonagem = 2;

    private static HashSet<string>? _catalogoPersonagens;

    /// <summary>Códigos válidos, lidos de wwwroot/imagens/personagens/catalogo.json.</summary>
    private static HashSet<string> CatalogoPersonagens()
    {
        if (_catalogoPersonagens != null) return _catalogoPersonagens;
        var codigos = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        string caminho = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "imagens", "personagens", "catalogo.json");
        if (System.IO.File.Exists(caminho))
        {
            using var doc = System.Text.Json.JsonDocument.Parse(System.IO.File.ReadAllText(caminho));
            foreach (var p in doc.RootElement.GetProperty("personagens").EnumerateArray())
                codigos.Add(p.GetProperty("codigo").GetString() ?? "");
        }
        _catalogoPersonagens = codigos;
        return codigos;
    }

    /// <summary>Níveis completos (30 fases concluídas) = troféus, como no jogo.</summary>
    private static int ContarTrofeus(IEnumerable<ProgressoFase> progressos) =>
        progressos.Where(p => p.Concluida && p.NumeroFase >= 1 && p.NumeroFase <= 30)
                  .GroupBy(p => new { p.Materia, p.Nivel })
                  .Count(g => g.Select(p => p.NumeroFase).Distinct().Count() == 30);

    /// <summary>Personagens já resgatados na escola (o jogo mostra a foto de cada aluno e bloqueia os que têm dono).</summary>
    [HttpGet("personagens")]
    public async Task<IActionResult> ListarPersonagens([FromQuery] string? token, [FromHeader(Name = "X-Escola-Token")] string? tokenHeader)
    {
        var tokenEfetivo = NormalizarToken(tokenHeader, token);
        if (tokenEfetivo == null) return BadRequest("Token obrigatório.");
        var escola = await _db.Escolas.Include(e => e.Municipio).FirstOrDefaultAsync(e => e.TokenAcesso.ToUpper() == tokenEfetivo);
        if (escola == null) return NotFound("Escola não encontrada.");
        if (BloqueioLicenca(escola) is IActionResult bloqueio) return bloqueio;

        var lista = await _db.PersonagensResgatados
            .Where(p => p.EscolaId == escola.Id)
            .Select(p => new { aluno = p.Aluno!.NomeJogo, codigo = p.Codigo })
            .ToListAsync();
        return Ok(new { trofeusNecessarios = TrofeusParaPersonagem, personagens = lista });
    }

    /// <summary>
    /// Resgata um personagem para o aluno. Regras: o aluno precisa de 2 troféus (níveis completos),
    /// ainda não ter personagem, e ninguém da escola pode ter escolhido o mesmo.
    /// </summary>
    [HttpPost("personagens/resgatar")]
    public async Task<IActionResult> ResgatarPersonagem(
        [FromHeader(Name = "X-Escola-Token")] string? tokenHeader,
        [FromQuery] string? token,
        [FromBody] ResgatarPersonagemDto dto)
    {
        var tokenEfetivo = NormalizarToken(tokenHeader, token);
        if (tokenEfetivo == null) return BadRequest("Token obrigatório.");
        string codigo = (dto.Codigo ?? "").Trim().ToLowerInvariant();
        if (!CatalogoPersonagens().Contains(codigo))
            return BadRequest(new { codigo = "personagem_invalido", erro = "Personagem não encontrado no catálogo." });

        await _travaCadastro.WaitAsync();
        try
        {
            var escola = await BuscarEscolaComAlunos(tokenEfetivo);
            if (escola == null) return NotFound("Escola não encontrada.");
            if (BloqueioLicenca(escola) is IActionResult bloqueio) return bloqueio;

            var aluno = AcharAlunoPorNome(escola, dto.NomeAluno);
            if (aluno == null) return NotFound(new { codigo = "aluno_nao_encontrado", erro = "Aluno não encontrado nesta escola." });

            if (await _db.PersonagensResgatados.AnyAsync(p => p.AlunoId == aluno.Id))
                return Conflict(new { codigo = "ja_tem_personagem", erro = "Você já resgatou o seu personagem." });

            var progressos = await _db.ProgressoFases.Where(p => p.AlunoId == aluno.Id).ToListAsync();
            int trofeus = ContarTrofeus(progressos);
            if (trofeus < TrofeusParaPersonagem)
                return Conflict(new { codigo = "trofeus_insuficientes", trofeus, erro = $"Você precisa de {TrofeusParaPersonagem} troféus (tem {trofeus})." });

            if (await _db.PersonagensResgatados.AnyAsync(p => p.EscolaId == escola.Id && p.Codigo == codigo))
                return Conflict(new { codigo = "personagem_ocupado", erro = "Esse personagem já tem dono na sua escola. Escolha outro!" });

            _db.PersonagensResgatados.Add(new PersonagemResgatado { EscolaId = escola.Id, AlunoId = aluno.Id, Codigo = codigo });
            try
            {
                await _db.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                // Outro computador resgatou o mesmo personagem no mesmo instante (índice único)
                return Conflict(new { codigo = "personagem_ocupado", erro = "Esse personagem acabou de ser escolhido por outra criança. Escolha outro!" });
            }
            _logger.LogInformation("Personagem {Codigo} resgatado por {Aluno} na escola {Escola}", codigo, aluno.NomeJogo, escola.Nome);
            return Ok(new { codigo, trofeus });
        }
        finally
        {
            _travaCadastro.Release();
        }
    }

    // =====================================================================
    // HABILIDADES DA BNCC
    // =====================================================================

    /// <summary>Soma acertos e erros por habilidade de uma fase jogada.</summary>
    [HttpPost("habilidades")]
    public async Task<IActionResult> RegistrarHabilidades(
        [FromHeader(Name = "X-Escola-Token")] string? tokenHeader,
        [FromQuery] string? token,
        [FromBody] HabilidadesLoteDto dto)
    {
        var tokenEfetivo = NormalizarToken(tokenHeader, token);
        if (tokenEfetivo == null) return BadRequest("Token obrigatório.");

        await _travaCadastro.WaitAsync();
        try
        {
            var escola = await BuscarEscolaComAlunos(tokenEfetivo);
            if (escola == null) return Unauthorized("Token da escola inválido.");
            if (BloqueioLicenca(escola) is IActionResult bloqueio) return bloqueio;
            var aluno = AcharAlunoPorNome(escola, dto.NomeAluno);
            if (aluno == null) return NotFound(new { erro = "Aluno não encontrado nesta escola." });

            var existentes = await _db.AlunoHabilidades.Where(h => h.AlunoId == aluno.Id).ToListAsync();
            int salvos = 0;
            foreach (var item in dto.Itens ?? new List<HabilidadeItemDto>())
            {
                if (!HabilidadesBncc.Todas.ContainsKey(item.Codigo ?? "")) continue;
                int acertos = Math.Clamp(item.Acertos, 0, 50), erros = Math.Clamp(item.Erros, 0, 50);
                if (acertos + erros == 0) continue;
                string cod = HabilidadesBncc.Todas[item.Codigo!].Codigo;
                var linha = existentes.FirstOrDefault(h => h.Codigo == cod);
                if (linha == null)
                {
                    linha = new AlunoHabilidade { AlunoId = aluno.Id, Codigo = cod };
                    _db.AlunoHabilidades.Add(linha);
                    existentes.Add(linha);
                }
                linha.Acertos += acertos;
                linha.Erros += erros;
                linha.UltimaAtualizacao = DateTime.UtcNow;
                salvos++;
            }
            await _db.SaveChangesAsync();
            return Ok(new { salvos });
        }
        finally
        {
            _travaCadastro.Release();
        }
    }

    /// <summary>Relatório por habilidade de uma sala (turmaId) ou da escola inteira (sem turmaId).</summary>
    [HttpGet("habilidades-sala")]
    public async Task<IActionResult> RelatorioHabilidadesSala(
        [FromQuery] string? token,
        [FromHeader(Name = "X-Escola-Token")] string? tokenHeader,
        [FromQuery] Guid? turmaId)
    {
        var tokenEfetivo = NormalizarToken(tokenHeader, token);
        if (tokenEfetivo == null) return BadRequest("Token obrigatório.");
        var escola = await _db.Escolas.Include(e => e.Municipio).Include(e => e.Turmas).FirstOrDefaultAsync(e => e.TokenAcesso.ToUpper() == tokenEfetivo);
        if (escola == null) return NotFound("Escola não encontrada.");
        if (BloqueioLicenca(escola) is IActionResult bloqueio) return bloqueio;

        var turmaIds = escola.Turmas.Where(t => turmaId == null || t.Id == turmaId).Select(t => t.Id).ToList();
        var dados = await _db.AlunoHabilidades
            .Where(h => turmaIds.Contains(h.Aluno!.TurmaId) && h.Aluno.Ativo)
            .Select(h => new { h.Codigo, h.Acertos, h.Erros, Aluno = h.Aluno!.NomeJogo })
            .ToListAsync();

        return Ok(new { habilidades = RelatorioHabilidades.Montar(dados.Select(d => (d.Codigo, d.Aluno, d.Acertos, d.Erros))) });
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
            .FirstOrDefaultAsync(e => e.TokenAcesso.ToUpper() == tokenEfetivo);

        if (escola == null) return NotFound("Escola não encontrada com este token.");
        if (BloqueioLicenca(escola) is IActionResult bloqueio) return bloqueio;

        // O login do professor não é enviado ao jogo (só o nome)
        var salasResumo = escola.Turmas
            .OrderBy(t => t.Nome)
            .Select(t => CalculoDesempenho.ResumirSala(t, incluirLoginProfessor: false))
            .ToList();

        return Ok(new
        {
            escolaId = escola.Id,
            nomeEscola = escola.Nome,
            municipio = escola.Municipio != null ? $"{escola.Municipio.Nome}/{escola.Municipio.UF}" : "Geral",
            tokenAcesso = escola.TokenAcesso,
            salas = salasResumo
        });
    }

    // =====================================================================
    // MÁQUINAS: LIMITE DE COMPUTADORES USANDO O JOGO AO MESMO TEMPO
    // =====================================================================

    private static readonly SemaphoreSlim _travaMaquinas = new(1, 1);

    private static string Cortar(string? texto, int max)
    {
        texto = (texto ?? "").Trim();
        return texto.Length > max ? texto[..max] : texto;
    }

    /// <summary>
    /// O jogo chama ao abrir e depois a cada minuto. Registra o computador e confere:
    /// licença da escola, computador bloqueado e limite de computadores em uso ao mesmo tempo.
    /// </summary>
    [HttpPost("maquina/sinal")]
    public async Task<IActionResult> SinalMaquina(
        [FromHeader(Name = "X-Escola-Token")] string? tokenHeader,
        [FromQuery] string? token,
        [FromBody] SinalMaquinaDto dto)
    {
        var tokenEfetivo = NormalizarToken(tokenHeader, token);
        if (tokenEfetivo == null) return BadRequest(new { erro = "Token obrigatório." });
        string identificador = Cortar(dto.Identificador, 128);
        if (identificador.Length == 0) return BadRequest(new { erro = "Identificador do computador obrigatório." });

        await _travaMaquinas.WaitAsync();
        try
        {
            var escola = await _db.Escolas.Include(e => e.Municipio).FirstOrDefaultAsync(e => e.TokenAcesso.ToUpper() == tokenEfetivo);
            if (escola == null) return NotFound(new { codigo = "escola_nao_encontrada", erro = $"Escola com token '{tokenEfetivo}' não encontrada." });
            if (BloqueioLicenca(escola) is IActionResult bloqueio) return bloqueio;

            DateTime agora = DateTime.UtcNow;
            DateTime inicioJanela = agora - Licenca.JanelaMaquinaAtiva;

            var maquina = await _db.Maquinas.FirstOrDefaultAsync(m => m.EscolaId == escola.Id && m.Identificador == identificador);
            if (maquina == null)
            {
                maquina = new Maquina
                {
                    Id = Guid.NewGuid(),
                    EscolaId = escola.Id,
                    Identificador = identificador,
                    PrimeiroAcesso = agora,
                    UltimoSinal = agora.AddDays(-1) // ainda não está "em uso"
                };
                _db.Maquinas.Add(maquina);
            }
            maquina.Nome = Cortar(dto.Nome, 100);
            maquina.VersaoJogo = Cortar(dto.VersaoJogo, 20);

            if (maquina.Bloqueada)
            {
                await _db.SaveChangesAsync();
                return StatusCode(StatusCodes.Status403Forbidden, new
                {
                    codigo = "maquina_bloqueada",
                    erro = "Este computador foi bloqueado pela administração para esta escola. Fale com a coordenação."
                });
            }

            bool jaEmUso = maquina.UltimoSinal >= inicioJanela;
            int outrasEmUso = await _db.Maquinas.CountAsync(m =>
                m.EscolaId == escola.Id && !m.Bloqueada && m.UltimoSinal >= inicioJanela && m.Id != maquina.Id);

            if (!jaEmUso && outrasEmUso >= escola.LimiteMaquinas)
            {
                maquina.UltimaRecusa = agora;
                await _db.SaveChangesAsync();
                _logger.LogWarning("Computador {Maquina} recusado na escola {Escola}: {EmUso} de {Limite} em uso",
                    maquina.Nome, escola.Nome, outrasEmUso, escola.LimiteMaquinas);
                return StatusCode(StatusCodes.Status403Forbidden, new
                {
                    codigo = "limite_maquinas",
                    erro = $"A escola já está usando o jogo em {outrasEmUso} computador(es), o máximo da licença ({escola.LimiteMaquinas}). " +
                           "Feche o jogo em outro computador e tente novamente.",
                    maquinasEmUso = outrasEmUso,
                    limiteMaquinas = escola.LimiteMaquinas
                });
            }

            maquina.UltimoSinal = agora;
            await _db.SaveChangesAsync();

            var licenca = Licenca.Avaliar(escola);
            int totalAlunos = await _db.Alunos.CountAsync(a => a.Ativo && a.Turma!.EscolaId == escola.Id);
            return Ok(new
            {
                liberado = true,
                maquinasEmUso = outrasEmUso + 1,
                limiteMaquinas = escola.LimiteMaquinas,
                intervaloSinalSegundos = Licenca.IntervaloSinalSegundos,
                temExpiracao = licenca.DataExpiracao.HasValue,
                dataExpiracao = licenca.DataExpiracao?.ToString("yyyy-MM-dd") ?? "",
                diasRestantes = licenca.DiasRestantes ?? 0,
                temLimiteAlunos = escola.LimiteAlunos.HasValue,
                limiteAlunos = escola.LimiteAlunos ?? 0,
                totalAlunos
            });
        }
        finally
        {
            _travaMaquinas.Release();
        }
    }

    /// <summary>
    /// O jogo chama ao fechar: libera a vaga deste computador na hora (sem esperar 3 minutos).
    /// </summary>
    [HttpPost("maquina/encerrar")]
    public async Task<IActionResult> EncerrarMaquina(
        [FromHeader(Name = "X-Escola-Token")] string? tokenHeader,
        [FromQuery] string? token,
        [FromBody] SinalMaquinaDto dto)
    {
        var tokenEfetivo = NormalizarToken(tokenHeader, token);
        if (tokenEfetivo == null) return BadRequest(new { erro = "Token obrigatório." });
        string identificador = Cortar(dto.Identificador, 128);

        await _travaMaquinas.WaitAsync();
        try
        {
            var escola = await _db.Escolas.FirstOrDefaultAsync(e => e.TokenAcesso.ToUpper() == tokenEfetivo);
            if (escola == null) return NotFound(new { codigo = "escola_nao_encontrada", erro = "Escola não encontrada." });
            var maquina = await _db.Maquinas.FirstOrDefaultAsync(m => m.EscolaId == escola.Id && m.Identificador == identificador);
            if (maquina != null)
            {
                maquina.UltimoSinal = DateTime.UtcNow - Licenca.JanelaMaquinaAtiva - TimeSpan.FromMinutes(1);
                await _db.SaveChangesAsync();
            }
            return Ok(new { liberado = true });
        }
        finally
        {
            _travaMaquinas.Release();
        }
    }

    /// <summary>
    /// Detalhe fase a fase de um aluno (Área do Professor do jogo). Resposta embrulhada em objeto
    /// porque o JsonUtility do Unity não lê um array solto.
    /// </summary>
    [HttpGet("progresso-aluno/{alunoId:guid}")]
    public async Task<IActionResult> ObterProgressoAluno(
        Guid alunoId,
        [FromQuery] string? token,
        [FromHeader(Name = "X-Escola-Token")] string? tokenHeader)
    {
        var aluno = await _db.Alunos
            .Include(a => a.Turma)
                .ThenInclude(t => t!.Escola)
            .Include(a => a.Progressos)
            .FirstOrDefaultAsync(a => a.Id == alunoId);
        if (aluno == null) return NotFound(new { erro = "Aluno não encontrado." });

        // Se o jogo informar o token, confere que o aluno é da escola dele
        var tokenEfetivo = NormalizarToken(tokenHeader, token);
        if (tokenEfetivo != null && !string.Equals(aluno.Turma?.Escola?.TokenAcesso, tokenEfetivo, StringComparison.OrdinalIgnoreCase))
            return NotFound(new { erro = "Aluno não encontrado nesta escola." });

        var resumo = CalculoDesempenho.ResumirAluno(aluno);
        return Ok(new
        {
            alunoId = aluno.Id,
            nomeJogo = resumo.NomeJogo,
            nomeCompleto = aluno.NomeCompleto,
            turmaNome = aluno.Turma?.Nome ?? "",
            resumo,
            fases = aluno.Progressos
                .OrderBy(p => p.Materia).ThenBy(p => p.Nivel).ThenBy(p => p.NumeroFase)
                .Select(p => new
                {
                    materia = p.Materia,
                    nivel = p.Nivel,
                    numeroFase = p.NumeroFase,
                    concluida = p.Concluida,
                    estrelas = p.Estrelas,
                    pontuacao = p.Pontuacao,
                    quantidadeErros = p.QuantidadeErros,
                    tempoGastoSegundos = p.TempoGastoSegundos,
                    ultimaAtualizacao = p.UltimaAtualizacao
                })
                .ToList()
        });
    }

    /// <summary>
    /// Envia de uma vez o progresso que estava salvo só no computador (versões antigas do jogo
    /// não mandavam as fases para o servidor). Cadastra o aluno se ele ainda não existir.
    /// </summary>
    [HttpPost("progresso-lote")]
    public async Task<IActionResult> SalvarProgressoLote(
        [FromHeader(Name = "X-Escola-Token")] string? tokenHeader,
        [FromQuery] string? token,
        [FromBody] ProgressoLoteDto dto)
    {
        var tokenEfetivo = NormalizarToken(tokenHeader, token);
        if (tokenEfetivo == null) return BadRequest("Token obrigatório.");
        if (string.IsNullOrWhiteSpace(dto.NomeAluno)) return BadRequest("Nome do aluno é obrigatório.");

        await _travaCadastro.WaitAsync();
        try
        {
            var escola = await BuscarEscolaComAlunos(tokenEfetivo);
            if (escola == null) return Unauthorized("Token da escola inválido.");
            if (BloqueioLicenca(escola) is IActionResult bloqueio) return bloqueio;

            var aluno = AcharAlunoPorNome(escola, dto.NomeAluno);
            if (aluno == null)
            {
                if (BloqueioLimiteAlunos(escola) is IActionResult limite) return limite;
                var turma = !string.IsNullOrWhiteSpace(dto.NomeTurma)
                    ? AcharTurma(escola, dto.NomeTurma)
                    : escola.Turmas.OrderBy(t => t.Nome).FirstOrDefault();
                if (turma == null) return SalaInexistente(dto.NomeTurma);
                aluno = new Aluno
                {
                    Id = Guid.NewGuid(),
                    TurmaId = turma.Id,
                    NomeCompleto = dto.NomeAluno.Trim(),
                    NomeJogo = dto.NomeAluno.Trim(),
                    Ativo = true,
                    DataCriacao = DateTime.UtcNow
                };
                _db.Alunos.Add(aluno);
                await _db.SaveChangesAsync();
            }

            var existentes = await _db.ProgressoFases.Where(p => p.AlunoId == aluno.Id).ToListAsync();
            int salvos = 0;
            foreach (var item in dto.Itens ?? new List<ProgressoItemDto>())
            {
                if (item.NumeroFase <= 0) continue;
                var atual = existentes.FirstOrDefault(p => p.Materia == item.Materia && p.Nivel == item.Nivel && p.NumeroFase == item.NumeroFase);
                if (atual == null)
                {
                    atual = new ProgressoFase
                    {
                        Id = Guid.NewGuid(),
                        AlunoId = aluno.Id,
                        Materia = item.Materia,
                        Nivel = item.Nivel,
                        NumeroFase = item.NumeroFase
                    };
                    _db.ProgressoFases.Add(atual);
                    existentes.Add(atual);
                }
                else if (atual.Concluida)
                {
                    // O servidor já tem esta fase: o histórico local não sobrescreve um dado mais novo
                    continue;
                }

                atual.Concluida = atual.Concluida || item.Concluida;
                atual.Estrelas = Math.Max(atual.Estrelas, item.Estrelas);
                atual.Pontuacao = Math.Max(atual.Pontuacao, item.Pontuacao);
                atual.QuantidadeErros = item.QuantidadeErros;
                atual.TempoGastoSegundos = item.TempoGastoSegundos;
                atual.UltimaAtualizacao = DateTime.UtcNow;
                salvos++;
            }

            await _db.SaveChangesAsync();
            _logger.LogInformation("Progresso local enviado em lote: {Aluno} ({Salvos} fases) na escola {Escola}", aluno.NomeJogo, salvos, escola.Nome);
            return Ok(new { alunoId = aluno.Id, salvos });
        }
        finally
        {
            _travaCadastro.Release();
        }
    }
}

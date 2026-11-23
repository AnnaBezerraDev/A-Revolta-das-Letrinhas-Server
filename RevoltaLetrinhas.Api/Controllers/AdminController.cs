using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RevoltaLetrinhas.Api.Data;
using RevoltaLetrinhas.Api.DTOs;
using RevoltaLetrinhas.Api.Models;
using RevoltaLetrinhas.Api.Servicos;

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

        DateTime inicioJanela = DateTime.UtcNow - Licenca.JanelaMaquinaAtiva;
        var escolasDados = await _db.Escolas
            .Select(e => new
            {
                e.Id,
                e.Nome,
                e.TokenAcesso,
                e.MunicipioId,
                e.Ativa,
                e.LimiteAlunos,
                e.LimiteProfessores,
                e.LimiteMaquinas,
                MunicipioNome = e.Municipio != null ? e.Municipio.Nome : null,
                MunicipioUF = e.Municipio != null ? e.Municipio.UF : null,
                DataExpiracao = e.Municipio != null ? e.Municipio.DataExpiracaoContrato : null,
                TotalTurmas = e.Turmas.Count,
                TotalAlunos = e.Turmas.SelectMany(t => t.Alunos).Count(a => a.Ativo),
                TotalProfessores = e.Turmas.Count(t => t.LoginProfessor != null && t.LoginProfessor != ""),
                FasesConcluidas = e.Turmas.SelectMany(t => t.Alunos).SelectMany(a => a.Progressos).Count(p => p.Concluida),
                MaquinasEmUso = e.Maquinas.Count(m => !m.Bloqueada && m.UltimoSinal >= inicioJanela),
                MaquinasRegistradas = e.Maquinas.Count
            })
            .ToListAsync();

        var escolas = escolasDados.Select(e =>
        {
            var licenca = Licenca.Avaliar(e.Ativa, e.MunicipioNome, e.DataExpiracao);
            return new
            {
                e.Id,
                e.Nome,
                e.TokenAcesso,
                e.MunicipioId,
                Municipio = e.MunicipioNome != null ? $"{e.MunicipioNome}/{e.MunicipioUF}" : "Geral",
                e.TotalTurmas,
                e.TotalAlunos,
                e.FasesConcluidas,
                e.Ativa,
                e.LimiteAlunos,
                e.LimiteProfessores,
                e.LimiteMaquinas,
                e.TotalProfessores,
                e.MaquinasEmUso,
                e.MaquinasRegistradas,
                LicencaValida = licenca.Valida,
                LicencaCodigo = licenca.Codigo,
                LicencaMensagem = licenca.Mensagem
            };
        }).OrderBy(e => e.Nome).ToList();

        var municipiosDados = await _db.Municipios
            .Select(m => new
            {
                m.Id,
                m.Nome,
                m.UF,
                m.DataExpiracaoContrato,
                m.NumeroConvenio,
                TotalEscolas = m.Escolas.Count
            })
            .ToListAsync();

        var municipios = municipiosDados.Select(m =>
        {
            var licenca = Licenca.Avaliar(true, m.Nome, m.DataExpiracaoContrato);
            return new
            {
                m.Id,
                m.Nome,
                m.UF,
                m.DataExpiracaoContrato,
                m.NumeroConvenio,
                m.TotalEscolas,
                DiasRestantes = licenca.DiasRestantes,
                Vencido = !licenca.Valida
            };
        }).OrderBy(m => m.Nome).ToList();

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

        var salasResumo = escola.Turmas
            .OrderBy(t => t.Nome)
            .Select(t => CalculoDesempenho.ResumirSala(t, incluirLoginProfessor: true))
            .ToList();

        var licenca = Licenca.Avaliar(escola);
        DateTime inicioJanela = DateTime.UtcNow - Licenca.JanelaMaquinaAtiva;
        int maquinasEmUso = await _db.Maquinas.CountAsync(m => m.EscolaId == escola.Id && !m.Bloqueada && m.UltimoSinal >= inicioJanela);

        return Ok(new
        {
            EscolaId = escola.Id,
            NomeEscola = escola.Nome,
            Municipio = escola.Municipio != null ? $"{escola.Municipio.Nome}/{escola.Municipio.UF}" : "Geral",
            DataExpiracao = escola.Municipio?.DataExpiracaoContrato,
            TokenAcesso = escola.TokenAcesso,
            Licenca = new
            {
                escola.Ativa,
                Valida = licenca.Valida,
                Codigo = licenca.Codigo,
                Mensagem = licenca.Mensagem,
                licenca.DiasRestantes,
                escola.LimiteAlunos,
                escola.LimiteProfessores,
                escola.LimiteMaquinas,
                TotalAlunos = escola.Turmas.SelectMany(t => t.Alunos).Count(a => a.Ativo),
                TotalProfessores = escola.Turmas.Count(t => !string.IsNullOrWhiteSpace(t.LoginProfessor)),
                MaquinasEmUso = maquinasEmUso
            },
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
                int erros = aluno.Progressos.Where(p => p.Concluida).Sum(p => p.QuantidadeErros);
                double estrelas = aluno.Progressos.Any(p => p.Concluida) ? Math.Round(aluno.Progressos.Where(p => p.Concluida).Average(p => p.Estrelas), 1) : 0;
                string status = CalculoDesempenho.Status(total, erros);
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
        return Ok(new { mun.Id, mun.Nome, mun.UF, mun.DataExpiracaoContrato, mun.NumeroConvenio, mun.DataCriacao });
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
        return Ok(new { mun.Id, mun.Nome, mun.UF, mun.DataExpiracaoContrato, mun.NumeroConvenio, mun.DataCriacao });
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

        if (await _db.Escolas.AnyAsync(e => e.TokenAcesso.ToUpper() == token))
            return Conflict($"Já existe uma escola com o token '{token}'. Escolha outro token.");

        var escola = new Escola
        {
            Id = Guid.NewGuid(),
            MunicipioId = mun.Id,
            Nome = dto.Nome.Trim(),
            CodigoInep = dto.CodigoInep?.Trim(),
            TokenAcesso = token,
            Ativa = true,
            DataCriacao = DateTime.UtcNow,
            LimiteAlunos = dto.LimiteAlunos is > 0 ? dto.LimiteAlunos : null,
            LimiteProfessores = dto.LimiteProfessores is > 0 ? dto.LimiteProfessores : null,
            LimiteMaquinas = dto.LimiteMaquinas is > 0 ? dto.LimiteMaquinas.Value : 100
        };

        _db.Escolas.Add(escola);
        await _db.SaveChangesAsync();

        return Ok(new { escola.Id, escola.Nome, escola.TokenAcesso, escola.MunicipioId, escola.CodigoInep, escola.Ativa, escola.LimiteAlunos, escola.LimiteProfessores, escola.LimiteMaquinas });
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

        string nomeNovo = dto.Nome.Trim();
        var nomesExistentes = await _db.Turmas.Where(t => t.EscolaId == escola.Id).Select(t => t.Nome).ToListAsync();
        if (nomesExistentes.Any(n => string.Equals(n.Trim(), nomeNovo, StringComparison.OrdinalIgnoreCase)))
            return Conflict($"A escola já possui uma sala chamada '{nomeNovo}'.");

        if (!string.IsNullOrWhiteSpace(dto.LoginProfessor) && await BloqueioLimiteProfessores(escola, null) is IActionResult limite)
            return limite;

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
        return Ok(new { turma.Id, turma.Nome, turma.EscolaId, turma.AnoLetivo, turma.NomeProfessor, turma.LoginProfessor });
    }

    /// <summary>
    /// Vincula ou atualiza o professor exclusivo responsável por uma turma
    /// </summary>
    [HttpPut("turmas/{turmaId:guid}/professor")]
    public async Task<IActionResult> AtualizarProfessorTurma(Guid turmaId, [FromBody] AtualizarTurmaDto dto)
    {
        var turma = await _db.Turmas.FindAsync(turmaId);
        if (turma == null) return NotFound("Turma/Sala não encontrada.");

        if (string.IsNullOrWhiteSpace(turma.LoginProfessor) && !string.IsNullOrWhiteSpace(dto.LoginProfessor))
        {
            var escolaDaTurma = await _db.Escolas.FindAsync(turma.EscolaId);
            if (escolaDaTurma != null && await BloqueioLimiteProfessores(escolaDaTurma, turma.Id) is IActionResult limite)
                return limite;
        }

        if (!string.IsNullOrWhiteSpace(dto.NomeProfessor)) turma.NomeProfessor = dto.NomeProfessor.Trim();
        if (!string.IsNullOrWhiteSpace(dto.LoginProfessor)) turma.LoginProfessor = dto.LoginProfessor.Trim().ToLower();
        if (!string.IsNullOrWhiteSpace(dto.SenhaProfessor)) turma.SenhaProfessor = dto.SenhaProfessor.Trim();

        await _db.SaveChangesAsync();
        return Ok(new { turma.Id, turma.Nome, turma.EscolaId, turma.AnoLetivo, turma.NomeProfessor, turma.LoginProfessor });
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

        var escolaDoAluno = await _db.Escolas.FindAsync(turma.EscolaId);
        if (escolaDoAluno?.LimiteAlunos is int limiteAlunos)
        {
            int ativos = await _db.Alunos.CountAsync(a => a.Ativo && a.Turma!.EscolaId == turma.EscolaId);
            if (ativos >= limiteAlunos)
                return Conflict($"A escola atingiu o limite de {limiteAlunos} alunos da licença. Aumente o limite em \"Licença da escola\" para cadastrar mais.");
        }

        string nomeJogo = string.IsNullOrWhiteSpace(dto.NomeJogo) ? dto.NomeCompleto.Trim().Split(' ')[0] : dto.NomeJogo.Trim();

        // O jogo identifica o aluno pelo "nome no jogo": dois alunos com o mesmo nome na mesma escola se misturariam.
        var nomesNaEscola = await _db.Alunos
            .Where(a => a.Turma!.EscolaId == turma.EscolaId)
            .Select(a => a.NomeJogo)
            .ToListAsync();
        if (nomesNaEscola.Any(n => string.Equals(n?.Trim(), nomeJogo, StringComparison.OrdinalIgnoreCase)))
            return Conflict($"Já existe um aluno chamado '{nomeJogo}' no jogo nesta escola. Use um apelido diferente (ex: {nomeJogo} {turma.Nome}).");

        var aluno = new Aluno
        {
            Id = Guid.NewGuid(),
            TurmaId = turma.Id,
            NomeCompleto = dto.NomeCompleto.Trim(),
            NomeJogo = nomeJogo,
            Ativo = true,
            DataCriacao = DateTime.UtcNow
        };

        _db.Alunos.Add(aluno);
        await _db.SaveChangesAsync();
        return Ok(new { aluno.Id, aluno.NomeCompleto, aluno.NomeJogo, aluno.TurmaId, aluno.Ativo });
    }

    // =====================================================================
    // LICENÇA DA ESCOLA E COMPUTADORES
    // =====================================================================

    private async Task<IActionResult?> BloqueioLimiteProfessores(Escola escola, Guid? turmaIgnorada)
    {
        if (escola.LimiteProfessores is not int limite) return null;
        int comLogin = await _db.Turmas.CountAsync(t =>
            t.EscolaId == escola.Id && t.LoginProfessor != null && t.LoginProfessor != "" &&
            (turmaIgnorada == null || t.Id != turmaIgnorada));
        if (comLogin < limite) return null;
        return Conflict($"A escola atingiu o limite de {limite} professores da licença. Aumente o limite em \"Licença da escola\" para cadastrar mais.");
    }

    /// <summary>
    /// Define os limites da licença da escola e se ela está ativa.
    /// LimiteAlunos/LimiteProfessores nulos = sem limite.
    /// </summary>
    [HttpPut("escolas/{escolaId:guid}/licenca")]
    public async Task<IActionResult> AtualizarLicencaEscola(Guid escolaId, [FromBody] AtualizarLicencaEscolaDto dto)
    {
        var escola = await _db.Escolas.FindAsync(escolaId);
        if (escola == null) return NotFound("Escola não encontrada.");
        if (dto.LimiteMaquinas < 1 || dto.LimiteMaquinas > 100000) return BadRequest("O limite de computadores deve ser de 1 a 100000.");
        if (dto.LimiteAlunos is < 0 || dto.LimiteProfessores is < 0) return BadRequest("Os limites não podem ser negativos.");

        escola.LimiteAlunos = dto.LimiteAlunos;
        escola.LimiteProfessores = dto.LimiteProfessores;
        escola.LimiteMaquinas = dto.LimiteMaquinas;
        escola.Ativa = dto.Ativa;
        await _db.SaveChangesAsync();

        int alunos = await _db.Alunos.CountAsync(a => a.Ativo && a.Turma!.EscolaId == escola.Id);
        int professores = await _db.Turmas.CountAsync(t => t.EscolaId == escola.Id && t.LoginProfessor != null && t.LoginProfessor != "");
        var avisos = new List<string>();
        if (escola.LimiteAlunos is int la && alunos > la)
            avisos.Add($"A escola já tem {alunos} alunos, acima do novo limite ({la}). Nenhum aluno é apagado, mas novos cadastros ficam bloqueados.");
        if (escola.LimiteProfessores is int lp && professores > lp)
            avisos.Add($"A escola já tem {professores} professores com login, acima do novo limite ({lp}). Novos logins ficam bloqueados.");

        return Ok(new { escola.Id, escola.LimiteAlunos, escola.LimiteProfessores, escola.LimiteMaquinas, escola.Ativa, avisos });
    }

    /// <summary>Computadores que já abriram o jogo com o token da escola.</summary>
    [HttpGet("escolas/{escolaId:guid}/maquinas")]
    public async Task<IActionResult> ListarMaquinas(Guid escolaId)
    {
        var escola = await _db.Escolas.FindAsync(escolaId);
        if (escola == null) return NotFound("Escola não encontrada.");

        DateTime inicioJanela = DateTime.UtcNow - Licenca.JanelaMaquinaAtiva;
        var maquinas = await _db.Maquinas.Where(m => m.EscolaId == escolaId).ToListAsync();
        static DateTime Utc(DateTime d) => DateTime.SpecifyKind(d, DateTimeKind.Utc);

        return Ok(new
        {
            escola.LimiteMaquinas,
            EmUso = maquinas.Count(m => !m.Bloqueada && m.UltimoSinal >= inicioJanela),
            Maquinas = maquinas
                .OrderByDescending(m => m.UltimoSinal)
                .Select(m => new
                {
                    m.Id,
                    m.Nome,
                    Identificador = m.Identificador.Length > 12 ? m.Identificador[..12] + "..." : m.Identificador,
                    m.VersaoJogo,
                    m.Bloqueada,
                    EmUso = !m.Bloqueada && m.UltimoSinal >= inicioJanela,
                    PrimeiroAcesso = Utc(m.PrimeiroAcesso),
                    UltimoSinal = Utc(m.UltimoSinal),
                    UltimaRecusa = m.UltimaRecusa.HasValue ? Utc(m.UltimaRecusa.Value) : (DateTime?)null
                })
        });
    }

    /// <summary>Bloqueia (ou libera) um computador: bloqueado, ele não consegue usar o jogo com este token.</summary>
    [HttpPut("maquinas/{maquinaId:guid}/bloqueio")]
    public async Task<IActionResult> BloquearMaquina(Guid maquinaId, [FromBody] BloqueioMaquinaDto dto)
    {
        var maquina = await _db.Maquinas.FindAsync(maquinaId);
        if (maquina == null) return NotFound("Computador não encontrado.");
        maquina.Bloqueada = dto.Bloqueada;
        await _db.SaveChangesAsync();
        return Ok(new { maquina.Id, maquina.Bloqueada });
    }

    /// <summary>Remove o computador da lista (se ele abrir o jogo de novo, aparece outra vez).</summary>
    [HttpDelete("maquinas/{maquinaId:guid}")]
    public async Task<IActionResult> RemoverMaquina(Guid maquinaId)
    {
        var maquina = await _db.Maquinas.FindAsync(maquinaId);
        if (maquina == null) return NotFound("Computador não encontrado.");
        _db.Maquinas.Remove(maquina);
        await _db.SaveChangesAsync();
        return Ok(new { mensagem = "Computador removido da lista." });
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

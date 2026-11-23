using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RevoltaLetrinhas.Api.Data;

namespace RevoltaLetrinhas.Api.Controllers;

/// <summary>
/// Diagnóstico rápido: mostra qual banco o servidor está usando e quantos registros existem.
/// Abra https://api.kairosstudios.site/api/status no navegador para conferir.
/// </summary>
[ApiController]
[Route("api/status")]
public class StatusController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly InfoBanco _banco;
    private readonly IWebHostEnvironment _env;

    public StatusController(AppDbContext db, InfoBanco banco, IWebHostEnvironment env)
    {
        _db = db;
        _banco = banco;
        _env = env;
    }

    [HttpGet]
    public async Task<IActionResult> Get()
    {
        return Ok(new
        {
            online = true,
            ambiente = _env.EnvironmentName,
            banco = _banco.Provedor,
            bancoPersistente = _banco.Persistente,
            aviso = _banco.Aviso,
            horarioServidorUtc = DateTime.UtcNow,
            totais = new
            {
                municipios = await _db.Municipios.CountAsync(),
                escolas = await _db.Escolas.CountAsync(),
                turmas = await _db.Turmas.CountAsync(),
                alunos = await _db.Alunos.CountAsync(),
                fasesConcluidas = await _db.ProgressoFases.CountAsync(p => p.Concluida)
            }
        });
    }
}

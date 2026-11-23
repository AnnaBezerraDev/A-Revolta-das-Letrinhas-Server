using Microsoft.EntityFrameworkCore;
using RevoltaLetrinhas.Api.Data;
using RevoltaLetrinhas.Api.Models;
using RevoltaLetrinhas.Api.Seguranca;

// Permite gravar datas de qualquer tipo (Local/Unspecified/UTC) no PostgreSQL.
// Sem isso, cadastrar uma prefeitura com data de expiração dá erro 500 quando o banco é PostgreSQL.
AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

var builder = WebApplication.CreateBuilder(args);

// Banco de dados: PostgreSQL (ConnectionStrings:PostgresConnection ou DATABASE_URL) ou SQLite local.
var infoBanco = ConfiguracaoBanco.Registrar(builder);
builder.Services.AddSingleton(infoBanco);

// Senha do painel de administração (variável de ambiente ADMIN_SENHA). Ver Seguranca/AcessoAdmin.cs.
builder.Services.AddSingleton<AcessoAdmin>();

builder.Services.AddControllers();
builder.Services.AddOpenApi();

// Habilita CORS para o jogo Unity e para o painel web (inclusive o painel aberto em http://localhost)
builder.Services.AddCors(options =>
{
    options.AddPolicy("PermitirTudo", p =>
        p.AllowAnyOrigin()
         .AllowAnyMethod()
         .AllowAnyHeader());
});

var app = builder.Build();

app.Logger.LogInformation("Banco de dados em uso: {Provedor} ({Local})", infoBanco.Provedor, infoBanco.Local);
if (!string.IsNullOrEmpty(infoBanco.Aviso))
{
    app.Logger.LogWarning("{Aviso}", infoBanco.Aviso);
}

var acessoAdmin = app.Services.GetRequiredService<AcessoAdmin>();
if (acessoAdmin.SenhaConfigurada)
{
    app.Logger.LogInformation("Painel de administração protegido pela senha {Chave}.", AcessoAdmin.ChaveConfiguracao);
}
foreach (var aviso in acessoAdmin.Avisos)
{
    app.Logger.LogWarning("{Aviso}", aviso);
}

// Inicializa o banco de dados e garante as escolas fixas
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();

    // Dados de demonstração SOMENTE no ambiente de desenvolvimento (servidor local).
    // Em produção eles misturavam escolas fictícias com as escolas reais.
    if (app.Environment.IsDevelopment() && !db.Escolas.Any())
    {
        var mun1 = new Municipio { Nome = "Campinas", UF = "SP" };
        var mun2 = new Municipio { Nome = "Ribeirão Preto", UF = "SP" };
        db.Municipios.AddRange(mun1, mun2);

        var esc1 = new Escola { Municipio = mun1, Nome = "E.M. Monteiro Lobato", TokenAcesso = "ESC-CAMPINAS-01" };
        var esc2 = new Escola { Municipio = mun1, Nome = "Colégio Aquarela", TokenAcesso = "ESC-CAMPINAS-02" };
        var esc3 = new Escola { Municipio = mun2, Nome = "E.E. Cecília Meireles", TokenAcesso = "ESC-RIBEIRAO-01" };
        db.Escolas.AddRange(esc1, esc2, esc3);

        var tur1 = new Turma { Escola = esc1, Nome = "2º Ano A" };
        var tur2 = new Turma { Escola = esc1, Nome = "3º Ano B" };
        db.Turmas.AddRange(tur1, tur2);

        var a1 = new Aluno { Turma = tur1, NomeCompleto = "Arthur Silva", NomeJogo = "Arthur" };
        var a2 = new Aluno { Turma = tur1, NomeCompleto = "Patty Oliveira", NomeJogo = "Patty" };
        var a3 = new Aluno { Turma = tur1, NomeCompleto = "Fernanda Costa", NomeJogo = "Fernanda" };
        var a4 = new Aluno { Turma = tur2, NomeCompleto = "Gabriela Santos", NomeJogo = "Gabriela" };
        db.Alunos.AddRange(a1, a2, a3, a4);

        db.ProgressoFases.AddRange(
            new ProgressoFase { Aluno = a1, Materia = "Portugues", Nivel = "Facil", NumeroFase = 1, Concluida = true, Estrelas = 3, Pontuacao = 1500, TempoGastoSegundos = 45 },
            new ProgressoFase { Aluno = a1, Materia = "Portugues", Nivel = "Facil", NumeroFase = 2, Concluida = true, Estrelas = 3, Pontuacao = 1400, TempoGastoSegundos = 50 },
            new ProgressoFase { Aluno = a1, Materia = "Matematica", Nivel = "Facil", NumeroFase = 1, Concluida = true, Estrelas = 2, Pontuacao = 1100, TempoGastoSegundos = 65 },
            new ProgressoFase { Aluno = a2, Materia = "Portugues", Nivel = "Facil", NumeroFase = 1, Concluida = true, Estrelas = 3, Pontuacao = 1600, TempoGastoSegundos = 38 }
        );

        db.SaveChanges();
    }

    // Escolas fixas usadas pelo jogo: são recriadas automaticamente se não existirem.
    GarantirEscola(db, "ESC-TESTE", "Teste", "Ribeirão Preto", "3A", "Anna", "profanna");
    GarantirEscola(db, "ESC-OLINTO", "Olinto", "Jaborandi", "3ºANO A", "Professora Anna", "prof.anna");
}

app.UseCors("PermitirTudo");

// Servir arquivos estáticos do Painel Web (wwwroot/index.html)
app.UseDefaultFiles();
app.UseStaticFiles();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapControllers();

app.Run();

static void GarantirEscola(AppDbContext db, string token, string nomeEscola, string nomeMunicipio,
                           string nomeTurma, string nomeProfessor, string loginProfessor)
{
    if (db.Escolas.Any(e => e.TokenAcesso == token)) return;

    var mun = db.Municipios.FirstOrDefault(m => m.Nome == nomeMunicipio);
    if (mun == null)
    {
        mun = new Municipio { Nome = nomeMunicipio, UF = "SP" };
        db.Municipios.Add(mun);
    }

    var escola = new Escola { Municipio = mun, Nome = nomeEscola, TokenAcesso = token, Ativa = true };
    db.Escolas.Add(escola);

    db.Turmas.Add(new Turma
    {
        Escola = escola,
        Nome = nomeTurma,
        NomeProfessor = nomeProfessor,
        LoginProfessor = loginProfessor,
        SenhaProfessor = "1234"
    });

    db.SaveChanges();
}

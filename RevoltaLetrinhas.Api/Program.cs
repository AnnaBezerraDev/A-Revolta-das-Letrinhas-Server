using Microsoft.EntityFrameworkCore;
using RevoltaLetrinhas.Api.Data;
using RevoltaLetrinhas.Api.Models;

var builder = WebApplication.CreateBuilder(args);

// Configuração do Banco de Dados: SQLite local por padrão (ou PostgreSQL via appsettings/env)
var postgresConn = builder.Configuration.GetConnectionString("PostgresConnection");
if (!string.IsNullOrEmpty(postgresConn))
{
    builder.Services.AddDbContext<AppDbContext>(opt => opt.UseNpgsql(postgresConn));
}
else
{
    builder.Services.AddDbContext<AppDbContext>(opt =>
        opt.UseSqlite("Data Source=revoltaletrinhas.db"));
}

builder.Services.AddControllers();
builder.Services.AddOpenApi();

// Habilita CORS para o jogo Unity e painel Web
builder.Services.AddCors(options =>
{
    options.AddPolicy("PermitirTudo", p =>
        p.AllowAnyOrigin()
         .AllowAnyMethod()
         .AllowAnyHeader());
});

var app = builder.Build();

// Inicializa o banco de dados e cria dados demo se estiver vazio
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();

    if (!db.Escolas.Any())
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

        // Fases iniciais de exemplo
        db.ProgressoFases.AddRange(
            new ProgressoFase { Aluno = a1, Materia = "Portugues", Nivel = "Facil", NumeroFase = 1, Concluida = true, Estrelas = 3, Pontuacao = 1500, TempoGastoSegundos = 45 },
            new ProgressoFase { Aluno = a1, Materia = "Portugues", Nivel = "Facil", NumeroFase = 2, Concluida = true, Estrelas = 3, Pontuacao = 1400, TempoGastoSegundos = 50 },
            new ProgressoFase { Aluno = a1, Materia = "Matematica", Nivel = "Facil", NumeroFase = 1, Concluida = true, Estrelas = 2, Pontuacao = 1100, TempoGastoSegundos = 65 },
            new ProgressoFase { Aluno = a2, Materia = "Portugues", Nivel = "Facil", NumeroFase = 1, Concluida = true, Estrelas = 3, Pontuacao = 1600, TempoGastoSegundos = 38 }
        );

        db.SaveChanges();
    }

    // Garante que a escola Teste (ESC-TESTE) existe com a turma 3A e professora Anna
    if (!db.Escolas.Any(e => e.TokenAcesso == "ESC-TESTE"))
    {
        var mun = db.Municipios.FirstOrDefault(m => m.Nome == "Ribeirão Preto") ?? new Municipio { Nome = "Ribeirão Preto", UF = "SP" };
        if (mun.Id == Guid.Empty) db.Municipios.Add(mun);

        var escTeste = new Escola
        {
            Municipio = mun,
            Nome = "Teste",
            TokenAcesso = "ESC-TESTE",
            Ativa = true
        };
        db.Escolas.Add(escTeste);

        var tur3A = new Turma
        {
            Escola = escTeste,
            Nome = "3A",
            NomeProfessor = "Anna",
            LoginProfessor = "profanna",
            SenhaProfessor = "1234"
        };
        db.Turmas.Add(tur3A);
        db.SaveChanges();
    }

    // Garante que a escola Olinto (ESC-OLINTO) também existe com turma 3ºANO A
    if (!db.Escolas.Any(e => e.TokenAcesso == "ESC-OLINTO"))
    {
        var munJaborandi = db.Municipios.FirstOrDefault(m => m.Nome == "Jaborandi") ?? new Municipio { Nome = "Jaborandi", UF = "SP" };
        if (munJaborandi.Id == Guid.Empty) db.Municipios.Add(munJaborandi);

        var escOlinto = new Escola
        {
            Municipio = munJaborandi,
            Nome = "Olinto",
            TokenAcesso = "ESC-OLINTO",
            Ativa = true
        };
        db.Escolas.Add(escOlinto);

        var turOlinto = new Turma
        {
            Escola = escOlinto,
            Nome = "3ºANO A",
            NomeProfessor = "Professora Anna",
            LoginProfessor = "prof.anna",
            SenhaProfessor = "1234"
        };
        db.Turmas.Add(turOlinto);
        db.SaveChanges();
    }
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

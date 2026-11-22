using Microsoft.EntityFrameworkCore;
using RevoltaLetrinhas.Api.Models;

namespace RevoltaLetrinhas.Api.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Municipio> Municipios => Set<Municipio>();
    public DbSet<Escola> Escolas => Set<Escola>();
    public DbSet<Turma> Turmas => Set<Turma>();
    public DbSet<Aluno> Alunos => Set<Aluno>();
    public DbSet<ProgressoFase> ProgressoFases => Set<ProgressoFase>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Índice único no Token da Escola
        modelBuilder.Entity<Escola>()
            .HasIndex(e => e.TokenAcesso)
            .IsUnique();

        // Índice composto no Progresso da Fase
        modelBuilder.Entity<ProgressoFase>()
            .HasIndex(p => new { p.AlunoId, p.Materia, p.Nivel, p.NumeroFase })
            .IsUnique();
    }
}

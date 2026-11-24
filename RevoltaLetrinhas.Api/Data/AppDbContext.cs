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
    public DbSet<Maquina> Maquinas => Set<Maquina>();
    public DbSet<PersonagemResgatado> PersonagensResgatados => Set<PersonagemResgatado>();
    public DbSet<AlunoHabilidade> AlunoHabilidades => Set<AlunoHabilidade>();

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

        // Um computador aparece uma única vez por escola
        modelBuilder.Entity<Maquina>()
            .HasIndex(m => new { m.EscolaId, m.Identificador })
            .IsUnique();

        // Personagem de perfil: um dono por escola e um personagem por aluno
        modelBuilder.Entity<PersonagemResgatado>()
            .HasIndex(p => new { p.EscolaId, p.Codigo })
            .IsUnique();
        modelBuilder.Entity<PersonagemResgatado>()
            .HasIndex(p => p.AlunoId)
            .IsUnique();
        modelBuilder.Entity<PersonagemResgatado>()
            .HasOne(p => p.Aluno).WithMany().HasForeignKey(p => p.AlunoId).OnDelete(DeleteBehavior.Cascade);

        // Acertos/erros por habilidade da BNCC: uma linha por aluno e habilidade
        modelBuilder.Entity<AlunoHabilidade>()
            .HasIndex(h => new { h.AlunoId, h.Codigo })
            .IsUnique();
        modelBuilder.Entity<AlunoHabilidade>()
            .HasOne(h => h.Aluno).WithMany().HasForeignKey(h => h.AlunoId).OnDelete(DeleteBehavior.Cascade);
    }
}

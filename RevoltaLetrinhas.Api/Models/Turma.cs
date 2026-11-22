using System.ComponentModel.DataAnnotations;

namespace RevoltaLetrinhas.Api.Models;

public class Turma
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid EscolaId { get; set; }
    public Escola? Escola { get; set; }

    [Required]
    [MaxLength(50)]
    public string Nome { get; set; } = string.Empty; // Ex: "2º Ano A", "3º Ano B"

    public int AnoLetivo { get; set; } = DateTime.UtcNow.Year;

    /// <summary>
    /// Nome do professor responsável pela turma/sala
    /// </summary>
    [MaxLength(100)]
    public string? NomeProfessor { get; set; }

    /// <summary>
    /// Usuário exclusivo do professor para login nesta turma (ex: "prof.mariana")
    /// </summary>
    [MaxLength(50)]
    public string? LoginProfessor { get; set; }

    /// <summary>
    /// Senha de acesso exclusivo do professor para esta turma
    /// </summary>
    [MaxLength(100)]
    public string? SenhaProfessor { get; set; }

    public List<Aluno> Alunos { get; set; } = new();
}

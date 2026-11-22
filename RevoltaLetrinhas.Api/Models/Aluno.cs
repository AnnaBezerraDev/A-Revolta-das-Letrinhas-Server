using System.ComponentModel.DataAnnotations;

namespace RevoltaLetrinhas.Api.Models;

public class Aluno
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid TurmaId { get; set; }
    public Turma? Turma { get; set; }

    [Required]
    [MaxLength(120)]
    public string NomeCompleto { get; set; } = string.Empty;

    /// <summary>
    /// Nome simplificado exibido no jogo (ex: "Arthur M.", "Patty")
    /// </summary>
    [MaxLength(50)]
    public string NomeJogo { get; set; } = string.Empty;

    public bool Ativo { get; set; } = true;

    public DateTime DataCriacao { get; set; } = DateTime.UtcNow;

    public List<ProgressoFase> Progressos { get; set; } = new();
}

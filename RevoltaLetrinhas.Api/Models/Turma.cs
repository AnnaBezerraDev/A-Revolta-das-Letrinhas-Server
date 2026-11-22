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

    public List<Aluno> Alunos { get; set; } = new();
}

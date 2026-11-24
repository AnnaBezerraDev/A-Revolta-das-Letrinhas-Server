using System.ComponentModel.DataAnnotations;

namespace RevoltaLetrinhas.Api.Models;

/// <summary>
/// Acertos e erros de um aluno em uma habilidade da BNCC (somados a cada fase jogada,
/// concluída ou não). Base dos relatórios por habilidade.
/// </summary>
public class AlunoHabilidade
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid AlunoId { get; set; }
    public Aluno? Aluno { get; set; }

    [Required]
    [MaxLength(12)]
    public string Codigo { get; set; } = string.Empty;

    public int Acertos { get; set; }

    public int Erros { get; set; }

    public DateTime UltimaAtualizacao { get; set; } = DateTime.UtcNow;
}

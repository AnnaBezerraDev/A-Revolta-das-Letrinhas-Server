using System.ComponentModel.DataAnnotations;

namespace RevoltaLetrinhas.Api.Models;

/// <summary>
/// Personagem de perfil resgatado por um aluno. Cada personagem tem um único dono por escola
/// (índice único EscolaId + Codigo) e cada aluno tem um personagem (índice único AlunoId).
/// Ao excluir o aluno, o registro some junto e o personagem fica livre de novo.
/// </summary>
public class PersonagemResgatado
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid EscolaId { get; set; }

    public Guid AlunoId { get; set; }
    public Aluno? Aluno { get; set; }

    /// <summary>Código do personagem no catálogo (wwwroot/imagens/personagens/catalogo.json).</summary>
    [Required]
    [MaxLength(80)]
    public string Codigo { get; set; } = string.Empty;

    public DateTime DataResgate { get; set; } = DateTime.UtcNow;
}

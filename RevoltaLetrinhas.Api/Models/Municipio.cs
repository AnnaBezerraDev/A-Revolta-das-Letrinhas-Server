using System.ComponentModel.DataAnnotations;

namespace RevoltaLetrinhas.Api.Models;

public class Municipio
{
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    [MaxLength(100)]
    public string Nome { get; set; } = string.Empty;

    [Required]
    [MaxLength(2)]
    public string UF { get; set; } = "SP";

    public DateTime DataCriacao { get; set; } = DateTime.UtcNow;

    public List<Escola> Escolas { get; set; } = new();
}

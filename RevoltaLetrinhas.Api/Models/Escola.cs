using System.ComponentModel.DataAnnotations;

namespace RevoltaLetrinhas.Api.Models;

public class Escola
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid MunicipioId { get; set; }
    public Municipio? Municipio { get; set; }

    [Required]
    [MaxLength(150)]
    public string Nome { get; set; } = string.Empty;

    [MaxLength(20)]
    public string? CodigoInep { get; set; }

    /// <summary>
    /// Token único que o laboratório da escola usa no config.json do jogo .exe
    /// Exemplo: ESC-CAMPINAS-01
    /// </summary>
    [Required]
    [MaxLength(50)]
    public string TokenAcesso { get; set; } = string.Empty;

    public bool Ativa { get; set; } = true;

    public DateTime DataCriacao { get; set; } = DateTime.UtcNow;

    public List<Turma> Turmas { get; set; } = new();
}

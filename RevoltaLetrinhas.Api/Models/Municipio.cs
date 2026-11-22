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

    /// <summary>
    /// Data de validade/expiração do contrato com a prefeitura
    /// </summary>
    public DateTime? DataExpiracaoContrato { get; set; }

    /// <summary>
    /// CNPJ ou número do convênio do município
    /// </summary>
    [MaxLength(50)]
    public string? NumeroConvenio { get; set; }

    public DateTime DataCriacao { get; set; } = DateTime.UtcNow;

    public List<Escola> Escolas { get; set; } = new();
}

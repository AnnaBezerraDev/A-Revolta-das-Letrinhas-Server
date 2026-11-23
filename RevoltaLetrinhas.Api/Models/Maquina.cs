using System.ComponentModel.DataAnnotations;

namespace RevoltaLetrinhas.Api.Models;

/// <summary>
/// Computador que já abriu o jogo com o token de uma escola. O jogo envia um "sinal" a cada minuto;
/// as máquinas com sinal recente contam no limite de computadores usando o jogo ao mesmo tempo.
/// </summary>
public class Maquina
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid EscolaId { get; set; }
    public Escola? Escola { get; set; }

    /// <summary>Identificador único do computador (gerado pelo jogo a partir do hardware).</summary>
    [Required]
    [MaxLength(128)]
    public string Identificador { get; set; } = string.Empty;

    /// <summary>Nome do computador no Windows (ex.: LAB-PC-07).</summary>
    [MaxLength(100)]
    public string Nome { get; set; } = string.Empty;

    [MaxLength(20)]
    public string? VersaoJogo { get; set; }

    /// <summary>Bloqueada pela administração: não consegue usar o jogo com este token.</summary>
    public bool Bloqueada { get; set; }

    public DateTime PrimeiroAcesso { get; set; } = DateTime.UtcNow;

    /// <summary>Último sinal recebido (máquina em uso se for recente).</summary>
    public DateTime UltimoSinal { get; set; } = DateTime.UtcNow;

    /// <summary>Última vez que a máquina foi recusada por limite de computadores.</summary>
    public DateTime? UltimaRecusa { get; set; }
}

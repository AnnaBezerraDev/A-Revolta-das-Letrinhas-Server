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

    // ===== Licença =====

    /// <summary>Máximo de alunos cadastrados na escola (null = sem limite).</summary>
    public int? LimiteAlunos { get; set; }

    /// <summary>Máximo de professores com login (null = sem limite).</summary>
    public int? LimiteProfessores { get; set; }

    /// <summary>Máximo de computadores usando o jogo ao mesmo tempo.</summary>
    public int LimiteMaquinas { get; set; } = 100;

    /// <summary>
    /// Senha da coordenação (login "coordenacao" no jogo, acesso a todas as salas), só como hash
    /// (ver Seguranca/SenhaHash). null = ainda não definida: o acesso geral fica desligado.
    /// </summary>
    [MaxLength(200)]
    public string? SenhaCoordenacaoHash { get; set; }

    public List<Turma> Turmas { get; set; } = new();

    public List<Maquina> Maquinas { get; set; } = new();
}

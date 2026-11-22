using System.ComponentModel.DataAnnotations;

namespace RevoltaLetrinhas.Api.Models;

public class ProgressoFase
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid AlunoId { get; set; }
    public Aluno? Aluno { get; set; }

    [Required]
    [MaxLength(20)]
    public string Materia { get; set; } = "Portugues"; // "Portugues" ou "Matematica"

    [Required]
    [MaxLength(20)]
    public string Nivel { get; set; } = "Facil"; // "Facil", "Medio", "Dificil"

    public int NumeroFase { get; set; } // 1 a 30

    public bool Concluida { get; set; }

    public int Estrelas { get; set; } // 0 a 3

    public int Pontuacao { get; set; }

    public int QuantidadeErros { get; set; }

    public int TempoGastoSegundos { get; set; }

    public DateTime UltimaAtualizacao { get; set; } = DateTime.UtcNow;
}

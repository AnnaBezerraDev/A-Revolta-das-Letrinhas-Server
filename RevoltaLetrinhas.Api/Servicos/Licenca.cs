using RevoltaLetrinhas.Api.Models;

namespace RevoltaLetrinhas.Api.Servicos;

/// <summary>
/// Situação da licença de uma escola: escola ativa + contrato da prefeitura dentro da validade.
/// </summary>
public record StatusLicenca(bool Valida, string Codigo, string Mensagem, DateTime? DataExpiracao, int? DiasRestantes);

public static class Licenca
{
    /// <summary>Tempo sem sinal para um computador deixar de contar como "em uso".</summary>
    public static readonly TimeSpan JanelaMaquinaAtiva = TimeSpan.FromMinutes(3);

    /// <summary>Intervalo que o jogo deve usar entre um sinal e outro.</summary>
    public const int IntervaloSinalSegundos = 60;

    /// <summary>
    /// O contrato vale até o fim do dia da data de expiração (horário de Brasília).
    /// </summary>
    public static DateTime FimDaValidade(DateTime dataExpiracao) => dataExpiracao.Date.AddDays(1).AddHours(3);

    public static StatusLicenca Avaliar(Escola escola) =>
        Avaliar(escola.Ativa, escola.Municipio?.Nome, escola.Municipio?.DataExpiracaoContrato);

    public static StatusLicenca Avaliar(bool escolaAtiva, string? nomeMunicipio, DateTime? expiracao)
    {
        int? diasRestantes = null;
        if (expiracao.HasValue)
        {
            diasRestantes = (int)Math.Floor((FimDaValidade(expiracao.Value) - DateTime.UtcNow).TotalDays);
        }

        if (!escolaAtiva)
        {
            return new StatusLicenca(false, "escola_inativa",
                "O acesso desta escola está desativado. Entre em contato com o fornecedor do jogo.",
                expiracao, diasRestantes);
        }

        if (expiracao.HasValue && DateTime.UtcNow >= FimDaValidade(expiracao.Value))
        {
            string prefeitura = nomeMunicipio ?? "município";
            return new StatusLicenca(false, "contrato_vencido",
                $"O contrato da prefeitura de {prefeitura} venceu em {expiracao.Value:dd/MM/yyyy}. " +
                "O jogo volta a funcionar assim que o contrato for renovado.",
                expiracao, diasRestantes);
        }

        return new StatusLicenca(true, "ok", "", expiracao, diasRestantes);
    }

    /// <summary>Corpo padrão das respostas 403 de licença (o jogo lê o "codigo").</summary>
    public static object Resposta(StatusLicenca s) => new
    {
        erro = s.Mensagem,
        codigo = s.Codigo,
        temExpiracao = s.DataExpiracao.HasValue,
        dataExpiracao = s.DataExpiracao?.ToString("yyyy-MM-dd") ?? "",
        diasRestantes = s.DiasRestantes ?? 0
    };
}

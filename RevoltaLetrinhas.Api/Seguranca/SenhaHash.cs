using System.Security.Cryptography;

namespace RevoltaLetrinhas.Api.Seguranca;

/// <summary>
/// Guarda senhas só como hash PBKDF2-SHA256 com sal aleatório (a senha em si nunca vai para o banco).
/// Formato gravado: "pbkdf2-sha256$iteracoes$salBase64$hashBase64".
/// </summary>
public static class SenhaHash
{
    private const string Prefixo = "pbkdf2-sha256";
    private const int Iteracoes = 100_000;
    private const int TamanhoSal = 16;
    private const int TamanhoHash = 32;

    public static string Gerar(string senha)
    {
        byte[] sal = RandomNumberGenerator.GetBytes(TamanhoSal);
        byte[] hash = Rfc2898DeriveBytes.Pbkdf2(senha, sal, Iteracoes, HashAlgorithmName.SHA256, TamanhoHash);
        return $"{Prefixo}${Iteracoes}${Convert.ToBase64String(sal)}${Convert.ToBase64String(hash)}";
    }

    public static bool Confere(string? senha, string? armazenado)
    {
        if (string.IsNullOrEmpty(senha) || string.IsNullOrEmpty(armazenado)) return false;
        string[] partes = armazenado.Split('$');
        if (partes.Length != 4 || partes[0] != Prefixo || !int.TryParse(partes[1], out int iteracoes) || iteracoes < 1)
            return false;
        try
        {
            byte[] sal = Convert.FromBase64String(partes[2]);
            byte[] esperado = Convert.FromBase64String(partes[3]);
            byte[] calculado = Rfc2898DeriveBytes.Pbkdf2(senha, sal, iteracoes, HashAlgorithmName.SHA256, esperado.Length);
            return CryptographicOperations.FixedTimeEquals(calculado, esperado);
        }
        catch (FormatException)
        {
            return false;
        }
    }
}

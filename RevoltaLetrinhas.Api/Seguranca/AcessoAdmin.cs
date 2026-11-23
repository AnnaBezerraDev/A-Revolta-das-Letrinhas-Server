using System.Net;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace RevoltaLetrinhas.Api.Seguranca;

/// <summary>
/// Senha única de administrador, lida da configuração "ADMIN_SENHA"
/// (variável de ambiente ADMIN_SENHA no Render, ou appsettings / user-secrets no PC).
///
/// Ela protege o painel (/api/admin/*): cadastros, exclusões, licenças, computadores e contratos.
/// As rotas usadas pelo jogo (/api/jogo/conectar, criar-aluno, progresso...) NÃO usam esta senha:
/// elas continuam identificadas pelo token da escola.
///
/// Se ADMIN_SENHA não estiver configurada:
///   - Produção (Render): todas as rotas de administração são RECUSADAS (HTTP 503) e o log avisa.
///   - Desenvolvimento (IniciarServidor.bat / dotnet run): liberadas SOMENTE para chamadas feitas
///     do próprio computador (localhost), para o painel de testes local continuar funcionando.
/// </summary>
public sealed class AcessoAdmin
{
    public const string ChaveConfiguracao = "ADMIN_SENHA";
    public const string NomeCabecalho = "X-Admin-Senha";
    public const int TamanhoMinimoRecomendado = 12;

    private readonly byte[]? _hashSenha;

    public bool SenhaConfigurada => _hashSenha != null;

    /// <summary>Sem senha configurada, libera chamadas vindas do próprio PC (apenas em Development).</summary>
    public bool LiberarLocalhostSemSenha { get; }

    /// <summary>Avisos para o log de inicialização (senha ausente, curta ou com acentos).</summary>
    public IReadOnlyList<string> Avisos { get; }

    public AcessoAdmin(IConfiguration configuracao, IWebHostEnvironment ambiente)
    {
        var avisos = new List<string>();
        string? senha = configuracao[ChaveConfiguracao]?.Trim();

        if (string.IsNullOrEmpty(senha))
        {
            LiberarLocalhostSemSenha = ambiente.IsDevelopment();
            avisos.Add(LiberarLocalhostSemSenha
                ? $"{ChaveConfiguracao} não configurada: as rotas de administração só aceitam chamadas deste computador (localhost), sem senha. Isso vale apenas no ambiente de desenvolvimento."
                : $"{ChaveConfiguracao} não configurada: o painel de administração está DESATIVADO (HTTP 503). Defina a variável de ambiente {ChaveConfiguracao} no servidor (Render → Environment) e reinicie.");
        }
        else
        {
            _hashSenha = Hash(senha);
            if (senha.Length < TamanhoMinimoRecomendado)
                avisos.Add($"{ChaveConfiguracao} tem menos de {TamanhoMinimoRecomendado} caracteres. Use uma senha longa: ela é a única proteção contra apagar todos os dados.");
            if (senha.Any(c => c < 0x20 || c > 0x7E))
                avisos.Add($"{ChaveConfiguracao} contém acentos ou caracteres especiais que o navegador não consegue enviar em um cabeçalho HTTP. Use só letras sem acento, números e símbolos comuns.");
        }

        Avisos = avisos;
    }

    public bool SenhaConfere(string? informada)
    {
        if (_hashSenha == null || string.IsNullOrEmpty(informada)) return false;
        // Compara os hashes em tempo constante (não revela quantos caracteres estão certos).
        return CryptographicOperations.FixedTimeEquals(Hash(informada.Trim()), _hashSenha);
    }

    private static byte[] Hash(string texto) => SHA256.HashData(Encoding.UTF8.GetBytes(texto));

    public static bool ChamadaLocal(HttpContext http)
    {
        var ip = http.Connection.RemoteIpAddress;
        if (ip == null) return false;
        if (ip.IsIPv4MappedToIPv6) ip = ip.MapToIPv4();
        return IPAddress.IsLoopback(ip);
    }
}

/// <summary>
/// Exige o cabeçalho "X-Admin-Senha" com a senha de ADMIN_SENHA.
/// Coloque [ExigirSenhaAdmin] em um controller inteiro ou em uma ação.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class ExigirSenhaAdminAttribute : Attribute, IAsyncAuthorizationFilter
{
    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var http = context.HttpContext;
        var acesso = http.RequestServices.GetRequiredService<AcessoAdmin>();
        var log = http.RequestServices.GetRequiredService<ILogger<AcessoAdmin>>();
        string rota = $"{http.Request.Method} {http.Request.Path}";

        if (!acesso.SenhaConfigurada)
        {
            if (acesso.LiberarLocalhostSemSenha && AcessoAdmin.ChamadaLocal(http)) return;

            log.LogWarning("Chamada de administração recusada ({Rota}): {Chave} não está configurada no servidor.", rota, AcessoAdmin.ChaveConfiguracao);
            context.Result = Resposta(StatusCodes.Status503ServiceUnavailable, "admin_senha_nao_configurada",
                acesso.LiberarLocalhostSemSenha
                    ? $"Sem a variável {AcessoAdmin.ChaveConfiguracao}, este servidor de testes só aceita o painel aberto neste mesmo computador (localhost)."
                    : $"O painel de administração está desativado porque a variável {AcessoAdmin.ChaveConfiguracao} não foi configurada no servidor. Configure-a no Render (Environment) e reinicie o serviço.");
            return;
        }

        string? informada = http.Request.Headers[AcessoAdmin.NomeCabecalho].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(informada))
        {
            context.Result = Resposta(StatusCodes.Status401Unauthorized, "senha_admin_ausente",
                "Este painel exige a senha de administrador.");
            return;
        }

        if (!acesso.SenhaConfere(informada))
        {
            log.LogWarning("Senha de administrador incorreta ({Rota}) vinda de {Ip}.", rota, http.Connection.RemoteIpAddress);
            await Task.Delay(500); // atrasa tentativas de adivinhar a senha
            context.Result = Resposta(StatusCodes.Status401Unauthorized, "senha_admin_incorreta",
                "Senha de administrador incorreta.");
        }
    }

    private static ObjectResult Resposta(int status, string codigo, string erro) =>
        new(new { erro, codigo }) { StatusCode = status };
}

# Senha do painel de administração (`ADMIN_SENHA`)

Antes, qualquer pessoa na internet podia chamar `/api/admin/*` e apagar prefeituras, escolas, salas e alunos.
Agora essas rotas exigem uma senha única de administrador.

## O que exige a senha

| Rota | Usada por |
|------|-----------|
| `/api/admin/*` (listar tudo, cadastrar, editar, **excluir**, exportar CSV, **licença da escola, computadores e contrato da prefeitura**) | Painel web (`index.html`) |

## O que continua SEM a senha (usa o token da escola)

`/api/status`, `/api/jogo/conectar`, `criar-aluno`, `criar-turma`, `atualizar-turma-aluno`, `excluir-aluno`,
`progresso`, `progresso-lote`, `progresso-aluno`, `salas-resumo`, `login-professor`, `maquina/sinal`, `maquina/encerrar`,
`versao`, `download-patch`.
O jogo instalado nas escolas não precisa de nenhuma mudança.

## Como funciona

- O servidor lê a senha da configuração `ADMIN_SENHA` (variável de ambiente).
- O painel pede a senha uma vez (caixa "Acesso do administrador"), guarda só na aba do navegador
  (`sessionStorage`) e envia no cabeçalho `X-Admin-Senha`. Fechar a aba ou clicar em **🔒 Sair** apaga a senha.
- Senha errada → HTTP 401 e o painel pede de novo ("Senha incorreta").
- Tentativas erradas aparecem no log do servidor ("Senha de administrador incorreta ... vinda de <IP>").

## Se `ADMIN_SENHA` não estiver configurada

| Ambiente | Comportamento |
|----------|---------------|
| **Produção** (Render) | Painel **desativado**: respondem HTTP 503 com a mensagem "a variável ADMIN_SENHA não foi configurada no servidor". O log de inicialização avisa. Nada fica aberto por engano. |
| **Desenvolvimento** (`IniciarServidor.bat` / `dotnet run`) | Liberado **somente** para chamadas do próprio computador (localhost), sem senha — o painel de testes local continua funcionando como antes. |

Se a senha tiver menos de 12 caracteres, ou tiver acento/ç, o log de inicialização avisa.

## Configurar no Render

1. **Antes** de publicar este código, abra o painel do Render → serviço da API → **Environment**.
2. **Add Environment Variable**: Key `ADMIN_SENHA`, Value = uma senha longa (só letras sem acento, números e símbolos comuns).
   Para gerar uma no PowerShell:
   `-join ((48..57)+(65..90)+(97..122) | Get-Random -Count 24 | ForEach-Object {[char]$_})`
3. **Save Changes** (o Render reinicia o serviço sozinho).
4. Faça o push do código. Quando o deploy terminar, o log deve mostrar
   `Painel de administração protegido pela senha ADMIN_SENHA.`
5. Abra `https://api.kairosstudios.site`, digite a senha na caixa "Acesso do administrador".

Se o código for publicado antes da variável existir, o painel da nuvem mostra o erro 503 até a variável ser criada.

## Usar uma senha também no PC (opcional)

Para testar o servidor local exigindo senha, no PowerShell antes do `dotnet run`:
`$env:ADMIN_SENHA = "minha-senha-de-teste"`

## Publicar patch do jogo

Não há rota de envio de patch (a antiga `upload-patch` foi removida na 1.0.5). Coloque o novo
`Assembly-CSharp.dll` em `wwwroot/patch/`, aumente a versão em `JogoController.ObterVersaoJogo` e faça commit.

## Código

- `RevoltaLetrinhas.Api/Seguranca/AcessoAdmin.cs` — leitura da senha, regras acima e o filtro `[ExigirSenhaAdmin]`.
- `AdminController` (classe inteira) usa `[ExigirSenhaAdmin]`.
- `wwwroot/index.html` — caixa de senha, cabeçalho `X-Admin-Senha` e download do CSV via `fetch`.

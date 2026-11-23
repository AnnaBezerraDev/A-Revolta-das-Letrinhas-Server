# Correções da comunicação Jogo ↔ API ↔ Painel (23/11/2026)

Backup completo de antes das mudanças: `Downloads\Backup_RevoltaLetrinhas_2026-11-23`
(teste2, teste2_Servidor, Jogo Novo - Publicar e Jogo_RevoltaDasLetrinhas_Windows — sem a pasta `Library`, que a Unity recria).

## O que estava errado

| # | Problema | Efeito |
|---|----------|--------|
| 1 | O jogo envia `"turmaId": ""` ao cadastrar aluno e a API esperava um GUID | **Todo** aluno criado no jogo era recusado (HTTP 400) e nunca chegava ao servidor |
| 2 | O painel aberto pelo `IniciarServidor.bat` lia um banco SQLite **local**, e o jogo grava na **nuvem** | O que era feito no jogo nunca aparecia no painel |
| 3 | O banco da nuvem estava vazio (nem a escola `ESC-OLINTO` existia) | O jogo não conseguia conectar nem salvar nada |
| 4 | A nuvem não tinha o endpoint `atualizar-turma-aluno` (só existia no PC, sem commit) | Mudar a sala de um aluno no jogo dava erro 404 |
| 5 | Cadastrar escola/sala/aluno pelo painel devolvia erro 500 **mesmo salvando** (referência circular no JSON) | O painel parecia falhar, mas os dados apareciam depois |
| 6 | O painel mostrava "cadastrado com sucesso!" sem checar a resposta | Erros da API ficavam escondidos |
| 7 | Salas criadas no jogo ficavam só no PC; excluir aluno no jogo não avisava o servidor | O aluno excluído "voltava" na próxima abertura |
| 8 | Progresso feito sem internet ficava só na memória | Perdia-se ao fechar o jogo |
| 9 | Reenvio do cadastro criava alunos duplicados | Mesmo aluno aparecia 2x no painel |
| 10 | Em produção, um banco novo recebia escolas fictícias (Campinas, Ribeirão...) | Dados de demonstração misturados com os reais |
| 11 | O auto-update ignorava a versão do próprio jogo (usava só um valor salvo no PC, padrão 100) | Num PC novo, baixava o patch **antigo** do servidor (v1.0.2, código do `teste2`) e, ao sair pelo botão do jogo, trocava o código novo pelo antigo |

## O que mudou

**Servidor (`teste2_Servidor`)**
- `CriarAlunoDto.TurmaId` agora aceita `""` (corrige o item 1 até para o jogo já instalado nas escolas).
- Cadastros vindos do jogo são idempotentes (não duplicam) e serializados.
- Novo `POST /api/jogo/criar-turma` (sala criada no jogo).
- `atualizar-turma-aluno` cadastra o aluno se ele ainda não existir no servidor.
- Tokens de escola sem diferenciar maiúsculas/minúsculas.
- Admin: respostas sem referência circular; bloqueia escola com token repetido, sala repetida e aluno com o mesmo nome no jogo.
- `GET /api/status`: mostra qual banco está em uso, se é persistente e quantos registros existem.
- Banco: aceita `DATABASE_URL` (PostgreSQL do Render) e `SQLITE_PATH` (disco persistente). Dados de demonstração só no servidor local.
- Painel: lê da **nuvem** por padrão (mesmo aberto em `localhost`), tem seletor Nuvem/Local, mostra erros reais e atualiza sozinho a cada 30 s.

**Jogo (`Jogo Novo - Publicar\Assets`)** — essa é a pasta com o código mais recente (a `teste2` está mais antiga)
- `ServicoNuvem.cs`: fila de envio **salva em disco**, enviada em ordem e reenviada automaticamente (alunos, salas, exclusões, mudança de sala e fases).
- Reconecta sozinho se o servidor estava dormindo/sem internet na abertura (espera até 30 s).
- Na 1ª sincronização, envia uma única vez os alunos que existiam só neste PC.
- `GerenciadorPerfis.cs`: excluir aluno e criar sala avisam o servidor; abrir "Quem vai jogar hoje?" busca a lista atualizada; a sincronização não reinicia a tela de fases.
- Auto-update: o build agora se identifica como versão **103** e só baixa patch com número maior que o dele.

## Testes feitos (servidor local + build de teste isolado do jogo)
- JSON exato do jogo aceito pelo servidor; reenvio não duplica; criar + mudar sala ao mesmo tempo não duplica.
- Jogo aberto **sem servidor** com 5 envios pendentes → guardou tudo; servidor religado → o jogo reconectou sozinho e enviou sala, aluno, fase, exclusão e mudança de sala, em ordem.
- Aluno antigo que só existia no PC foi enviado uma única vez.
- Aluno excluído / cadastrado no painel sumiu / apareceu no jogo na abertura seguinte.
- Não testado: a tela do jogo clicando nos botões (as chamadas dos botões foram conferidas na compilação), e o deploy real no Render.

## Para publicar

1. **Servidor**: commit + push do `teste2_Servidor` para o GitHub (o Render faz o deploy). Depois abra
   `https://api.kairosstudios.site/api/status` e confira `"banco"` e `"bancoPersistente"`.
2. **Persistência no Render (importante)**: se `/api/status` mostrar `"banco": "SQLite"` com aviso, os dados são
   apagados a cada deploy/reinício. Crie um PostgreSQL no Render e coloque a *Internal Database URL* na variável
   de ambiente `DATABASE_URL` do serviço (ou adicione um disco persistente e defina `SQLITE_PATH=/dados/revoltaletrinhas.db`).
3. **Jogo**: gere o build pela Unity (menu *Publicacao → 2. Gerar Jogo para Windows*) a partir de `Jogo Novo - Publicar`.
   Para distribuir pelo auto-update às escolas que já têm o jogo, copie o novo
   `RevoltaDasLetrinhas_Data\Managed\Assembly-CSharp.dll` para `wwwroot/patch/` e mude `versaoCodigo` para **103**
   (e `versao` para "1.0.3") em `JogoController.ObterVersaoJogo`. Enquanto isso não for feito, o servidor continua
   oferecendo o patch antigo 102 — que o build novo ignora, mas instalações antigas ainda baixam.

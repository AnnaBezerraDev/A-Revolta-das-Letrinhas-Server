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

## Versão 1.0.3 (23/11/2026) — salas, professor e nova Área do Professor

**Problemas corrigidos**
- **Salas no jogo que não existiam no banco:** o jogo juntava salas "padrão" (1º Ano A, 1º Ano B…) e salas antigas
  salvas no PC. Agora, ao sincronizar, a lista de salas do PC passa a ser **exatamente a do servidor** (mais as
  salas criadas no jogo que ainda estão sendo enviadas). As sugestões só aparecem se o jogo nunca falou com o servidor.
- **Professor via dados incorretos:** a Área do Professor lia só os dados salvos naquele computador; o login tinha
  um "plano B" local que aceitava a senha `1234` para qualquer professor; e o jogo enviava sempre 3 estrelas,
  1000 pontos e 0 erros. Agora o login de professor é só pelo servidor, o jogo envia o resultado real de cada
  fase (vidas que sobraram e tempo) e o painel usa os dados do servidor (todos os computadores da escola).
- **Progresso antigo:** as fases concluídas antes da correção são enviadas ao servidor uma única vez
  (`POST /api/jogo/progresso-lote`).
- Taxa de conclusão calculada sobre 90 fases por matéria (antes usava 30 e passava de 100%).
- Situação do aluno pela taxa de erros (antes era por número absoluto de erros).

**Nova Área do Professor (`Assets/PainelProfessorNuvem.cs`)**
- *Visão da sala:* alunos, fases concluídas, média por aluno, média de estrelas, taxa de acerto, progresso em
  Português e Matemática, situação dos alunos, ranking clicável e lista "Precisam de atenção".
- *Alunos:* busca, ficha com mapa das 90 fases de cada matéria (cor pelas estrelas), pontuação, tempo jogado,
  última atividade, parecer pedagógico e "Copiar relatório".
- Professor de sala vê só a sua sala; o acesso geral (`professor` / senha geral) vê todas e compara as salas.

**Servidor:** `Servicos/CalculoDesempenho.cs` centraliza os indicadores (painel web e jogo usam o mesmo cálculo);
`/api/jogo/salas-resumo` traz professor e última atividade; `/api/jogo/progresso-aluno/{id}` traz o detalhe fase
a fase; `/api/jogo/versao` = 1.0.3 (código 103) com o patch novo em `wwwroot/patch/`.

## Versão 1.0.4 — licença por escola

**Como funciona**
- **Computadores ao mesmo tempo:** cada computador tem uma identidade (do hardware) e avisa o servidor a cada
  minuto (`POST /api/jogo/maquina/sinal`). Conta como "em uso" quem mandou sinal nos últimos 3 minutos.
  Passou do limite da escola (padrão 100), o computador novo vê "Limite de computadores atingido" e tenta de novo
  a cada 30 s. Ao sair pelo botão do jogo, a vaga é liberada na hora (`/maquina/encerrar`).
  No painel: **Computadores** lista cada PC (em uso, parado, recusado por limite) e permite **bloquear** ou remover.
- **Alunos e professores:** `LimiteAlunos` e `LimiteProfessores` por escola (vazio = sem limite). Cadastro acima do
  limite é recusado (jogo e painel); no jogo, o aluno não é criado e aparece "X de Y vagas" na lista.
- **Vencimento:** usa a validade do contrato da prefeitura (vale até o fim do dia). Vencido — ou escola desativada —
  todas as rotas do jogo respondem 403 e o jogo mostra a tela de bloqueio. Renovando a data, volta sozinho.
- **Sem internet:** o jogo usa a última verificação por até **7 dias**; depois exige conexão.
- **Banco:** `Data/AtualizacaoEsquema.cs` cria as colunas/tabela novas em bancos que já existem (se falhar, o servidor
  não inicia e o Render mantém a versão anterior).
- **Segurança:** removida a rota `/api/jogo/escolas-disponiveis`, que devolvia o token de TODAS as escolas.

**Limitações conhecidas:** jogos com versão anterior à 1.0.4 não mandam sinal (não entram na contagem de
computadores) até receberem o patch; a regra roda no computador, então um jogo adulterado poderia ignorá-la
(o servidor continua recusando os dados de escolas vencidas).

## Versão 1.0.5 — aviso de atualização

**Como funciona (`Assets/TelaAtualizacao.cs` + `ServicoNuvem.cs`)**
- Ao abrir, o jogo consulta `/api/jogo/versao`. Havendo versão nova, aparece **"Nova versão disponível!"** com as
  novidades, a barra de progresso e o tamanho baixado (o jogo fica bloqueado atrás do aviso enquanto baixa).
- Terminado o download: **"Atualização pronta!"** com **Reiniciar agora** (fecha, instala e reabre sozinho em poucos
  segundos) ou **Continuar jogando** (instala quando sair pelo botão de sair do jogo).
- Se a atualização baixada não foi instalada (ex.: fechou pelo Alt+F4), na próxima abertura o aviso
  "pronta" aparece de novo, sem baixar outra vez.
- Falha no download ou arquivo incompleto (o tamanho é conferido): mensagem de erro e o jogo segue normalmente.
- O script de instalação (`atualizar_jogo.bat`) agora espera o jogo fechar de verdade (tenta por até ~30 s),
  é gravado sem BOM e usa o nome real do executável. No Editor da Unity a atualização automática fica desligada.
- **Atenção:** o aviso só aparece a partir da 1.0.5. Quem está na 1.0.4 recebe a 1.0.5 do jeito antigo (em silêncio,
  instalada ao sair pelo botão do jogo).

**Servidor:** `versao` = 1.0.5 (código 105) com o patch novo. **Removida a rota `POST /api/jogo/upload-patch`**,
que aceitava sem senha um arquivo que todos os computadores das escolas baixam e executam.

**Testes (build isolado + servidor local):** download com progresso → "pronta" → *Continuar jogando*; abertura
seguinte com o patch pendente → *Reiniciar agora* → jogo fechou, arquivo trocado (hash igual ao do servidor),
jogo reabriu sozinho já sem aviso; arquivo defeituoso → mensagem de erro.

## Versão 1.0.6 — versão na tela inicial

- O Lobby mostra **"Versão 1.0.6"** pequeno no canto inferior esquerdo (`GerenciadorPerfis.CriarTextoVersao`).
- O número vem de `ServicoNuvem.VersaoCodigoDoBuild` / `VersaoNomeDoBuild`, o mesmo usado pela atualização
  automática; ao publicar uma versão nova, basta mudar ali (e em `JogoController.ObterVersaoJogo`).
- Quem está na 1.0.5 recebe a 1.0.6 com o aviso de atualização.

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

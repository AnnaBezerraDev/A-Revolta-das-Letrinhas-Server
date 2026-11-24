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

## Versão 1.0.7 — fases mais fáceis para o 1º e 2º ano

**Como o jogo decide (`Assets/ConteudoAlfabetizacao.cs`)**
- Ao abrir cada fase, o jogo lê o ano no nome da sala do aluno que está jogando ("1º Ano A", "2°ANO B",
  "Ano 1", "2ºA", "Segundo ano"...). Ano 1 ou 2 → conteúdo de alfabetização. Ano 3 ou mais, ou sala sem
  número/desconhecida → conteúdo original. Trocar o aluno de sala vale a partir da fase seguinte.
- Só mudam as palavras e as continhas: telas, vidas, estrelas, progresso enviado ao servidor e troféus são os mesmos.

**Português (1º e 2º ano):** uma vogal faltando em palavra pequena; as 3 cartas são vogais.
Fácil = 3–4 letras (UVA, BOLA), Médio = 4–5 letras (LIVRO, ZEBRA), Difícil = 5–7 letras (PANELA, GIRAFA),
sem a etapa de classe gramatical. As vogais erradas nunca formam outra palavra comum (ex.: em S_NO não aparece
o O, que formaria SONO) nem palavrão.

**Matemática (1º e 2º ano):** somas com números de 1 algarismo. Fácil = parcelas de 1 a 5, Médio = resultado até 10,
Difícil = resultado acima de 10. Alternativas próximas da resposta e nunca negativas; explicação "comece pelo maior
e conte nos dedos".

**Gabarito do Modo Professor:** mostra a palavra, a vogal e "Conteúdo de 1º e 2º ano".

**Testes (build isolado + servidor local):** aluno de "1º Ano A" e de "3ºANO A", Português e Matemática nos 3 níveis;
acerto no Difícil do 1º ano conclui a fase direto; as 120 palavras geram desafio válido.

## Versão 1.0.8 — acesso do professor seguro e progresso que acompanha o aluno

**Fim do "professor / 1234"**
- Antes, o jogo aceitava "professor / 1234" em cada computador, e o servidor também, em qualquer escola.
  Com isso um aluno via o gabarito, usava "Auto-completar", liberava todas as fases e via os dados da escola inteira.
- Agora existe a **senha da coordenação** de cada escola: definida no painel de administração
  (Licença da escola → 🔑 Senha da coordenação), mínimo de 8 caracteres, guardada só como hash PBKDF2.
  No jogo: usuário `coordenacao` (o nome antigo `professor` também é aceito) com essa senha. Sem senha definida,
  o acesso da coordenação fica desligado. Professores de sala continuam com o login da sala.
- Senha errada demora meio segundo para responder e aparece no log do servidor.
- **Sem internet:** só entra quem já fez login com internet naquele computador (o jogo guarda um hash da senha,
  não a senha). Se a senha for trocada, a antiga deixa de valer assim que for testada com internet.
- **A sessão termina sozinha:** 5 minutos depois de fechar todas as telas do professor, ou na hora em que outro
  aluno é escolhido. O gabarito e as fases liberadas não ficam abertos para o aluno seguinte.
- **"Auto-completar" honesto:** a fase resolvida pelo professor é liberada naquele computador, mas não entra no
  desempenho do aluno no servidor (antes contava como acerto perfeito).

**Progresso que acompanha o aluno**
- Ao escolher o nome (e a cada sincronização), o jogo baixa do servidor as fases que o aluno já concluiu em qualquer
  computador da escola e marca no PC atual, com as vidas, os níveis liberados e os troféus. Só acrescenta: nada que
  foi feito no PC é desmarcado.

**Testes (servidor local + build isolado):** `professor/1234` recusado; coordenação sem senha → desligada; senha curta
ou óbvia recusada; banco guarda só o hash; senha certa/errada; professor de sala; troca de senha pelo painel (a antiga
para de valer); aluno com 35 fases feitas "em outro PC" → 30 fases de Português Fácil, Médio liberado e troféu no PC novo;
sessão encerrada por tempo e por troca de aluno; auto-completar não chegou ao servidor e a fase jogada chegou;
sem internet, só o login já usado no PC entra.

**Ao publicar:** depois do deploy do servidor, defina a senha da coordenação de cada escola no painel
(sem ela, o login `coordenacao` não funciona; os professores de sala não são afetados).

### 1.0.8 — novidades para professores e alunos (tudo em código: chega pela atualização automática)

- **Resgate das Letrinhas (recompensa):** cada fase vale 1 a 3 estrelas (pelas vidas que sobraram); as estrelas
  resgatam as 26 letrinhas que fugiram ("A de ABELHA"...) para o **Álbum das Letrinhas** (botão no Lobby).
  Aviso "+N estrelas" ao concluir a fase e tela especial a cada letrinha resgatada. As estrelas saem do progresso
  (que vai ao servidor), então o álbum acompanha o aluno; repetir fase não dá estrela a mais; fase resolvida pelo
  professor não dá estrelas. (`ResgateLetrinhas.cs`)
- **Várias perguntas por fase (contra o "chute"):** 3 perguntas (2 no 1º/2º ano e no Português Difícil, que já tem
  2 etapas), com as mesmas 2 vidas para a fase inteira. O indicador mostra "PERGUNTA 2/3".
- **Nível pela série:** 4º ano começa com o Médio liberado; 5º ano com Médio e Difícil.
- **2º ano separado do 1º:** Português Fácil = vogal (palavras maiores), Médio = sílaba que falta (BO__ → LA),
  Difícil = consoante que falta, com as trocas típicas (B/P, D/T, F/V, M/N, L/R). Matemática Fácil = somas e
  subtrações até 10, Médio = dezenas sem "vai um", Difícil = dobro, metade e probleminhas. 1º ano continua igual.
  Opções erradas nunca formam outra palavra conhecida nem palavrão (listas revisadas).
- **5º ano:** metade das perguntas do Difícil viram **leitura** (30 textos curtos com pergunta) e **problemas**
  (frações, porcentagem, dinheiro com centavos e duas etapas). (`ConteudoQuintoAno.cs`)
- **Ouvir:** botão "OUVIR" na fase lê a pergunta com a voz em português do Windows ("Microsoft Maria");
  no 1º e 2º ano a leitura é automática. Sem voz em português, o botão some. (`LeitorDeVoz.cs`)
- **Dica depois do erro:** explica o caminho sem dar a resposta; aparece numa faixa na parte de baixo da tela,
  longe dos personagens. (`ApoioGameplay.cs`)
- **Pontuação:** a fonte de giz não tinha ponto, vírgula nem "?" (ex.: "R$ 3,00" saía "3 00"); agora usa uma fonte
  reserva só para esses sinais.
- Tela de explicação e gabarito do professor adaptados ao 1º/2º ano e à leitura do 5º ano.

### 1.0.8 — desenhos, personagens de perfil e relatório por habilidade da BNCC

- **Imagens:** Noto Emoji do Google, versão **v2.042** (a última com as imagens sob **licença Apache 2.0**; as versões
  novas do repositório passaram para a OFL 1.1). Ficam no servidor em `wwwroot/imagens` com `LICENSE-noto-emoji.txt`
  e `LEIA-ME.txt`; o jogo baixa cada imagem uma vez e guarda no PC (`ImagensRemotas.cs`) — chega pela atualização
  automática, sem reinstalar. 117 desenhos de palavras + 518 personagens (~5 MB).
- **Desenho da palavra:** **dentro** do balão da pergunta, à direita do texto (ex.: vela em "VEL_", coruja em "CORU__"),
  e nas letrinhas do álbum. O texto do balão abre espaço para o desenho, então nada cobre os personagens da fase.
- **Personagens de perfil (`TelaPersonagens.cs`):** galeria com 518 personagens (animais, heróis e magia, profissões
  e esportes em 6 tons de pele, divertidos). Com **2 troféus** (2 níveis inteiros) o aluno resgata **um** personagem;
  cada personagem tem **um único dono por escola** (índice único no banco); ao excluir o aluno, o personagem fica livre.
  Vira a foto do aluno no crachá do Lobby e na lista "Quem vai jogar hoje?". Personagens de marcas (Disney, Pokémon...)
  não podem ser usados sem licença. Servidor: `GET /api/jogo/personagens`, `POST /api/jogo/personagens/resgatar`
  (confere troféus, dono e catálogo).
- **Habilidades da BNCC:** cada pergunta tem o código da habilidade (texto oficial do MEC em `Servicos/HabilidadesBncc.cs`);
  o jogo envia acertos/erros por habilidade ao fim de cada fase (concluída ou não) pela fila. Relatórios:
  aba **Habilidades BNCC** na Área do Professor (sala ou escola), botão **📊 Habilidades BNCC** no painel web
  (escola e por sala) e **CSV**. "Precisam de atenção": 4+ respostas e menos de 60% de acerto.
  Sem habilidade própria até o 5º ano na BNCC: homófonos (→ EF35LP13/EF05LP01), expressões com parênteses
  (→ EF04MA05) e advérbio (→ EF07LP09, antecipada) — aparece explicado no relatório.

**Testes (servidor local + build isolado):** imagens e licença servidas; resgate sem troféus recusado; com 2 troféus
resgatou; mesmo personagem para outra criança recusado; segundo personagem recusado; aluno excluído libera o personagem;
tabelas novas criadas em banco existente; habilidades registradas (código inválido ignorado); relatório da escola/sala,
CSV e aba do professor com os números certos; foto do personagem no crachá e na lista; álbum com desenhos; galeria
com "É SEU!" e "Tem dono".

**Testes (build isolado + servidor local, alunos de 1º a 5º ano):** desbloqueio por série; 2º ano (sílaba, consoante,
contas); 5º ano (≈50% leitura/problemas, resposta com centavos aceita); 3 perguntas com "PERGUNTA 2/3"; dica após erro;
+2 estrelas com um erro; letrinhas A e B resgatadas e aviso "faltam 3 para a C"; auto-completar sem estrelas; estrelas
voltando do servidor num PC "novo"; voz em português ativa e o processo de voz fechando com o jogo.

### 1.0.8 — salas cadastradas só pela administração

- **Servidor:** o jogo não cria mais salas. `POST /api/jogo/criar-turma` só devolve uma sala que já existe; sala nova
  responde **409** com `codigo: "sala_inexistente"`. Cadastrar ou mudar aluno para uma sala que não existe também
  responde 409 (antes a sala era criada sozinha). Salas novas: painel da escola (`POST /api/admin/turmas`).
- **Jogo:** o botão "+ CRIAR SALA" saiu da janela de salas; fica só o filtro/escolha de sala existente, com o aviso
  "As salas são cadastradas pela administração da escola, no painel.". Pedidos antigos de "criar sala" que estavam
  na fila do PC são descartados; aluno recusado por sala inexistente mostra o motivo.

**Testes (servidor local + build isolado):** sala inventada pelo jogo → 409 e não aparece no banco; sala existente → 200;
aluno em sala inexistente → 409; mudar aluno para sala inexistente → 409; admin cria sala → 200; janela de salas sem
"criar sala". Fotos das 6 telas (Português e Matemática, 3 níveis, com dica): desenho dentro do balão, dica embaixo,
nenhum personagem coberto.

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

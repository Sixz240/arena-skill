# Architecture: jogo de futebol 3D (uma recriação dos recursos do FIFA 14 mobile)

Fontes: `replica/recon.md` (telas S01-S17, fluxos F01-F06) e `replica/features.csv`.
Jogo offline para um jogador. **Não há servidor, login, pagamentos nem e-mail**: o template web do pack não se aplica a essas camadas.

## Stack

| layer | choice | why |
| --- | --- | --- |
| motor | Unity 6 LTS, render pipeline URP | escolhido pelo usuário; URP é o pipeline feito para celular |
| linguagem | C# | padrão do Unity |
| plataforma | Android, IL2CPP, **ARM64 apenas** | é o que gera o APK/AAB de 64 bits (o motivo do projeto) |
| controles | Unity Input System (touch + gestos) | toque, arrasto e deslize com pressão/velocidade; botões clássicos como alternativa |
| física | física do Unity (PhysX) só para a bola; jogadores por movimento cinemático | física completa nos 22 jogadores é cara e difícil de controlar |
| animação | Mecanim (Animator + Blend Trees), Humanoid rig | um rig para todos os jogadores; animações de licença livre (ex.: Mixamo) ou próprias |
| modelos 3D | um corpo low-poly humanoide, 2-3 tipos físicos, cabelos e tons de pele variáveis | estilo cartoon, sem rostos reais; 22 em campo a 30+ fps |
| uniformes | textura gerada em runtime (shader com máscaras de padrão + cores do `kits`) | ~750 uniformes sem fazer 750 texturas |
| UI | Unity UI Toolkit | menus, tabelas e listas grandes (elencos) |
| dados | SQLite no aparelho (`data.db` + `save.db`), via sqlite-net | 6.000+ jogadores, filtros, editor; consultas rápidas |
| escudos | MediaWiki API pública (`pageimages`) + cache em disco | escudos atuais, com fallback para bandeira/cores |
| elencos | MediaWiki API pública (seção "Current squad") + Editor | nomes reais atualizáveis; uso pessoal |
| áudio | sons próprios / CC0 | narração e músicas do original são licenciadas |
| build | Unity Cloud Build ou build local; Gradle do Unity | gera APK para instalar e AAB se um dia for para loja |

## Schema

Tables: 13 (4 em `data.db`, 9 em `save.db`). Access rules: um só usuário offline, sem RLS; todo acesso passa pela camada de dados em C# (`TeamRepository`, `CareerRepository`...). Ver `replica/schema.sql` (validado no SQLite).

- `data.db`: `leagues`, `teams`, `kits`, `players`.
- `save.db`: `competitions`, `competition_teams`, `matches`, `match_events`, `standings`, `careers`, `career_players`, `transfers`, `settings`.

Restrições que a recon pediu:
- `players_team_number` único: dois jogadores do mesmo time não têm o mesmo número.
- `matches_unique_fixture` e `check (home_team_id <> away_team_id)`: nenhum jogo duplicado ou do time contra ele mesmo.
- `players.edited_by_user`: a atualização de elencos não sobrescreve o que o usuário editou.
- `career_players`: a carreira tem cópia própria do estado dos jogadores, então atualizar elencos não quebra um save em andamento.

## API

Não há API web. Cada fluxo vira um conjunto de serviços em C#:

| serviço.método | does | who | input | output | flow |
| --- | --- | --- | --- | --- | --- |
| `TeamCatalog.Search(liga, texto)` | lista times com escudo e força | S03 | filtro | `Team[]` | F01, F03-F05 |
| `MatchSetup.Create(casa, fora, regras)` | prepara a partida e resolve conflito de uniforme | S04, S05 | times, tempo, dificuldade | `MatchConfig` | F01 |
| `MatchEngine.Run(config)` | roda a partida 3D | S06, S07 | `MatchConfig` | `MatchResult` | F01, F02 |
| `MatchSimulator.Simulate(config)` | resultado sem jogar (rodadas dos outros times) | S10 | `MatchConfig` | `MatchResult` | F03-F05 |
| `LeagueService.Create / PlayRound / Table` | cria liga, gera rodadas, atualiza tabela | S09, S10 | liga, time do usuário | `Competition`, `Standing[]` | F03 |
| `CupService.Create / Advance` | chaveamento e mata-mata | S11 | times, formato | `Bracket` | F04 |
| `CareerService.Start / NextWeek / EndSeason` | calendário, moral, evolução, aposentadorias | S12, S13, S15 | clube | `Career` | F05 |
| `TransferService.Offer / Respond` | compra e venda | S14 | jogador, valor | `Transfer` | F05 |
| `EditorService.SaveTeam / SavePlayer` | edição manual | S16 | formulário | registro salvo | F06 |
| `CrestService.Get(time)` | escudo do cache ou da Wikipédia, com filtro e fallback | S03, S06, S08-S11 | `Team` | `Texture2D` | todos |
| `SquadUpdater.Update(liga)` | atualiza elencos pela Wikipédia | S16, S17 | liga | relatório de mudanças | F06 |
| `SaveService.Save / Load` | salva e retoma | S02 | — | — | F03-F05 |

Webhooks in: nenhum. Webhooks out: nenhum.
Chamadas externas (só APIs públicas, sem chave): MediaWiki API da Wikipédia (`en.wikipedia.org/w/api.php`, `prop=pageimages` e `action=parse`), com `User-Agent` identificado e no máximo 1 pedido por vez.
Jobs: nenhum agendado. `SquadUpdater` roda quando o usuário pede (S16/S17). Baixar escudos roda em segundo plano na primeira abertura (S01), com fila e cache.

## The parts that bite

- **Desempenho:** 22 jogadores animados + estádio + UI a 30 fps num celular médio. LOD nos modelos, GPU instancing, animação com culling, um único material com textura de uniforme gerada, 30 fps travado por padrão.
- **IA da partida:** é o maior risco do projeto. Começar com IA simples por posição (manter formação, pressionar quem está com a bola, se oferecer para passe) e melhorar depois.
- **Controle por gesto:** diferenciar toque, arrasto e deslize com velocidade sem atraso; testar em telas pequenas.
- **Física da bola:** chute com efeito, quique, rolamento; passo de física fixo para o resultado não depender do fps.
- **Determinismo do simulador:** usar semente aleatória salva, para uma rodada simulada não mudar ao recarregar o save.
- **Ciclo de vida do Android:** o app vai para segundo plano no meio da partida; pausar e salvar o estado.
- **Escudos:** a imagem principal da página pode não ser o escudo; filtrar pelo nome do arquivo e cair para bandeira/cores. Respeitar limites da API (fila, cache, sem rajadas).
- **Elencos:** o formato da seção "Current squad" muda entre páginas; o parser falha em alguns times, e o editor cobre isso.
- **Tamanho do APK:** modelos e animações compactados; escudos ficam fora do APK (baixados e cacheados).
- **Offline:** sem internet, o jogo funciona com escudo trocado por cores e elencos da última versão.
- **64 bits:** marcar só ARM64 nas Player Settings; plugins nativos (SQLite) precisam da versão arm64-v8a.
- **Uso pessoal:** escudos e nomes reais ficam atrás de um ajuste "dados reais"; uma versão publicável roda com dados fictícios.

## Build order

1. **Vertical slice (a partida):** S06 com 2 times fixos, um estádio, câmera de TV, mover/passar/chutar por gesto, gol e placar, S08 simples. Tabelas: nenhuma (dados fixos no código). Serviços: `MatchEngine`. Prova que o Unity entrega 3D jogável a 30 fps no seu celular, em APK 64 bits.
2. **Must-haves, por área:**
   - **partida:** IA de time, defesa por toque/deslize, regras (lateral, escanteio, tiro de meta, faltas, cartões), pênaltis/prorrogação, pausa com substituições. Serviços: `MatchEngine`, `MatchSimulator`.
   - **dados e visual:** `data.db` com Brasileirão, `CrestService`, uniformes por textura; depois europeias, sul-americanos e seleções. Telas S01, S03, S05. Tabelas: `leagues`, `teams`, `kits`, `players`.
   - **modos:** Amistoso (F01, S02-S08), Liga (F03, S09-S10), Copa (F04, S11), Carreira (F05, S12-S15), salvar/continuar. Tabelas do `save.db`.
3. **Should-haves:** controle clássico com botões, impedimento, estatísticas, dificuldade, editor (S16), atualizar elencos, escolha de uniforme com aviso de conflito, configurações (S17), modo offline.
4. **Could-haves:** replay dos gols, lesões/moral na carreira, nomes reais dos estádios.
5. **Fixes do replica-entrepreneur:** depois que ele rodar.

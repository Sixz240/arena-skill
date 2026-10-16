# Recon map: FIFA 14 mobile (Android)

Scope: jogo de futebol para Android inspirado no FIFA 14 mobile. Partida com controle por toque, mais os modos Amistoso, Campeonato/Liga, Copa mata-mata e Carreira. Times: Brasileirão Série A, as 5 grandes ligas europeias, seleções e clubes sul-americanos. Escudos e cores de uniforme atuais. Nomes reais dos jogadores.
For: uso pessoal do usuário (não publicar nem vender; ver "Out of scope").
Date: 2026-10-16

## Sources

| # | source | URL | notes |
| --- | --- | --- | --- |
| 1 | reportagem de lançamento | https://www.techradar.com/news/gaming/android-finally-gets-some-love-as-fifa-14-hits-the-mobile-app-stores-1183697 | lançamento no Android, free-to-play |
| 2 | review iPad (Digit) | https://www.digit.in/reviews/general/fifa-14-for-ipad-review-3191.html | controle por toque em detalhe: arrastar, tocar para passar, deslizar para chutar |
| 3 | MobyGames | https://www.mobygames.com/game/android/fifa-14___?s=platform | só Ultimate Team liberado; outros modos pagos; toque/deslize na defesa |
| 4 | EA, "FIFA 14 makes moves on mobile" | https://www.ea.com/en-au/games/fifa/news/fifa-14-makes-moves-on-mobile | descrição oficial dos modos Manager, Tournament, Kick Off |
| 5 | Pocket Gamer | https://www.pocketgamer.co.uk/r/iPhone/FIFA+14/news.asp?c=51895 | números: 33/34 ligas, 600+ times, 16.000+ jogadores, 34 estádios |
| 6 | EFTM preview | https://eftm.com/2013/09/fifa-14-mobile-is-almost-here-eftm-preview-12494 | prévia dos controles e modos |
| 7 | Wikipedia | https://en.wikipedia.org/wiki/FIFA_14 | data de lançamento (set/2013), primeiro FIFA mobile free-to-play |
| 8 | conta do próprio usuário | n/a | **não disponível**: o app é 32 bits e não instala no aparelho do usuário. Faltam prints. |
| 9 | gameplays públicos (YouTube) | a coletar | melhor fonte para telas e fluxos clique a clique; ainda não vistos |

Limitação desta recon: os sites de review não abriram neste ambiente; os fatos vêm dos resumos de busca dessas páginas. Nenhuma tela foi vista diretamente, então a lista de telas abaixo é inferida e tem confiança média. Prints de gameplays em `replica/screens/` vão confirmar ou corrigir.

## Core loop

Escolher um time e jogar uma partida de futebol controlando os jogadores pelo toque (arrastar para mover, tocar para passar, deslizar para chutar).

## Screens

| ID | screen | route / how to reach | purpose | key components | states seen |
| --- | --- | --- | --- | --- | --- |
| S01 | Abertura / carregamento | ao abrir o app | carregar dados e escudos | logo próprio, barra de progresso | carregando, sem internet (escudos caem para cores) |
| S02 | Menu principal | depois de S01 | escolher modo | cards de modo (Amistoso, Liga, Copa, Carreira), Configurações, Editor | padrão |
| S03 | Escolha de times | Amistoso / Copa / Carreira | escolher time da casa e visitante | filtro por liga, grade de escudos, estrelas de força | lista, busca vazia |
| S04 | Escalação e tática | antes da partida e em pausa | titulares, reservas, formação | campo com 11 jogadores, lista do elenco, seletor de formação (4-4-2, 4-3-3...) | padrão, jogador lesionado/suspenso (carreira) |
| S05 | Escolha de uniforme | antes da partida | uniforme 1 ou 2, evitar cores iguais | prévia das camisas | conflito de cores |
| S06 | Partida | depois de S03-S05 | jogar | campo, jogadores, bola, placar, relógio, minimapa, botões (modo clássico) | 1º/2º tempo, prorrogação, pênaltis, pausado |
| S07 | Pausa | botão de pausa em S06 | substituições, tática, sair | menu sobreposto | padrão |
| S08 | Fim de jogo / estatísticas | apito final | resultado | placar, gols, posse, chutes, cartões, melhor jogador | vitória, empate, derrota |
| S09 | Campeonato: tabela | modo Liga | acompanhar temporada | tabela de classificação, próxima rodada, artilharia | início, meio, campeão |
| S10 | Campeonato: rodada | modo Liga | jogar ou simular a rodada | lista de jogos, botão "jogar" / "simular" | jogo do usuário, outros jogos simulados |
| S11 | Copa: chaveamento | modo Copa | mata-mata | chave (oitavas → final), grupos opcionais | em andamento, eliminado, campeão |
| S12 | Carreira: painel | modo Carreira | central do clube | próximo jogo, calendário, objetivos, finanças | início de temporada, janela aberta/fechada |
| S13 | Carreira: elenco | painel → Elenco | ver jogadores e evolução | lista com posição, idade, nota, moral, contrato | padrão, lesionado |
| S14 | Carreira: transferências | painel → Mercado | comprar e vender | busca de jogador, oferta, negociação | janela fechada, sem dinheiro, oferta recusada |
| S15 | Carreira: fim de temporada | após a última rodada | balanço | títulos, evolução dos jogadores, aposentadorias | campeão, rebaixado |
| S16 | Editor de times e jogadores | Menu → Editor | corrigir nomes, notas, escudo | formulário, importar/exportar | padrão |
| S17 | Configurações | Menu → Config | controles, dificuldade, tempo de jogo, som, idioma | seletores | padrão |

## Flows

```
F01 Jogar um amistoso
    S02 -> S03 -> S04 -> S05 -> S06 -> S08
    happy path clicks: 5 (Amistoso, time 1, time 2, confirmar, jogar)
    edge: uniformes da mesma cor, escudo não carrega, app vai para segundo plano no meio do jogo

F02 Jogar uma partida (dentro de S06)
    saída de bola -> arrastar para mover -> tocar no companheiro para passar -> deslizar para o gol para chutar (força = velocidade do deslize)
    defesa: tocar para pressionar, deslizar curto para dar carrinho
    edge: gol contra, impedimento, falta e cartão, pênalti, bola fora (lateral, escanteio, tiro de meta), empate no mata-mata -> prorrogação -> pênaltis

F03 Disputar um campeonato
    S02 -> escolher liga -> S03 -> S09 -> S10 -> (S04 -> S06 -> S08) x 38 rodadas -> S09 campeão
    edge: simular jogos que o usuário não quer jogar, salvar e voltar depois, desempate na tabela

F04 Disputar uma copa
    S02 -> S03 -> S11 -> (S06 -> S08) por fase -> S11 campeão ou eliminado
    edge: empate -> prorrogação/pênaltis, eliminado antes da final

F05 Temporada de carreira
    S02 -> S03 (escolher clube) -> S12 -> S13/S14 (montar elenco) -> F03 -> S15 -> nova temporada
    edge: janela de transferências fechada, sem orçamento, jogador lesionado, demissão por maus resultados

F06 Corrigir um jogador ou escudo
    S02 -> S16 -> buscar -> editar -> salvar
    edge: escudo errado da Wikipédia, jogador que trocou de clube
```

## Components

| component | variants | states | used on |
| --- | --- | --- | --- |
| Botão | primário, secundário, ícone | normal, pressionado, desativado | todas |
| Card de modo | 4 modos | normal, bloqueado (sem save) | S02 |
| Escudo | grande, médio, mini | carregando, escudo, fallback em cores | S03, S06, S08-S11 |
| Camisa | lisa, listrada vertical, listrada horizontal, faixa, metade | uniforme 1, 2, goleiro | S05, S06 |
| Tabela de classificação | completa, resumida | zona de título, rebaixamento | S09 |
| Chaveamento | 8, 16, 32 times | em andamento, concluído | S11 |
| Placar e relógio | partida, fim de jogo | 1º/2º tempo, acréscimos, prorrogação, pênaltis | S06, S08 |
| Linha de jogador | elenco, mercado | normal, lesionado, suspenso, selecionado | S04, S13, S14 |
| Campo de escalação | formações | arrastar para trocar | S04, S07 |
| Controles da partida | toque/gesto, botões clássicos (direcional + passe/chute/drible) | ataque, defesa | S06 |
| Modal / sobreposição | pausa, confirmação | aberto | S07, S14 |

## Inferred data model

```
League      id, name, country, season, team_ids, format (pontos corridos | mata-mata)
            evidence: fonte 5 (33 ligas), escopo do usuário
            confidence: high

Team        id, name, short_name, league_id, crest_source (página da Wikipédia), crest_url (cache),
            kit_home {pattern, primary, secondary, shorts, socks}, kit_away {...}, kit_gk {...},
            rating_attack, rating_mid, rating_defense, stadium_name
            evidence: escopo do usuário (escudos e uniformes atuais)
            confidence: high

Player      id, team_id, name, shirt_number, position, age, nationality,
            attributes {pace, shooting, passing, dribbling, defending, physical}, overall
            evidence: fonte 5 (16.000+ jogadores), escopo do usuário (nomes reais)
            confidence: high para os campos; os atributos são inventados (não copiar notas da EA)

Match       id, home_team_id, away_team_id, competition_id, round, score_home, score_away,
            events [{minute, type: gol|cartão|substituição, player_id}], stats {posse, chutes, ...}
            evidence: S08
            confidence: high

Competition id, type (liga | copa | carreira), team_ids, fixtures, table, bracket, current_round
            evidence: modos (fontes 3, 4)
            confidence: high

Career      id, team_id, season, budget, objectives, squad_state {player_id: moral, forma, lesão, contrato},
            transfer_window_open
            evidence: fonte 4 ("develop players and choose the best tactics")
            confidence: medium (o detalhe da carreira do FIFA 14 mobile não foi confirmado)

Settings    controls (gestos | botões), difficulty, half_length_min, sound, language
            evidence: fontes 2, 3 (controle clássico opcional)
            confidence: high
```

Relationships: League 1-n Team, Team 1-n Player, Competition n-n Team, Competition 1-n Match, Career 1-1 Team.

## Feature matrix

See `features.csv`. Must: 23, should: 10, could: 4, skip: 7. Três linhas (Brasileirão, editor, APK 64 bits) não estavam no original e não contam na nota de paridade.

## Out of scope (cannot or should not be cloned)

- **Código, gráficos, sons, narração e arquivos do FIFA 14 (APK/OBB).** Nada é extraído do jogo original; tudo é feito do zero.
- **Marca FIFA / EA SPORTS** e o nome do jogo. O app precisa de nome próprio.
- **Notas e atributos dos jogadores da EA.** São dados da EA; as notas do clone são próprias.
- **Ultimate Team:** depende de servidores, mercado online e compras. Fora do escopo pedido.
- **Narração licenciada e músicas licenciadas da trilha.**
- **Rostos e modelos 3D dos jogadores reais.**
- **Escudos, uniformes e nomes reais em versão publicada:** para uso pessoal, o app busca os escudos na Wikipédia e mostra nomes reais. Para publicar ou vender, isso precisa sair (são marcas dos clubes e direitos de imagem dos atletas) e o editor (S16) vira o caminho.
- **Conversão do FIFA 14 original para 64 bits:** impossível sem o código-fonte da EA; não é o que este projeto faz.

## Size

Screens 17, flows 6, entities 7.
Hard parts:
1. **Motor da partida:** física da bola, IA dos 21 outros jogadores, regras (impedimento, faltas, bola parada), controle por gesto que pareça bom. Em 3D como o original é trabalho de estúdio.
2. **Dados:** cerca de 250 times e 6.000+ jogadores com nomes reais atualizados. Elencos mudam toda janela; precisa de uma fonte atualizável (Wikipédia, seções "Current squad") mais o editor.
3. **Escudos e uniformes atuais:** buscar ~250 escudos com fallback, e descrever as cores/padrões de ~750 uniformes.
4. **Modo Carreira:** mercado, evolução, finanças, IA dos outros clubes.
5. **Empacotar como APK 64 bits.**

Size: **XL** como pedido (partida 3D igual ao FIFA 14). Proposta de reescopo para **L**: partida em 2D com câmera de cima (estilo clássico, tipo Sensible Soccer / Dream League antigo), feita em tecnologia web e empacotada como APK (Capacitor), que já sai em 64 bits. Começar só com Amistoso + Brasileirão, depois somar ligas e modos.

# Build log

Uma linha por tela/etapa: ID, data, estado, o que falta, o que foi mais difícil que o esperado.

| ID | data | estado | falta | mais difícil que o esperado |
| --- | --- | --- | --- | --- |
| Motor (Core) | 2026-10-16 | done | faltas, cartões, impedimento, pênaltis, substituições | A IA: na primeira versão os times trocavam a bola sem parar e nunca chutavam (0 chutes em 200 jogos). Precisou de marcação que acompanha em vez de grudar, espera entre desarmes, passe em profundidade e corrida dos atacantes. Hoje: 3,1 gols, 11 chutes e 250 passes por jogo; time forte vence 62 x 16. |
| S06 Partida | 2026-10-16 | partial | modelos semi-realistas, animações, número na camisa, sons, replays, controle clássico | **Não rodado no Unity**: o ambiente não tem Unity. O código usa APIs padrão do Unity 6 / URP / Input System, mas pode precisar de ajustes na primeira abertura. |
| S07 Pausa | 2026-10-16 | partial | substituições, tática | — |
| S08 Fim de jogo | 2026-10-16 | done | — | — |
| S01-S05, S09-S17 | — | not started | menus, escolha de times, liga, copa, carreira, editor, configurações | — |

Notas:
- O HUD da fatia usa IMGUI com os tokens (Tokens.cs gerado de tokens.json). Os menus vão para UI Toolkit, que precisa de um Theme criado no editor.
- Sem prints em `replica/clone-screens/`: não dá para tirar print do Unity aqui. Tirar no editor depois do primeiro Play.

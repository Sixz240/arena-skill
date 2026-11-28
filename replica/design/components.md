# Componentes e direção visual

Tokens: `tokens.json` (gerado para Unity em `tokens.uss`). Contraste: 22 pares, 0 falhas AA.
Uniformes: `../data/kits-brasileirao-2026.json` (40 pares número/camisa, 0 falhas).
Telas: IDs da recon (S01-S17). Tudo em paisagem.

## Sobre "menu igual ao do FIFA 14 mobile"

O que dá para recriar é o **sistema**: a estrutura do menu, a hierarquia e o jeito de navegar. Isso não pertence a ninguém, e é o que o jogador reconhece.

- **Estrutura recriada:** barra superior fina (perfil, moedas/temporada, configurações), grade de blocos (tiles) com um bloco grande em destaque, blocos com o nome do modo e uma imagem de fundo, rodapé com voltar/avançar, abas no topo para trocar de seção.
- **Feito do zero:** ícones (Phosphor ou Lucide, licença MIT), fontes abertas (Barlow / Barlow Condensed, SIL OFL), todos os textos, imagens de fundo dos blocos (renderizadas com os nossos próprios jogadores 3D), sons de menu.
- **Nunca usar:** logo FIFA/EA, ícones, imagens, fontes ou sons extraídos do jogo, e a cor-assinatura da EA. `accent` é um azul neutro provisório; o `/replica-brand` troca pela cor do seu jogo.
- **Falta medir:** não há prints em `replica/screens/`. Os tamanhos abaixo são provisórios. Com prints dos menus do FIFA 14 mobile (os seus ou de gameplays no YouTube) eu meço proporções, espaçamentos e tamanhos de texto e ajusto os tokens.

## Componentes

```
Tile (bloco do menu)
  variants  hero (2x2), normal (1x1), wide (2x1)
  sizes     normal 296x200 na resolução de referência; gap 12 (space-3)
  states    normal, pressed (escala 0.97, tile-press 80ms), focused (borda 3px focus), locked (cadeado + text-muted), new (selo "NOVO")
  tokens    bg tile, texto text, título display/xl, legenda sm text-muted, radius md, shadow tile
  a11y      alvo mínimo 48px; foco navegável por controle/teclado; leitor de tela lê "Modo Carreira, botão"
  used on   S02

TopBar
  variants  menu, partida (oculta)
  states    padrão, sem save (esconde temporada)
  tokens    bg surface, altura 64, ícones 24 text-muted, título lg
  used on   S02-S05, S09-S17

Button
  variants  primary, secondary, ghost, danger
  sizes     md 48, lg 56 (alvo de toque)
  states    default, pressed, focus (anel 3px focus), disabled (40% opacidade), loading (spinner, mantém o rótulo)
  tokens    primary: bg accent, text on-accent; secondary: bg surface-2, text text; radius md; font base/600
  used on   todas

Crest (escudo)
  variants  lg 128, md 64, sm 32, mini 20 (placar)
  states    loading (silhueta de escudo em surface-2), crest (imagem da Wikipédia), flag (bandeira, seleções), colors (fallback: escudo genérico com as cores do uniforme 1 e a sigla)
  regras    nunca esticar: encaixar mantendo proporção; fundo transparente; cache em disco
  used on   S03, S06, S08-S11, S13

Shirt (camisa, UI e 3D)
  variants  pattern: plain, v_stripes, h_hoops, sash, halves, chest_band, tricolor_stripes, sleeves (mangas de outra cor: Arsenal, Aston Villa)
  slots     home, away, gk
  regras    número nas costas sempre sobre um painel liso na cor 'primary' (listras não atrapalham a leitura); conflito de cor = diferença < 25 em deltaE entre camisas -> trocar para away
  tokens    cores de kits-brasileirao-2026.json
  used on   S03, S05, S06

TeamCard (escolha de time)
  layout    escudo md, nome lg, liga sm text-muted, 5 estrelas de força, mini camisa
  states    normal, selected (borda accent 3px), disabled (time já escolhido pelo outro lado)
  used on   S03

Scoreboard (placar da partida)
  layout    canto superior esquerdo: sigla casa | gols | gols | sigla fora | relógio; mini escudos
  states    1º/2º tempo, acréscimos (+3), prorrogação (badge "PRORR."), pênaltis (bolinhas de cobrança)
  tokens    bg score-bg 85% opacidade, text score-text, números font numbers/score
  used on   S06, S08

TouchControls (partida)
  variants  gestos (padrão), clássico (direcional virtual à esquerda + botões Passe / Chute / Lançamento / Drible à direita)
  gestos    arrastar a partir do jogador = correr; tocar no companheiro = passe; deslizar em direção ao gol = chute (força = velocidade do deslize, mostrada por um rastro); defesa: tocar no adversário = pressionar, deslize curto = carrinho
  states    ataque, defesa, bola parada (seta de direção + barra de força)
  tokens    rastro do gesto accent 60%, botões surface 70% opacidade, raio pill
  used on   S06

StandingsTable
  colunas   pos, escudo sm, time, J, V, E, D, SG, Pts
  states    zona Libertadores (faixa success à esquerda), rebaixamento (faixa danger), time do usuário (linha surface-2 + negrito)
  regras    números alinhados à direita com algarismos tabulares
  used on   S09

Bracket (chaveamento)
  variants  8, 16, 32 times
  states    a jogar, jogado (vencedor em text, perdedor em text-muted), time do usuário (accent)
  used on   S11

PlayerRow (elenco e mercado)
  layout    posição (badge), nome, idade, nota geral (círculo), moral (ícone), valor
  states    normal, lesionado (ícone cruz danger), suspenso (cartão), selecionado
  used on   S04, S13, S14

FormationPitch (escalação)
  layout    campo vertical pitch/pitch-line, 11 fichas com número e nome
  interação arrastar ficha sobre ficha = trocar; tocar no banco + tocar na ficha = substituir
  used on   S04, S07

Modal / Overlay
  variants  pausa, confirmação, aviso (sem internet)
  states    aberto, fechando
  tokens    fundo bg 80%, painel surface, shadow pop, radius lg
  a11y      foco preso no modal; botão voltar do Android fecha
  used on   S07, S14

Toast
  variants  info, sucesso, erro
  uso       "Elencos atualizados: 38 mudanças", "Sem internet: escudos em cores"
```

## Estilo dos jogadores: semi-realista

- **Proporção:** humana real (7,5 cabeças de altura), sem cabeçudo de desenho animado.
- **Corpo:** um modelo base humanoide (ex.: MakeHuman, que exporta em CC0), 3 tipos físicos (magro, médio, forte), 6 tons de pele, 10 cortes de cabelo, 8 rostos **genéricos**. Nenhum rosto modelado a partir de jogador real.
- **Materiais:** PBR simples no URP (albedo + normal + máscara), camisa com textura gerada pelo shader de padrões, leve brilho de suor desligado em aparelhos fracos.
- **Animação:** Humanoid do Mecanim, animações de licença livre (Mixamo) ou próprias: parado, trote, corrida, sprint, passe curto/longo, chute, carrinho, cabeceio, domínio, goleiro (pegar, espalmar, saída), comemorações. Blend Trees para curvas e mudança de velocidade.
- **Câmera de TV:** lateral, altura ~18 m, segue a bola com atraso suave; câmeras de replay atrás do gol.
- **Desempenho:** LOD0 ~8k triângulos (perto da câmera), LOD1 ~3k, LOD2 ~1k; texturas 512 px; 30 fps travado; opção "gráficos baixo/médio/alto" nas configurações (S17).
- **Estádio:** genérico, feito do zero, com torcida em cartões (sprites) e cores da torcida da casa.

## Primitivos

Não dá para montar e testar o Unity neste ambiente. O primeiro passo do `/replica-build` é uma cena `DesignGallery` com todos os componentes acima, em todos os estados, usando `tokens.uss`. Um print dessa cena é a checagem do design.

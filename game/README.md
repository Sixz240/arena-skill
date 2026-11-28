# Futebol (projeto Unity)

Jogo de futebol 3D para Android 64 bits. Plano em `../replica/` (recon, arquitetura, design).

## Montar no seu PC

1. Instale o **Unity Hub** e o **Unity 6 LTS** com o módulo **Android Build Support** (com OpenJDK e Android SDK/NDK).
2. No Unity Hub: **New project → Universal 3D (URP)**. Dê o nome `Futebol` e crie.
3. Feche o Unity. Copie a pasta `game/Assets/Game` deste repositório para dentro de `Assets/` do projeto novo (fica `Assets/Game/...`).
4. Abra o projeto. Confira em **Edit → Project Settings → Player → Active Input Handling** que está em **Input System Package (New)** ou **Both**.
5. Menu **Futebol → 1. Configurar projeto (Android 64 bits)**. Ele cria a cena `Partida`, coloca no build e configura IL2CPP + ARM64.
6. Aperte **Play** para jogar no editor. O mouse faz o papel do dedo.
7. Para o celular: **Futebol → 2. Gerar APK (64 bits)**. O arquivo sai em `Build/Futebol.apk`. Passe para o celular e instale (é preciso permitir "instalar apps de fontes desconhecidas").

## Controles

| gesto | com a bola | sem a bola |
| --- | --- | --- |
| arrastar | conduz até o dedo (dedo longe = sprint) | corre até o dedo |
| tocar num companheiro | passe | passa a controlar ele |
| deslizar rápido | chute na direção do deslize (mais rápido = mais forte) | carrinho |

Nos reinícios (saída, lateral, escanteio, tiro de meta), toque num companheiro para cobrar.

## Estrutura

```
Assets/Game/Core/        motor da partida em C# puro (sem Unity): bola, IA, regras, placar
Assets/Game/Runtime/     camada Unity: estádio, jogadores, câmera de TV, toque, placar, escudos
Assets/Game/Editor/      menu Futebol (configurar e gerar APK)
Assets/Game/Resources/data/  uniformes e páginas de escudo (cópia de replica/data)
Tests/                   testes do motor, rodam sem Unity
tools/gen_tokens.py      gera Runtime/Tokens.cs a partir de replica/design/tokens.json
```

## Testar o motor sem Unity

```bash
cd game/Tests
dotnet run -c Release
```

Simula 100 partidas IA x IA com tempos de 3 minutos e 120 jogos forte x fraco, e confere
gol, tiro de meta, lateral, saída, comandos de toque e determinismo.

## Escudos e nomes

Os escudos vêm da Wikipédia quando o jogo abre (precisa de internet na primeira vez; depois fica em cache).
Sem internet, aparece um escudo genérico com as cores do time. Escudos e nomes reais são para uso pessoal.

using System;
using System.Collections.Generic;
using Futebol.Core;

// Testes do motor da partida, sem Unity. Saída diferente de zero = falhou.
public static class Program
{
    private static int _fails;

    private static void Check(bool ok, string what)
    {
        if (!ok) { _fails++; Console.WriteLine("FALHOU: " + what); }
    }

    private static TeamInfo Team(string id, int r) => new TeamInfo
    {
        Id = id, Name = id, ShortName = id.Substring(0, 3).ToUpperInvariant(),
        RatingAttack = r, RatingMidfield = r, RatingDefense = r,
    };

    private static MatchEngine NewMatch(int seed, int user = -1, float half = 60f, int rHome = 75, int rAway = 75) =>
        new MatchEngine(new MatchConfig { Home = Team("flamengo", rHome), Away = Team("palmeiras", rAway), Seed = seed, UserTeam = user, HalfLengthSeconds = half });

    private static void RunToEnd(MatchEngine m, Action<MatchEngine> eachStep = null)
    {
        int guard = 0;
        while (m.Phase != MatchPhase.FullTime && guard++ < 60 * 60 * 30)
        {
            m.Step(MatchEngine.FixedDt);
            eachStep?.Invoke(m);
        }
    }

    public static int Main()
    {
        // 1. Partidas completas IA x IA, com tempos de 3 min (o padrão do jogo).
        int matches = 100, totalGoals = 0, homeWins = 0, draws = 0, totalShots = 0, totalPasses = 0;
        bool finite = true, inBounds = true;
        for (int s = 1; s <= matches; s++)
        {
            var m = NewMatch(s, half: 180f);
            RunToEnd(m, e =>
            {
                if (!e.Ball.Pos.IsFinite || !e.Ball.Vel.IsFinite) finite = false;
                foreach (var p in e.Players)
                {
                    if (!p.Pos.IsFinite) finite = false;
                    if (MathF.Abs(p.Pos.X) > Pitch.HalfLength + 3.5f || MathF.Abs(p.Pos.Z) > Pitch.HalfWidth + 3.5f) inBounds = false;
                }
            });
            Check(m.Phase == MatchPhase.FullTime, $"partida {s} terminou");
            Check(m.Half == 2, $"partida {s} teve segundo tempo");
            int goals = m.Score[0] + m.Score[1];
            int goalEvents = m.Events.FindAll(e => e.Kind == EventKind.Goal || e.Kind == EventKind.OwnGoal).Count;
            Check(goalEvents == goals, $"partida {s}: eventos de gol ({goalEvents}) = placar ({goals})");
            totalGoals += goals;
            if (m.Score[0] > m.Score[1]) homeWins++; else if (m.Score[0] == m.Score[1]) draws++;
            totalShots += m.Stats.Shots[0] + m.Stats.Shots[1];
            totalPasses += m.Stats.Passes[0] + m.Stats.Passes[1];
            Check(m.Stats.PossessionPercent(0) + m.Stats.PossessionPercent(1) >= 99, $"partida {s}: posse soma 100");
        }
        Check(finite, "posições sempre finitas (sem NaN)");
        Check(inBounds, "jogadores não saem para longe do campo");
        float avgGoals = totalGoals / (float)matches;
        Console.WriteLine($"IA x IA, {matches} jogos: {avgGoals:0.00} gols/jogo, {totalShots / (float)matches:0.0} chutes/jogo, {totalPasses / (float)matches:0} passes/jogo, mandante venceu {homeWins}, empates {draws}");
        Check(avgGoals > 1f && avgGoals < 7f, $"média de gols plausível (1 a 7): {avgGoals:0.00}");
        Check(totalShots / (float)matches > 6f, "times chutam ao gol (mais de 6 chutes por jogo)");
        Check(totalPasses / (float)matches > 100f, "times trocam passes (mais de 100 por jogo)");

        // 2. Time mais forte vence mais.
        int strongWins = 0, weakWins = 0;
        for (int s = 1; s <= 120; s++)
        {
            var m = NewMatch(1000 + s, rHome: 88, rAway: 55);
            RunToEnd(m);
            if (m.Score[0] > m.Score[1]) strongWins++; else if (m.Score[1] > m.Score[0]) weakWins++;
        }
        Console.WriteLine($"Forte (88) x fraco (55), 120 jogos: forte venceu {strongWins}, fraco venceu {weakWins}");
        Check(strongWins > weakWins * 2, "time forte vence bem mais que o fraco");

        // 3. Mesma semente, mesmo resultado (determinismo).
        var a = NewMatch(77); RunToEnd(a);
        var b = NewMatch(77); RunToEnd(b);
        Check(a.Score[0] == b.Score[0] && a.Score[1] == b.Score[1] && a.Stats.Passes[0] == b.Stats.Passes[0], "mesma semente = mesma partida");

        // 4. Bola que cruza a linha entre as traves e abaixo do travessão é gol.
        {
            var m = NewMatch(5);
            while (m.Phase != MatchPhase.Play) m.Step(MatchEngine.FixedDt);
            int dir = m.AttackDir(0);
            // O camisa 9 da casa chuta de frente para o gol, de perto, sem goleiro no caminho.
            var gk = m.Players[11];
            gk.Pos = new Vec2(dir * 40f, 30f);
            var striker = m.Players[9];
            m.Ball.Owner = -1;
            m.Ball.Pos = new Vec3(dir * 49f, Pitch.BallRadius, 0f);
            m.Ball.Vel = new Vec3(dir * 20f, 0.5f, 0f);
            m.Ball.LastTouchPlayer = striker.Id; m.Ball.LastTouchTeam = 0;
            m.Ball.NoPickupPlayer = -1;
            foreach (var p in m.Players) if (p.Id != striker.Id && !p.IsKeeper) p.Pos = new Vec2(0, (p.Id % 11) * 2f - 10f);
            for (int i = 0; i < 60 && m.Phase == MatchPhase.Play; i++) m.Step(MatchEngine.FixedDt);
            Check(m.Score[0] == 1 && m.Phase == MatchPhase.GoalScored, $"chute no gol vira gol (placar {m.Score[0]}x{m.Score[1]}, fase {m.Phase})");
            // Depois do gol, quem sofreu dá a saída.
            for (int i = 0; i < 400 && m.Phase == MatchPhase.GoalScored; i++) m.Step(MatchEngine.FixedDt);
            Check(m.Phase == MatchPhase.KickOff && m.RestartTeam == 1, "depois do gol, quem sofreu dá a saída");
        }

        // 5. Bola por cima do travessão sai em tiro de meta, não é gol.
        {
            var m = NewMatch(6);
            while (m.Phase != MatchPhase.Play) m.Step(MatchEngine.FixedDt);
            int dir = m.AttackDir(0);
            m.Players[11].Pos = new Vec2(dir * 30f, 30f);
            m.Ball.Owner = -1;
            m.Ball.Pos = new Vec3(dir * 50f, 4f, 0f);
            m.Ball.Vel = new Vec3(dir * 15f, 2f, 0f);
            m.Ball.LastTouchPlayer = 9; m.Ball.LastTouchTeam = 0; m.Ball.NoPickupPlayer = -1;
            for (int i = 0; i < 60 && m.Phase == MatchPhase.Play; i++) m.Step(MatchEngine.FixedDt);
            Check(m.Score[0] == 0 && m.PendingRestart == RestartKind.GoalKick && m.RestartTeam == 1, $"por cima = tiro de meta do adversário (fase {m.Phase}, {m.PendingRestart})");
        }

        // 6. Bola que sai pela lateral: lateral para quem não tocou.
        {
            var m = NewMatch(7);
            while (m.Phase != MatchPhase.Play) m.Step(MatchEngine.FixedDt);
            m.Ball.Owner = -1;
            m.Ball.Pos = new Vec3(10f, Pitch.BallRadius, Pitch.HalfWidth - 0.5f);
            m.Ball.Vel = new Vec3(0, 0, 10f);
            m.Ball.LastTouchPlayer = 3; m.Ball.LastTouchTeam = 0; m.Ball.NoPickupPlayer = -1;
            foreach (var p in m.Players) p.Pos = new Vec2(-20f + (p.Id % 11), -20f);
            for (int i = 0; i < 60 && m.Phase == MatchPhase.Play; i++) m.Step(MatchEngine.FixedDt);
            Check(m.PendingRestart == RestartKind.ThrowIn && m.RestartTeam == 1, $"lateral para o visitante ({m.PendingRestart}, time {m.RestartTeam})");
        }

        // 7. Comandos do usuário: passe e chute.
        {
            var m = NewMatch(8, user: 0);
            Check(m.Controlled == 9, "usuário controla quem dá a saída");
            bool passed = m.CommandPass(10);
            Check(passed && m.Phase == MatchPhase.Play && m.Ball.Owner == -1, "toque no companheiro na saída = passe");
            for (int i = 0; i < 120 && m.Ball.Owner < 0; i++) m.Step(MatchEngine.FixedDt);
            Check(m.Ball.Owner >= 0, "alguém domina a bola depois do passe");
            // Põe a bola no pé do controlado e chuta.
            var me = m.Players[9];
            m.Ball.Owner = me.Id; m.Step(MatchEngine.FixedDt);
            Check(m.Controlled == me.Id, "controle segue quem tem a bola");
            int shots = m.Stats.Shots[0];
            bool shot = m.CommandShoot(new Vec2(m.AttackDir(0), 0), 1f);
            Check(shot && m.Stats.Shots[0] == shots + 1 && m.Ball.Vel.FlatLength > 20f, "deslize = chute forte");
            // Arrasto move o controlado até o ponto.
            var m2 = NewMatch(9, user: 0);
            m2.CommandPass(10);
            for (int i = 0; i < 30; i++) m2.Step(MatchEngine.FixedDt);
            var ctl = m2.Players[m2.Controlled];
            var target = ctl.Pos + new Vec2(0, 10f);
            m2.CommandRun(target);
            for (int i = 0; i < 180; i++) m2.Step(MatchEngine.FixedDt);
            Check(Vec2.Distance(m2.Players[ctl.Id].Pos, target) < 2.5f || m2.Controlled != ctl.Id, "arrastar leva o jogador até o ponto");
        }

        // 8. Usuário jogando parado (sem tocar): a partida não trava e termina.
        {
            var m = NewMatch(10, user: 0, half: 30f);
            RunToEnd(m);
            Check(m.Phase == MatchPhase.FullTime, "partida com usuário parado termina");
        }

        Console.WriteLine(_fails == 0 ? "TODOS OS TESTES PASSARAM" : $"{_fails} TESTE(S) FALHARAM");
        return _fails == 0 ? 0 : 1;
    }
}

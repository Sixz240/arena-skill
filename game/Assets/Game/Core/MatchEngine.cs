using System;
using System.Collections.Generic;

namespace Futebol.Core
{
    public enum MatchPhase { KickOff, Play, Restart, GoalScored, HalfTime, FullTime }
    public enum RestartKind { None, KickOff, ThrowIn, Corner, GoalKick }
    public enum EventKind { Goal, OwnGoal, HalfTime, FullTime }

    public sealed class MatchEvent
    {
        public EventKind Kind;
        public int Minute;
        public int Team;
        public string PlayerId;
    }

    public sealed class MatchStats
    {
        public readonly int[] Shots = new int[2];
        public readonly int[] ShotsOnTarget = new int[2];
        public readonly int[] Passes = new int[2];
        public readonly float[] PossessionSeconds = new float[2];
        public int PossessionPercent(int team)
        {
            float total = PossessionSeconds[0] + PossessionSeconds[1];
            return total < 0.01f ? 50 : (int)MathF.Round(100f * PossessionSeconds[team] / total);
        }
    }

    public sealed class MatchConfig
    {
        public TeamInfo Home;
        public TeamInfo Away;
        /// <summary>Duração real de cada tempo, em segundos (padrão 3 min).</summary>
        public float HalfLengthSeconds = 180f;
        /// <summary>Time do usuário: 0 casa, 1 visitante, -1 nenhum (IA contra IA).</summary>
        public int UserTeam = 0;
        public int Seed = 1;
        /// <summary>0 a 1. Deixa a IA mais rápida para decidir e mais precisa.</summary>
        public float Difficulty = 0.5f;
    }

    public sealed class PlayerState
    {
        public int Id;          // índice global 0..21
        public int Team;        // 0 casa, 1 visitante
        public int Slot;        // 0..10 na formação
        public PlayerInfo Info;
        public bool IsKeeper => Slot == 0;
        public Vec2 Pos;
        public Vec2 Vel;
        public Vec2 Facing = new Vec2(1, 0);
        public float MaxSpeed;
        /// <summary>Segundos restantes do carrinho em andamento.</summary>
        public float SlideTime;
        public Vec2 SlideDir;
        /// <summary>Segundos caído/recuperando (depois de um carrinho).</summary>
        public float RecoverTime;
        /// <summary>Animação sugerida para a camada visual.</summary>
        public string Anim = "idle";
    }

    public sealed class BallState
    {
        public Vec3 Pos;
        public Vec3 Vel;
        /// <summary>Jogador com a bola no pé, ou -1.</summary>
        public int Owner = -1;
        public int LastTouchPlayer = -1;
        public int LastTouchTeam = -1;
        /// <summary>Quem acabou de chutar não pega a bola de volta por este tempo.</summary>
        public int NoPickupPlayer = -1;
        public float NoPickupTime;
    }

    /// <summary>
    /// Simulação da partida, sem nenhuma dependência do Unity. A camada visual chama Step()
    /// num passo fixo, manda os comandos do toque e desenha o estado (Players, Ball, placar).
    /// </summary>
    public sealed class MatchEngine
    {
        public const float FixedDt = 1f / 60f;
        private const float ControlRadius = 0.85f;
        private const float KeeperReach = 1.7f;
        private const float Accel = 14f;

        public readonly MatchConfig Config;
        public readonly PlayerState[] Players = new PlayerState[22];
        public readonly BallState Ball = new BallState();
        public readonly int[] Score = new int[2];
        public readonly List<MatchEvent> Events = new List<MatchEvent>();
        public readonly MatchStats Stats = new MatchStats();

        public MatchPhase Phase { get; private set; } = MatchPhase.KickOff;
        public RestartKind PendingRestart { get; private set; } = RestartKind.KickOff;
        public int RestartTeam { get; private set; }
        public int Half { get; private set; } = 1;
        /// <summary>Tempo de jogo em segundos (0..5400).</summary>
        public float GameClock { get; private set; }
        public int Minute => Math.Min(Half == 1 ? 45 : 90, (int)(GameClock / 60f) + 1);
        public bool IsAddedTime => GameClock >= (Half == 1 ? 45f : 90f) * 60f;
        /// <summary>Jogador que o usuário controla agora (-1 se não há usuário).</summary>
        public int Controlled { get; private set; } = -1;

        private readonly Rng _rng;
        private float _phaseTimer;
        private float _decisionTimer;
        private float _switchCooldown;
        private int _restartTaker = -1;
        private Vec2? _userRunTarget;
        private bool _userWantsSprint;
        private readonly float _gameSecondsPerRealSecond;
        private float _addedTime;
        /// <summary>Espera entre tentativas de desarme, por jogador.</summary>
        private readonly float[] _tackleCooldown = new float[22];
        /// <summary>Quem acabou de dominar fica protegido um instante.</summary>
        private float _possessionShield;

        public MatchEngine(MatchConfig config)
        {
            Config = config;
            _rng = new Rng(config.Seed);
            _gameSecondsPerRealSecond = 45f * 60f / Math.Max(10f, config.HalfLengthSeconds);
            var teams = new[] { config.Home, config.Away };
            for (int t = 0; t < 2; t++)
            {
                if (teams[t].Players.Count < 11)
                    SquadGenerator.FillStarters(teams[t], config.Seed * 31 + t);
                for (int s = 0; s < 11; s++)
                {
                    var info = teams[t].Players[s];
                    Players[t * 11 + s] = new PlayerState
                    {
                        Id = t * 11 + s,
                        Team = t,
                        Slot = s,
                        Info = info,
                        MaxSpeed = 6.2f + 2.6f * (info.Pace / 99f),
                    };
                }
            }
            PrepareKickOff(0);
        }

        /// <summary>Lado que o time ataca: +1 ataca o gol em +X, -1 o gol em -X. Inverte no intervalo.</summary>
        public int AttackDir(int team) => (team == 0 ? 1 : -1) * (Half == 1 ? 1 : -1);

        public TeamInfo TeamOf(int team) => team == 0 ? Config.Home : Config.Away;

        // ------------------------------------------------------------------ comandos do usuário

        /// <summary>Arrastar: o jogador controlado corre até o ponto. null para soltar.</summary>
        public void CommandRun(Vec2? target, bool sprint = false)
        {
            _userRunTarget = target;
            _userWantsSprint = sprint;
        }

        /// <summary>Tocar num companheiro: passe para ele, se o controlado está com a bola.</summary>
        public bool CommandPass(int receiverId)
        {
            if (Controlled < 0 || receiverId < 0 || receiverId >= 22) return false;
            var p = Players[Controlled];
            var r = Players[receiverId];
            if (r.Team != p.Team || r.Id == p.Id) return false;
            if (Ball.Owner != p.Id && !(Phase == MatchPhase.Restart || Phase == MatchPhase.KickOff) ) return false;
            if (Phase == MatchPhase.Restart || Phase == MatchPhase.KickOff)
            {
                if (_restartTaker != p.Id) return false;
                StartPlay();
            }
            Pass(p, r);
            return true;
        }

        /// <summary>Deslizar com a bola: chute na direção do gesto, força de 0 a 1.</summary>
        public bool CommandShoot(Vec2 direction, float power)
        {
            if (Controlled < 0) return false;
            var p = Players[Controlled];
            if (Ball.Owner != p.Id) return false;
            Shoot(p, direction, Math.Clamp(power, 0f, 1f));
            return true;
        }

        /// <summary>Deslizar sem a bola: carrinho na direção do gesto.</summary>
        public bool CommandTackle(Vec2 direction)
        {
            if (Controlled < 0) return false;
            var p = Players[Controlled];
            if (Ball.Owner == p.Id || p.SlideTime > 0 || p.RecoverTime > 0) return false;
            p.SlideTime = 0.55f;
            p.SlideDir = direction.Normalized;
            p.Vel = p.SlideDir * (p.MaxSpeed + 1.5f);
            p.Anim = "slide";
            return true;
        }

        /// <summary>Tocar num jogador do próprio time quando está defendendo: passa a controlá-lo.</summary>
        public void CommandSelect(int playerId)
        {
            if (Config.UserTeam < 0 || playerId < 0 || playerId >= 22) return;
            if (Players[playerId].Team != Config.UserTeam) return;
            if (Ball.Owner >= 0 && Players[Ball.Owner].Team == Config.UserTeam) return;
            Controlled = playerId;
            _switchCooldown = 1.2f;
        }

        // ------------------------------------------------------------------ passo da simulação

        public void Step(float dt)
        {
            if (Phase == MatchPhase.FullTime) return;
            Ball.NoPickupTime = Math.Max(0, Ball.NoPickupTime - dt);
            _switchCooldown = Math.Max(0, _switchCooldown - dt);
            _possessionShield = Math.Max(0, _possessionShield - dt);
            for (int i = 0; i < 22; i++) _tackleCooldown[i] = Math.Max(0, _tackleCooldown[i] - dt);

            switch (Phase)
            {
                case MatchPhase.GoalScored:
                    _phaseTimer -= dt;
                    MovePlayersCelebrate(dt);
                    if (_phaseTimer <= 0) PrepareKickOff(1 - _scoringTeam);
                    return;
                case MatchPhase.HalfTime:
                    _phaseTimer -= dt;
                    if (_phaseTimer <= 0)
                    {
                        Half = 2;
                        GameClock = 45f * 60f;
                        PrepareKickOff(1);
                    }
                    return;
                case MatchPhase.KickOff:
                case MatchPhase.Restart:
                    _phaseTimer -= dt;
                    UpdateControlled();
                    MovePlayers(dt, restart: true);
                    HoldBallAtRestart();
                    bool userTakes = Config.UserTeam >= 0 && _restartTaker >= 0 && Players[_restartTaker].Team == Config.UserTeam;
                    if (_phaseTimer <= 0 && (!userTakes || _phaseTimer <= -2.5f))
                    {
                        StartPlay();
                        AutoRestartKick(Players[_restartTaker]);
                    }
                    return;
            }

            // Jogo correndo.
            GameClock += dt * _gameSecondsPerRealSecond;
            if (Ball.Owner >= 0) Stats.PossessionSeconds[Players[Ball.Owner].Team] += dt;
            else if (Ball.LastTouchTeam >= 0) Stats.PossessionSeconds[Ball.LastTouchTeam] += dt;

            float halfEnd = (Half == 1 ? 45f : 90f) * 60f + _addedTime;
            if (GameClock >= halfEnd)
            {
                EndHalf();
                return;
            }

            UpdateControlled();
            AiDecisions(dt);
            MovePlayers(dt, restart: false);
            UpdateBall(dt);
            ResolvePossession(dt);
            CheckBallOut();
        }

        private void EndHalf()
        {
            if (Half == 1)
            {
                Events.Add(new MatchEvent { Kind = EventKind.HalfTime, Minute = 45, Team = -1 });
                Phase = MatchPhase.HalfTime;
                _phaseTimer = 2f;
                _addedTime = _rng.Range(0, 3) * 60f;
                Ball.Owner = -1;
                Ball.Vel = new Vec3(0, 0, 0);
            }
            else
            {
                Events.Add(new MatchEvent { Kind = EventKind.FullTime, Minute = 90, Team = -1 });
                Phase = MatchPhase.FullTime;
                Ball.Owner = -1;
                Ball.Vel = new Vec3(0, 0, 0);
            }
        }

        // ------------------------------------------------------------------ reinícios

        private void PrepareKickOff(int team)
        {
            Phase = MatchPhase.KickOff;
            PendingRestart = RestartKind.KickOff;
            _kickRestartKind = RestartKind.KickOff;
            RestartTeam = team;
            _phaseTimer = 1.2f;
            if (Half == 1 && GameClock <= 0) _addedTime = _rng.Range(1, 4) * 60f;
            foreach (var p in Players)
            {
                var home = FormationPos(p, attacking: false, ballFlat: Vec2.Zero);
                // Ninguém no campo adversário nem dentro do círculo, menos quem dá a saída.
                float own = -AttackDir(p.Team);
                home.X = own * MathF.Max(MathF.Abs(home.X), 1.0f);
                p.Pos = home;
                p.Vel = Vec2.Zero;
                p.SlideTime = p.RecoverTime = 0;
                p.Facing = new Vec2(AttackDir(p.Team), 0);
                p.Anim = "idle";
            }
            var taker = Players[team * 11 + 9];
            taker.Pos = new Vec2(-AttackDir(team) * 0.4f, 0f);
            var mate = Players[team * 11 + 10];
            mate.Pos = new Vec2(-AttackDir(team) * 1.5f, 2.5f);
            _restartTaker = taker.Id;
            Ball.Pos = new Vec3(0, Pitch.BallRadius, 0);
            Ball.Vel = new Vec3(0, 0, 0);
            Ball.Owner = taker.Id;
            Ball.LastTouchTeam = team;
            Ball.LastTouchPlayer = taker.Id;
            if (Config.UserTeam == team) { Controlled = taker.Id; }
            else if (Config.UserTeam >= 0) { Controlled = Config.UserTeam * 11 + 9; }
        }

        private void PrepareRestart(RestartKind kind, int team, Vec2 spot)
        {
            Phase = MatchPhase.Restart;
            PendingRestart = kind;
            RestartTeam = team;
            _phaseTimer = kind == RestartKind.ThrowIn ? 0.9f : 1.4f;
            PlayerState taker;
            if (kind == RestartKind.GoalKick)
                taker = Players[team * 11];
            else
                taker = Nearest(team, spot, excludeKeeper: true);
            _restartTaker = taker.Id;
            taker.Pos = spot - new Vec2(MathF.Sign(spot.X) * 0.3f, MathF.Sign(spot.Z) * 0.3f);
            taker.Vel = Vec2.Zero;
            Ball.Pos = Vec3.FromFlat(spot, Pitch.BallRadius);
            Ball.Vel = new Vec3(0, 0, 0);
            Ball.Owner = taker.Id;
            Ball.LastTouchTeam = team;
            Ball.LastTouchPlayer = taker.Id;
            if (Config.UserTeam == team) Controlled = taker.Id;
            // Afasta os adversários 9 m da bola.
            foreach (var p in Players)
            {
                if (p.Team == team) continue;
                var d = p.Pos - spot;
                if (d.Length < 9.15f) p.Pos = spot + (d.Length < 0.1f ? new Vec2(0, 9.2f) : d.Normalized * 9.2f);
                p.Pos = ClampToField(p.Pos, 1f);
            }
        }

        private void HoldBallAtRestart()
        {
            if (_restartTaker < 0) return;
            var t = Players[_restartTaker];
            if (PendingRestart != RestartKind.KickOff)
            {
                t.Pos = Ball.Pos.Flat - new Vec2(MathF.Sign(Ball.Pos.X) * 0.3f, MathF.Sign(Ball.Pos.Z) * 0.3f);
                t.Vel = Vec2.Zero;
            }
            else
            {
                Ball.Pos = new Vec3(0, Pitch.BallRadius, 0);
            }
            Ball.Owner = t.Id;
        }

        private void StartPlay()
        {
            Phase = MatchPhase.Play;
            PendingRestart = RestartKind.None;
        }

        private void AutoRestartKick(PlayerState taker)
        {
            switch (PendingRestartAtKick(taker))
            {
                case RestartKind.Corner:
                {
                    int dir = AttackDir(taker.Team);
                    var target = new Vec2(dir * (Pitch.HalfLength - _rng.Range(6f, 12f)), _rng.Range(-6f, 6f));
                    KickTo(taker, target, lofted: true, power: 0.75f);
                    break;
                }
                case RestartKind.GoalKick:
                {
                    var r = BestPassTarget(taker, preferLong: true) ?? Players[taker.Team * 11 + 9];
                    Pass(taker, r);
                    break;
                }
                default:
                {
                    var r = BestPassTarget(taker, preferLong: false) ?? Players[taker.Team * 11 + 6];
                    Pass(taker, r);
                    break;
                }
            }
        }

        /// <summary>Tipo do reinício em andamento, guardado para a cobrança automática.</summary>
        private RestartKind _kickRestartKind;
        private RestartKind PendingRestartAtKick(PlayerState taker) => _kickRestartKind;

        private void CheckBallOut()
        {
            var b = Ball.Pos;
            if (MathF.Abs(b.Z) > Pitch.HalfWidth + Pitch.BallRadius)
            {
                // Lateral para quem não tocou por último.
                int team = 1 - Math.Max(0, Ball.LastTouchTeam);
                var spot = new Vec2(Math.Clamp(b.X, -Pitch.HalfLength + 1, Pitch.HalfLength - 1), MathF.Sign(b.Z) * Pitch.HalfWidth);
                _kickRestartKind = RestartKind.ThrowIn;
                PrepareRestart(RestartKind.ThrowIn, team, spot);
                return;
            }
            if (MathF.Abs(b.X) > Pitch.HalfLength + Pitch.BallRadius)
            {
                int side = MathF.Sign(b.X) > 0 ? 1 : -1;
                bool betweenPosts = MathF.Abs(b.Z) < Pitch.GoalHalfWidth - Pitch.BallRadius;
                bool underBar = b.Y < Pitch.GoalHeight - Pitch.BallRadius;
                // Time que defende este gol:
                int defending = AttackDir(0) == side ? 1 : 0;
                int attacking = 1 - defending;
                if (betweenPosts && underBar)
                {
                    GoalFor(attacking);
                    return;
                }
                if (Ball.LastTouchTeam == defending)
                {
                    var spot = new Vec2(side * (Pitch.HalfLength - 0.3f), MathF.Sign(b.Z == 0 ? 1 : b.Z) * (Pitch.HalfWidth - 0.3f));
                    _kickRestartKind = RestartKind.Corner;
                    PrepareRestart(RestartKind.Corner, attacking, spot);
                }
                else
                {
                    var spot = new Vec2(side * (Pitch.HalfLength - Pitch.SixYardDepth), MathF.Sign(b.Z == 0 ? 1 : b.Z) * 6f);
                    _kickRestartKind = RestartKind.GoalKick;
                    PrepareRestart(RestartKind.GoalKick, defending, spot);
                }
            }
        }

        private void GoalFor(int team)
        {
            Score[team]++;
            bool own = Ball.LastTouchTeam >= 0 && Ball.LastTouchTeam != team;
            Events.Add(new MatchEvent
            {
                Kind = own ? EventKind.OwnGoal : EventKind.Goal,
                Minute = Minute,
                Team = team,
                PlayerId = Ball.LastTouchPlayer >= 0 ? Players[Ball.LastTouchPlayer].Info.Id : null,
            });
            Phase = MatchPhase.GoalScored;
            _phaseTimer = 2.5f;
            _scoringTeam = team;
            Ball.Owner = -1;
            Ball.Vel = new Vec3(Ball.Vel.X * 0.1f, 0, Ball.Vel.Z * 0.1f);
        }

        /// <summary>Time que marcou o último gol; quem sofreu dá a saída.</summary>
        private int _scoringTeam;

        // ------------------------------------------------------------------ controle do usuário

        private void UpdateControlled()
        {
            if (Config.UserTeam < 0) { Controlled = -1; return; }
            if (Ball.Owner >= 0 && Players[Ball.Owner].Team == Config.UserTeam)
            {
                Controlled = Ball.Owner;
                return;
            }
            if (Controlled < 0 || Players[Controlled].Team != Config.UserTeam) Controlled = Config.UserTeam * 11 + 9;
            if (_switchCooldown > 0 || _userRunTarget.HasValue) return;
            // Defendendo: troca para o mais perto da bola (sem o goleiro).
            var near = Nearest(Config.UserTeam, Ball.Pos.Flat, excludeKeeper: true);
            if (near.Id != Controlled &&
                Vec2.Distance(near.Pos, Ball.Pos.Flat) + 3f < Vec2.Distance(Players[Controlled].Pos, Ball.Pos.Flat))
            {
                Controlled = near.Id;
                _switchCooldown = 0.8f;
            }
        }

        // ------------------------------------------------------------------ IA

        private void AiDecisions(float dt)
        {
            _decisionTimer -= dt;
            if (_decisionTimer > 0) return;
            _decisionTimer = 0.32f - 0.15f * Config.Difficulty;
            if (Ball.Owner < 0) return;
            var owner = Players[Ball.Owner];
            if (owner.Id == Controlled) return; // o usuário decide
            int dir = AttackDir(owner.Team);
            var goal = Pitch.GoalCenter(dir);
            float distGoal = Vec2.Distance(owner.Pos, goal);
            var pressure = NearestOpponentDistance(owner);

            if (distGoal < 30f)
            {
                float angleFactor = MathF.Abs(owner.Pos.Z) < 18f ? 1f : 0.4f;
                float p = (distGoal < 14f ? 0.75f : distGoal < 22f ? 0.35f : 0.08f) * angleFactor;
                if (_rng.Chance(p))
                {
                    var aim = new Vec2(goal.X, _rng.Range(-Pitch.GoalHalfWidth + 0.6f, Pitch.GoalHalfWidth - 0.6f));
                    Shoot(owner, aim - owner.Pos, _rng.Range(0.65f, 1f));
                    return;
                }
            }
            var target = BestPassTarget(owner, preferLong: false);
            bool pressed = pressure < 1.6f;
            float forward = (owner.Pos.X * dir + Pitch.HalfLength) / Pitch.Length; // 0 no próprio gol, 1 no gol adversário
            float passChance = pressed ? 0.55f : 0.08f + 0.1f * (1f - forward);
            if (target != null && _rng.Chance(passChance))
            {
                Pass(owner, target);
            }
        }

        private PlayerState BestPassTarget(PlayerState from, bool preferLong)
        {
            int dir = AttackDir(from.Team);
            PlayerState best = null;
            float bestScore = float.MinValue;
            for (int i = 0; i < 11; i++)
            {
                var r = Players[from.Team * 11 + i];
                if (r.Id == from.Id || r.IsKeeper) continue;
                float d = Vec2.Distance(from.Pos, r.Pos);
                if (d < 5f || d > (preferLong ? 55f : 32f)) continue;
                float forward = (r.Pos.X - from.Pos.X) * dir;
                float open = MathF.Min(8f, NearestOpponentDistance(r));
                float lane = LaneOpenness(from.Pos, r.Pos, from.Team);
                float score = forward * 0.6f + open * 1.2f + lane * 3f - d * 0.05f + _rng.Range(0f, 2f);
                if (preferLong) score += d * 0.1f;
                if (score > bestScore) { bestScore = score; best = r; }
            }
            return best;
        }

        /// <summary>1 se nenhum adversário está perto da linha do passe, 0 se tem alguém em cima.</summary>
        private float LaneOpenness(Vec2 a, Vec2 b, int team)
        {
            var ab = b - a;
            float len = ab.Length;
            if (len < 0.1f) return 1f;
            var n = ab * (1f / len);
            float worst = 1f;
            for (int i = 0; i < 11; i++)
            {
                var o = Players[(1 - team) * 11 + i];
                var ao = o.Pos - a;
                float t = Vec2.Dot(ao, n);
                if (t < 0 || t > len) continue;
                float perp = (ao - n * t).Length;
                worst = MathF.Min(worst, Math.Clamp((perp - 0.8f) / 3f, 0f, 1f));
            }
            return worst;
        }

        private float NearestOpponentDistance(PlayerState p)
        {
            float best = float.MaxValue;
            for (int i = 0; i < 11; i++)
                best = MathF.Min(best, Vec2.Distance(p.Pos, Players[(1 - p.Team) * 11 + i].Pos));
            return best;
        }

        private PlayerState Nearest(int team, Vec2 point, bool excludeKeeper)
        {
            PlayerState best = null;
            float bd = float.MaxValue;
            for (int i = excludeKeeper ? 1 : 0; i < 11; i++)
            {
                var p = Players[team * 11 + i];
                if (p.RecoverTime > 0) continue;
                float d = (p.Pos - point).SqrLength;
                if (d < bd) { bd = d; best = p; }
            }
            return best ?? Players[team * 11 + 9];
        }

        // ------------------------------------------------------------------ chutes e passes

        private void Pass(PlayerState from, PlayerState to)
        {
            var lead = to.Pos + to.Vel * 0.45f;
            int dir = AttackDir(from.Team);
            // Passe em profundidade: se o companheiro está na frente e com espaço, joga no espaço.
            if ((to.Pos.X - from.Pos.X) * dir > 4f && NearestOpponentDistance(to) > 3f)
            {
                var space = to.Pos + new Vec2(dir * 5f, 0);
                if (LaneOpenness(from.Pos, space, from.Team) > 0.5f) lead = space;
            }
            float d = Vec2.Distance(from.Pos, lead);
            bool lofted = d > 30f || LaneOpenness(from.Pos, lead, from.Team) < 0.3f && d > 14f;
            KickTo(from, lead, lofted, power: -1f);
            Stats.Passes[from.Team]++;
        }

        private void KickTo(PlayerState from, Vec2 target, bool lofted, float power)
        {
            var delta = target - from.Pos;
            float d = delta.Length;
            float errDeg = (100 - from.Info.Passing) / 100f * 7f;
            var dir = delta.Normalized.Rotated(_rng.Range(-errDeg, errDeg) * MathF.PI / 180f);
            if (dir.SqrLength < 0.5f) dir = new Vec2(AttackDir(from.Team), 0);
            float speed;
            float vy;
            if (lofted)
            {
                // Lançamento: tempo de voo cresce com a distância.
                float flight = Math.Clamp(0.6f + d / 28f, 0.8f, 2.2f);
                speed = d / flight * 0.95f;
                vy = 9.81f * flight / 2f;
            }
            else
            {
                speed = Math.Clamp(6f + d * 0.55f, 8f, 19f);
                vy = 0.5f;
            }
            if (power > 0) speed *= 0.8f + 0.4f * power;
            ReleaseBall(from, dir, speed, vy);
        }

        private void Shoot(PlayerState from, Vec2 direction, float power)
        {
            var dir = direction.Normalized;
            if (dir.SqrLength < 0.5f) dir = new Vec2(AttackDir(from.Team), 0);
            float errDeg = (100 - from.Info.Shooting) / 100f * 9f + (1f - power) * 2f;
            dir = dir.Rotated(_rng.Range(-errDeg, errDeg) * MathF.PI / 180f);
            float speed = 14f + 17f * power * (0.7f + 0.3f * from.Info.Shooting / 99f);
            float vy = 1.0f + 4.5f * power * power + _rng.Range(-0.5f, 1.2f);
            ReleaseBall(from, dir, speed, vy);
            Stats.Shots[from.Team]++;
            if (WouldBeOnTarget(from.Pos, dir, speed, vy, AttackDir(from.Team))) Stats.ShotsOnTarget[from.Team]++;
            from.Anim = "shoot";
        }

        private static bool WouldBeOnTarget(Vec2 from, Vec2 dir, float speed, float vy, int attackDir)
        {
            if (dir.X * attackDir <= 0.05f) return false;
            float dx = attackDir * Pitch.HalfLength - from.X;
            float t = dx / (dir.X * speed);
            float z = from.Z + dir.Z * speed * t;
            float y = Pitch.BallRadius + vy * t - 0.5f * 9.81f * t * t;
            return MathF.Abs(z) < Pitch.GoalHalfWidth && y < Pitch.GoalHeight && y > -1f;
        }

        private void ReleaseBall(PlayerState from, Vec2 dir, float speed, float vy)
        {
            Ball.Owner = -1;
            var start = from.Pos + dir * 0.5f;
            Ball.Pos = Vec3.FromFlat(start, Pitch.BallRadius);
            Ball.Vel = new Vec3(dir.X * speed, vy, dir.Z * speed);
            Ball.LastTouchPlayer = from.Id;
            Ball.LastTouchTeam = from.Team;
            Ball.NoPickupPlayer = from.Id;
            Ball.NoPickupTime = 0.35f;
            if (from.Anim != "shoot") from.Anim = "pass";
        }

        // ------------------------------------------------------------------ movimento

        private Vec2 FormationPos(PlayerState p, bool attacking, Vec2 ballFlat)
        {
            int dir = AttackDir(p.Team);
            var slot = Formation442.Slots[p.Slot];
            float x = slot.X * Pitch.HalfLength * 0.92f;
            float z = slot.Z * Pitch.HalfWidth * 0.85f;
            if (!p.IsKeeper)
            {
                x += attacking ? 20f : 0f;
                // O bloco acompanha a bola.
                x += ballFlat.X * dir * 0.45f;
                z += ballFlat.Z * 0.25f;
                // Atacantes e pontas atacam o espaço quando o time tem a bola.
                bool forward = p.Slot >= 8 || p.Slot == 5;
                if (attacking && forward)
                    x = MathF.Max(x, MathF.Min(Pitch.HalfLength - 8f, ballFlat.X * dir + 12f));
            }
            var pos = new Vec2(x * dir, z);
            return ClampToField(pos, 2f);
        }

        private void MovePlayers(float dt, bool restart)
        {
            int possTeam = Ball.Owner >= 0 ? Players[Ball.Owner].Team : Ball.LastTouchTeam;
            var ball = Ball.Pos.Flat;
            // Previsão de onde a bola vai estar, para a marcação.
            var ballAhead = ball + Ball.Vel.Flat * 0.4f;

            // Quem pressiona em cada time: os dois mais perto da bola.
            int[] chasers = new int[4] { -1, -1, -1, -1 };
            for (int t = 0; t < 2; t++)
            {
                float b1 = float.MaxValue, b2 = float.MaxValue;
                for (int i = 1; i < 11; i++)
                {
                    var p = Players[t * 11 + i];
                    float d = (p.Pos - ballAhead).SqrLength;
                    if (d < b1) { b2 = b1; chasers[t * 2 + 1] = chasers[t * 2]; b1 = d; chasers[t * 2] = p.Id; }
                    else if (d < b2) { b2 = d; chasers[t * 2 + 1] = p.Id; }
                }
            }

            foreach (var p in Players)
            {
                if (p.RecoverTime > 0)
                {
                    p.RecoverTime -= dt;
                    p.Vel = p.Vel * MathF.Max(0, 1 - 6 * dt);
                    p.Pos += p.Vel * dt;
                    p.Anim = "getup";
                    continue;
                }
                if (p.SlideTime > 0)
                {
                    p.SlideTime -= dt;
                    p.Pos += p.Vel * dt;
                    p.Vel = p.Vel * MathF.Max(0, 1 - 2.5f * dt);
                    if (p.SlideTime <= 0) p.RecoverTime = 0.6f;
                    continue;
                }

                Vec2 target;
                float speedFactor = 0.85f;
                bool owns = Ball.Owner == p.Id;
                bool attacking = possTeam == p.Team;

                if (restart)
                {
                    if (p.Id == _restartTaker) { target = p.Pos; }
                    else target = FormationPos(p, attacking, ball);
                    speedFactor = 0.6f;
                }
                else if (p.Id == Controlled)
                {
                    if (_userRunTarget.HasValue)
                    {
                        target = _userRunTarget.Value;
                        speedFactor = _userWantsSprint ? 1f : 0.9f;
                    }
                    else if (owns)
                    {
                        // Sem dedo na tela: conduz devagar para a frente.
                        target = p.Pos + new Vec2(AttackDir(p.Team) * 2f, 0);
                        speedFactor = 0.35f;
                    }
                    else
                    {
                        // Defesa assistida: vai na bola em ritmo moderado.
                        target = ballAhead;
                        speedFactor = 0.7f;
                    }
                }
                else if (owns)
                {
                    target = DribbleTarget(p);
                    speedFactor = 0.92f;
                }
                else if (p.IsKeeper)
                {
                    target = KeeperTarget(p);
                    speedFactor = 0.9f;
                }
                else if (Ball.Owner < 0 && (chasers[p.Team * 2] == p.Id))
                {
                    // Bola solta: o mais perto vai buscar.
                    target = ballAhead;
                    speedFactor = 1f;
                }
                else if (!attacking && (chasers[p.Team * 2] == p.Id || chasers[p.Team * 2 + 1] == p.Id))
                {
                    // Pressão: o primeiro vai no portador, o segundo fecha a linha do gol.
                    if (chasers[p.Team * 2] == p.Id)
                    {
                        // Fica entre o portador e o próprio gol, colado nele.
                        // Acompanha a 1,6 m e só encosta para dar o bote de tempos em tempos.
                        var ownGoal = Pitch.GoalCenter(-AttackDir(p.Team));
                        bool engage = _tackleCooldown[p.Id] <= 0 && _rng.Chance(dt * (0.6f + 0.8f * Config.Difficulty));
                        target = ball + (ownGoal - ball).Normalized * (engage ? 0.3f : 1.6f);
                    }
                    else
                    {
                        var ownGoal = Pitch.GoalCenter(-AttackDir(p.Team));
                        target = ball + (ownGoal - ball).Normalized * 6f;
                    }
                    speedFactor = 0.95f;
                }
                else
                {
                    target = FormationPos(p, attacking, ball);
                    speedFactor = 0.75f;
                }

                Steer(p, target, speedFactor, dt);
            }
        }

        private void MovePlayersCelebrate(float dt)
        {
            foreach (var p in Players)
            {
                p.Vel = p.Vel * MathF.Max(0, 1 - 3 * dt);
                p.Pos += p.Vel * dt;
                p.Anim = p.Team == _scoringTeam ? "celebrate" : "idle";
            }
            UpdateBall(dt);
        }

        private void Steer(PlayerState p, Vec2 target, float speedFactor, float dt)
        {
            var to = target - p.Pos;
            float dist = to.Length;
            float max = p.MaxSpeed * speedFactor * (Ball.Owner == p.Id ? 0.88f : 1f);
            // Desacelera ao chegar.
            float desiredSpeed = MathF.Min(max, dist * 2.2f);
            var desired = dist > 0.05f ? to * (desiredSpeed / dist) : Vec2.Zero;
            var dv = (desired - p.Vel).ClampLength(Accel * dt);
            p.Vel += dv;
            p.Pos += p.Vel * dt;
            p.Pos = ClampToField(p.Pos, -3f);
            float s = p.Vel.Length;
            if (s > 0.3f) p.Facing = p.Vel * (1f / s);
            if (p.Anim == "shoot" || p.Anim == "pass") { if (s > 1f) p.Anim = s > 5.5f ? "run" : "jog"; }
            else p.Anim = s < 0.4f ? "idle" : s < 3f ? "walk" : s < 5.5f ? "jog" : s < 7.4f ? "run" : "sprint";
        }

        private Vec2 DribbleTarget(PlayerState p)
        {
            int dir = AttackDir(p.Team);
            var goal = Pitch.GoalCenter(dir);
            var toGoal = (goal - p.Pos).Normalized;
            // Desvia do adversário mais perto que está na frente.
            Vec2 avoid = Vec2.Zero;
            for (int i = 0; i < 11; i++)
            {
                var o = Players[(1 - p.Team) * 11 + i];
                var d = o.Pos - p.Pos;
                float dist = d.Length;
                if (dist < 6f && Vec2.Dot(d, toGoal) > 0)
                    avoid += new Vec2(-d.Z, d.X).Normalized * (MathF.Sign(p.Pos.Z - o.Pos.Z + 0.01f) * (6f - dist));
            }
            return p.Pos + toGoal * 5f + avoid * 0.6f;
        }

        private Vec2 KeeperTarget(PlayerState gk)
        {
            int own = -AttackDir(gk.Team);
            var goal = Pitch.GoalCenter(own);
            var ball = Ball.Pos.Flat;
            float ballDistToGoal = Vec2.Distance(ball, goal);
            // Fica na linha entre a bola e o centro do gol, saindo um pouco quando a bola chega.
            float outDist = Math.Clamp(ballDistToGoal * 0.12f, 0.8f, 5f);
            var t = goal + (ball - goal).Normalized * outDist;
            // Bola solta dentro da área e perto: sai para pegar.
            if (Ball.Owner < 0 && Pitch.InsideBox(ball, own) && ballDistToGoal < 12f) t = ball;
            t.Z = Math.Clamp(t.Z, -Pitch.BoxHalfWidth + 1, Pitch.BoxHalfWidth - 1);
            return t;
        }

        private static Vec2 ClampToField(Vec2 p, float margin)
        {
            return new Vec2(
                Math.Clamp(p.X, -Pitch.HalfLength - margin, Pitch.HalfLength + margin),
                Math.Clamp(p.Z, -Pitch.HalfWidth - margin, Pitch.HalfWidth + margin));
        }

        // ------------------------------------------------------------------ bola

        private void UpdateBall(float dt)
        {
            if (Ball.Owner >= 0)
            {
                var o = Players[Ball.Owner];
                var foot = o.Pos + o.Facing * 0.55f;
                Ball.Pos = Vec3.FromFlat(foot, Pitch.BallRadius);
                Ball.Vel = Vec3.FromFlat(o.Vel, 0);
                // Quem conduz não deixa a bola sair do campo.
                if (!Pitch.InsideField(foot))
                {
                    o.Pos = ClampToField(o.Pos, -0.8f);
                    Ball.Pos = Vec3.FromFlat(ClampToField(foot, -0.3f), Pitch.BallRadius);
                }
                return;
            }
            var v = Ball.Vel;
            var p = Ball.Pos;
            if (p.Y > Pitch.BallRadius + 0.01f || v.Y > 0.01f)
            {
                v.Y -= 9.81f * dt;
                // Arrasto do ar.
                float drag = MathF.Max(0, 1 - 0.08f * dt);
                v.X *= drag; v.Z *= drag;
            }
            else
            {
                // Rolando: atrito com a grama.
                float roll = MathF.Max(0, 1 - 0.9f * dt);
                v.X *= roll; v.Z *= roll;
                float s = MathF.Sqrt(v.X * v.X + v.Z * v.Z);
                if (s < 0.15f) { v.X = 0; v.Z = 0; }
            }
            p.X += v.X * dt; p.Y += v.Y * dt; p.Z += v.Z * dt;
            if (p.Y < Pitch.BallRadius)
            {
                p.Y = Pitch.BallRadius;
                if (v.Y < -1.5f) v.Y = -v.Y * 0.5f; else v.Y = 0;
                v.X *= 0.85f; v.Z *= 0.85f;
            }
            // Traves e travessão: rebatem a bola.
            PostCollision(ref p, ref v);
            Ball.Pos = p;
            Ball.Vel = v;
        }

        private static void PostCollision(ref Vec3 p, ref Vec3 v)
        {
            for (int side = -1; side <= 1; side += 2)
            {
                float gx = side * Pitch.HalfLength;
                if (MathF.Abs(p.X - gx) > 0.4f) continue;
                for (int post = -1; post <= 1; post += 2)
                {
                    float pz = post * Pitch.GoalHalfWidth;
                    float dx = p.X - gx, dz = p.Z - pz;
                    if (dx * dx + dz * dz < 0.18f * 0.18f && p.Y < Pitch.GoalHeight)
                    {
                        v.X = -v.X * 0.6f; v.Z = -v.Z * 0.6f;
                        p.X = gx + MathF.Sign(dx == 0 ? -side : dx) * 0.2f;
                    }
                }
                if (MathF.Abs(p.Z) < Pitch.GoalHalfWidth && MathF.Abs(p.Y - Pitch.GoalHeight) < 0.15f && MathF.Abs(p.X - gx) < 0.2f)
                {
                    v.Y = -MathF.Abs(v.Y) * 0.5f;
                    v.X = -v.X * 0.5f;
                }
            }
        }

        private void ResolvePossession(float dt)
        {
            var ball = Ball.Pos;
            if (Ball.Owner < 0)
            {
                PlayerState best = null;
                float bestD = float.MaxValue;
                float ballSpeed = Ball.Vel.FlatLength;
                foreach (var p in Players)
                {
                    if (p.RecoverTime > 0) continue;
                    if (p.Id == Ball.NoPickupPlayer && Ball.NoPickupTime > 0) continue;
                    float d = Vec2.Distance(p.Pos, ball.Flat);
                    bool keeperInBox = p.IsKeeper && Pitch.InsideBox(ball.Flat, -AttackDir(p.Team));
                    float reach = keeperInBox ? KeeperReach : p.SlideTime > 0 ? 1.3f : ControlRadius;
                    float maxH = keeperInBox ? 2.5f : 1.1f;
                    if (d > reach || ball.Y > maxH) continue;
                    // Bola muito rápida: chance de não dominar.
                    if (!keeperInBox && ballSpeed > 18f && !_rng.Chance(0.25f)) continue;
                    if (keeperInBox && ballSpeed > 24f && !_rng.Chance(0.55f + p.Info.Goalkeeping / 400f))
                    {
                        // Espalma.
                        Ball.Vel = new Vec3(-Ball.Vel.X * 0.3f, 3f, Ball.Vel.Z * 0.6f + _rng.Range(-4f, 4f));
                        Ball.LastTouchPlayer = p.Id; Ball.LastTouchTeam = p.Team;
                        Ball.NoPickupPlayer = p.Id; Ball.NoPickupTime = 0.5f;
                        p.Anim = "save";
                        return;
                    }
                    if (d < bestD) { bestD = d; best = p; }
                }
                if (best != null)
                {
                    if (best.SlideTime > 0)
                    {
                        // Carrinho que pega a bola solta: afasta.
                        Ball.Vel = Vec3.FromFlat(best.SlideDir * 9f, 1f);
                        Ball.LastTouchPlayer = best.Id; Ball.LastTouchTeam = best.Team;
                        Ball.NoPickupPlayer = best.Id; Ball.NoPickupTime = 0.4f;
                    }
                    else GainBall(best);
                }
                return;
            }

            // Disputa com quem está com a bola.
            var owner = Players[Ball.Owner];
            if (_possessionShield > 0) return;
            for (int i = 0; i < 11; i++)
            {
                var d = Players[(1 - owner.Team) * 11 + i];
                if (d.RecoverTime > 0 || _tackleCooldown[d.Id] > 0) continue;
                float dist = Vec2.Distance(d.Pos, ball.Flat);
                bool slide = d.SlideTime > 0;
                if (dist > (slide ? 1.6f : 0.9f)) continue;
                // Uma tentativa de desarme e depois uma espera.
                _tackleCooldown[d.Id] = slide ? 0.6f : 1.4f;
                float p = (slide ? 0.7f : 0.2f) * (d.Info.Defending + 10f) / (d.Info.Defending + owner.Info.Dribbling + 20f) * 2f;
                if (_rng.Chance(p))
                {
                    if (slide)
                    {
                        Ball.Owner = -1;
                        Ball.Vel = Vec3.FromFlat(d.SlideDir * 8f, 0.8f);
                        Ball.LastTouchPlayer = d.Id; Ball.LastTouchTeam = d.Team;
                        Ball.NoPickupPlayer = d.Id; Ball.NoPickupTime = 0.3f;
                        owner.RecoverTime = 0.7f;
                    }
                    else GainBall(d);
                    return;
                }
            }
        }

        private void GainBall(PlayerState p)
        {
            Ball.Owner = p.Id;
            _possessionShield = 0.45f;
            // Quem recebe olha o jogo antes de decidir.
            if (_decisionTimer < 0.45f) _decisionTimer = 0.45f;
            Ball.LastTouchPlayer = p.Id;
            Ball.LastTouchTeam = p.Team;
            Ball.Vel = new Vec3(0, 0, 0);
            if (p.IsKeeper)
            {
                p.Vel = Vec2.Zero;
                _decisionTimer = 0.9f; // goleiro segura um pouco antes de repor
            }
        }
    }
}

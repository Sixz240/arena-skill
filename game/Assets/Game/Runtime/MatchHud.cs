using System.Linq;
using Futebol.Core;
using UnityEngine;

namespace Futebol.Runtime
{
    /// <summary>
    /// Placar, pausa e tela de fim de jogo (S06, S07, S08 da recon), em IMGUI para a fatia vertical.
    /// Cores e tamanhos vêm de Tokens (gerados de replica/design/tokens.json). Os menus de verdade
    /// (S02-S05, S09+) vão para UI Toolkit com tokens.uss.
    /// </summary>
    public sealed class MatchHud : MonoBehaviour
    {
        public MatchRunner Runner;
        public CrestService Crests;

        private GUIStyle _label, _score, _small, _button, _title;
        private Texture2D _scoreBg, _surface, _accent, _overlay, _surface2;
        private float _scale = 1f;
        private Rect _pauseRect;

        private MatchEngine E => Runner.Engine;

        private void EnsureStyles()
        {
            if (_label != null) return;
            _scoreBg = Solid(new Color(Tokens.ScoreBg.r, Tokens.ScoreBg.g, Tokens.ScoreBg.b, 0.85f));
            _surface = Solid(Tokens.Surface);
            _surface2 = Solid(Tokens.Surface2);
            _accent = Solid(Tokens.Accent);
            _overlay = Solid(new Color(Tokens.Bg.r, Tokens.Bg.g, Tokens.Bg.b, 0.8f));
            _label = new GUIStyle(GUI.skin.label) { fontSize = Tokens.FontBase, alignment = TextAnchor.MiddleLeft };
            _label.normal.textColor = Tokens.Text;
            _small = new GUIStyle(_label) { fontSize = Tokens.FontSm };
            _small.normal.textColor = Tokens.TextMuted;
            _score = new GUIStyle(_label) { fontSize = Tokens.FontScore, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            _score.normal.textColor = Tokens.ScoreText;
            _title = new GUIStyle(_label) { fontSize = Tokens.FontDisplay, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            _button = new GUIStyle(GUI.skin.button) { fontSize = Tokens.FontLg, fontStyle = FontStyle.Bold };
            _button.normal.background = _accent; _button.hover.background = _accent; _button.active.background = _surface2;
            _button.normal.textColor = _button.hover.textColor = Tokens.OnAccent;
            _button.active.textColor = Tokens.Text;
        }

        private static Texture2D Solid(Color c)
        {
            var t = new Texture2D(1, 1); t.SetPixel(0, 0, c); t.Apply(); return t;
        }

        /// <summary>O TouchController pergunta se o dedo está num botão do HUD.</summary>
        public bool IsOverUi(Vector2 screenPos)
        {
            // GUI tem Y para baixo; a tela do Input System, para cima.
            var gui = new Vector2(screenPos.x, Screen.height - screenPos.y) / _scale;
            return Runner.Paused || E.Phase == MatchPhase.FullTime || _pauseRect.Contains(gui);
        }

        private void OnGUI()
        {
            if (Runner == null || Runner.Engine == null) return;
            EnsureStyles();
            _scale = Screen.height / Tokens.ReferenceHeight;
            GUI.matrix = Matrix4x4.Scale(new Vector3(_scale, _scale, 1));
            var safe = Screen.safeArea;
            float left = safe.x / _scale + Tokens.HudMargin;
            float top = (Screen.height - safe.yMax) / _scale + Tokens.HudMargin;
            float width = Screen.width / _scale;
            float right = width - (Screen.width - safe.xMax) / _scale - Tokens.HudMargin;

            Scoreboard(left, top);

            _pauseRect = new Rect(right - Tokens.TouchTargetMin * 1.4f, top, Tokens.TouchTargetMin * 1.4f, Tokens.TouchTargetMin * 1.4f);
            if (E.Phase != MatchPhase.FullTime && !Runner.Paused)
            {
                GUI.DrawTexture(_pauseRect, _scoreBg);
                if (GUI.Button(_pauseRect, "II", _score)) Runner.Paused = true;
            }

            Banners(width);
            if (Runner.Paused) PauseMenu(width);
            if (E.Phase == MatchPhase.FullTime) FullTime(width);
        }

        private void Scoreboard(float x, float y)
        {
            var home = E.TeamOf(0); var away = E.TeamOf(1);
            float h = 64, w = 430;
            GUI.DrawTexture(new Rect(x, y, w, h), _scoreBg);
            var ch = Crests.Get(home).Texture; var ca = Crests.Get(away).Texture;
            GUI.DrawTexture(new Rect(x + 8, y + 10, 44, 44), ch, ScaleMode.ScaleToFit);
            GUI.Label(new Rect(x + 56, y, 70, h), home.ShortName, _score);
            GUI.Label(new Rect(x + 126, y, 90, h), $"{E.Score[0]} - {E.Score[1]}", _score);
            GUI.Label(new Rect(x + 216, y, 70, h), away.ShortName, _score);
            GUI.DrawTexture(new Rect(x + 290, y + 10, 44, 44), ca, ScaleMode.ScaleToFit);
            string clock = E.Phase == MatchPhase.HalfTime ? "INT" : E.Phase == MatchPhase.FullTime ? "FIM" : $"{E.Minute}'";
            if (E.IsAddedTime && E.Phase == MatchPhase.Play) clock += "+";
            GUI.Label(new Rect(x + 340, y, 86, h), clock, _score);
        }

        private void Banners(float width)
        {
            string text = null;
            if (E.Phase == MatchPhase.GoalScored)
            {
                var last = E.Events.LastOrDefault(ev => ev.Kind == EventKind.Goal || ev.Kind == EventKind.OwnGoal);
                text = last != null && last.Kind == EventKind.OwnGoal ? "GOL CONTRA!" : "GOL!";
                if (last != null && last.Kind == EventKind.Goal && last.PlayerId != null)
                    text += "  " + E.TeamOf(last.Team).Players.Find(p => p.Id == last.PlayerId)?.Name;
            }
            else if (E.Phase == MatchPhase.HalfTime) text = "INTERVALO";
            else if ((E.Phase == MatchPhase.Restart || E.Phase == MatchPhase.KickOff) && E.Config.UserTeam >= 0 && E.RestartTeam == E.Config.UserTeam)
            {
                string what = E.PendingRestart switch
                {
                    RestartKind.ThrowIn => "Lateral",
                    RestartKind.Corner => "Escanteio",
                    RestartKind.GoalKick => "Tiro de meta",
                    _ => "Saída de bola",
                };
                GUI.Label(new Rect(0, 140, width, 40), $"{what}: toque num companheiro para passar", Center(_label));
                return;
            }
            if (text == null) return;
            var r = new Rect(width / 2 - 360, 150, 720, 100);
            GUI.DrawTexture(r, _scoreBg);
            GUI.Label(r, text, _title);
        }

        private static GUIStyle Center(GUIStyle s) => new GUIStyle(s) { alignment = TextAnchor.MiddleCenter };

        private void PauseMenu(float width)
        {
            GUI.DrawTexture(new Rect(0, 0, width, Tokens.ReferenceHeight), _overlay);
            var panel = new Rect(width / 2 - 320, 220, 640, 560);
            GUI.DrawTexture(panel, _surface);
            GUI.Label(new Rect(panel.x, panel.y + 24, panel.width, 60), "Pausa", _title);
            float bx = panel.x + 80, bw = panel.width - 160, by = panel.y + 130;
            if (GUI.Button(new Rect(bx, by, bw, 64), "Continuar", _button)) Runner.Paused = false;
            if (GUI.Button(new Rect(bx, by + 84, bw, 64), "Recomeçar partida", _button)) Runner.Restart();
            var help =
                "Arrastar: correr até o dedo (longe = sprint)\n" +
                "Tocar num companheiro: passe\n" +
                "Deslizar rápido com a bola: chute (mais rápido = mais forte)\n" +
                "Deslizar sem a bola: carrinho\n" +
                "Tocar num companheiro na defesa: controlar ele";
            GUI.Label(new Rect(bx, by + 180, bw, 220), help, new GUIStyle(_small) { wordWrap = true, alignment = TextAnchor.UpperLeft });
        }

        private void FullTime(float width)
        {
            GUI.DrawTexture(new Rect(0, 0, width, Tokens.ReferenceHeight), _overlay);
            var panel = new Rect(width / 2 - 460, 110, 920, 860);
            GUI.DrawTexture(panel, _surface);
            var home = E.TeamOf(0); var away = E.TeamOf(1);
            GUI.Label(new Rect(panel.x, panel.y + 20, panel.width, 50), "Fim de jogo", Center(_small));
            GUI.DrawTexture(new Rect(panel.x + 120, panel.y + 80, 110, 110), Crests.Get(home).Texture, ScaleMode.ScaleToFit);
            GUI.DrawTexture(new Rect(panel.xMax - 230, panel.y + 80, 110, 110), Crests.Get(away).Texture, ScaleMode.ScaleToFit);
            GUI.Label(new Rect(panel.x, panel.y + 80, panel.width, 110), $"{E.Score[0]}  -  {E.Score[1]}", _title);
            GUI.Label(new Rect(panel.x + 40, panel.y + 190, 300, 40), home.Name, Center(_label));
            GUI.Label(new Rect(panel.xMax - 340, panel.y + 190, 300, 40), away.Name, Center(_label));

            // Gols.
            float y = panel.y + 240;
            foreach (var ev in E.Events.Where(ev => ev.Kind == EventKind.Goal || ev.Kind == EventKind.OwnGoal))
            {
                var team = E.TeamOf(ev.Team);
                string who = ev.Kind == EventKind.OwnGoal ? "gol contra" : team.Players.Find(p => p.Id == ev.PlayerId)?.Name ?? "";
                var r = ev.Team == 0 ? new Rect(panel.x + 40, y, 380, 30) : new Rect(panel.xMax - 420, y, 380, 30);
                GUI.Label(r, $"{ev.Minute}'  {who}", Center(_small));
                y += 30;
                if (y > panel.y + 430) break;
            }

            // Estatísticas.
            float sy = panel.y + 450;
            void Row(string name, string a, string b)
            {
                GUI.Label(new Rect(panel.x + 120, sy, 200, 40), a, Center(_label));
                GUI.Label(new Rect(panel.x + 320, sy, 280, 40), name, Center(_small));
                GUI.Label(new Rect(panel.x + 600, sy, 200, 40), b, Center(_label));
                sy += 44;
            }
            var st = E.Stats;
            Row("Posse de bola", st.PossessionPercent(0) + "%", st.PossessionPercent(1) + "%");
            Row("Chutes", st.Shots[0].ToString(), st.Shots[1].ToString());
            Row("Chutes no alvo", st.ShotsOnTarget[0].ToString(), st.ShotsOnTarget[1].ToString());
            Row("Passes", st.Passes[0].ToString(), st.Passes[1].ToString());

            if (GUI.Button(new Rect(panel.x + 260, panel.yMax - 110, panel.width - 520, 72), "Jogar de novo", _button)) Runner.Restart();
        }
    }
}

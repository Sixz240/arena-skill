using Futebol.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;

namespace Futebol.Runtime
{
    /// <summary>
    /// Controle por gesto, como descrito na recon (F02):
    ///   arrastar = correr até o dedo; tocar no companheiro = passe (ou trocar de jogador, na defesa);
    ///   deslizar rápido = chute com a bola, carrinho sem a bola (força = velocidade do deslize).
    /// No editor, o mouse faz o papel do dedo.
    /// </summary>
    public sealed class TouchController : MonoBehaviour
    {
        public MatchEngine Engine;
        public Camera Cam;
        /// <summary>Rastro do último deslize, para o HUD desenhar.</summary>
        public Vector2 SwipeFrom, SwipeTo;
        public float SwipeFlash;
        /// <summary>Retângulos de tela (em pixels) que pertencem a botões do HUD e não viram gesto.</summary>
        public System.Func<Vector2, bool> IsOverUi;

        private const float TapMaxTime = 0.22f;
        private const float SwipeMaxTime = 0.35f;
        private float TapMaxPixels => Screen.dpi > 0 ? Screen.dpi * 0.12f : 18f;
        private float SwipeMinSpeed => (Screen.dpi > 0 ? Screen.dpi : 160f) * 6f; // px/s

        private bool _down;
        private Vector2 _start, _last;
        private float _startTime;
        private bool _dragging;

        private void OnEnable() { EnhancedTouchSupport.Enable(); }
        private void OnDisable() { EnhancedTouchSupport.Disable(); }

        private void Update()
        {
            if (Engine == null || Cam == null) return;
            SwipeFlash = Mathf.Max(0, SwipeFlash - Time.unscaledDeltaTime);

            bool pressed = false, released = false;
            Vector2 pos = _last;
            if (Touch.activeTouches.Count > 0)
            {
                var t = Touch.activeTouches[0];
                pos = t.screenPosition;
                pressed = t.phase == UnityEngine.InputSystem.TouchPhase.Began;
                released = t.phase == UnityEngine.InputSystem.TouchPhase.Ended || t.phase == UnityEngine.InputSystem.TouchPhase.Canceled;
            }
            else if (Mouse.current != null)
            {
                pos = Mouse.current.position.ReadValue();
                pressed = Mouse.current.leftButton.wasPressedThisFrame;
                released = Mouse.current.leftButton.wasReleasedThisFrame;
            }

            if (pressed)
            {
                if (IsOverUi != null && IsOverUi(pos)) return;
                _down = true; _dragging = false;
                _start = _last = pos; _startTime = Time.unscaledTime;
                return;
            }
            if (!_down) return;

            float elapsed = Time.unscaledTime - _startTime;
            float moved = (pos - _start).magnitude;

            if (released)
            {
                _down = false;
                float speed = moved / Mathf.Max(0.01f, elapsed);
                if (!_dragging && elapsed <= TapMaxTime && moved <= TapMaxPixels) Tap(pos);
                else if (elapsed <= SwipeMaxTime && speed >= SwipeMinSpeed) Swipe(_start, pos, speed);
                Engine.CommandRun(null);
                _dragging = false;
                return;
            }

            // Segurando: depois de um tempo, ou se o dedo anda devagar, vira arrasto.
            if (!_dragging && (elapsed > SwipeMaxTime || (moved > TapMaxPixels && elapsed > 0.12f && moved / elapsed < SwipeMinSpeed)))
                _dragging = true;
            if (_dragging && ToPitch(pos, out var world))
            {
                var me = Engine.Controlled >= 0 ? Engine.Players[Engine.Controlled].Pos : world;
                Engine.CommandRun(world, sprint: Vec2.Distance(me, world) > 7f);
            }
            _last = pos;
        }

        private void Tap(Vector2 screen)
        {
            if (!ToPitch(screen, out var world)) return;
            int user = Engine.Config.UserTeam;
            int best = -1; float bestD = 3.0f;
            foreach (var p in Engine.Players)
            {
                float d = Vec2.Distance(p.Pos, world);
                if (d < bestD) { bestD = d; best = p.Id; }
            }
            if (best < 0) return;
            var tapped = Engine.Players[best];
            bool weHaveBall = Engine.Ball.Owner >= 0 && Engine.Players[Engine.Ball.Owner].Team == user
                              || Engine.Phase == MatchPhase.Restart || Engine.Phase == MatchPhase.KickOff;
            if (tapped.Team == user)
            {
                if (weHaveBall) Engine.CommandPass(best);
                else Engine.CommandSelect(best);
            }
        }

        private void Swipe(Vector2 from, Vector2 to, float pxPerSecond)
        {
            SwipeFrom = from; SwipeTo = to; SwipeFlash = 0.35f;
            if (!ToPitch(from, out var a) || !ToPitch(to, out var b)) return;
            var dir = (b - a).Normalized;
            if (dir.SqrLength < 0.5f) return;
            float dpi = Screen.dpi > 0 ? Screen.dpi : 160f;
            float power = Mathf.Clamp01((pxPerSecond / dpi - 6f) / 18f);
            bool hasBall = Engine.Controlled >= 0 && Engine.Ball.Owner == Engine.Controlled;
            if (hasBall) Engine.CommandShoot(dir, power);
            else Engine.CommandTackle(dir);
        }

        private bool ToPitch(Vector2 screen, out Vec2 world)
        {
            var ray = Cam.ScreenPointToRay(screen);
            var plane = new Plane(Vector3.up, Vector3.zero);
            if (plane.Raycast(ray, out float enter))
            {
                var p = ray.GetPoint(enter);
                world = new Vec2(p.x, p.z);
                return true;
            }
            world = Vec2.Zero;
            return false;
        }
    }
}

using Futebol.Core;
using UnityEngine;

namespace Futebol.Runtime
{
    /// <summary>
    /// Liga o motor (Futebol.Core) à cena: roda a simulação em passo fixo, interpola as posições
    /// para desenhar suave e pausa quando o app vai para segundo plano.
    /// </summary>
    public sealed class MatchRunner : MonoBehaviour
    {
        public string HomeTeamId = "flamengo";
        public string AwayTeamId = "palmeiras";
        public float HalfLengthSeconds = 180f;
        public int UserTeam = 0;

        public MatchEngine Engine { get; private set; }
        public bool Paused;

        private PlayerView[] _players;
        private Transform _ball, _ballShadow;
        private Vector3[] _prev, _curr;
        private Vector3 _prevBall, _currBall;
        private float _acc;
        private Transform _world;
        private Transform _teamRoot;
        private TouchController _touch;
        private CrestService _crests;
        private MatchHud _hud;
        private BroadcastCamera _cam;

        private void Start()
        {
            Application.targetFrameRate = 30;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
            Screen.orientation = ScreenOrientation.LandscapeLeft;

            _crests = gameObject.AddComponent<CrestService>();
            BuildWorld();
            NewMatch();
        }

        public void Restart()
        {
            Paused = false;
            if (_teamRoot != null) Destroy(_teamRoot.gameObject);
            NewMatch();
        }

        private void BuildWorld()
        {
            _world = new GameObject("Mundo").transform;
            StadiumBuilder.Build(_world);

            var sun = new GameObject("Sol").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.15f;
            sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(52f, -35f, 0);
            sun.transform.SetParent(_world, false);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.62f, 0.68f, 0.78f);
            RenderSettings.ambientEquatorColor = new Color(0.42f, 0.45f, 0.42f);
            RenderSettings.ambientGroundColor = new Color(0.2f, 0.22f, 0.18f);

            var ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Destroy(ball.GetComponent<Collider>());
            ball.name = "Bola";
            ball.transform.localScale = Vector3.one * Pitch.BallRadius * 2f * 1.6f; // um pouco maior que o real, para ler na tela do celular
            ball.GetComponent<Renderer>().sharedMaterial = Materials.Color(Color.white, 0.5f);
            _ball = ball.transform;
            _ball.SetParent(_world, false);
            var shadow = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Destroy(shadow.GetComponent<Collider>());
            shadow.name = "Sombra da bola";
            shadow.transform.localScale = new Vector3(0.45f, 0.005f, 0.45f);
            shadow.GetComponent<Renderer>().sharedMaterial = Materials.Color(new Color(0, 0, 0, 1) * 0.35f);
            _ballShadow = shadow.transform;
            _ballShadow.SetParent(_world, false);

            var camGo = Camera.main != null ? Camera.main.gameObject : new GameObject("Camera", typeof(Camera));
            camGo.tag = "MainCamera";
            var cam = camGo.GetComponent<Camera>();
            cam.fieldOfView = 34f;
            cam.nearClipPlane = 0.5f;
            cam.farClipPlane = 400f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Tokens.Bg;
            _cam = camGo.GetComponent<BroadcastCamera>();
            if (_cam == null) _cam = camGo.AddComponent<BroadcastCamera>();
            _cam.Target = _ball;

            _touch = gameObject.AddComponent<TouchController>();
            _touch.Cam = cam;
            _hud = gameObject.AddComponent<MatchHud>();
            _hud.Runner = this;
            _hud.Crests = _crests;
            _touch.IsOverUi = _hud.IsOverUi;
        }

        private void NewMatch()
        {
            var home = KitDatabase.Get(HomeTeamId);
            var away = KitDatabase.Get(AwayTeamId);
            home.RatingAttack = home.RatingMidfield = home.RatingDefense = 78;
            away.RatingAttack = away.RatingMidfield = away.RatingDefense = 77;
            var awayKit = KitDatabase.AwayKitFor(home, away);
            Engine = new MatchEngine(new MatchConfig
            {
                Home = home,
                Away = away,
                HalfLengthSeconds = HalfLengthSeconds,
                UserTeam = UserTeam,
                Seed = Random.Range(1, int.MaxValue),
            });
            _touch.Engine = Engine;

            _players = new PlayerView[22];
            _prev = new Vector3[22];
            _curr = new Vector3[22];
            var teamRoot = new GameObject("Jogadores").transform;
            teamRoot.SetParent(_world, false);
            _teamRoot = teamRoot;
            for (int i = 0; i < 22; i++)
            {
                var p = Engine.Players[i];
                var kit = p.Team == 0 ? home.Home : awayKit;
                _players[i] = PlayerView.Create(teamRoot, p, kit, p.IsKeeper);
                _prev[i] = _curr[i] = new Vector3(p.Pos.X, 0, p.Pos.Z);
            }
            _prevBall = _currBall = ToUnity(Engine.Ball.Pos);
            _acc = 0;
        }

        private static Vector3 ToUnity(Vec3 v) => new Vector3(v.X, v.Y, v.Z);

        private void Update()
        {
            if (Engine == null) return;
            if (!Paused)
            {
                _acc += Mathf.Min(Time.deltaTime, 0.1f);
                int steps = 0;
                while (_acc >= MatchEngine.FixedDt && steps++ < 8)
                {
                    for (int i = 0; i < 22; i++) _prev[i] = _curr[i];
                    _prevBall = _currBall;
                    Engine.Step(MatchEngine.FixedDt);
                    for (int i = 0; i < 22; i++) { var p = Engine.Players[i].Pos; _curr[i] = new Vector3(p.X, 0, p.Z); }
                    _currBall = ToUnity(Engine.Ball.Pos);
                    _acc -= MatchEngine.FixedDt;
                }
            }
            float alpha = Mathf.Clamp01(_acc / MatchEngine.FixedDt);
            for (int i = 0; i < 22; i++)
                _players[i].Sync(Engine.Players[i], Vector3.Lerp(_prev[i], _curr[i], alpha), i == Engine.Controlled);
            var b = Vector3.Lerp(_prevBall, _currBall, alpha);
            _ball.position = b;
            _ball.Rotate(new Vector3(Engine.Ball.Vel.Z, 0, -Engine.Ball.Vel.X) * (Time.deltaTime * 360f / (Mathf.PI * 0.44f)), Space.World);
            _ballShadow.position = new Vector3(b.x, 0.01f, b.z);
        }

        private void OnApplicationPause(bool pause)
        {
            if (pause && Engine != null && Engine.Phase != MatchPhase.FullTime) Paused = true;
        }
    }
}

using Futebol.Core;
using UnityEngine;

namespace Futebol.Runtime
{
    /// <summary>
    /// Jogador provisório montado com formas simples (tronco, cabeça, braços, pernas) e animação
    /// procedural de corrida. É o substituto do modelo semi-realista (ver replica/design/components.md)
    /// até os modelos e animações entrarem; a interface com o motor não muda.
    /// </summary>
    public sealed class PlayerView : MonoBehaviour
    {
        public int PlayerId;
        private Transform _body, _legL, _legR, _armL, _armR, _marker;
        private Renderer _markerRenderer;
        private float _phase;

        private static readonly Color[] SkinTones =
        {
            new Color32(255, 224, 196, 255), new Color32(234, 192, 160, 255), new Color32(198, 145, 108, 255),
            new Color32(160, 110, 76, 255), new Color32(116, 76, 52, 255), new Color32(82, 54, 38, 255),
        };

        public static PlayerView Create(Transform parent, PlayerState p, KitColors kit, bool keeper)
        {
            var go = new GameObject($"Jogador {p.Team}-{p.Info.ShirtNumber}");
            go.transform.SetParent(parent, false);
            var v = go.AddComponent<PlayerView>();
            v.PlayerId = p.Id;
            v.Build(kit, keeper, p.Id);
            return v;
        }

        private void Build(KitColors kit, bool keeper, int seed)
        {
            var shirtMat = keeper
                ? Materials.Color(KeeperColor(seed))
                : Materials.Textured(KitTextures.Shirt(kit), 0.2f);
            var sleeveColor = kit.Pattern == "sleeves" ? KitDatabase.ParseColor(kit.Secondary, Color.white) : KitDatabase.ParseColor(kit.Primary, Color.white);
            var sleeveMat = keeper ? shirtMat : Materials.Color(sleeveColor);
            var shortsMat = Materials.Color(KitDatabase.ParseColor(kit.Shorts, Color.white));
            var socksMat = Materials.Color(KitDatabase.ParseColor(kit.Socks, Color.white));
            var skinMat = Materials.Color(SkinTones[seed % SkinTones.Length], 0.35f);
            var hairMat = Materials.Color(new Color(0.08f, 0.06f, 0.05f));

            _body = new GameObject("Corpo").transform;
            _body.SetParent(transform, false);

            // Proporção humana (~1,80 m).
            Part(PrimitiveType.Capsule, _body, new Vector3(0, 1.22f, 0), new Vector3(0.42f, 0.33f, 0.26f), shirtMat);   // tronco
            Part(PrimitiveType.Cylinder, _body, new Vector3(0, 0.92f, 0), new Vector3(0.40f, 0.11f, 0.26f), shortsMat); // calção
            Part(PrimitiveType.Sphere, _body, new Vector3(0, 1.66f, 0), new Vector3(0.22f, 0.25f, 0.23f), skinMat);     // cabeça
            Part(PrimitiveType.Sphere, _body, new Vector3(0, 1.73f, -0.02f), new Vector3(0.23f, 0.15f, 0.23f), hairMat); // cabelo
            _legL = Limb(_body, new Vector3(-0.1f, 0.88f, 0), 0.86f, 0.13f, socksMat, skinMat);
            _legR = Limb(_body, new Vector3(0.1f, 0.88f, 0), 0.86f, 0.13f, socksMat, skinMat);
            _armL = Limb(_body, new Vector3(-0.26f, 1.45f, 0), 0.62f, 0.09f, sleeveMat, skinMat);
            _armR = Limb(_body, new Vector3(0.26f, 1.45f, 0), 0.62f, 0.09f, sleeveMat, skinMat);

            // Marcador do jogador controlado (anel no chão).
            var ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Destroy(ring.GetComponent<Collider>());
            ring.name = "Marcador";
            ring.transform.SetParent(transform, false);
            ring.transform.localPosition = new Vector3(0, 0.02f, 0);
            ring.transform.localScale = new Vector3(1.3f, 0.01f, 1.3f);
            _markerRenderer = ring.GetComponent<Renderer>();
            _markerRenderer.sharedMaterial = Materials.Color(Tokens.Focus);
            _marker = ring.transform;
            _marker.gameObject.SetActive(false);
        }

        private static Color KeeperColor(int seed)
        {
            // Mesmas 4 opções da regra de goleiro em kits-brasileirao-2026.json.
            var opts = new[] { "#FFD400", "#00C2A8", "#FF7A00", "#7B2CBF" };
            return KitDatabase.ParseColor(opts[seed < 11 ? 0 : 1], Color.yellow);
        }

        private static Transform Part(PrimitiveType type, Transform parent, Vector3 pos, Vector3 scale, Material m)
        {
            var g = GameObject.CreatePrimitive(type);
            Destroy(g.GetComponent<Collider>());
            g.transform.SetParent(parent, false);
            g.transform.localPosition = pos;
            g.transform.localScale = scale;
            var r = g.GetComponent<Renderer>();
            r.sharedMaterial = m;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            return g.transform;
        }

        /// <summary>Membro com pivô no topo (quadril ou ombro): parte de cima com a roupa, de baixo com pele/meia.</summary>
        private static Transform Limb(Transform parent, Vector3 pivot, float length, float thickness, Material upper, Material lower)
        {
            var j = new GameObject("Articulacao").transform;
            j.SetParent(parent, false);
            j.localPosition = pivot;
            Part(PrimitiveType.Capsule, j, new Vector3(0, -length * 0.25f, 0), new Vector3(thickness, length * 0.25f, thickness), upper);
            Part(PrimitiveType.Capsule, j, new Vector3(0, -length * 0.72f, 0), new Vector3(thickness * 0.9f, length * 0.25f, thickness * 0.9f), lower);
            return j;
        }

        public void Sync(PlayerState p, Vector3 renderPos, bool controlled)
        {
            transform.position = renderPos;
            var face = new Vector3(p.Facing.X, 0, p.Facing.Z);
            if (face.sqrMagnitude > 0.01f)
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(face), 0.35f);

            float speed = p.Vel.Length;
            _phase += Time.deltaTime * (3f + speed * 1.6f);
            float swing = Mathf.Clamp01(speed / 7f) * 45f;
            float s = Mathf.Sin(_phase) * swing;

            _body.localRotation = Quaternion.identity;
            _body.localPosition = Vector3.zero;
            switch (p.Anim)
            {
                case "slide":
                    _body.localRotation = Quaternion.Euler(-70f, 0, 0);
                    _body.localPosition = new Vector3(0, 0.35f, 0);
                    s = 0; break;
                case "getup":
                    _body.localRotation = Quaternion.Euler(-35f, 0, 0);
                    s = 0; break;
                case "shoot":
                case "pass":
                    _legR.localRotation = Quaternion.Euler(-60f, 0, 0);
                    _legL.localRotation = Quaternion.Euler(10f, 0, 0);
                    _armL.localRotation = Quaternion.Euler(0, 0, -30f);
                    _armR.localRotation = Quaternion.Euler(0, 0, 30f);
                    _marker.gameObject.SetActive(controlled);
                    return;
                case "celebrate":
                    _armL.localRotation = Quaternion.Euler(0, 0, -150f);
                    _armR.localRotation = Quaternion.Euler(0, 0, 150f);
                    _body.localPosition = new Vector3(0, Mathf.Abs(Mathf.Sin(Time.time * 8f)) * 0.25f, 0);
                    _legL.localRotation = _legR.localRotation = Quaternion.identity;
                    _marker.gameObject.SetActive(false);
                    return;
            }
            _legL.localRotation = Quaternion.Euler(s, 0, 0);
            _legR.localRotation = Quaternion.Euler(-s, 0, 0);
            _armL.localRotation = Quaternion.Euler(-s * 0.8f, 0, -6f);
            _armR.localRotation = Quaternion.Euler(s * 0.8f, 0, 6f);
            // Inclina o tronco para a frente na corrida.
            _body.localRotation = Quaternion.Euler(Mathf.Clamp(speed * 1.5f, 0, 12f), 0, 0);
            _marker.gameObject.SetActive(controlled);
        }
    }
}

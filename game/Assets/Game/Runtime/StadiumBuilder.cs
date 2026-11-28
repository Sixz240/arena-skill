using Futebol.Core;
using UnityEngine;

namespace Futebol.Runtime
{
    /// <summary>
    /// Monta o campo (grama listrada com as linhas oficiais), os gols e uma arquibancada genérica.
    /// Tudo gerado por código, sem nenhum modelo ou textura de outro jogo.
    /// </summary>
    public static class StadiumBuilder
    {
        private const int PxPerMeter = 14;

        public static GameObject Build(Transform parent)
        {
            var root = new GameObject("Estadio");
            root.transform.SetParent(parent, false);

            // Grama além do campo (área técnica).
            var outer = GameObject.CreatePrimitive(PrimitiveType.Plane);
            outer.name = "Entorno";
            outer.transform.SetParent(root.transform, false);
            outer.transform.localScale = new Vector3((Pitch.Length + 16f) / 10f, 1, (Pitch.Width + 16f) / 10f);
            outer.transform.localPosition = new Vector3(0, -0.01f, 0);
            outer.GetComponent<Renderer>().sharedMaterial = Materials.Color(Tokens.Pitch * 0.8f);
            Object.Destroy(outer.GetComponent<Collider>());

            var field = GameObject.CreatePrimitive(PrimitiveType.Plane);
            field.name = "Campo";
            field.transform.SetParent(root.transform, false);
            field.transform.localScale = new Vector3(Pitch.Length / 10f, 1, Pitch.Width / 10f);
            field.GetComponent<Renderer>().sharedMaterial = Materials.Textured(FieldTexture(), 0.05f);
            Object.Destroy(field.GetComponent<Collider>());

            BuildGoal(root.transform, 1);
            BuildGoal(root.transform, -1);
            BuildStands(root.transform);
            return root;
        }

        private static Texture2D FieldTexture()
        {
            int w = (int)(Pitch.Length * PxPerMeter), h = (int)(Pitch.Width * PxPerMeter);
            var px = new Color32[w * h];
            Color32 g1 = Tokens.Pitch;
            Color32 g2 = Color.Lerp(Tokens.Pitch, Color.black, 0.12f);
            Color32 line = Tokens.PitchLine;
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                px[y * w + x] = ((x / (PxPerMeter * 5)) % 2 == 0) ? g1 : g2; // faixas de 5 m

            void Dot(float mx, float mz)
            {
                int x = Mathf.RoundToInt((mx + Pitch.HalfLength) * PxPerMeter);
                int y = Mathf.RoundToInt((mz + Pitch.HalfWidth) * PxPerMeter);
                for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    int xx = x + dx, yy = y + dy;
                    if (xx >= 0 && yy >= 0 && xx < w && yy < h) px[yy * w + xx] = line;
                }
            }
            void Line(float x0, float z0, float x1, float z1)
            {
                float len = Mathf.Max(Mathf.Abs(x1 - x0), Mathf.Abs(z1 - z0));
                int n = Mathf.Max(2, (int)(len * PxPerMeter * 1.5f));
                for (int i = 0; i <= n; i++) Dot(Mathf.Lerp(x0, x1, i / (float)n), Mathf.Lerp(z0, z1, i / (float)n));
            }
            void Arc(float cx, float cz, float r, float a0, float a1)
            {
                int n = (int)(r * PxPerMeter * 8);
                for (int i = 0; i <= n; i++)
                {
                    float a = Mathf.Lerp(a0, a1, i / (float)n);
                    Dot(cx + Mathf.Cos(a) * r, cz + Mathf.Sin(a) * r);
                }
            }

            float L = Pitch.HalfLength - 0.1f, W = Pitch.HalfWidth - 0.1f;
            Line(-L, -W, L, -W); Line(-L, W, L, W); Line(-L, -W, -L, W); Line(L, -W, L, W);
            Line(0, -W, 0, W);
            Arc(0, 0, Pitch.CenterCircleRadius, 0, Mathf.PI * 2);
            Dot(0, 0);
            for (int side = -1; side <= 1; side += 2)
            {
                float gx = side * L;
                float bx = side * (Pitch.HalfLength - Pitch.BoxDepth);
                Line(gx, -Pitch.BoxHalfWidth, bx, -Pitch.BoxHalfWidth);
                Line(gx, Pitch.BoxHalfWidth, bx, Pitch.BoxHalfWidth);
                Line(bx, -Pitch.BoxHalfWidth, bx, Pitch.BoxHalfWidth);
                float sx = side * (Pitch.HalfLength - Pitch.SixYardDepth);
                float sw = Pitch.GoalHalfWidth + 5.5f;
                Line(gx, -sw, sx, -sw); Line(gx, sw, sx, sw); Line(sx, -sw, sx, sw);
                float px0 = side * (Pitch.HalfLength - Pitch.PenaltySpot);
                Dot(px0, 0);
                // Meia-lua: parte do círculo de 9,15 m fora da área.
                float half = Mathf.Acos((Pitch.BoxDepth - Pitch.PenaltySpot) / Pitch.CenterCircleRadius);
                float baseAngle = side > 0 ? Mathf.PI : 0f;
                Arc(px0, 0, Pitch.CenterCircleRadius, baseAngle - half, baseAngle + half);
                // Escanteios.
                Arc(gx, -W, 1f, side > 0 ? Mathf.PI / 2 : 0, side > 0 ? Mathf.PI : Mathf.PI / 2);
                Arc(gx, W, 1f, side > 0 ? Mathf.PI : -Mathf.PI / 2, side > 0 ? Mathf.PI * 1.5f : 0);
            }

            var tex = new Texture2D(w, h, TextureFormat.RGBA32, true) { name = "campo", anisoLevel = 4, wrapMode = TextureWrapMode.Clamp };
            tex.SetPixels32(px);
            tex.Apply(true, true);
            return tex;
        }

        private static void BuildGoal(Transform parent, int side)
        {
            var goal = new GameObject(side > 0 ? "Gol +X" : "Gol -X");
            goal.transform.SetParent(parent, false);
            var white = Materials.Color(Color.white, 0.6f);
            var net = Materials.Color(new Color(1, 1, 1, 1) * 0.85f, 0.1f);
            float x = side * Pitch.HalfLength;
            const float r = 0.06f;
            void Bar(Vector3 a, Vector3 b, Material m, float radius)
            {
                var c = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                Object.Destroy(c.GetComponent<Collider>());
                c.transform.SetParent(goal.transform, false);
                c.transform.position = (a + b) / 2f;
                c.transform.up = (b - a).normalized;
                c.transform.localScale = new Vector3(radius * 2, (b - a).magnitude / 2f, radius * 2);
                c.GetComponent<Renderer>().sharedMaterial = m;
            }
            float gw = Pitch.GoalHalfWidth, gh = Pitch.GoalHeight, depth = 2f * side;
            Bar(new Vector3(x, 0, -gw), new Vector3(x, gh, -gw), white, r);
            Bar(new Vector3(x, 0, gw), new Vector3(x, gh, gw), white, r);
            Bar(new Vector3(x, gh, -gw), new Vector3(x, gh, gw), white, r);
            // Rede: malha simples de barras finas.
            for (int i = 0; i <= 12; i++)
            {
                float z = Mathf.Lerp(-gw, gw, i / 12f);
                Bar(new Vector3(x, gh, z), new Vector3(x + depth, 0, z), net, 0.012f);
            }
            for (int i = 0; i <= 5; i++)
            {
                float t = i / 5f;
                Bar(new Vector3(x + depth * t, gh * (1 - t), -gw), new Vector3(x + depth * t, gh * (1 - t), gw), net, 0.012f);
            }
        }

        private static void BuildStands(Transform parent)
        {
            var stands = new GameObject("Arquibancada");
            stands.transform.SetParent(parent, false);
            var concrete = Materials.Color(Tokens.Surface2);
            var crowdA = Materials.Color(Tokens.Tile);
            var crowdB = Materials.Color(Tokens.Surface);
            void Block(Vector3 pos, Vector3 size, Material m)
            {
                var b = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Object.Destroy(b.GetComponent<Collider>());
                b.transform.SetParent(stands.transform, false);
                b.transform.localPosition = pos;
                b.transform.localScale = size;
                b.GetComponent<Renderer>().sharedMaterial = m;
            }
            // Degraus nos quatro lados (sem a lateral da câmera, para não tampar a visão).
            for (int step = 0; step < 6; step++)
            {
                float off = 10f + step * 2.2f, hgt = 1f + step * 1.6f;
                var m = step % 2 == 0 ? crowdA : crowdB;
                Block(new Vector3(0, hgt / 2, Pitch.HalfWidth + off), new Vector3(Pitch.Length + 30, hgt, 2.2f), m);
                Block(new Vector3(Pitch.HalfLength + off, hgt / 2, 0), new Vector3(2.2f, hgt, Pitch.Width + 30), m);
                Block(new Vector3(-Pitch.HalfLength - off, hgt / 2, 0), new Vector3(2.2f, hgt, Pitch.Width + 30), m);
            }
            Block(new Vector3(0, 0.4f, -Pitch.HalfWidth - 8f), new Vector3(Pitch.Length + 30, 0.8f, 1.5f), concrete);
        }
    }
}

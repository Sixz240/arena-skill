using System.Collections.Generic;
using Futebol.Core;
using UnityEngine;

namespace Futebol.Runtime
{
    /// <summary>
    /// Desenha a textura da camisa a partir das cores e do padrão (listras, faixa, tricolor...).
    /// Nada é copiado de jogo nenhum: é um desenho gerado na hora com as cores do clube.
    /// </summary>
    public static class KitTextures
    {
        private const int Size = 128;
        private static readonly Dictionary<string, Texture2D> Cache = new Dictionary<string, Texture2D>();

        public static Texture2D Shirt(KitColors kit)
        {
            string key = $"{kit.Pattern}|{kit.Primary}|{kit.Secondary}|{kit.Tertiary}";
            if (Cache.TryGetValue(key, out var cached)) return cached;
            var p = KitDatabase.ParseColor(kit.Primary, Color.white);
            var s = KitDatabase.ParseColor(kit.Secondary, Color.black);
            var t = KitDatabase.ParseColor(kit.Tertiary, s);
            var px = new Color32[Size * Size];
            for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
            {
                float u = x / (float)Size, v = y / (float)Size;
                Color c = p;
                switch (kit.Pattern)
                {
                    case "v_stripes":
                        c = ((int)(u * 10f)) % 2 == 0 ? p : s; break;
                    case "h_hoops":
                        c = ((int)(v * 8f)) % 2 == 0 ? p : s; break;
                    case "sash":
                        c = Mathf.Abs(u - v) < 0.12f ? s : p; break;
                    case "halves":
                        c = u < 0.5f ? p : s; break;
                    case "chest_band":
                        // Faixa no peito; com terceira cor vira faixa dupla (ex.: São Paulo).
                        if (v > 0.55f && v < 0.63f) c = s;
                        else if (kit.Tertiary != null && v >= 0.63f && v < 0.71f) c = t;
                        break;
                    case "tricolor_stripes":
                    {
                        int band = ((int)(u * 12f)) % 3;
                        c = band == 0 ? p : band == 1 ? s : t; break;
                    }
                    case "sleeves":
                        // O tronco é liso; as mangas (braços) usam a cor secundária, ver PlayerView.
                        c = p; break;
                }
                px[y * Size + x] = c;
            }
            var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Bilinear, name = "camisa " + key };
            tex.SetPixels32(px);
            tex.Apply(true, true);
            Cache[key] = tex;
            return tex;
        }
    }
}

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Futebol.Core;
using UnityEngine;
using UnityEngine.Networking;

namespace Futebol.Runtime
{
    /// <summary>
    /// Escudos dos clubes: busca a imagem principal da página do clube na Wikipédia (API pública
    /// MediaWiki, miniatura PNG), guarda em disco e usa o cache nas próximas vezes.
    /// Se não achar, ou a imagem não parecer um escudo, desenha um escudo genérico com as cores do time.
    /// Uso pessoal: para publicar o jogo, troque por escudos próprios (ver replica/recon.md).
    /// </summary>
    public sealed class CrestService : MonoBehaviour
    {
        public enum Kind { Loading, Crest, Colors }
        public sealed class Entry { public Texture2D Texture; public Kind Kind; }

        private const string Api = "https://en.wikipedia.org/w/api.php?action=query&format=json&prop=pageimages&piprop=thumbnail%7Cname&pithumbsize=160&redirects=1&titles=";
        private const string UserAgent = "FutebolPessoal/0.1 (jogo pessoal; uso não comercial)";
        // Arquivos que não são escudo: bandeira, brasão do país, uniforme, estádio, foto.
        private static readonly Regex NotACrest = new Regex(@"flag|coat_of_arms|kit_|kit\.|stadium|estadio|squad|team_photo|map|\.jpe?g$", RegexOptions.IgnoreCase);

        private readonly Dictionary<string, Entry> _entries = new Dictionary<string, Entry>();
        private readonly Queue<TeamInfo> _queue = new Queue<TeamInfo>();
        private bool _busy;

        private static string CacheDir => Path.Combine(Application.persistentDataPath, "escudos");

        public Entry Get(TeamInfo team)
        {
            if (_entries.TryGetValue(team.Id, out var e)) return e;
            e = new Entry { Texture = FallbackCrest(team), Kind = Kind.Loading };
            _entries[team.Id] = e;
            var cached = Path.Combine(CacheDir, team.Id + ".png");
            if (File.Exists(cached))
            {
                var tex = new Texture2D(2, 2);
                if (tex.LoadImage(File.ReadAllBytes(cached))) { e.Texture = tex; e.Kind = Kind.Crest; return e; }
            }
            if (!string.IsNullOrEmpty(team.WikiPage)) { _queue.Enqueue(team); if (!_busy) StartCoroutine(Worker()); }
            else e.Kind = Kind.Colors;
            return e;
        }

        private IEnumerator Worker()
        {
            _busy = true;
            while (_queue.Count > 0)
            {
                var team = _queue.Dequeue();
                yield return Fetch(team);
                yield return new WaitForSecondsRealtime(0.25f); // um pedido por vez, sem rajada
            }
            _busy = false;
        }

        private IEnumerator Fetch(TeamInfo team)
        {
            var entry = _entries[team.Id];
            using (var req = UnityWebRequest.Get(Api + UnityWebRequest.EscapeURL(team.WikiPage)))
            {
                req.SetRequestHeader("Api-User-Agent", UserAgent);
                req.timeout = 10;
                yield return req.SendWebRequest();
                if (req.result != UnityWebRequest.Result.Success) { entry.Kind = Kind.Colors; yield break; }
                var json = req.downloadHandler.text;
                var name = Regex.Match(json, "\"pageimage\"\\s*:\\s*\"([^\"]+)\"").Groups[1].Value;
                var src = Regex.Match(json, "\"source\"\\s*:\\s*\"([^\"]+)\"").Groups[1].Value.Replace("\\/", "/");
                if (string.IsNullOrEmpty(src) || NotACrest.IsMatch(name)) { entry.Kind = Kind.Colors; yield break; }
                using (var img = UnityWebRequestTexture.GetTexture(src))
                {
                    img.SetRequestHeader("Api-User-Agent", UserAgent);
                    img.timeout = 10;
                    yield return img.SendWebRequest();
                    if (img.result != UnityWebRequest.Result.Success) { entry.Kind = Kind.Colors; yield break; }
                    var tex = DownloadHandlerTexture.GetContent(img);
                    entry.Texture = tex;
                    entry.Kind = Kind.Crest;
                    try
                    {
                        Directory.CreateDirectory(CacheDir);
                        File.WriteAllBytes(Path.Combine(CacheDir, team.Id + ".png"), tex.EncodeToPNG());
                    }
                    catch (Exception ex) { Debug.LogWarning("Não deu para salvar o escudo no cache: " + ex.Message); }
                }
            }
        }

        /// <summary>Escudo genérico: formato de escudo com a cor principal e a secundária do uniforme 1.</summary>
        public static Texture2D FallbackCrest(TeamInfo team)
        {
            const int s = 64;
            var a = KitDatabase.ParseColor(team.Home.Primary, Color.white);
            var b = KitDatabase.ParseColor(team.Home.Secondary, Color.black);
            var px = new Color32[s * s];
            for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                float u = (x - s / 2f) / (s / 2f), v = y / (float)s;
                // Topo reto, laterais que afinam até a ponta de baixo.
                float halfW = v > 0.45f ? 0.9f : 0.9f * Mathf.Sqrt(v / 0.45f);
                bool inside = Mathf.Abs(u) <= halfW && v < 0.95f;
                bool border = inside && Mathf.Abs(u) > halfW - 0.12f;
                px[y * s + x] = !inside ? new Color32(0, 0, 0, 0) : border ? (Color32)b : (u < 0 ? (Color32)a : (Color32)Color.Lerp(a, b, 0.5f));
            }
            var tex = new Texture2D(s, s, TextureFormat.RGBA32, false) { name = "escudo genérico " + team.Id };
            tex.SetPixels32(px);
            tex.Apply();
            return tex;
        }
    }
}

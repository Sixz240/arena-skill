using System;
using System.Collections.Generic;
using Futebol.Core;
using UnityEngine;

namespace Futebol.Runtime
{
    /// <summary>
    /// Lê os arquivos Resources/data/kits-*.json (copiados de replica/data) e monta os TeamInfo.
    /// É a camada de dados provisória da fatia vertical; o SQLite (data.db) entra depois com a mesma interface.
    /// </summary>
    public static class KitDatabase
    {
        [Serializable] private class KitJson { public string pattern, primary, secondary, tertiary, shorts, socks, number; }
        [Serializable] private class TeamJson { public string id, name, @short, wiki_page, confidence; public KitJson home, away; }
        [Serializable] private class FileJson { public string league, season; public TeamJson[] teams; }

        public static readonly string[] Files =
        {
            "kits-brasileirao-2026", "kits-laliga-2026-27", "kits-premier-league-2026-27",
            "kits-bundesliga-2026-27", "kits-mls-2026",
        };

        private static Dictionary<string, TeamInfo> _teams;
        private static Dictionary<string, string> _leagueOf;

        public static IReadOnlyDictionary<string, TeamInfo> Teams { get { Load(); return _teams; } }

        public static string LeagueOf(string teamId) { Load(); return _leagueOf.TryGetValue(teamId, out var l) ? l : null; }

        public static TeamInfo Get(string id)
        {
            Load();
            if (!_teams.TryGetValue(id, out var t)) throw new KeyError(id);
            return Clone(t);
        }

        private sealed class KeyError : Exception { public KeyError(string id) : base("Time não encontrado: " + id) { } }

        private static void Load()
        {
            if (_teams != null) return;
            _teams = new Dictionary<string, TeamInfo>();
            _leagueOf = new Dictionary<string, string>();
            foreach (var f in Files)
            {
                var asset = Resources.Load<TextAsset>("data/" + f);
                if (asset == null) { Debug.LogWarning("Arquivo de uniformes não encontrado: " + f); continue; }
                var file = JsonUtility.FromJson<FileJson>(asset.text);
                if (file?.teams == null) continue;
                foreach (var t in file.teams)
                {
                    var info = new TeamInfo
                    {
                        Id = t.id,
                        Name = t.name,
                        ShortName = t.@short,
                        WikiPage = t.wiki_page,
                        Home = ToKit(t.home),
                        Away = ToKit(t.away),
                    };
                    _teams[t.id] = info;
                    _leagueOf[t.id] = file.league;
                }
            }
        }

        private static KitColors ToKit(KitJson k)
        {
            if (k == null) return new KitColors();
            return new KitColors
            {
                Pattern = string.IsNullOrEmpty(k.pattern) ? "plain" : k.pattern,
                Primary = k.primary ?? "#FFFFFF",
                Secondary = k.secondary ?? "#000000",
                Tertiary = string.IsNullOrEmpty(k.tertiary) ? null : k.tertiary,
                Shorts = k.shorts ?? "#FFFFFF",
                Socks = k.socks ?? "#FFFFFF",
                Number = k.number ?? "#000000",
            };
        }

        private static TeamInfo Clone(TeamInfo t) => new TeamInfo
        {
            Id = t.Id, Name = t.Name, ShortName = t.ShortName, WikiPage = t.WikiPage,
            RatingAttack = t.RatingAttack, RatingMidfield = t.RatingMidfield, RatingDefense = t.RatingDefense,
            Home = t.Home, Away = t.Away,
        };

        public static Color ParseColor(string hex, Color fallback)
        {
            return !string.IsNullOrEmpty(hex) && ColorUtility.TryParseHtmlString(hex, out var c) ? c : fallback;
        }

        /// <summary>
        /// Escolhe o uniforme do visitante: o de fora quando as camisas de casa ficam parecidas demais.
        /// </summary>
        public static KitColors AwayKitFor(TeamInfo home, TeamInfo away)
        {
            var a = ParseColor(home.Home.Primary, Color.white);
            var b = ParseColor(away.Home.Primary, Color.white);
            var b2 = ParseColor(away.Home.Secondary, Color.black);
            float d = ColorDistance(a, b);
            if (away.Home.Pattern != "plain") d = Mathf.Max(d, ColorDistance(a, b2) * 0.7f);
            return d < 0.35f ? away.Away : away.Home;
        }

        private static float ColorDistance(Color a, Color b)
        {
            // Distância perceptual simples (pesos do olho humano).
            float dr = a.r - b.r, dg = a.g - b.g, db = a.b - b.b;
            return Mathf.Sqrt(0.3f * dr * dr + 0.59f * dg * dg + 0.11f * db * db);
        }
    }
}

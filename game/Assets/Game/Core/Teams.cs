using System;
using System.Collections.Generic;

namespace Futebol.Core
{
    public enum Position { GK, RB, CB, LB, DM, CM, AM, RW, LW, ST }

    /// <summary>Cores de um uniforme (texto #RRGGBB), vindas dos arquivos kits-*.json.</summary>
    public sealed class KitColors
    {
        public string Pattern = "plain";
        public string Primary = "#FFFFFF";
        public string Secondary = "#000000";
        public string Tertiary;
        public string Shorts = "#FFFFFF";
        public string Socks = "#FFFFFF";
        public string Number = "#000000";
    }

    public sealed class PlayerInfo
    {
        public string Id;
        public string Name;
        public string DisplayName;
        public int ShirtNumber;
        public Position Position;
        public int Pace, Shooting, Passing, Dribbling, Defending, Physical, Goalkeeping;
        public int Overall => Position == Position.GK
            ? (Goalkeeping * 6 + Physical * 2 + Passing * 2) / 10
            : (Pace * 2 + Shooting * 2 + Passing * 2 + Dribbling * 2 + Defending + Physical) / 10;
    }

    public sealed class TeamInfo
    {
        public string Id;
        public string Name;
        public string ShortName;
        public string WikiPage;
        public int RatingAttack = 70, RatingMidfield = 70, RatingDefense = 70;
        public KitColors Home = new KitColors();
        public KitColors Away = new KitColors();
        public List<PlayerInfo> Players = new List<PlayerInfo>();
        public int Strength => (RatingAttack + RatingMidfield + RatingDefense) / 3;
    }

    /// <summary>
    /// Elencos provisórios para a fatia vertical: 11 titulares com atributos derivados da força do time.
    /// Os nomes reais entram na etapa de dados (Wikipédia + editor); aqui são só "Camisa N".
    /// </summary>
    public static class SquadGenerator
    {
        // 4-4-2: goleiro, laterais, zagueiros, meias, atacantes.
        private static readonly Position[] Lineup =
        {
            Position.GK, Position.RB, Position.CB, Position.CB, Position.LB,
            Position.RW, Position.CM, Position.CM, Position.LW, Position.ST, Position.ST
        };
        private static readonly int[] Numbers = { 1, 2, 3, 4, 6, 7, 5, 8, 11, 9, 10 };

        public static void FillStarters(TeamInfo team, int seed)
        {
            var rng = new Rng(seed);
            team.Players.Clear();
            for (int i = 0; i < Lineup.Length; i++)
            {
                var pos = Lineup[i];
                int baseAtt = team.RatingAttack, baseMid = team.RatingMidfield, baseDef = team.RatingDefense;
                int J(int v) => Math.Clamp(v + rng.Range(-6, 7), 30, 95);
                var p = new PlayerInfo
                {
                    Id = $"{team.Id}-{Numbers[i]}",
                    ShirtNumber = Numbers[i],
                    Name = $"Camisa {Numbers[i]}",
                    DisplayName = Numbers[i].ToString(),
                    Position = pos,
                    Pace = J((baseAtt + baseMid) / 2),
                    Shooting = J(pos == Position.ST || pos == Position.AM ? baseAtt + 4 : baseAtt - 12),
                    Passing = J(baseMid),
                    Dribbling = J(pos == Position.RW || pos == Position.LW || pos == Position.ST ? baseAtt + 2 : baseMid - 6),
                    Defending = J(pos == Position.CB || pos == Position.RB || pos == Position.LB ? baseDef + 4 : baseDef - 18),
                    Physical = J((baseDef + baseMid) / 2),
                    Goalkeeping = pos == Position.GK ? J(baseDef + 2) : 10,
                };
                team.Players.Add(p);
            }
        }
    }
}

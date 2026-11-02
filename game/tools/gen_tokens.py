#!/usr/bin/env python3
"""Gera Assets/Game/Runtime/Tokens.cs a partir de replica/design/tokens.json.
As telas usam Tokens.X, nunca um hex solto. Rodar de novo sempre que tokens.json mudar:
    python3 game/tools/gen_tokens.py
"""
import json, os, re
here = os.path.dirname(os.path.abspath(__file__))
src = os.path.join(here, "..", "..", "replica", "design", "tokens.json")
out = os.path.join(here, "..", "Assets", "Game", "Runtime", "Tokens.cs")
t = json.load(open(src, encoding="utf-8"))

def pascal(name):
    return "".join(p.capitalize() for p in re.split(r"[-_]", name))

def color(hexv):
    h = hexv.lstrip("#")
    r, g, b = (int(h[i:i + 2], 16) for i in (0, 2, 4))
    return f"new Color32({r}, {g}, {b}, 255)"

lines = [
    "// Gerado por game/tools/gen_tokens.py a partir de replica/design/tokens.json. Não editar à mão.",
    "using UnityEngine;",
    "",
    "namespace Futebol.Runtime",
    "{",
    "    public static class Tokens",
    "    {",
]
for k, v in t["color"].items():
    lines.append(f"        public static readonly Color {pascal(k)} = {color(v)};")
for k, v in t["type"].items():
    lines.append(f"        public const int Font{pascal(k)} = {v['size']};")
for i, v in enumerate(t["space"]):
    lines.append(f"        public const int Space{i} = {v};")
for k, v in t["radius"].items():
    lines.append(f"        public const int Radius{pascal(k)} = {v};")
lay = t.get("layout", {})
lines.append(f"        public const int TouchTargetMin = {lay.get('touch_target_min', 48)};")
lines.append(f"        public const int TopBarHeight = {lay.get('top_bar_height', 64)};")
lines.append(f"        public const int HudMargin = {lay.get('hud_margin', 24)};")
lines.append("        /// <summary>Resolução de referência: a interface escala pela altura da tela.</summary>")
lines.append("        public const float ReferenceHeight = 1080f;")
lines += ["    }", "}", ""]
open(out, "w", encoding="utf-8").write("\n".join(lines))
print("ok:", os.path.relpath(out))

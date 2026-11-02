using UnityEngine;

namespace Futebol.Runtime
{
    /// <summary>Cria materiais em tempo de execução, com o shader Lit do URP (ou o padrão, fora do URP).</summary>
    public static class Materials
    {
        private static Shader _lit;
        private static Shader _unlit;

        // Os materiais em Resources/ShaderRefs (criados por Futebol > Configurar projeto) garantem que
        // os shaders entram no APK; Shader.Find sozinho falha se nenhum asset usa o shader.
        private static Shader Lit => _lit != null ? _lit : (_lit = Find("ShaderRefs/Lit", "Universal Render Pipeline/Lit", "Standard"));
        private static Shader Unlit => _unlit != null ? _unlit : (_unlit = Find("ShaderRefs/Unlit", "Universal Render Pipeline/Unlit", "Unlit/Texture"));

        private static Shader Find(string resource, string name, string fallback)
        {
            var m = Resources.Load<Material>(resource);
            if (m != null && m.shader != null) return m.shader;
            var s = Shader.Find(name);
            return s != null ? s : Shader.Find(fallback);
        }

        public static Material Color(Color c, float smoothness = 0.2f)
        {
            var m = new Material(Lit);
            SetColor(m, c);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smoothness);
            if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", smoothness);
            m.enableInstancing = true;
            return m;
        }

        public static Material Textured(Texture tex, float smoothness = 0.15f, Vector2? tiling = null)
        {
            var m = Color(UnityEngine.Color.white, smoothness);
            if (m.HasProperty("_BaseMap")) { m.SetTexture("_BaseMap", tex); if (tiling.HasValue) m.SetTextureScale("_BaseMap", tiling.Value); }
            if (m.HasProperty("_MainTex")) { m.SetTexture("_MainTex", tex); if (tiling.HasValue) m.SetTextureScale("_MainTex", tiling.Value); }
            return m;
        }

        public static Material UnlitTexture(Texture tex)
        {
            var m = new Material(Unlit);
            if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", tex);
            if (m.HasProperty("_MainTex")) m.SetTexture("_MainTex", tex);
            return m;
        }

        public static void SetColor(Material m, Color c)
        {
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            if (m.HasProperty("_Color")) m.SetColor("_Color", c);
        }
    }
}

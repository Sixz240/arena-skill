using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Futebol.EditorTools
{
    /// <summary>
    /// Menu "Futebol": deixa o projeto pronto para Android 64 bits e gera o APK.
    /// </summary>
    public static class ProjectSetup
    {
        private const string ScenePath = "Assets/Game/Scenes/Partida.unity";

        [MenuItem("Futebol/1. Configurar projeto (Android 64 bits)")]
        public static void Configure()
        {
            // Cena da partida (vazia: o MatchBootstrap monta tudo ao abrir).
            Directory.CreateDirectory("Assets/Game/Scenes");
            if (!File.Exists(ScenePath))
            {
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                var cam = new GameObject("Camera", typeof(Camera), typeof(AudioListener)) { tag = "MainCamera" };
                cam.transform.position = new Vector3(0, 19, -68);
                EditorSceneManager.SaveScene(scene, ScenePath);
            }
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };

            // Materiais de referência para os shaders entrarem no build.
            Directory.CreateDirectory("Assets/Game/Resources/ShaderRefs");
            MakeShaderRef("Assets/Game/Resources/ShaderRefs/Lit.mat", "Universal Render Pipeline/Lit", "Standard");
            MakeShaderRef("Assets/Game/Resources/ShaderRefs/Unlit.mat", "Universal Render Pipeline/Unlit", "Unlit/Texture");

            // Android: IL2CPP + só ARM64 = APK 64 bits.
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.productName = "Futebol";
            PlayerSettings.companyName = "Pessoal";
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "com.pessoal.futebol");
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.Android.renderOutsideSafeArea = true;

            AssetDatabase.SaveAssets();
            EditorSceneManager.OpenScene(ScenePath);
            Debug.Log("Futebol: projeto configurado. Aperte Play para testar no editor (o mouse faz o papel do dedo).");
        }

        private static void MakeShaderRef(string path, string shader, string fallback)
        {
            if (File.Exists(path)) return;
            var s = Shader.Find(shader) ?? Shader.Find(fallback);
            if (s == null) { Debug.LogWarning("Shader não encontrado: " + shader); return; }
            AssetDatabase.CreateAsset(new Material(s), path);
        }

        [MenuItem("Futebol/2. Gerar APK (64 bits)")]
        public static void BuildApk()
        {
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
            {
                if (!EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android))
                {
                    EditorUtility.DisplayDialog("Futebol", "Não deu para trocar a plataforma para Android. Instale o módulo Android Build Support no Unity Hub.", "OK");
                    return;
                }
            }
            Configure();
            Directory.CreateDirectory("Build");
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = "Build/Futebol.apk",
                target = BuildTarget.Android,
                options = BuildOptions.None,
            });
            var ok = report.summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded;
            EditorUtility.DisplayDialog("Futebol", ok
                ? "APK gerado em Build/Futebol.apk (64 bits, ARM64). Copie para o celular e instale."
                : "O build falhou. Veja o Console para o motivo.", "OK");
            if (ok) EditorUtility.RevealInFinder("Build/Futebol.apk");
        }
    }
}

using UnityEngine;
using UnityEngine.SceneManagement;

namespace Futebol.Runtime
{
    /// <summary>
    /// Ponto de entrada: na cena "Partida", cria o MatchRunner se ele ainda não existe.
    /// Assim a cena pode ficar vazia; o menu Futebol > Configurar projeto cria essa cena.
    /// </summary>
    public static class MatchBootstrap
    {
        public const string SceneName = "Partida";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void OnLoad()
        {
            if (SceneManager.GetActiveScene().name != SceneName) return;
            if (Object.FindFirstObjectByType<MatchRunner>() != null) return;
            new GameObject("Partida").AddComponent<MatchRunner>();
        }
    }
}

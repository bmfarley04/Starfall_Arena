using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Keeps a gameplay scene warm in the background during menu flows so activation is fast later.
/// This is intentionally local-flow only; network scene loading still goes through NGO.
/// </summary>
public sealed class GameplayScenePreloader : MonoBehaviour
{
    public static GameplayScenePreloader Instance { get; private set; }

    private AsyncOperation _preloadOperation;
    private string _preloadedSceneName;

    public bool HasPendingPreload => _preloadOperation != null;

    public bool IsReady(string sceneName)
    {
        return
            _preloadOperation != null &&
            !string.IsNullOrEmpty(sceneName) &&
            string.Equals(_preloadedSceneName, sceneName, System.StringComparison.Ordinal) &&
            _preloadOperation.progress >= 0.89f;
    }

    public static GameplayScenePreloader GetOrCreate()
    {
        if (Instance != null)
        {
            return Instance;
        }

        GameObject root = new GameObject(nameof(GameplayScenePreloader));
        Instance = root.AddComponent<GameplayScenePreloader>();
        DontDestroyOnLoad(root);
        return Instance;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public void BeginPreload(string sceneName)
    {
        if (string.IsNullOrWhiteSpace(sceneName))
        {
            return;
        }

        Scene activeScene = SceneManager.GetActiveScene();
        if (activeScene.IsValid() && string.Equals(activeScene.name, sceneName, System.StringComparison.Ordinal))
        {
            return;
        }

        if (_preloadOperation != null)
        {
            if (string.Equals(_preloadedSceneName, sceneName, System.StringComparison.Ordinal))
            {
                return;
            }

            Debug.LogWarning(
                $"GameplayScenePreloader: Cannot preload '{sceneName}' because '{_preloadedSceneName}' is already pending.");
            return;
        }

        _preloadedSceneName = sceneName;
        _preloadOperation = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
        if (_preloadOperation == null)
        {
            _preloadedSceneName = null;
            return;
        }

        _preloadOperation.allowSceneActivation = false;
    }

    public IEnumerator ActivateOrLoad(string sceneName)
    {
        if (string.IsNullOrWhiteSpace(sceneName))
        {
            yield break;
        }

        if (_preloadOperation != null &&
            string.Equals(_preloadedSceneName, sceneName, System.StringComparison.Ordinal))
        {
            _preloadOperation.allowSceneActivation = true;

            while (!_preloadOperation.isDone)
            {
                yield return null;
            }

            _preloadOperation = null;
            _preloadedSceneName = null;
            yield break;
        }

        AsyncOperation loadOperation = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
        if (loadOperation == null)
        {
            yield break;
        }

        while (!loadOperation.isDone)
        {
            yield return null;
        }
    }
}

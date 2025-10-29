using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

public class DDOLUiRoot : MonoBehaviour
{
    private static DDOLUiRoot _instance;
    private IRebindOnSceneChange[] _rebinders;

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
        DontDestroyOnLoad(gameObject);

        // 자식들에서 한번 수집
        _rebinders = GetComponentsInChildren<IRebindOnSceneChange>(true);
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
        // 첫 진입 씬에서도 즉시 1회
        RebindAll();
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene s, LoadSceneMode m) => RebindAll();

    private void RebindAll()
    {
        if (_rebinders == null || _rebinders.Length == 0)
            _rebinders = GetComponentsInChildren<IRebindOnSceneChange>(true);

        foreach (var r in _rebinders)
            r?.RebindSceneRefs();
    }
}

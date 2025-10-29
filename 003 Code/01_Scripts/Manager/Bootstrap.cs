using UnityEngine;
using UnityEngine.SceneManagement;

public class Bootstrap : MonoBehaviour
{
    [Header("선택: 부팅 후 자동으로 로드할 씬 이름")]
    [SerializeField] private string nextSceneName; // 예: "Town" 또는 "Title"

    private static bool s_initialized = false; // 중복 초기화 방지

    private void Awake()
    {
        if (s_initialized)
        {
            Destroy(gameObject);
            return;
        }

        s_initialized = true;
        DontDestroyOnLoad(gameObject);

        if (!string.IsNullOrEmpty(nextSceneName))
        {
            SceneManager.LoadScene(nextSceneName, LoadSceneMode.Single);
        }
    }
}

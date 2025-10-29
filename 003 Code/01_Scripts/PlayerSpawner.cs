using UnityEngine;

public class PlayerSpawner : MonoBehaviour
{
    public static PlayerSpawner Instance;

    [SerializeField] private GameObject playerPrefab;
    [SerializeField] private Transform player;
    [SerializeField] private GameObject playerSpawnPoint;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public void EnsurePlayerInTown()
    {
        if (player == null && playerPrefab != null)
        {
            Vector3 spawnPos = playerSpawnPoint.transform.localPosition;
            var go = Instantiate(playerPrefab, spawnPos, Quaternion.identity);
            player = go.transform;
        }
        else if (player != null)
        {
            player.transform.position = Vector3.zero;
        }
    }
}

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem; // Input System
// using NUnit.Framework; // 런타임 빌드에 불필요하면 제거 권장

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    // 결과창
    public struct RunResultLine
    {
        public OreData ore;
        public int count;
        public int price;
        public int total;
    }

    // 상태
    public enum GameState { InLobby, InTown, EnteringDungeon, InDungeon, RunComplete, Result, ReturnToTown, EnterToBoss, InBoss, Dead }
    public GameState State { get; private set; } = GameState.InLobby;
    public enum RunEndReason { Success, Giveup, Death }
    private RunEndReason _lastReason;

    public int clearedFloorThisRun = 0;
    private int earnedMoney = 0;

    public bool IsGamePaused { get; private set; }
    public bool IsGameOver { get; private set; }
    public bool IsBossDead { get; private set; }

    private float dungeonStartTime;

    [Header("씬 이름")]
    [SerializeField] private string lobbyScene = "MainMenuScene";
    [SerializeField] private string townScene = "TownScene";
    [SerializeField] private string dungeonGenerationScene = "DungeonGenerationScene";
    [SerializeField] private string bossScene = "Boss";

    [Header("플레이어 스폰")]
    [SerializeField] private GameObject playerPrefab;
    [SerializeField] private Transform player;
    private Transform _spawnPoint;
    [SerializeField] private string spawnPointTag = "PlayerSpawnPoint"; // 태그 이름

    [Header("참조할 SO")]
    [SerializeField] private RunInventory runInventory;
    [SerializeField] private PricingService pricingService;
    // [SerializeField] private EconomyService economyService;

    [Header("Result/UI")]
    [SerializeField] private ResultUIManager resultUI;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }
        DontDestroyOnLoad(gameObject);
        Time.timeScale = 1f;

        // 씬 로드 콜백 등록
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // 씬이 바뀔 때마다 태그로 스폰지점을 다시 찾는다
        RebindSpawnPoint();

        if (scene.name == bossScene)
        {
            State = GameState.InBoss;
            OnDungeonEnter();
        }
        else if (scene.name == dungeonGenerationScene)
        {
            State = GameState.InDungeon;
            OnDungeonEnter();
        }
        else if (scene.name == townScene)
        {
            State = GameState.InTown;
            EnsurePlayerInTown(); // ★ 스폰 포인트 재바인딩 후 호출
        }
        else if (scene.name == lobbyScene)
        {
            State = GameState.InLobby;
        }
    }

    private void Update()
    {
        // 게임 오버 상태에서 아무 입력 감지 → 로비로 리셋 복귀
        if (IsGameOver  && AnyInputPressed())
        {
            ResetAndReturnToLobby();
        }

        //// R 키를 누르면 즉시 메인 메뉴(로비)로 재시작
        //if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
        //{
            
        //    ResetAndReturnToLobby();
        //}
    }

    public void LobbyToTown()
    {
        if (State != GameState.InLobby) return;

        ResetRunData();
        IsGameOver = false;
        ResumeGame();

        SceneManager.LoadScene(townScene);
        State = GameState.InTown;
    }

    // 로비 UI의 "게임 시작" 버튼에서 이 함수 연결
    public void StartGameFromTown()
    {
        // 로비 상태에서만 시작
        if (State != GameState.InTown) return;

        // 런/플래그 초기화
        ResetRunData();
        IsGameOver = false;
        ResumeGame();

        EnterDungeon();
    }

    public void EnterDungeon()
    {
        if (State != GameState.InTown) return;

        State = GameState.EnteringDungeon;
        runInventory.Clear();
        // SceneManager.LoadScene(dungeonGenerationScene);
        LoadSceneManager.Instance.LoadSceneWaitGenerator(dungeonGenerationScene);

        DungeonRunBuffManager.Instance.ApplyAllTo(player.gameObject);

        State = GameState.InDungeon;
        OnDungeonEnter();
    }

    public void EndRun(RunEndReason reason)
    {
        if (State != GameState.InDungeon && State != GameState.InBoss) return;

        State = GameState.RunComplete;
        _lastReason = reason;
        float playTime = GetDungeonPlayTime();

        earnedMoney = (reason == RunEndReason.Death) ? 0 : pricingService.EvaluateTotal(runInventory.Stacks, clearedFloorThisRun);

        OpenResultUI();
    }

    public void EnterBoss()
    {
        Debug.Log("보스전 진입");

        // 상태가 InTown이 아니라면 리턴
        if (State != GameState.InDungeon)
        {
            return;
        }
        State = GameState.EnterToBoss;

        // LoadSceneManager의 전역 함수로 씬 전환 호출
        LoadSceneManager.Instance.LoadScene(bossScene);
        // SceneManager.LoadScene(bossScene);
    }

    private void OpenResultUI()
    {
        if (resultUI != null)
        {
            resultUI.gameObject.SetActive(true);
            resultUI.Show();
        }

        PauseGame();
        State = GameState.Result;
    }

    // 결과창에서 "확인" 버튼이 눌렸을 때 연결
    public void OnResultConfirmed()
    {
        var eco = EconomyService.Instance ?? FindObjectOfType<EconomyService>();
        if (eco == null) { Debug.LogError("[GM] EconomyService 없음"); return; }
        if (runInventory == null) { Debug.LogError("[GM] runInventory 미할당"); return; }

        if (_lastReason == RunEndReason.Death)
        {
            runInventory.Clear();
        }
        else
        {
            EconomyService.Instance.AddMoney(earnedMoney);
            runInventory.Clear();
        }
        // economyService.Save();

        ReturnToTown();
    }

    private void ReturnToTown()
    {
        State = GameState.ReturnToTown;
        // SceneManager.LoadScene(townScene);
        LoadSceneManager.Instance.LoadScene(townScene);
        DungeonRunBuffManager.Instance.ClearAfterRun(player.gameObject);
        State = GameState.InTown;
        
        // 던전 → 마을 이동 시 인벤토리 클리어
        var inv = FindAnyObjectByType<Inventory>();
        if (inv != null)
        {
            inv.ResetAtferRun();
        }

        ResumeGame();
    }

    public void GameOver()
    {
        // 게임 오버 UI만 띄우고, 입력 대기 → Update()에서 리셋 처리
        State = GameManager.GameState.Dead;
        IsGameOver = true;
        if (resultUI != null) resultUI.ShowGameoverUI();
        PauseGame(); // 움직임 정지
        //if (gameoverUI != null) gameoverUI.SetActive(true);
        //if (statusUI != null) statusUI.SetActive(false);
    }

    // === 리셋 & 초기화 ===
    private void ResetRunData()
    {
        _lastReason = RunEndReason.Success;
        clearedFloorThisRun = 0;
        earnedMoney = 0;
        runInventory.Clear();
        dungeonStartTime = 0f;

        if (InventoryStateHandle.Runtime != null)
        {
            InventoryStateHandle.Runtime.Clear();
        }
    }

    private void ResetAndReturnToLobby()
    {
        ResetRunData();
        IsGameOver = false;
        ResumeGame();

        State = GameState.Dead;
        SceneManager.LoadScene(lobbyScene);
        State = GameState.InLobby;
    }

    // === 보조 ===
    public void AddOreToRun(OreData ore, int count) => runInventory.Add(ore, count);

    private int EvaluateSubtotal(OreData ore, int count)
    {
        if (ore == null || count <= 0) return 0;
        var tmp = new List<RunInventory.Entry> { new RunInventory.Entry { ore = ore, count = count } };
        return pricingService.EvaluateTotal(tmp, clearedFloorThisRun);
    }

    public List<RunResultLine> BuildRunResultLines(out int grandTotal)
    {
        grandTotal = 0;
        var lines = new List<RunResultLine>();

        foreach (var e in runInventory.Stacks)
        {
            if (e.ore == null || e.count <= 0) continue;
            int subtotal = EvaluateSubtotal(e.ore, e.count);
            int unit = (e.count > 0) ? Mathf.RoundToInt((float)subtotal / e.count) : 0;

            lines.Add(new RunResultLine
            {
                ore = e.ore,
                count = e.count,
                price = unit,
                total = subtotal
            });
            grandTotal += subtotal;
        }
        return lines;
    }

    // 타운 씬에 플레이어 없으면 스폰
    private void EnsurePlayerInTown()
    {
        // 먼저 씬에 Player가 이미 있는지 확인
        if (player == null) player = FindExistingPlayer();

        Vector3 spawnPos = GetSpawnPositionOrDefault();

        // 없으면 생성
        if (player == null && playerPrefab != null)
        {
            var go = Instantiate(playerPrefab, spawnPos, Quaternion.identity);
            player = go.transform;
        }
        else if (player != null)
        {
            // 있으면 위치만 이동
            player.transform.position = spawnPos;
        }
    }

    // 씬에서 존재하는 플레이어 Transform 찾기 (Tag 또는 컴포넌트 기반)
    private Transform FindExistingPlayer()
    {
        var tagged = GameObject.FindGameObjectWithTag("Player");
        if (tagged != null) return tagged.transform;

        return null;
    }

    private void RebindSpawnPoint()
    {
        _spawnPoint = null;
        var go = GameObject.FindGameObjectWithTag(spawnPointTag);
        if (go != null) _spawnPoint = go.transform;
    }

    private Vector3 GetSpawnPositionOrDefault()
    {
        // 스폰 포인트가 있으면 그 위치, 없으면 (0,0,0)
        return _spawnPoint != null ? _spawnPoint.position : Vector3.zero;
    }

    public void PauseGame()
    {
        IsGamePaused = true;
        Time.timeScale = 0f;
    }

    public void ResumeGame()
    {
        IsGamePaused = false;
        Time.timeScale = 1f;
    }

    public void OnDungeonEnter()
    {
        dungeonStartTime = Time.time;
    }

    public void ConvertOresInRunToMoney()
    {
        // 광물 정산을 위해 PricingService를 사용하여 총 금액을 계산합니다.
        int moneyToEarn = pricingService.EvaluateTotal(runInventory.Stacks, clearedFloorThisRun);

        // 계산된 돈을 EconomyService에 추가합니다.
        if (EconomyService.Instance != null)
        {
            EconomyService.Instance.AddMoney(moneyToEarn);
        }
        else
        {
            Debug.LogError("EconomyService 인스턴스를 찾을 수 없습니다. 돈을 정산할 수 없습니다.");
            return;
        }

        // 광물을 돈으로 바꾼 후 현재 인벤토리를 정리
        var inv = player.GetComponent<Inventory>();
        if (inv != null)
        {
            inv.ResetAtferRun();
        }

        Debug.Log($"중간 정산: {moneyToEarn}원을 획득하고 광물 인벤토리를 비웠습니다.");
    }

    public float GetDungeonPlayTime() => Time.time - dungeonStartTime;
    public RunEndReason GetDungeonEndReason() => _lastReason;
    public void RegisterResultUI(ResultUIManager ui) => resultUI = ui;

    // === 입력 감지 (키보드/패드) ===
    private bool AnyInputPressed()
    {
        // Keyboard
        if (Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame) return true;

        // Mouse (선택사항: 클릭만 감지)
        if (Mouse.current != null && (Mouse.current.leftButton.wasPressedThisFrame ||
                                      Mouse.current.rightButton.wasPressedThisFrame ||
                                      Mouse.current.middleButton.wasPressedThisFrame)) return true;

        // 기타 디바이스 추가 가능
        return false;
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

[Serializable]
public class SkillLevelKV
{
    public string id;
    public int level;
}

[Serializable]
public class SaveData
{
    public int money = 0;

    // 해금된 장비 ID 목록
    public List<string> unlockedEquipments = new();

    // 해금된 스킬 ID 목록
    public List<string> unlockedSkills = new();

    // 스킬 레벨(직렬화 가능 형태)
    public List<SkillLevelKV> skillLevels = new();

    // 장착 슬롯(스킬 ID)
    public string slot1Id;
    public string slot2Id;

    public List<string> runBuffBasket = new();
}

public class DataManager : MonoBehaviour
{
    public static DataManager Instance { get; private set; }

    [Header("카탈로그 (모든 장비 및 스킬SO 등록)")]
    [SerializeField] private EquipmentData[] equipmentCatalog;
    [SerializeField] private SkillData[] skillCatalog;

    // id → EquipmentData/SkillData
    private readonly Dictionary<string, EquipmentData> _equipIndex = new();
    private readonly Dictionary<string, SkillData> _skillIndex = new();

    // id → level (런타임 캐시; 파일 저장 전/후 List로 변환)
    private readonly Dictionary<string, int> _levelMap = new();

    private string _path;
    private SaveData cache = new SaveData();

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

    private void Start()
    {
        // 추후 배포 시 Application.persistentDataPath로 수정
        // 편의를 위해 dataPath 사용
        _path = Path.Combine(Application.dataPath, "database.json");

        BuildIndex();
        JsonLoad();
        ValidateCatalog();
    }

    #region === 유틸 ===
    private void BuildIndex()
    {
        _skillIndex.Clear();
        if (skillCatalog != null)
            foreach (var s in skillCatalog)
                if (s && !string.IsNullOrEmpty(s.skillId)) _skillIndex[s.skillId] = s;

        _equipIndex.Clear();
        if (equipmentCatalog != null)
            foreach (var e in equipmentCatalog)
                if (e && !string.IsNullOrEmpty(e.Id)) _equipIndex[e.Id] = e;
    }

    private void ValidateCatalog()
    {
        // 중복 skillId 탐지 + 아이콘 누락 경고
        var seen = new HashSet<string>();
        foreach (var s in skillCatalog)
        {
            if (!s) continue;
            if (string.IsNullOrEmpty(s.skillId))
                Debug.LogWarning($"[DataManager] SkillData '{s.name}' has empty skillId.");

            if (!seen.Add(s.skillId))
                Debug.LogError($"[DataManager] Duplicate skillId detected: '{s.skillId}'.");

            if (s.skillIcon == null)
                Debug.LogWarning($"[DataManager] Skill '{s.skillId}' has no icon assigned.");
        }
    }
    #endregion

    #region JSON Load / Save
    public void JsonLoad()
    {
        if (!File.Exists(_path))
        {
            Debug.Log($"[DataManager] database.json을 찾지 못함. 새로 생성");
            // 최초 생성
            cache = new SaveData();
            cache.money = 0;
            PushLevelMapToList(); // 비어 있어도 형식 보장
            JsonSave();
            return;
        }

        try
        {
            string loadJson = File.ReadAllText(_path);
            cache = JsonUtility.FromJson<SaveData>(loadJson) ?? new SaveData(); // JsonUtility를 사용해 loadJson을 SaveData 형식의 직렬화 모델로 변환

            var eco = EconomyServiceInstance();
            if (eco != null)
            {
                eco.SetMoney(cache.money);
                Debug.Log($"[DataManager] database.json 복구 성공! 현재 소지 금액: {eco.Money}");
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[DataManager] JsonLoad 실패: {e.Message}. 새 파일로 초기화합니다.");
            cache = new SaveData();
        }

        // List → Dictionary 재구성
        PullLevelListToMap();
    }

    public void JsonSave()
    {
        // Dictionary → List 반영
        PushLevelMapToList();

        var eco = EconomyServiceInstance();
        if (eco != null)
        {
            // 캐시에 런타임 중 보유금액 저장
            cache.money = eco.Money;
        }

        try
        {
            string json = JsonUtility.ToJson(cache, true);
            File.WriteAllText(_path, json);
        }
        catch (Exception e)
        {
            Debug.LogError($"[DataManager] JsonSave 실패: {e.Message}");
        }
    }

    public void SaveNow() => JsonSave();

    private EconomyService EconomyServiceInstance()
    {
        return EconomyService.Instance ?? FindObjectOfType<EconomyService>();
    }

    private void PullLevelListToMap()
    {
        _levelMap.Clear();
        if (cache.skillLevels == null) return;

        foreach (var kv in cache.skillLevels)
        {
            if (kv == null || string.IsNullOrEmpty(kv.id)) continue;
            _levelMap[kv.id] = kv.level;
        }
    }

    private void PushLevelMapToList()
    {
        if (cache.skillLevels == null) cache.skillLevels = new List<SkillLevelKV>();
        cache.skillLevels.Clear();

        foreach (var pair in _levelMap)
        {
            cache.skillLevels.Add(new SkillLevelKV { id = pair.Key, level = pair.Value });
        }
    }

    #endregion

    #region 장비 해금/장착/조회
    
    private bool IsEquipmentUnlocked(EquipmentData data)
    {
        return data != null && cache.unlockedEquipments.Contains(data.Id);
    }

    public bool UnlockEquipment(EquipmentData data)
    {
        if (data == null) return false;
        if (IsEquipmentUnlocked(data)) return false;
        cache.unlockedEquipments.Add(data.Id);
        JsonSave();
        return true;
    }

    public EquipmentData GetEquipmentById(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        return _equipIndex.TryGetValue(id, out var d) ? d : null;
    }

    #endregion

    #region 스킬 해금/장착/조회

    public bool IsSkillUnlocked(SkillData skillData)
    {
        return skillData != null && cache.unlockedSkills.Contains(skillData.skillId);
    }

    public bool UnlockSkill(SkillData skillData)
    {
        if (skillData == null) return false;
        if (IsSkillUnlocked(skillData)) return false;

        cache.unlockedSkills.Add(skillData.skillId);

        // 기본 레벨 보장
        if (!_levelMap.ContainsKey(skillData.skillId))
            _levelMap[skillData.skillId] = 1;

        JsonSave();
        return true;
    }

    public void LockSkill(SkillData skillData)
    {
        if (skillData == null) return;

        if (cache.unlockedSkills.Remove(skillData.skillId))
        {
            // 장착 해제
            if (cache.slot1Id == skillData.skillId) cache.slot1Id = null;
            if (cache.slot2Id == skillData.skillId) cache.slot2Id = null;

            // 레벨 삭제(원하면 유지 가능)
            _levelMap.Remove(skillData.skillId);

            JsonSave();
        }
    }

    public void ApplySkillsTo(PlayerSkillSystem pss)
    {
        if (!pss) return;
        pss.slot1 = GetSkillById(cache.slot1Id);
        pss.slot2 = GetSkillById(cache.slot2Id);
        // 필요 시 HUD/이벤트 알림 호출
    }

    public void SaveSkillsFrom(PlayerSkillSystem pss)
    {
        if (!pss) return;
        cache.slot1Id = pss.slot1 ? pss.slot1.skillId : null;
        cache.slot2Id = pss.slot2 ? pss.slot2.skillId : null;
        JsonSave();
    }

    public SkillData GetSkillById(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        if (_skillIndex.TryGetValue(id, out var data)) return data;

        Debug.LogWarning($"[DataManager] Unknown SkillId '{id}'. " +
                         $"Check: SaveData(slot ids/unlocked list) ↔ skillCatalog(skillId).");
        return null;
    }

    #endregion

    #region 스킬 레벨

    public int GetSkillLevel(SkillData data)
    {
        if (data == null) return 0;
        if (!IsSkillUnlocked(data)) return 0;

        if (_levelMap.TryGetValue(data.skillId, out var lv))
            return Mathf.Clamp(lv, 1, data.maxLevel);

        // 해금되어 있는데 레벨 엔트리가 없으면 1로 고정
        _levelMap[data.skillId] = 1;
        JsonSave();
        return 1;
    }

    public void SetSkillLevel(SkillData data, int level)
    {
        if (data == null) return;

        int clamped = Mathf.Clamp(level, 1, data.maxLevel);
        _levelMap[data.skillId] = clamped;
        JsonSave();
    }

    /// <summary>
    /// 스킬 해금 조건(선행 스킬 레벨 등) 검사
    /// </summary>
    public bool CanUnlock(SkillData data)
    {
        if (data == null) return false;
        if (data.unlockConditions == null || data.unlockConditions.Length == 0) return true;

        foreach (var condition in data.unlockConditions)
        {
            var required = GetSkillById(condition.requiredSkillId);
            if (required == null) return false;

            int lv = GetSkillLevel(required);
            if (lv < condition.requiredLevel) return false;
        }
        return true;
    }

    #endregion

    #region 버프 저장
    public void SaveRunBuffBasket(IReadOnlyList<string> ids)
    {
        if (cache.runBuffBasket == null) cache.runBuffBasket = new List<string>();
        cache.runBuffBasket.Clear();
        if (ids != null) cache.runBuffBasket.AddRange(ids);
        JsonSave();
    }

    public List<string> LoadRunBuffBasket() 
    { 
        return cache.runBuffBasket != null
            ? new List<string>(cache.runBuffBasket)
            : new List<string>();
    }

    #endregion
}

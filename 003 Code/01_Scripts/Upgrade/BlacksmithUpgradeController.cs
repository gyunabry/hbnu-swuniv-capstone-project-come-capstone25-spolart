using System.Linq;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;

public class BlacksmithUpgradeController : MonoBehaviour
{
    [SerializeField] private UpgradeEffectPlayer effectPlayer;

    [Header("데이터 소스")]
    [Tooltip("강화 UI에 표시할 장비 SO 목록")]
    [SerializeField] private EquipmentData[] equipmentCatalog;

    [Header("좌측 리스트")]
    [SerializeField] private GameObject weaponView; // 무기 스크롤뷰
    [SerializeField] private Transform weaponList; // 무기가 들어갈 콘텐츠 리스트
    [SerializeField] private GameObject miningtoolView; // 채광도구 스크롤뷰
    [SerializeField] private Transform miningtoolList; // 채광도구가 들어갈 콘텐츠 리스트
    [SerializeField] private UpgradeListItem listItemPrefab; // 콘텐츠 아래에 생성할 아이템 프리팹
    [SerializeField] private Button weaponButton; // 무기 선택 탭
    [SerializeField] private Button miningtoolButton; // 채광도구 선택 탭

    [Header("우측 상세 패널")]
    [SerializeField] private Image iconImage;
    [SerializeField] private TMP_Text nameText;

    // 레벨 라인
    [SerializeField] private GameObject levelRow;           // 강화 등급 라인
    [SerializeField] private TMP_Text currentGradeText;     // 현재 강화 등급 텍스트
    [SerializeField] private TMP_Text nextGradeText;        // 다음 강화 등급 텍스트
    [SerializeField] private TMP_Text topArrow;             // 강화 등급 라인 화살표

    // 공격력 라인
    [SerializeField] private GameObject attackRow;          // 데미지 라인
    [SerializeField] private TMP_Text currentBaseDmgText;   // 현재 기본 데미지
    [SerializeField] private TMP_Text nextBaseDmgText;      // 다음 기본 데미지
    [SerializeField] private TMP_Text underArrow;
    [SerializeField] private TMP_Text costText;             // 강화 비용
    [SerializeField] private Button enhanceButton;          // 강화 버튼

    // 표기할 텍스트
    [SerializeField] private string arrowTextWhenMaxGrade = "최고 단계";
    [SerializeField] private string defaultArrowText = "→";
    
    [Header("장비 장착")]
    [SerializeField] private Button equipButton; // 장착 버튼
    [SerializeField] private PlayerEquipment playerEquip;

    [Header("상단 보유 금액")]
    [SerializeField] private TMP_Text moneyText;

    private EquipmentData _selected; // 선택한 장비
    private enum Tab { Weapon, Mining }
    private Tab _currentTab = Tab.Weapon;

    private void Awake()
    {
        if (!playerEquip)
            playerEquip = FindFirstObjectByType<PlayerEquipment>(); // 자동 연결

        if (weaponButton) weaponButton.onClick.AddListener(OnClickShowWeapon);
        if (miningtoolButton) miningtoolButton.onClick.AddListener(OnClickShowMiningtool);

        BuildList();             // 무기/채광도구 각각의 콘텐츠에 생성
        ShowTab(_currentTab);
        SelectFirstIfAnyInCurrentTab();

        if (enhanceButton) enhanceButton.onClick.AddListener(OnClickEnhance);
        if (equipButton) equipButton.onClick.AddListener(OnClickEquip);

        if (EnhancementService.Instance != null)
            EnhancementService.Instance.OnEnhanced += HandleEnhanced;
    }

    private void OnDestroy()
    {
        if (EnhancementService.Instance != null)
            EnhancementService.Instance.OnEnhanced -= HandleEnhanced;
    }

    private void HandleEnhanced(EquipmentData eq, int lv)
    {
        if (_selected == eq) RefreshDetail();
        RefreshListItems();
        RefreshMoney();
    }

    private void BuildList()
    {
        // 기존 항목 제거
        ClearChildren(weaponList);
        ClearChildren(miningtoolList);

        if (equipmentCatalog == null || listItemPrefab == null) return;

        foreach (var eq in equipmentCatalog.Where(e => e != null))
        {
            // ▶ 타입에 맞는 콘텐츠로 분배
            Transform targetParent = (eq.Type == EquipmentType.Weapon)
                ? weaponList
                : miningtoolList;

            if (!targetParent) continue;

            var item = Instantiate(listItemPrefab, targetParent);
            item.Init(eq, this);
        }
    }

    private void RefreshListItems()
    {
        void RefreshChildren(Transform t)
        {
            if (!t) return;
            foreach (Transform c in t)
            {
                var item = c.GetComponent<UpgradeListItem>();
                if (item) item.Refresh();
            }
        }

        RefreshChildren(weaponList);
        RefreshChildren(miningtoolList);
    }

    private static void ClearChildren(Transform t)
    {
        if (!t) return;
        for (int i = t.childCount - 1; i >= 0; i--)
            Object.Destroy(t.GetChild(i).gameObject);
    }

    private void SelectFirstIfAnyInCurrentTab()
    {
        EquipmentData first = null;

        if (_currentTab == Tab.Weapon)
            first = equipmentCatalog?.FirstOrDefault(e => e != null && e.Type == EquipmentType.Weapon);
        else
            first = equipmentCatalog?.FirstOrDefault(e => e != null && e.Type == EquipmentType.Mining);

        if (first != null) Select(first);
        else
        {
            _selected = null;
            RefreshDetail();
        }
    }

    public void Select(EquipmentData eq)
    {
        _selected = eq;

        // 다른 타입 선택 시 탭 자동 전환
        if (eq != null)
        {
            var desiredTab = (eq.Type == EquipmentType.Weapon) ? Tab.Weapon : Tab.Mining;
            if (_currentTab != desiredTab)
            {
                _currentTab = desiredTab;
                ShowTab(_currentTab);
            }
        }

        RefreshDetail();
    }

    private void RefreshDetail()
    {
        if (_selected == null)
        {
            if (iconImage) iconImage.sprite = null;
            if (nameText) nameText.text = "";
            if (currentGradeText) currentGradeText.text = "";
            if (nextGradeText) nextGradeText.text = "";
            if (currentBaseDmgText) currentBaseDmgText.text = "";
            if (nextBaseDmgText) nextBaseDmgText.text = "";
            if (costText) costText.text = "";
            if (enhanceButton) enhanceButton.interactable = false;
            if (equipButton) equipButton.interactable = false;
            RefreshMoney();
            return;
        }

        var svc = EnhancementService.Instance;
        int lv = svc != null ? svc.GetLevel(_selected) : _selected.EquipmentUpgrade;
        int maxLv = svc != null ? svc.GetMaxLevel(_selected) : lv;

        _selected.ApplyLoadedUpgrade(lv);

        int atkNow = _selected.CalculatedAttackDamage;
        int atkNext = atkNow;
        int nextCost = 0;

        // 업그레이드 강화 여부 확인 후 ui 갱신
        bool canUpgrade = (lv < maxLv);
        if (canUpgrade)
        {
            int bonusNext = svc.AttackBonusFor(_selected, lv + 1);
            atkNext = _selected.BaseAttackDamage + bonusNext;
            nextCost = svc.NextCost(_selected);

            // 화살표를 기본값으로 수정
            if (topArrow) topArrow.text = defaultArrowText;

            // 그 외 텍스트 오브젝트 활성화
            currentGradeText.gameObject.SetActive(true);
            nextGradeText.gameObject.SetActive(true);
            attackRow.gameObject.SetActive(true);
        }

        if (iconImage) iconImage.sprite = _selected.Icon;
        if (nameText) nameText.text = _selected.EquipmentName;

        if (currentGradeText) currentGradeText.text = $"+{lv}";
        if (nextGradeText) nextGradeText.text = canUpgrade ? $"+{lv + 1}" : $"+{lv}";

        if (currentBaseDmgText) currentBaseDmgText.text = atkNow.ToString();
        if (nextBaseDmgText) nextBaseDmgText.text = atkNext.ToString();

        if (costText)
        {
            costText.text = canUpgrade ? $"-{nextCost:N0}" : "최고 단계입니다.";
            costText.color = Color.green;
        }

        if (enhanceButton) enhanceButton.interactable = canUpgrade;
        if (equipButton) equipButton.interactable = (_selected != null);

        // 최고 단계일 시 UI
        if (!canUpgrade) 
        {
            // 화살표를 최고단계 텍스트로 표시
            if (topArrow) topArrow.text = arrowTextWhenMaxGrade;

            // 그 외 텍스트 오브젝트 비활성화
            currentGradeText.gameObject.SetActive(false);
            nextGradeText.gameObject.SetActive(false);
            attackRow.gameObject.SetActive(false);
        }

        RefreshMoney();
    }

    private void RefreshMoney()
    {
        if (moneyText != null && EconomyServiceLocator.TryGetMoney(out var m))
            moneyText.text = m.ToString("N0");
    }

    private void ShowTab(Tab tab)
    {
        _currentTab = tab;

        if (weaponView) weaponView.gameObject.SetActive(tab == Tab.Weapon);
        if (miningtoolView) miningtoolView.gameObject.SetActive(tab == Tab.Mining);

        // 현재 선택이 탭과 불일치하면 해당 탭 첫 항목 선택
        if (_selected == null ||
            (_selected.Type == EquipmentType.Weapon && tab == Tab.Mining) ||
            (_selected.Type == EquipmentType.Mining && tab == Tab.Weapon))
        {
            SelectFirstIfAnyInCurrentTab();
        }
    }

    // 강화 동작
    private void OnClickEnhance()
    {
        if (_selected == null || EnhancementService.Instance == null) return;

        if (EnhancementService.Instance.TryEnhance(_selected, out var newLv, out string reason))
        {
            RefreshDetail();
            RefreshListItems();
            RefreshMoney();

            if (effectPlayer) effectPlayer.PlaySuccess(iconImage, $"+{newLv} 강화 성공!");
        }

        if (reason == "골드 부족")
        {
            costText.text = $"골드 부족";
            costText.color = Color.red;

            Debug.LogWarning($"[Upgrade] 실패: {reason}");
            RefreshDetail();
        }
        else if (reason == "강화 실패")
        {
            if (effectPlayer) effectPlayer.PlayFail(iconImage, $"+{newLv} 강화 실패");
            RefreshDetail();
        }
    }

    // 장착 동작
    private void OnClickEquip()
    {
        if (_selected == null) return;

        // PlayerEquipment 연결
        if (!playerEquip)
        {
            playerEquip = FindAnyObjectByType<PlayerEquipment>();
        }

        if (playerEquip)
        {
            playerEquip.Equip(_selected); // 실제 장착
            Debug.Log($"{_selected.name} 장착 성공");

            RefreshDetail();

            if (effectPlayer) effectPlayer.PlaySuccess(iconImage, "장착 완료!");
        }
        else
        {
            Debug.Log("PlayerEquipment 연결 실패");
        }
    }

    private void OnClickShowWeapon() => ShowTab(Tab.Weapon);
    private void OnClickShowMiningtool() => ShowTab(Tab.Mining);
}

/// <summary>
/// EconomyService가 이벤트를 제공하지 않는 경우도 있어 안전하게 값을 읽는 헬퍼
/// </summary>
static class EconomyServiceLocator
{
    public static bool TryGetMoney(out int money)
    {
        money = 0;
        var svc = Object.FindFirstObjectByType<EconomyService>();
        if (svc == null) return false;
        money = svc.Money;
        return true;
    }
}

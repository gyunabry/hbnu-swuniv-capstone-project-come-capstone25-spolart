using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 버프 선택 시 상세설명을 보여줄 스크립트
/// </summary>

public class BuffDetailPanel : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private Image iconImage;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text costText;
    [SerializeField] private TMP_Text descriptionText;
    [SerializeField] private TMP_Text effectText;
    [SerializeField] private Button buyButton;
    [SerializeField] private Button removeButton;
    [SerializeField] private TMP_Text currentMoney;

    [Header("현재 구매한 버프 표시")]
    [SerializeField] private Image buffIconSlot1;
    [SerializeField] private Image buffIconSlot2;
    [SerializeField] private Button buffSlot1Button;
    [SerializeField] private Button buffSlot2Button;

    private BuffData _current;
    private DungeonRunBuffManager _runBuffs;
    private EconomyService _eco;

    private void Awake()
    {
        _runBuffs = DungeonRunBuffManager.Instance;
        _eco = EconomyService.Instance;

        if (buyButton) buyButton.onClick.AddListener(OnBuy);
        if (removeButton) removeButton.onClick.AddListener(OnRemoveCurrent);

        if (buffSlot1Button) buffSlot1Button.onClick.AddListener(() => OnClickCurrentBuffSlot(0));
        if (buffSlot2Button) buffSlot2Button.onClick.AddListener(() => OnClickCurrentBuffSlot(1));

        if (_eco != null)
        {
            HandleMoneyChanged(_eco.Money);
            _eco.OnMoneyChanged += HandleMoneyChanged;
        }
        if (_runBuffs != null) _runBuffs.OnBasketChanged += RefreshCurrentBasket;

        if (buffIconSlot1) buffIconSlot1.gameObject.SetActive(false);
        if (buffIconSlot2) buffIconSlot2.gameObject.SetActive(false);

        RefreshCurrentBasket();
        RefreshBuyRemoveButtonsForCurrent(); // 상세의 구매/해제 버튼 상태 초기화
    }

    private void OnDestroy()
    {
        if (_eco != null) _eco.OnMoneyChanged -= HandleMoneyChanged;

    }

    private void HandleMoneyChanged(long money)
    {
        if (currentMoney) currentMoney.text = money.ToString("N0");
        RefreshBuyButtonInteractable(); // 돈 변동 시 버튼 상태도 재평가
    }

    public void Show(BuffData data)
    {
        _current = data;
        if (!_current)
        {
            Clear();
            return;
        }

        if (iconImage) iconImage.sprite = _current.buffIcon;
        if (titleText) titleText.text = _current.buffName;
        if (costText) costText.text = _current.price > 0 ? $"구매 금액 : {_current.price:N0}" : "무료";
        if (descriptionText) descriptionText.text = _current.desription;
        if (effectText) effectText.text = _current.effectDesc;

        if (buyButton) buyButton.interactable = true; // 필요 시 조건(돈/중복구매 등) 체크
    }

    public void Clear()
    {
        _current = null;
        if (iconImage) iconImage.sprite = null;
        if (titleText) titleText.text = "";
        if (costText) costText.text = "";
        if (descriptionText) descriptionText.text = "";
        if (effectText) effectText.text = "";
        if (buyButton) buyButton.interactable = false;
    }

    private void OnBuy()
    {
        if (_current == null || _runBuffs == null || _eco == null) return;

        // 1) 슬롯/중복 사전검증
        if (!_runBuffs.CanBuy(_current.buffId, out var reason))
        {
            Debug.LogWarning($"구매 불가: {reason}");
            RefreshBuyButtonInteractable();
            return;
        }

        // 2) 결제 (돈 부족 시 종료)
        if (!_eco.TrySpendMoney(_current.price))
        {
            Debug.LogWarning("소지 금액이 부족합니다.");
            RefreshBuyButtonInteractable();
            return;
        }

        // 3) 장바구니 반영 (이상 시 롤백)
        if (!_runBuffs.Buy(_current.buffId))
        {
            // 방어적 롤백: 이 경우는 거의 없지만, 혹시 모를 실패 시 금액 환불
            _eco.AddMoney(_current.price);
            Debug.LogWarning("구매 실패(중복 또는 제한). 금액은 환불되었습니다.");
            RefreshBuyButtonInteractable();
            return;
        }

        // 성공
        RefreshBuyRemoveButtonsForCurrent();
        buyButton.interactable = false;
        Debug.Log($"구매 완료: {_current.buffName}");
    }

    private void OnRemoveCurrent()
    {
        if (_current == null || _runBuffs == null) return;

        if (_runBuffs.Remove(_current.buffId))
        {
            // 성공 시 버튼/슬롯 리프레시
            RefreshCurrentBasket();
            RefreshBuyRemoveButtonsForCurrent();
        }
    }

    private void OnClickCurrentBuffSlot(int index)
    {
        if (_runBuffs == null) return;
        var ids = _runBuffs.BasketIds;
        if (ids == null || index < 0 || index >= ids.Count) return;

        string id = ids[index];
        var data = FindInCatalog(id);
        if (data == null) return;

        Show(data);
        RefreshBuyRemoveButtonsForCurrent(); // 해제 버튼/구매 버튼 상태 갱신
    }

    // 카탈로그에서 ID로 BuffData 찾기(헬퍼)
    private BuffData FindInCatalog(string id)
    {
        if (string.IsNullOrEmpty(id) || _runBuffs == null) return null;
        foreach (var b in _runBuffs.Catalog)
            if (b != null && b.buffId == id) return b;
        return null;
    }

    private void RefreshBuyButtonInteractable()
    {
        if (!buyButton) return;

        bool can = false;
        if (_current != null && _runBuffs != null && _eco != null)
        {
            if (_runBuffs.CanBuy(_current.buffId, out _))
            {
                // 슬롯/중복 조건을 통과해야만 돈 조건 체크
                can = (_eco.Money >= _current.price);
            }
        }
        buyButton.interactable = can;
    }

    private void RefreshBuyRemoveButtonsForCurrent()
    {
        RefreshBuyButtonInteractable(); // 기존 구매 조건 평가

        if (removeButton != null)
        {
            bool canRemove = (_current != null && _runBuffs != null && _runBuffs.IsInBasket(_current.buffId));
            removeButton.gameObject.SetActive(canRemove);
            removeButton.interactable = canRemove;
        }
    }

    // 현재 적용 중인 버프를 표시
    private void RefreshCurrentBasket()
    {
        if (_runBuffs == null || buffIconSlot1 == null || buffIconSlot2 == null) return;

        var ids = _runBuffs.BasketIds;

        // 헬퍼: 카탈로그에서 ID로 BuffData 찾기
        BuffData FindInCatalog(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            foreach (var b in _runBuffs.Catalog)
                if (b != null && b.buffId == id) return b;
            return null;
        }

        // 슬롯1
        if (ids != null && ids.Count >= 1)
        {
            var b1 = FindInCatalog(ids[0]);
            if (b1 != null && b1.buffIcon != null)
            {
                buffIconSlot1.sprite = b1.buffIcon;
                buffIconSlot1.gameObject.SetActive(true);
            }
            else
            {
                buffIconSlot1.sprite = null;
                buffIconSlot1.gameObject.SetActive(false);
            }
        }
        else
        {
            buffIconSlot1.sprite = null;
            buffIconSlot1.gameObject.SetActive(false);
        }

        // 슬롯2
        if (ids != null && ids.Count >= 2)
        {
            var b2 = FindInCatalog(ids[1]);
            if (b2 != null && b2.buffIcon != null)
            {
                buffIconSlot2.sprite = b2.buffIcon;
                buffIconSlot2.gameObject.SetActive(true);
            }
            else
            {
                buffIconSlot2.sprite = null;
                buffIconSlot2.gameObject.SetActive(false);
            }
        }
        else
        {
            buffIconSlot2.sprite = null;
            buffIconSlot2.gameObject.SetActive(false);
        }
    }
}

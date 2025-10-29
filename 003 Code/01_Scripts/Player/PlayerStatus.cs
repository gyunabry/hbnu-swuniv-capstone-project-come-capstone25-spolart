using System;
using UnityEngine;

/*
 플레이어의 상태를 관리하는 스크립트
 체력 및 마나, 스탯 등
 */
public class PlayerStatus : MonoBehaviour
{
    [Header("플레이어 스탯")]
    [SerializeField] private float baseMaxHP;     // 기본 최대 체력
    [SerializeField] private float baseMaxMP;       // 기본 최대 마나
    [SerializeField] private float strength;    // 근력 : 공격력 및 채광력에 영향
    [SerializeField] private float handy;       // 손재주 : 채광속도에 영향

    [Header("의존성")]
    [SerializeField] private PlayerEquipment equipment;
    [SerializeField] private BuffSystem buffs;
    [SerializeField] private HitText hitText;

    [Header("참조")]
    [SerializeField] private Animator animator;

    public float MaxHP { get; private set; }
    public float MaxMP { get; private set; }
    public float CurrentHP { get; private set; }
    public float CurrentMP { get; private set; }

    // 체력 및 마나 변동 이벤트
    public event Action<float, float> OnHPChanged; // current, max
    public event Action<float, float> OnMPChanged;
    public event Action OnStatsChanged; // 상태창 UI에서 사용할 이벤트

    private void Awake()
    {
        if (!equipment) equipment = GetComponent<PlayerEquipment>();
        if (!buffs) buffs = GetComponent<BuffSystem>();
        if (!animator) animator = GetComponent<Animator>();

        MaxHP = baseMaxHP;
        MaxMP = baseMaxMP;

        // 플레이어의 현재 체력 및 마나 기본 값으로 설정
        CurrentHP = Mathf.Clamp(CurrentHP <= 0f ? MaxHP : CurrentHP, 0f, MaxHP);
        CurrentMP = Mathf.Clamp(CurrentMP <= 0f ? MaxMP : CurrentMP, 0f, MaxMP);
    }

    private void Start()
    {
        // HUD 초기 동기화
        OnHPChanged?.Invoke(CurrentHP, MaxHP);
        OnMPChanged?.Invoke(CurrentMP, MaxMP);
    }

    #region === 최종 스탯 계산 ===

    private int StrengthAttackBonus => Mathf.RoundToInt(strength);
    private int StrengthMiningBonus => Mathf.RoundToInt(strength * 0.5f);

    private float AttackSpeedMul => (buffs ? buffs.AttackSpeedMul : 1f);
    private float MiningSpeedMul => (buffs ? buffs.MiningSpeedMul : 1f);
    private float MoveSpeedMul => (buffs ? buffs.MoveSpeedMul * buffs.MoveSpeedSlowMul : 1f);
    private float DoubleDropChange => (buffs ? buffs.DoubleDropChance : 0f);

    // --- 공격 ---
    public int FinalAttackPower => Mathf.Max(1,
        (equipment ? equipment.GetAttackPower() : 1) + StrengthAttackBonus);

    public float FinalAttackCooldown =>
        ((equipment ? equipment.GetAttackCooldown() : 0.5f) / Mathf.Max(0.01f, AttackSpeedMul));

    public float FinalAttackCritChance =>
        Mathf.Clamp01(equipment ? equipment.GetAttackCriticalChance() : 0f);

    // --- 채광 ---
    public int FinalMiningPower => Mathf.Max(1,
       (equipment ? equipment.GetMiningPower() : 1) + StrengthMiningBonus);

    public float FinalMiningCooldown =>
        ((equipment ? equipment.GetMiningCooldown() : 0.5f) / Mathf.Max(0.01f, MiningSpeedMul));

    public float FinalMiningCritChance =>
        Mathf.Clamp01(equipment ? equipment.GetMiningCriticalChance() : 0f);

    // 필요 시 외부에서 호출해 강제 갱신 트리거
    public void ForceStatRecalcNotify() => OnStatsChanged?.Invoke();

    #endregion

    // 플레이어 캐릭터 피격 시 호출해 현재 체력 감소 함수
    public void TakeDamage(float damage, bool isCritical = false)
    {
        // 데미지가 0 이하일 경우 즉시 리턴
        if (damage <= 0)
        {
            return;
        }

        // 회피 시 무효
        if (UnityEngine.Random.value < Mathf.Clamp01(buffs.EvasionChance))
        { 
            return;
        }

        // 피해감소 적용
        if (buffs) damage *= Mathf.Max(0.01f, buffs.DamageTakenMul);

        // Clamp 함수로 플레이어가 받을 수 있는 최대 데미지 고정
        CurrentHP = Mathf.Clamp(CurrentHP - damage, 0f, MaxHP);
        Instantiate(hitText, transform.position, Quaternion.identity).Initialize(damage, isCritical, true);
        OnHPChanged?.Invoke(CurrentHP, MaxHP);

        // 현재 체력이 0 이하일 경우 사망 함수 호출
        if (CurrentHP <= 0) 
        {
            OnDead();
        }

        Debug.Log("[PlayerStatue] 피격! " + damage);
    }

    // 힐 스킬 대상일 경우 호출
    public void Heal(float amount)
    {
        if (amount <= 0)
        {
            return;
        }

        // Clamp 함수로 플레이어가 받을 수 있는 최대 힐량 고정
        CurrentHP = Mathf.Clamp(CurrentHP + amount, 0, MaxHP);
        OnHPChanged?.Invoke(CurrentHP, MaxHP);
    }

    // 스킬 사용 시 호출해 현재 마나 감소 함수
    // 추후 스킬 사용 시 해당 함수를 호출해 사용하려는 마나가 최대 마나보다 적은지 확인하는 로직 추가
    public bool TrySpendMP(float usedMana)
    {
        if (usedMana > CurrentMP)
        {
            Debug.Log($"마나 부족! 현재 마나: {CurrentMP}");
            return false;
        }

        CurrentMP = Mathf.Clamp(CurrentMP - usedMana, 0, MaxMP);
        OnMPChanged?.Invoke(CurrentMP, MaxMP);
        Debug.Log($"[PlayerStatus] 마나 소모! 남은 마나: {CurrentMP}");

        return true;
    }

    // 마나 회복 시 호출
    public void RestoreMP(float amount)
    {
        if (amount <= 0)
        {
            return;
        }

        CurrentMP = Mathf.Clamp(CurrentMP + amount, 0, MaxMP);
        OnMPChanged?.Invoke(CurrentMP, MaxMP);
    }

    public void OnDead()
    {
        // 플레이어 이동 불가능
        // 사망 애니메이션 출력
        Debug.Log("플레이어 사망");
        animator.SetTrigger("Dead");
        GameManager.Instance.GameOver();
    }


}

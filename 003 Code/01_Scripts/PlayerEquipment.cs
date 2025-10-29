using System;
using UnityEngine;

public class PlayerEquipment : MonoBehaviour
{
    [Header("현재 착용 중인 장비")]
    [SerializeField] private EquipmentItem currentWeapon;
    [SerializeField] private EquipmentItem currentMiningTool;

    [Header("기본 능력치")]
    [SerializeField] private int baseAttackPower = 1;
    [SerializeField] private float baseAttackCooldown = 0.5f;
    [SerializeField] private float baseAttackCriticalChance = 0f;

    [SerializeField] private int baseMiningPower = 1;
    [SerializeField] private float baseMiningCooldown = 0.5f;
    [SerializeField] private float baseMiningCriticalChance = 0f;

    public event Action OnEquipmentChanged;

    public EquipmentItem CurrentWeapon => currentWeapon;
    public EquipmentItem CurrentMiningTool => currentMiningTool;

    public void Equip(EquipmentData data)
    {
        if (data == null)
        {
            return;
        }
        var item = new EquipmentItem(data); // 매개변수로 전달받은 장비 데이터를 통해 EquipmentItem 생성

        // 해당 데이터의 타입에 따라 현재 무기/채광도구 설정
        if (data.Type == EquipmentType.Weapon)
        {
            currentWeapon = item;
            Debug.Log("current item: " + data.Type);
        }
        else
        {
            currentMiningTool = item;
            Debug.Log("current item: " + data.Type);
        }

        OnEquipmentChanged?.Invoke();
    }

    public void Unequip(EquipmentType type)
    {
        if (type == EquipmentType.Weapon)
        {
            currentWeapon = null;
        }
        else
        {
            currentMiningTool = null;
        }

        OnEquipmentChanged?.Invoke();
    }

    // --- 공격 관련 ---
    public int GetAttackPower() =>
        (currentWeapon?.AttackDamage ?? 0) + baseAttackPower;

    public float GetAttackCooldown()
    {
        float speed = currentWeapon?.Speed ?? 1f;
        return baseAttackCooldown / MathF.Max(0.01f, speed);
    }

    public float GetAttackCriticalChance() =>
        Mathf.Clamp01((currentWeapon?.CriticalChance ?? 0f) + baseAttackCriticalChance);

    // --- 채광 관련

    public int GetMiningPower() =>
        (currentMiningTool?.AttackDamage ?? 0) + baseMiningPower;

    public float GetMiningCooldown()
    {
        float speed = currentMiningTool?.Speed ?? 1f;
        return baseMiningCooldown / MathF.Max(0.01f, speed);
    }
    public float GetMiningCriticalChance()
    {
        float t = currentMiningTool?.CriticalChance ?? 0f;
        float b = baseMiningCriticalChance;

        if (t > 1f) t *= 0.01f;
        if (b > 1f) b *= 0.01f;

        return Mathf.Clamp01(t + b);
    }
}

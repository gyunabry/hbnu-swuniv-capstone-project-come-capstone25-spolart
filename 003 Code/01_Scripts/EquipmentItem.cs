using UnityEngine;

/* 플레이어가 들고 있는 장비 상태 */

public class EquipmentItem
{
    public EquipmentData data;

    public EquipmentItem(EquipmentData data)
    {
        this.data = data;
    }

    public string Name => data.EquipmentName;
    public Sprite Icon => data.Icon;
    public EquipmentType Type => data.Type;
    public EquipmentRarity Rarity => data.Rarity;
    public int AttackDamage => data.BaseAttackDamage;
    public float Speed => data.Speed;
    public float CriticalChance => data.CriticalChance;
}

using UnityEngine;

namespace Game.Foundation.Items
{
    public enum ItemCategory
    {
        MiningTool,
        Weapon
    }

    public enum Rarity
    {
        Common,
        Uncommon,
        Rare,
        Unique,
        Epic,
        Legendary,
        Mythic
    }

    public interface IDamageable
    {
        void ApplyDamage(float amount, GameObject source = null);
    }

    public interface IHealable
    {
        void Heal(float amount, GameObject source = null);
    }

    public interface IMoveSpeedModifier
    {
        void ApplySpeedModifier(float multiplier, float duration);
    }

    public interface IMineDropReceiver
    {
        void BonusDrop(int multiplier);
    }
}

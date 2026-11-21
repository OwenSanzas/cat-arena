using System.Collections.Generic;
using UnityEngine;

// 汇总角色 + 装备 + 药水增益 + 技能，每帧算出最终属性
public class PlayerStats : MonoBehaviour
{
    public float baseMaxHp = 150f;
    public float baseMoveSpeed = 5.5f;
    [System.NonSerialized] public CharacterDef character;

    public class Buff { public Item potion; public float endTime; }
    public readonly List<Buff> buffs = new();

    // 面板显示用
    public float Armor { get; private set; }
    public float TotalReduction { get; private set; }
    public float DamageBonus { get; private set; }
    public float AttackSpeedBonus { get; private set; }
    public float MoveSpeed { get; private set; }

    Health health;
    Combatant combatant;
    PlayerTopDown player;
    Inventory inventory;
    PlayerSkill skill;
    float regenAccum;

    void Awake()
    {
        health = GetComponent<Health>();
        combatant = GetComponent<Combatant>();
        player = GetComponent<PlayerTopDown>();
        inventory = GetComponent<Inventory>();
        skill = GetComponent<PlayerSkill>();
    }

    public void AddBuff(Item potion)
    {
        // 同种药水刷新时间，不叠加
        buffs.RemoveAll(b => b.potion.potion == potion.potion);
        buffs.Add(new Buff { potion = potion, endTime = Time.time + potion.potionDuration });
    }

    float BuffValue(PotionType t)
    {
        foreach (var b in buffs) if (b.potion.potion == t) return b.potion.potionValue;
        return 0f;
    }

    void Update()
    {
        buffs.RemoveAll(b => Time.time >= b.endTime);

        float armor = 0, hp = 0, move = 0, dmg = 0, aspd = 0, stam = 0, cdr = 0;
        if (inventory != null)
            foreach (var it in inventory.EquippedItems())
                foreach (var s in it.stats)
                    switch (s.type)
                    {
                        case StatType.Armor: armor += s.value; break;
                        case StatType.MaxHp: hp += s.value; break;
                        case StatType.MoveSpeed: move += s.value; break;
                        case StatType.Damage: dmg += s.value; break;
                        case StatType.AttackSpeed: aspd += s.value; break;
                        case StatType.StaminaRegen: stam += s.value; break;
                        case StatType.CooldownReduction: cdr += s.value; break;
                    }

        var c = character;
        bool berserk = skill != null && skill.type == SkillType.Berserk && skill.BuffActive;
        bool agility = skill != null && skill.type == SkillType.Agility && skill.BuffActive;

        // 生命上限变化时，按差值加减当前生命
        float newMax = Mathf.Round(baseMaxHp * (c?.hpMultiplier ?? 1f) + hp);
        if (!Mathf.Approximately(newMax, health.maxHp))
        {
            float delta = newMax - health.maxHp;
            health.maxHp = newMax;
            if (!health.IsDead) health.hp = Mathf.Clamp(health.hp + Mathf.Max(0f, delta), 1f, newMax);
        }

        Armor = armor;
        float armorRed = armor / (armor + 60f);   // 护甲减伤：60 护甲 = 50%
        TotalReduction = 1f - (1f - (c?.damageReduction ?? 0f)) * (1f - armorRed) * (1f - BuffValue(PotionType.Iron));
        health.damageReduction = TotalReduction;

        DamageBonus = (1f + dmg) * (1f + BuffValue(PotionType.Strength)) * (berserk ? 2f : 1f) - 1f;
        AttackSpeedBonus = (1f + aspd) * (berserk ? 2f : 1f) - 1f;
        combatant.damageMultiplier = 1f + DamageBonus;
        combatant.attackSpeedMultiplier = 1f + AttackSpeedBonus;
        combatant.rangeMultiplier = agility ? 1.4f : 1f;
        combatant.infiniteStamina = agility;
        combatant.staminaRegenMultiplier = 1f + stam;

        MoveSpeed = baseMoveSpeed * (c?.moveMultiplier ?? 1f) * (1f + move + BuffValue(PotionType.Swift));
        if (player != null) player.moveSpeed = MoveSpeed;
        if (skill != null) skill.cooldownMultiplier = Mathf.Max(0.4f, 1f - cdr);

        // 回复药水
        float regen = BuffValue(PotionType.Regen);
        if (regen > 0f && !health.IsDead && health.hp < health.maxHp)
        {
            regenAccum += regen * Time.deltaTime;
            if (regenAccum >= 1f)
            {
                float amt = Mathf.Floor(regenAccum);
                regenAccum -= amt;
                health.hp = Mathf.Min(health.maxHp, health.hp + amt);
            }
        }
    }
}

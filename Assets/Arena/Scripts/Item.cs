using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

public enum ItemKind { Weapon, Armor, Potion }
// 装备栏位（传统 RPG）：主手 副手 头盔 护甲 靴子 饰品×2
public enum EquipSlot { MainHand, OffHand, Head, Body, Feet, Accessory1, Accessory2 }
public enum ArmorSlot { Head, Body, Feet, Accessory }
public enum PotionType { Health, SkillUp, Strength, Swift, Iron, Regen }
public enum StatType { Armor, MaxHp, MoveSpeed, Damage, AttackSpeed, StaminaRegen, CooldownReduction }

[Serializable]
public struct Stat
{
    public StatType type;
    public float value;
    public Stat(StatType t, float v) { type = t; value = v; }

    public string Text => type switch
    {
        StatType.Armor => $"护甲 +{value:F0}",
        StatType.MaxHp => $"生命上限 +{value:F0}",
        StatType.MoveSpeed => $"移动速度 +{value * 100:F0}%",
        StatType.Damage => $"攻击力 +{value * 100:F0}%",
        StatType.AttackSpeed => $"攻击速度 +{value * 100:F0}%",
        StatType.StaminaRegen => $"体力恢复 +{value * 100:F0}%",
        StatType.CooldownReduction => $"技能冷却 -{value * 100:F0}%",
        _ => "",
    };
}

// 背包里的一件物品：武器 / 护甲饰品 / 药水
[Serializable]
public class Item
{
    public string name;
    public ItemKind kind;
    public int rarity;

    public WeaponData weapon;                    // 武器
    public ArmorSlot armorSlot;                  // 护甲 / 饰品
    public int style;                            // 外观款式
    public List<Stat> stats = new();

    public PotionType potion;                    // 药水
    public float potionValue, potionDuration;

    public Color RarityColor => WeaponData.RarityColors[Mathf.Clamp(rarity, 0, 3)];
    public string ColoredName => $"<color=#{ColorUtility.ToHtmlStringRGB(RarityColor)}>{name}</color>";

    // 格子里显示的大字
    public string Glyph => kind switch
    {
        ItemKind.Weapon => weapon.type switch { WeaponType.Sword => "剑", WeaponType.Spear => "枪", WeaponType.Axe => "斧", WeaponType.Bow => "弓", _ => "盾" },
        ItemKind.Armor => armorSlot switch { ArmorSlot.Head => "盔", ArmorSlot.Body => "甲", ArmorSlot.Feet => "靴", _ => "饰" },
        _ => "药",
    };

    public string TypeText => kind switch
    {
        ItemKind.Weapon => weapon.canMainHand ? (weapon.canOffHand ? "武器（主手/副手）" : "武器（主手）") : "副手",
        ItemKind.Armor => armorSlot switch { ArmorSlot.Head => "头盔", ArmorSlot.Body => "护甲", ArmorSlot.Feet => "靴子", _ => "饰品" },
        _ => "药水",
    };

    public string Description()
    {
        switch (kind)
        {
            case ItemKind.Weapon:
                return weapon.StatsText();
            case ItemKind.Armor:
                var lines = new List<string>();
                foreach (var s in stats) lines.Add(s.Text);
                return string.Join("    ", lines);
            default:
                return potion switch
                {
                    PotionType.Health => $"立即恢复 {potionValue:F0} 生命",
                    PotionType.SkillUp => "Q 技能等级 +1（最高 5 级）",
                    PotionType.Strength => $"攻击力 +{potionValue * 100:F0}%，持续 {potionDuration:F0} 秒",
                    PotionType.Swift => $"移动速度 +{potionValue * 100:F0}%，持续 {potionDuration:F0} 秒",
                    PotionType.Iron => $"受到伤害 -{potionValue * 100:F0}%，持续 {potionDuration:F0} 秒",
                    PotionType.Regen => $"每秒恢复 {potionValue:F0} 生命，持续 {potionDuration:F0} 秒",
                    _ => "",
                };
        }
    }

    public Color PotionColor => potion switch
    {
        PotionType.Health => new Color(1f, 0.3f, 0.3f),
        PotionType.SkillUp => new Color(1f, 0.85f, 0.2f),
        PotionType.Strength => new Color(1f, 0.5f, 0.2f),
        PotionType.Swift => new Color(0.3f, 1f, 0.6f),
        PotionType.Iron => new Color(0.6f, 0.7f, 0.85f),
        _ => new Color(0.4f, 1f, 0.4f),
    };

    // ---------------- 生成 ----------------
    public static Item FromWeapon(WeaponData w) => new Item { name = w.displayName, kind = ItemKind.Weapon, rarity = w.rarity, weapon = w };

    static readonly string[][] ArmorNames =
    {
        new[] { "皮帽", "铁盔", "角盔" },
        new[] { "皮甲", "锁子甲", "板甲" },
        new[] { "皮靴", "铁靴" },
        new[] { "猫铃铛", "鱼骨护符", "猫眼宝石" },
    };

    public static Item RandomArmor(ArmorSlot slot, int rarity, int level)
    {
        rarity = Mathf.Clamp(rarity, 0, 3);
        var names = ArmorNames[(int)slot];
        // 品质越高，越容易出好款式
        int style = Mathf.Clamp(Random.Range(0, names.Length) + (rarity >= 2 && Random.value < 0.5f ? 1 : 0), 0, names.Length - 1);
        var it = new Item { kind = ItemKind.Armor, armorSlot = slot, rarity = rarity, style = style };
        it.name = (rarity > 0 ? WeaponData.RarityNames[rarity] + "·" : "") + names[style];

        float rm = 1f + 0.3f * rarity;
        float lm = 1f + 0.03f * level;
        if (slot != ArmorSlot.Accessory)
        {
            float baseArmor = slot switch { ArmorSlot.Head => 5f, ArmorSlot.Body => 10f, _ => 4f };
            baseArmor *= 1f + 0.35f * style;
            it.stats.Add(new Stat(StatType.Armor, Mathf.Round(baseArmor * rm * lm)));
            if (slot == ArmorSlot.Feet) it.stats.Add(new Stat(StatType.MoveSpeed, 0.03f + 0.02f * rarity));
        }
        // 随机词条：普通 0 条（饰品 1 条），每升一级品质 +1 条
        int affixes = rarity + (slot == ArmorSlot.Accessory ? 1 : 0);
        var pool = new List<StatType> { StatType.MaxHp, StatType.MoveSpeed, StatType.Damage, StatType.AttackSpeed, StatType.StaminaRegen, StatType.CooldownReduction };
        for (int i = 0; i < affixes && pool.Count > 0; i++)
        {
            var t = pool[Random.Range(0, pool.Count)];
            pool.Remove(t);
            float v = t switch
            {
                StatType.MaxHp => Mathf.Round(Random.Range(10f, 25f) * rm * lm),
                StatType.MoveSpeed => Random.Range(0.04f, 0.08f) * rm,
                StatType.Damage => Random.Range(0.06f, 0.12f) * rm * lm,
                StatType.AttackSpeed => Random.Range(0.05f, 0.1f) * rm,
                StatType.StaminaRegen => Random.Range(0.1f, 0.25f) * rm,
                _ => Random.Range(0.05f, 0.1f) * rm,
            };
            it.stats.Add(new Stat(t, v));
        }
        return it;
    }

    public static Item Potion(PotionType type, int level = 0)
    {
        var it = new Item { kind = ItemKind.Potion, potion = type };
        switch (type)
        {
            case PotionType.Health:
                bool big = Random.value < 0.3f;
                it.name = big ? "大血药" : "小血药";
                it.rarity = big ? 1 : 0;
                it.potionValue = (big ? 90f : 45f) * (1f + 0.03f * level);
                break;
            case PotionType.SkillUp:
                it.name = "技能升级药水"; it.rarity = 3; break;
            case PotionType.Strength:
                it.name = "力量药水"; it.rarity = 1; it.potionValue = 0.3f; it.potionDuration = 60f; break;
            case PotionType.Swift:
                it.name = "迅捷药水"; it.rarity = 1; it.potionValue = 0.2f; it.potionDuration = 60f; break;
            case PotionType.Iron:
                it.name = "铁皮药水"; it.rarity = 2; it.potionValue = 0.25f; it.potionDuration = 60f; break;
            case PotionType.Regen:
                it.name = "回复药水"; it.rarity = 1; it.potionValue = 4f; it.potionDuration = 20f; break;
        }
        return it;
    }

    public static Item RandomBuffPotion()
    {
        var types = new[] { PotionType.Strength, PotionType.Swift, PotionType.Iron, PotionType.Regen };
        return Potion(types[Random.Range(0, types.Length)]);
    }
}

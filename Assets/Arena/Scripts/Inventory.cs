using System;
using System.Collections.Generic;
using UnityEngine;

// 背包 + 装备栏
[RequireComponent(typeof(Combatant))]
public class Inventory : MonoBehaviour
{
    public const int Capacity = 24;

    public readonly List<Item> bag = new();
    public readonly Item[] equipped = new Item[Enum.GetValues(typeof(EquipSlot)).Length];
    public event Action OnChanged;

    Combatant combatant;
    Health health;
    PlayerStats stats;
    PlayerSkill skill;

    void Awake()
    {
        combatant = GetComponent<Combatant>();
        health = GetComponent<Health>();
        stats = GetComponent<PlayerStats>();
        skill = GetComponent<PlayerSkill>();
    }

    public Item Get(EquipSlot s) => equipped[(int)s];
    public bool IsFull => bag.Count >= Capacity;

    public bool Add(Item it)
    {
        if (IsFull) return false;
        bag.Add(it);
        OnChanged?.Invoke();
        return true;
    }

    // 物品能放进哪些栏位
    public static bool Fits(Item it, EquipSlot s)
    {
        if (it == null) return false;
        if (it.kind == ItemKind.Weapon)
            return (s == EquipSlot.MainHand && it.weapon.canMainHand) || (s == EquipSlot.OffHand && it.weapon.canOffHand);
        if (it.kind == ItemKind.Armor)
            return it.armorSlot switch
            {
                ArmorSlot.Head => s == EquipSlot.Head,
                ArmorSlot.Body => s == EquipSlot.Body,
                ArmorSlot.Feet => s == EquipSlot.Feet,
                _ => s == EquipSlot.Accessory1 || s == EquipSlot.Accessory2,
            };
        return false;
    }

    // 护甲默认栏位（饰品优先放空的那格）
    public EquipSlot DefaultSlot(Item it)
    {
        if (it.kind == ItemKind.Weapon) return it.weapon.canMainHand ? EquipSlot.MainHand : EquipSlot.OffHand;
        return it.armorSlot switch
        {
            ArmorSlot.Head => EquipSlot.Head,
            ArmorSlot.Body => EquipSlot.Body,
            ArmorSlot.Feet => EquipSlot.Feet,
            _ => Get(EquipSlot.Accessory1) == null || Get(EquipSlot.Accessory2) != null ? EquipSlot.Accessory1 : EquipSlot.Accessory2,
        };
    }

    // 从背包装备到栏位，原来的装备回到背包
    public bool EquipFromBag(Item it, EquipSlot s)
    {
        if (!Fits(it, s) || !bag.Contains(it)) return false;
        bag.Remove(it);
        var old = equipped[(int)s];
        equipped[(int)s] = it;
        if (old != null) bag.Add(old);
        Refresh();
        return true;
    }

    public void SetEquipped(EquipSlot s, Item it)
    {
        equipped[(int)s] = it;
        Refresh();
    }

    public bool Unequip(EquipSlot s)
    {
        var it = equipped[(int)s];
        if (it == null || IsFull) return false;
        equipped[(int)s] = null;
        bag.Add(it);
        Refresh();
        return true;
    }

    public void Drop(Item it)
    {
        if (!bag.Remove(it)) return;
        LootBag.Spawn(new List<Item> { it }, transform.position - transform.forward * 1.0f, true);
        OnChanged?.Invoke();
    }

    public bool Use(Item it)
    {
        if (it.kind != ItemKind.Potion || !bag.Contains(it)) return false;
        Vector3 head = transform.position + Vector3.up * 2.4f;
        switch (it.potion)
        {
            case PotionType.Health:
                if (health.hp >= health.maxHp) { FX.Text(head, "生命已满", Color.gray, 16); return false; }
                health.Heal(it.potionValue);
                break;
            case PotionType.SkillUp:
                if (skill == null || !skill.LevelUp()) { FX.Text(head, "技能已满级", Color.gray, 16); return false; }
                break;
            default:
                stats.AddBuff(it);
                FX.Text(head, it.name + "！", it.PotionColor, 20);
                break;
        }
        FX.Sparks(transform.position + Vector3.up * 1.2f, Vector3.up, it.PotionColor, 20, 0.7f);
        bag.Remove(it);
        OnChanged?.Invoke();
        return true;
    }

    // 快捷键 1：喝回血最少但够用的血药
    public void QuickHeal()
    {
        Item best = null;
        foreach (var it in bag)
            if (it.kind == ItemKind.Potion && it.potion == PotionType.Health && (best == null || it.potionValue < best.potionValue))
                best = it;
        if (best == null) { FX.Text(transform.position + Vector3.up * 2.4f, "没有血药", Color.gray, 16); return; }
        Use(best);
    }

    public int CountPotions(PotionType t)
    {
        int n = 0;
        foreach (var it in bag) if (it.kind == ItemKind.Potion && it.potion == t) n++;
        return n;
    }

    // 装备变了：同步武器到战斗组件
    void Refresh()
    {
        var main = Get(EquipSlot.MainHand)?.weapon;
        var off = Get(EquipSlot.OffHand)?.weapon;
        if (combatant.mainHand != main || combatant.offHand != off) combatant.Equip(main, off, true);
        OnChanged?.Invoke();
    }

    public IEnumerable<Item> EquippedItems()
    {
        foreach (var it in equipped) if (it != null) yield return it;
    }
}

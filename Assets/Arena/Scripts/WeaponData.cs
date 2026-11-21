using UnityEngine;

public enum WeaponType { Sword, Spear, Axe, Shield, Bow }
public enum AttackShape { Arc, Thrust, Projectile }   // 扇形横扫 / 直线突刺 / 射箭

// 武器数据：每把武器是一个资源文件（ScriptableObject），在 Inspector 里直接调数值
[CreateAssetMenu(menuName = "Combat/Weapon", fileName = "Weapon")]
public class WeaponData : ScriptableObject
{
    public string displayName = "武器";
    [TextArea] public string description;
    public WeaponType type;

    [Header("品质（0 普通 1 精良 2 稀有 3 史诗）")]
    public int rarity;

    [Header("握持")]
    public bool canMainHand = true;
    public bool canOffHand = true;
    [Tooltip("副手空着时，主手双手持握的伤害倍率")]
    public float twoHandDamageMultiplier = 1.25f;
    [Tooltip("放在副手时的伤害倍率")]
    public float offHandDamageMultiplier = 0.8f;

    [Header("攻击")]
    public AttackShape shape = AttackShape.Arc;
    public float damage = 20f;
    public float range = 2f;
    [Tooltip("扇形攻击的角度")]
    public float arcAngle = 110f;
    [Tooltip("突刺攻击的宽度")]
    public float thrustWidth = 0.6f;
    [Tooltip("前摇 / 判定 / 后摇（秒）")]
    public float windup = 0.15f, active = 0.1f, recovery = 0.3f;
    [Tooltip("连击段数，最后一段是重击")]
    public int comboLength = 3;
    public float staminaCost = 10f;
    public float knockback = 4f;
    public float hitStun = 0.3f;
    [Tooltip("打在盾上时，削减对方的架势")]
    public float guardDamage = 15f;
    [Tooltip("出招时向前冲的速度")]
    public float lunge = 3f;
    [Tooltip("远程：箭的飞行速度")]
    public float projectileSpeed = 15f;

    [Header("格挡（盾牌用）")]
    public bool canBlock;
    [Range(0, 1)] public float blockDamageReduction = 0.8f;
    public float blockAngle = 140f;
    [Tooltip("刚举盾这么多秒内被打中 = 完美格挡")]
    public float parryWindow = 0.2f;
    public float blockMoveMultiplier = 0.55f;

    [Header("外观")]
    public Color color = Color.gray;
    public float length = 1f;

    public bool CanAttack => damage > 0f;

    // ---------- 品质 ----------
    public static readonly string[] RarityNames = { "普通", "精良", "稀有", "史诗" };
    public static readonly Color[] RarityColors =
    {
        new Color(0.85f, 0.85f, 0.85f),
        new Color(0.45f, 0.9f, 0.4f),
        new Color(0.35f, 0.65f, 1f),
        new Color(0.78f, 0.42f, 1f),
    };
    static readonly float[] RarityMultipliers = { 1f, 1.15f, 1.3f, 1.5f };

    public Color RarityColor => RarityColors[Mathf.Clamp(rarity, 0, 3)];
    public string ColoredName => $"<color=#{ColorUtility.ToHtmlStringRGB(RarityColor)}>{displayName}</color>";

    // 随机品质：55% 普通 / 30% 精良 / 12% 稀有 / 3% 史诗
    public static int RollRarity(int bonus = 0)
    {
        float r = Random.value;
        int tier = r < 0.03f ? 3 : r < 0.15f ? 2 : r < 0.45f ? 1 : 0;
        return Mathf.Min(3, tier + bonus);
    }

    // 运行时复制一份并按品质提升数值（掉落用）
    public WeaponData CreateVariant(int newRarity)
    {
        newRarity = Mathf.Clamp(newRarity, 0, 3);
        var w = Instantiate(this);
        float m = RarityMultipliers[newRarity] / RarityMultipliers[Mathf.Clamp(rarity, 0, 3)];
        w.rarity = newRarity;
        w.damage *= m;
        w.guardDamage *= m;
        if (w.canBlock) w.blockDamageReduction = Mathf.Min(0.95f, blockDamageReduction + 0.05f * (newRarity - rarity));
        w.displayName = newRarity > 0 ? $"{RarityNames[newRarity]}·{BaseName}" : BaseName;
        w.name = w.displayName;
        return w;
    }

    string BaseName
    {
        get
        {
            int dot = displayName.IndexOf('·');
            return dot >= 0 ? displayName.Substring(dot + 1) : displayName;
        }
    }

    public string StatsText()
    {
        if (!CanAttack && canBlock)
            return $"减伤 {blockDamageReduction * 100:F0}%   格挡角度 {blockAngle:F0}°   完美格挡窗口 {parryWindow:F2}s";
        string shapeText = shape == AttackShape.Arc ? $"横扫 {arcAngle:F0}°" : shape == AttackShape.Thrust ? "直刺" : "远程";
        return $"伤害 {damage:F0}   距离 {range:F1}m   {shapeText}\n" +
               $"出手 {windup:F2}s   连击 {comboLength} 段   体力 {staminaCost:F0}   削架势 {guardDamage:F0}";
    }
}

using UnityEngine;

// Q 技能：猫猫回旋 / 猫猫敏捷 / 狂战士。技能升级药水 +1 级（最高 5 级）
[RequireComponent(typeof(Combatant))]
public class PlayerSkill : MonoBehaviour
{
    public const int MaxLevel = 5;

    public SkillType type;
    public string skillName = "技能";
    public int level = 1;
    [System.NonSerialized] public float cooldownMultiplier = 1f;   // 装备的"技能冷却"词条

    Combatant combatant;
    float readyAt, activeUntil;

    public static readonly Color SpinColor = new(1f, 0.75f, 0.3f);
    public static readonly Color AgilityColor = new(0.3f, 1f, 0.8f);
    public static readonly Color BerserkColor = new(1f, 0.2f, 0.15f);

    void Awake() => combatant = GetComponent<Combatant>();

    int L => level - 1;
    public float Cooldown => cooldownMultiplier * type switch
    {
        SkillType.Spin => 10f - 0.8f * L,
        SkillType.Agility => 35f - 3f * L,
        _ => 30f - 2f * L,
    };
    public float Duration => type == SkillType.Spin ? 0f : 10f + 2f * L;
    public float SpinDamage => 1.5f + 0.25f * L;
    public float SpinRadius => 2f + 0.15f * L;
    public float SpinLifesteal => 0.3f;

    public float CooldownRemaining => Mathf.Max(0f, readyAt - Time.time);
    public bool BuffActive => type != SkillType.Spin && Time.time < activeUntil;
    public float BuffRemaining => Mathf.Max(0f, activeUntil - Time.time);
    public Color SkillColor => type switch { SkillType.Spin => SpinColor, SkillType.Agility => AgilityColor, _ => BerserkColor };

    public string LevelText => type switch
    {
        SkillType.Spin => $"伤害 ×{SpinDamage:F2}   范围 ×{SpinRadius:F2}   吸血 {SpinLifesteal * 100:F0}%   冷却 {Cooldown:F1}s",
        _ => $"持续 {Duration:F0}s   冷却 {Cooldown:F1}s",
    };

    public bool TryActivate()
    {
        Vector3 head = transform.position + Vector3.up * 2.4f;
        if (CooldownRemaining > 0f)
        {
            FX.Text(head, $"冷却中 {CooldownRemaining:F1}s", new Color(0.7f, 0.7f, 0.7f), 16);
            return false;
        }

        switch (type)
        {
            case SkillType.Spin:
                combatant.trailTint = SpinColor;
                if (!combatant.TrySpin(SpinDamage, SpinRadius, SpinLifesteal)) { combatant.trailTint = null; return false; }
                FX.Text(head, "猫猫回旋！", SpinColor, 26);
                break;
            case SkillType.Agility:
                activeUntil = Time.time + Duration;
                AuraFX.Attach(gameObject, AgilityColor, Duration, 1f);
                FX.Text(head, "猫猫敏捷！", AgilityColor, 26);
                break;
            case SkillType.Berserk:
                activeUntil = Time.time + Duration;
                AuraFX.Attach(gameObject, BerserkColor, Duration, 1.6f);
                FX.Text(head, "狂战士！", BerserkColor, 30);
                TopDownCamera.Shake(0.25f, 0.2f);
                break;
        }
        readyAt = Time.time + Cooldown;
        return true;
    }

    void Update()
    {
        // 技能期间刀光换成技能颜色
        if (type == SkillType.Spin)
        {
            if (!combatant.IsSpinning && combatant.trailTint == SpinColor && combatant.State != CombatState.Active)
                combatant.trailTint = null;
        }
        else combatant.trailTint = BuffActive ? SkillColor : null;
    }

    public bool LevelUp()
    {
        if (level >= MaxLevel) return false;
        level++;
        FX.Text(transform.position + Vector3.up * 2.6f, $"{skillName} 升到 {level} 级！", new Color(1f, 0.85f, 0.2f), 24);
        FX.Sparks(transform.position + Vector3.up, Vector3.up, new Color(1f, 0.85f, 0.2f), 40, 1f);
        FX.Shockwave(transform.position, new Color(1f, 0.85f, 0.2f, 0.7f), 2f, 0.4f);
        return true;
    }
}

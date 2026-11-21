using UnityEngine;

public enum SkillType { Spin, Agility, Berserk }

// 可选角色（外观照着三只真猫的照片做）
public class CharacterDef
{
    public string name, title, description;
    public float hpMultiplier = 1f, moveMultiplier = 1f, damageReduction;
    public SkillType skill;
    public string skillName, skillDescription;
    public CatLookDef look;
    public bool dualSword, axe;   // 初始武器

    // 选角卡片上的小头像用
    public Color fur => look.fur;
    public Color belly => look.white;
    public Color accent => look.point.a > 0f ? look.point : look.stripe;
    public Color eye => look.eye;
    public Color scarf => look.collar;

    public static readonly CharacterDef[] All =
    {
        new CharacterDef
        {
            name = "甘蓝", title = "重装斧猫",
            description = "胖乎乎的灰色虎斑，白胸口白爪子，鼻梁一道白。受到伤害 -10%，移动速度 -10%。\n初始武器：战斧（双手持握）",
            damageReduction = 0.1f, moveMultiplier = 0.9f, axe = true,
            skill = SkillType.Spin, skillName = "猫猫回旋",
            skillDescription = "原地转两圈，用当前武器横扫一圈：伤害 ×1.5，范围是攻击距离的 2 倍，造成伤害的 30% 转为生命。冷却 10 秒。",
            look = new CatLookDef
            {
                fur = new Color(0.56f, 0.52f, 0.46f), stripe = new Color(0.16f, 0.14f, 0.13f), white = new Color(0.94f, 0.94f, 0.92f),
                eye = new Color(0.58f, 0.74f, 0.5f), nose = new Color(0.82f, 0.5f, 0.47f), pawPad = new Color(0.95f, 0.6f, 0.66f),
                collar = new Color(0.35f, 0.65f, 0.25f), tailTip = new Color(0.08f, 0.08f, 0.09f),
                whiteChest = true, whitePaws = true, blaze = true, stripeStrength = 0.85f,
                bodyWidth = 0.93f, bodyHeight = 0.5f, headScale = 1f, tailThickness = 0.13f,
            },
        },
        new CharacterDef
        {
            name = "纳尔", title = "双刀疾猫",
            description = "又瘦又长的银灰色山猫重点色，蓝眼睛，腿上有深色条纹。生命 -20%，移动速度 +10%。\n初始武器：双剑",
            hpMultiplier = 0.8f, moveMultiplier = 1.1f, dualSword = true,
            skill = SkillType.Agility, skillName = "猫猫敏捷",
            skillDescription = "10 秒内体力无限，武器攻击范围 +40%。冷却 35 秒。",
            look = new CatLookDef
            {
                fur = new Color(0.78f, 0.78f, 0.8f), stripe = new Color(0.3f, 0.3f, 0.33f), white = new Color(0.92f, 0.92f, 0.93f),
                point = new Color(0.55f, 0.55f, 0.58f),
                eye = new Color(0.45f, 0.62f, 0.95f), nose = new Color(0.92f, 0.62f, 0.66f), pawPad = new Color(0.42f, 0.38f, 0.4f),
                collar = new Color(0.2f, 0.7f, 0.65f), tailTip = new Color(0.3f, 0.3f, 0.33f),
                whiteChest = true, stripeStrength = 0.35f,
                bodyWidth = 0.74f, bodyHeight = 0.56f, headScale = 0.95f, tailThickness = 0.11f,
            },
        },
        new CharacterDef
        {
            name = "阿咪", title = "剑盾守猫",
            description = "毛蓬蓬的长毛猫，大白围脖、白手套，脸上一块灰棕色虎斑面罩，蓝眼睛。均衡，没有短板。\n初始武器：单手剑 + 盾牌",
            skill = SkillType.Berserk, skillName = "狂战士",
            skillDescription = "10 秒内攻击速度 +100%、攻击力 +100%。冷却 30 秒。",
            look = new CatLookDef
            {
                fur = new Color(0.6f, 0.57f, 0.52f), stripe = new Color(0.3f, 0.27f, 0.24f), white = new Color(0.96f, 0.96f, 0.95f),
                point = new Color(0.45f, 0.4f, 0.35f),
                eye = new Color(0.55f, 0.68f, 0.88f), nose = new Color(0.55f, 0.4f, 0.38f), pawPad = new Color(0.9f, 0.65f, 0.68f),
                collar = new Color(0.9f, 0.35f, 0.5f),
                whiteChest = true, whitePaws = true, fluffy = true, stripeStrength = 0.6f,
                bodyWidth = 0.98f, bodyHeight = 0.52f, headScale = 1.05f, tailThickness = 0.2f,
            },
        },
    };
}

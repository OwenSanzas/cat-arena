using UnityEngine;

// 简单敌人 AI：靠近 → 保持距离绕圈 → 冷却好了就出招；有盾的会看你出手时举盾
[RequireComponent(typeof(CharacterMotor), typeof(Combatant), typeof(Health))]
public class EnemyAI : MonoBehaviour
{
    public float moveSpeed = 3.5f;
    public float attackCooldownMin = 0.9f;
    public float attackCooldownMax = 2f;
    [Range(0, 1)] public float comboChance = 0.25f;
    [Tooltip("双持时用副手出招的概率")]
    [Range(0, 1)] public float offHandAttackChance = 0.4f;
    [Tooltip("看到玩家出招时举盾的概率")]
    [Range(0, 1)] public float blockChance = 0.45f;
    public float blockHoldTime = 0.6f;

    [Header("远程")]
    [Tooltip("远程敌人：保持距离放箭，被贴脸就后退")]
    public bool ranged;
    public float preferredDistance = 8f;

    [Tooltip("出招冷却倍率（刷怪时设置，>1 攻击更慢）")]
    [System.NonSerialized] public float cooldownScale = 1f;

    [Header("掉落")]
    [Range(0, 1)] public float mainDropChance = 0.3f;
    [Range(0, 1)] public float offDropChance = 0.2f;
    [Range(0, 1)] public float armorDropChance = 0.25f;
    [Range(0, 1)] public float potionDropChance = 0.3f;
    [Range(0, 1)] public float buffDropChance = 0.1f;
    [Range(0, 1)] public float skillPotionChance = 0.04f;
    [Tooltip("掉落品质加成（精英怪用）")]
    public int rarityBonus;
    [Tooltip("掉几次（Boss 多次）")]
    public int lootRolls = 1;
    [Tooltip("第几波（刷怪时设置），影响属性和掉落")]
    [System.NonSerialized] public int level;
    CharacterMotor motor;
    Combatant combatant;
    Health health;
    Health target;
    Combatant targetCombatant;

    float cooldown;
    float strafeDir = 1f, strafeTimer;
    int lastSeenPlayerAttack = -1, lastOwnAttack = -1;
    bool lastHandOff;
    float blockUntil;

    void Awake()
    {
        motor = GetComponent<CharacterMotor>();
        combatant = GetComponent<Combatant>();
        health = GetComponent<Health>();
        health.OnDied += () =>
        {
            DropLoot();
            Destroy(gameObject, 3f);
        };
    }

    // 死亡掉落：一个战利品袋，里面随机有武器 / 护甲饰品 / 血药 / 增益药水 / 技能升级药水
    void DropLoot()
    {
        var loot = new System.Collections.Generic.List<Item>();
        int bonus = rarityBonus;
        // 越往后的波次，品质越高
        int RollR() => Mathf.Min(3, WeaponData.RollRarity(bonus) + (Random.value < level / 30f ? 1 : 0));

        for (int roll = 0; roll < lootRolls; roll++)
        {
        if (combatant.mainHand != null && Random.value < mainDropChance)
            loot.Add(Item.FromWeapon(combatant.mainHand.CreateVariant(RollR())));
        if (combatant.offHand != null && Random.value < offDropChance)
            loot.Add(Item.FromWeapon(combatant.offHand.CreateVariant(RollR())));
        if (Random.value < armorDropChance)
            loot.Add(Item.RandomArmor((ArmorSlot)Random.Range(0, 4), RollR(), level));
        if (Random.value < potionDropChance) loot.Add(Item.Potion(PotionType.Health, level));
        if (Random.value < buffDropChance) loot.Add(Item.RandomBuffPotion());
        if (Random.value < skillPotionChance) loot.Add(Item.Potion(PotionType.SkillUp));
        }

        LootBag.Spawn(loot, transform.position + RandomOffset() * 0.5f);
    }
    static Vector3 RandomOffset()
    {
        Vector2 c = Random.insideUnitCircle.normalized * Random.Range(0.6f, 1.2f);
        return new Vector3(c.x, 0f, c.y);
    }

    void Start()
    {
        cooldown = Random.Range(0.6f, 1.6f);
        var p = FindAnyObjectByType<PlayerTopDown>();
        if (p != null)
        {
            target = p.GetComponent<Health>();
            targetCombatant = p.GetComponent<Combatant>();
        }
    }

    void Update()
    {
        if (health.IsDead) return;
        float dt = Time.deltaTime;

        if (target == null || target.IsDead)
        {
            combatant.SetBlock(false);
            motor.Tick(Vector3.zero);
            return;
        }

        Vector3 to = target.transform.position - transform.position;
        to.y = 0f;
        float dist = to.magnitude;
        Vector3 dir = dist > 0.001f ? to / dist : transform.forward;

        // 转向玩家（出招前摇时转得慢，给玩家绕背的机会）
        if (combatant.CanRotate)
        {
            float turn = combatant.State == CombatState.Windup ? (ranged ? 1.5f : 3f) : 7f;
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir), 1f - Mathf.Exp(-turn * dt));
        }

        // 走位
        float range = combatant.mainHand ? combatant.mainHand.range : 1.5f;
        float ideal = range * 0.8f;
        Vector3 move;
        if (ranged)
        {
            // 弓箭手：太远就靠近，太近就后退，中间距离绕圈
            if (dist > preferredDistance + 1.5f) move = dir;
            else if (dist < preferredDistance - 2.5f) move = -dir * 0.85f;
            else move = Vector3.Cross(Vector3.up, dir) * strafeDir * 0.6f;
        }
        else if (dist > ideal + 0.4f) move = dir;
        else if (dist < ideal - 0.7f) move = -dir * 0.6f;
        else move = Vector3.Cross(Vector3.up, dir) * strafeDir * 0.45f;

        strafeTimer -= dt;
        if (strafeTimer <= 0f)
        {
            strafeDir = Random.value < 0.5f ? -1f : 1f;
            strafeTimer = Random.Range(1f, 2.5f);
        }

        // 举盾
        // 架势越低越不敢举盾（越容易被打穿）；盾碎了就举不了
        bool hasShield = combatant.HasShield && !combatant.ShieldBroken;
        if (hasShield && targetCombatant != null && targetCombatant.IsWindingUp
            && targetCombatant.AttackId != lastSeenPlayerAttack && dist < 5f)
        {
            lastSeenPlayerAttack = targetCombatant.AttackId;
            float postureFrac = combatant.Posture / Mathf.Max(1f, combatant.maxPosture);
            if (Random.value < blockChance * Mathf.Lerp(0.5f, 1f, postureFrac)) blockUntil = Time.time + blockHoldTime;
        }
        combatant.SetBlock(hasShield && Time.time < blockUntil);

        // 出招
        cooldown -= dt;
        bool facing = Vector3.Angle(transform.forward, dir) < 35f;
        if (cooldown <= 0f && dist <= range + 0.3f && facing && combatant.State == CombatState.Idle && !combatant.IsBlocking)
        {
            bool canOff = combatant.offHand != null && combatant.offHand.CanAttack;
            lastHandOff = canOff && Random.value < offHandAttackChance;
            if (combatant.TryAttack(lastHandOff)) cooldown = Random.Range(attackCooldownMin, attackCooldownMax) * cooldownScale;
        }

        // 一定概率接连击
        if (!ranged && combatant.State == CombatState.Active && combatant.AttackId != lastOwnAttack)
        {
            lastOwnAttack = combatant.AttackId;
            if (Random.value < comboChance) combatant.TryAttack(lastHandOff);
        }

        motor.Tick(move * moveSpeed * combatant.MoveMultiplier + combatant.ExtraVelocity);
    }
}

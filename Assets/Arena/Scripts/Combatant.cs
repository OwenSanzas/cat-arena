using System.Collections.Generic;
using UnityEngine;

public enum CombatState { Idle, Windup, Active, Recovery, Stunned, Dodging, Dead }

// 战斗核心：主手/副手、攻击状态机、连击、格挡、翻滚、体力、攻击特效
// 玩家和敌人都用这个组件，区别只是"谁在按按钮"（PlayerTopDown / EnemyAI）
[RequireComponent(typeof(Health))]
public class Combatant : MonoBehaviour
{
    [Header("装备")]
    public WeaponData mainHand;
    public WeaponData offHand;

    [Header("挂点")]
    [Tooltip("身体模型的根（翻滚时转它）")]
    public Transform model;
    public Transform rightHand;
    public Transform leftHand;

    [Header("体力")]
    public float maxStamina = 100f;
    public float staminaRegen = 40f;
    public float staminaRegenDelay = 0.6f;

    [Header("架势（举盾挡攻击会掉架势，掉光 = 破盾）")]
    public float maxPosture = 100f;
    [Tooltip("多久没掉架势后开始恢复")]
    public float postureRegenDelay = 2f;
    public float postureRegen = 15f;
    [Tooltip("破盾后盾牌失效的时间")]
    public float shieldBrokenTime = 5f;
    [Tooltip("破盾后的硬直")]
    public float guardBreakStun = 1.6f;

    [Header("翻滚")]
    public float dodgeSpeed = 10f;
    public float dodgeDuration = 0.4f;
    public float dodgeCost = 25f;

    [Header("其他")]
    [Tooltip("动作时间倍率，>1 更慢（给敌人留反应时间）")]
    public float attackTimeMultiplier = 1f;
    [Tooltip("攻击时被打不会硬直")]
    public bool superArmor;
    [Tooltip("受击硬直时间倍率，<1 不容易被连续打断")]
    public float hitStunMultiplier = 1f;
    [Tooltip("能否完美格挡")]
    public bool canParry;
    [Tooltip("前摇时武器变红提示")]
    public bool telegraphAttacks;

    // 由装备 / 增益 / 技能每帧设置
    [System.NonSerialized] public float damageMultiplier = 1f;
    [System.NonSerialized] public float attackSpeedMultiplier = 1f;
    [System.NonSerialized] public float rangeMultiplier = 1f;
    [System.NonSerialized] public float staminaRegenMultiplier = 1f;
    [System.NonSerialized] public bool infiniteStamina;
    [System.NonSerialized] public Color? trailTint;   // 技能期间刀光颜色

    public bool IsSpinning => spinning;
    float TimeMul => attackTimeMultiplier / Mathf.Max(0.1f, attackSpeedMultiplier);

    bool spinning;
    float spinDamageMul, spinRadius, spinLifesteal, appliedRangeScale = 1f;
    Transform aimLine;
    Material aimMat;

    public CombatState State { get; private set; }
    public float Stamina { get; private set; }
    public float Posture { get; private set; }
    public bool ShieldBroken => Time.time < shieldBrokenUntil;
    public float ShieldBrokenRemaining => Mathf.Max(0f, shieldBrokenUntil - Time.time);
    public bool HasShield => offHand != null && offHand.canBlock;
    public int AttackId { get; private set; }          // 每次出招 +1，AI 用来判断"新的一招"
    public bool IsWindingUp => State == CombatState.Windup;
    public bool IsTwoHanded => mainHand != null && offHand == null;
    public bool IsBlocking => blockHeld && State == CombatState.Idle && HasShield && !ShieldBroken;
    public bool CanRotate => State == CombatState.Idle || State == CombatState.Windup || State == CombatState.Recovery;
    public bool IsFinisher => currentWeapon != null && currentWeapon.comboLength > 1
                              && comboIndex == currentWeapon.comboLength - 1;
    bool IsVerticalChop => currentWeapon != null && currentWeapon.type == WeaponType.Axe && IsFinisher;

    public float MoveMultiplier => State switch
    {
        CombatState.Idle => IsBlocking ? offHand.blockMoveMultiplier : 1f,
        CombatState.Windup or CombatState.Active => 0.2f,
        CombatState.Recovery => 0.5f,
        _ => 0f,
    };

    // 出招前冲 / 翻滚 的额外速度
    public Vector3 ExtraVelocity => State switch
    {
        CombatState.Active => transform.forward * currentWeapon.lunge,
        CombatState.Dodging => dodgeDir * dodgeSpeed * Mathf.Lerp(1.2f, 0.6f, 1f - stateTimer / stateDuration),
        _ => Vector3.zero,
    };

    Health health;
    WeaponData currentWeapon;
    bool currentIsOff;
    int comboIndex;
    float stateTimer, stateDuration;
    int bufferedHand = -1;            // 预输入：0 主手，1 副手
    float bufferTime;
    float lastStaminaUse = -10f;
    float lastPostureHit = -10f, shieldBrokenUntil = -10f;
    bool shieldWasBroken;
    float vulnerableUntil = -10f;
    readonly HashSet<Health> hitThisSwing = new();
    Vector3 dodgeDir;

    bool blockHeld, wasBlocking;
    float blockStartTime = -10f;

    Vector3 rightBase, leftBase;
    GameObject mainModel, offModel;
    Renderer[] mainRenderers = new Renderer[0], offRenderers = new Renderer[0];
    TrailRenderer mainTrail, offTrail;
    float mainTrailWidth, offTrailWidth;
    bool telegraphOn;

    void Awake()
    {
        health = GetComponent<Health>();
        Stamina = maxStamina;
        Posture = maxPosture;
        if (rightHand) rightBase = rightHand.localPosition;
        if (leftHand) leftBase = leftHand.localPosition;
        BuildModels();
    }

    // ---------- 装备 ----------
    public bool Equip(WeaponData main, WeaponData off, bool force = false)
    {
        if (State == CombatState.Dead) return false;
        if (State != CombatState.Idle && !force) return false;
        if (force && State != CombatState.Idle)
        {
            State = CombatState.Idle;
            spinning = false;
            bufferedHand = -1;
            health.invulnerable = false;
            if (model) model.localRotation = Quaternion.identity;
        }
        if (main != null && !main.canMainHand) return false;
        if (off != null && !off.canOffHand) return false;
        mainHand = main;
        offHand = off;
        BuildModels();
        return true;
    }

    void BuildModels()
    {
        if (mainModel) Destroy(mainModel);
        if (offModel) Destroy(offModel);
        mainModel = WeaponModel.Create(mainHand, rightHand);
        offModel = WeaponModel.Create(offHand, leftHand);
        mainRenderers = mainModel ? mainModel.GetComponentsInChildren<Renderer>() : new Renderer[0];
        offRenderers = offModel ? offModel.GetComponentsInChildren<Renderer>() : new Renderer[0];
        mainTrail = mainModel ? mainModel.GetComponentInChildren<TrailRenderer>() : null;
        offTrail = offModel ? offModel.GetComponentInChildren<TrailRenderer>() : null;
        mainTrailWidth = mainTrail ? mainTrail.widthMultiplier : 0f;
        offTrailWidth = offTrail ? offTrail.widthMultiplier : 0f;
        telegraphOn = false;
        appliedRangeScale = 1f;
    }

    // 武器模型随攻击范围加成变长
    void ApplyRangeScale()
    {
        if (Mathf.Abs(appliedRangeScale - rangeMultiplier) < 0.001f) return;
        appliedRangeScale = rangeMultiplier;
        foreach (var m in new[] { mainModel, offModel })
            if (m) m.transform.localScale = new Vector3(1f, 1f, rangeMultiplier);
    }

    // ---------- 回旋斩（技能） ----------
    public bool TrySpin(float damageMul, float radiusMul, float lifesteal = 0f, float duration = 0.5f)
    {
        if (State != CombatState.Idle && State != CombatState.Recovery) return false;
        if (mainHand == null || !mainHand.CanAttack) return false;
        currentWeapon = mainHand;
        currentIsOff = false;
        comboIndex = 0;
        hitThisSwing.Clear();
        bufferedHand = -1;
        AttackId++;
        spinning = true;
        spinDamageMul = damageMul;
        spinLifesteal = lifesteal;
        spinRadius = mainHand.range * rangeMultiplier * radiusMul;
        SetState(CombatState.Active, duration);
        StartTrail();
        Color c = trailTint ?? new Color(1f, 0.75f, 0.3f);
        FX.Shockwave(transform.position, new Color(c.r, c.g, c.b, 0.7f), spinRadius, 0.35f);
        FX.Disc(transform, new Color(c.r, c.g, c.b, 0.28f), spinRadius, duration);
        TopDownCamera.Shake(0.15f, 0.2f);
        return true;
    }

    // ---------- 输入接口（由玩家或 AI 调用） ----------
    public bool TryAttack(bool useOffHand)
    {
        var w = useOffHand ? offHand : mainHand;
        if (w == null || !w.CanAttack) return false;

        if (State == CombatState.Idle) return StartSwing(useOffHand, 0);

        if (State == CombatState.Windup || State == CombatState.Active || State == CombatState.Recovery)
        {
            bufferedHand = useOffHand ? 1 : 0;
            bufferTime = Time.time;
            return true;
        }
        return false;
    }

    public void SetBlock(bool held) => blockHeld = held;

    public bool TryDodge(Vector3 dir)
    {
        if (State != CombatState.Idle && State != CombatState.Recovery) return false;
        if (Stamina < dodgeCost * 0.5f) { NoStamina(); return false; }
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.01f) dir = -transform.forward;   // 没按方向就往后翻
        UseStamina(dodgeCost);
        dodgeDir = dir.normalized;
        bufferedHand = -1;
        health.invulnerable = true;
        SetTrails(false);
        SetState(CombatState.Dodging, dodgeDuration);
        FX.Sparks(transform.position + Vector3.up * 0.1f, -dodgeDir, new Color(0.6f, 0.55f, 0.45f), 8, 0.4f);
        return true;
    }

    public void Stagger(float duration)
    {
        if (State == CombatState.Dead) return;
        health.invulnerable = false;
        bufferedHand = -1;
        spinning = false;
        SetTrails(false);
        if (State == CombatState.Stunned && stateTimer > duration) return;   // 不缩短已有的长硬直（如破盾）
        SetState(CombatState.Stunned, duration);
    }

    public void SetDead()
    {
        State = CombatState.Dead;
        spinning = false;
        blockHeld = false;
        SetTrails(false);
        if (model) model.localRotation = Quaternion.identity;
    }

    // ---------- 攻击 ----------
    bool StartSwing(bool off, int combo)
    {
        var w = off ? offHand : mainHand;
        if (w == null || !w.CanAttack) return false;
        if (Stamina < w.staminaCost * 0.5f) { NoStamina(); return false; }

        UseStamina(w.staminaCost);
        currentWeapon = w;
        currentIsOff = off;
        comboIndex = combo % Mathf.Max(1, w.comboLength);
        hitThisSwing.Clear();
        bufferedHand = -1;
        AttackId++;
        SetTrails(false);
        SetState(CombatState.Windup, w.windup * TimeMul);
        return true;
    }

    float CurrentDamage()
    {
        var w = currentWeapon;
        float d = w.damage;
        if (currentIsOff) d *= w.offHandDamageMultiplier;
        else if (IsTwoHanded) d *= w.twoHandDamageMultiplier;
        if (IsFinisher && !spinning) d *= 1.35f;
        if (spinning) d *= spinDamageMul;
        return d * damageMultiplier;
    }

    void DoHitDetection()
    {
        var w = currentWeapon;
        Vector3 pos = transform.position;
        Vector3 fwd = transform.forward;
        const float targetRadius = 0.4f;
        bool landedAny = false;

        float range = spinning ? spinRadius : w.range * rangeMultiplier;
        foreach (var col in Physics.OverlapSphere(pos + Vector3.up, range + 1.5f))
        {
            var h = col.GetComponentInParent<Health>();
            if (h == null || h == health || h.IsDead || h.team == health.team || hitThisSwing.Contains(h)) continue;

            Vector3 to = h.transform.position - pos;
            to.y = 0f;
            bool inside;
            if (spinning)
            {
                inside = to.magnitude - targetRadius <= range;   // 回旋：360° 圆形
            }
            else if (w.shape == AttackShape.Arc)
            {
                inside = to.magnitude - targetRadius <= range
                         && (to.magnitude < 0.6f || Vector3.Angle(fwd, to) <= w.arcAngle * 0.5f);
            }
            else
            {
                Vector3 local = transform.InverseTransformDirection(to);
                inside = local.z > -targetRadius && local.z - targetRadius <= range
                         && Mathf.Abs(local.x) <= w.thrustWidth * 0.5f + targetRadius;
            }
            if (!inside) continue;

            hitThisSwing.Add(h);
            var hit = new HitInfo
            {
                damage = CurrentDamage(),
                sourcePos = pos,
                knockback = w.knockback * (IsFinisher || spinning ? 1.6f : 1f),
                hitStun = w.hitStun + (IsFinisher ? 0.2f : 0f),
                guardDamage = w.guardDamage * (IsFinisher ? 1.5f : 1f),
                heavy = IsFinisher || spinning,
                attacker = this,
            };
            float before = h.hp;
            if (h.TakeHit(hit))
            {
                landedAny = true;
                // 回旋吸血：按实际造成的伤害回血
                if (spinning && spinLifesteal > 0f)
                {
                    float dealt = Mathf.Max(0f, before - h.hp);
                    if (dealt > 0f) health.Heal(dealt * spinLifesteal);
                }
            }
        }

        if (landedAny)
        {
            FX.HitStop(spinning ? 0.03f : IsFinisher ? 0.09f : 0.045f);
            TopDownCamera.Shake(IsFinisher ? 0.25f : 0.12f, 0.12f);
        }
    }

    // ---------- 受击 ----------
    // 返回 false = 这一击被完全化解（完美格挡）
    public bool FilterIncomingHit(ref HitInfo hit)
    {
        // 破盾硬直中被打 = 破绽，伤害 ×1.5
        if (State == CombatState.Stunned && Time.time < vulnerableUntil)
        {
            hit.damage *= 1.5f;
            hit.heavy = true;
        }
        if (!IsBlocking) return true;

        Vector3 to = hit.sourcePos - transform.position;
        to.y = 0f;
        if (Vector3.Angle(transform.forward, to) > offHand.blockAngle * 0.5f) return true;   // 从侧面/背后打

        Vector3 head = transform.position + Vector3.up * health.HeadHeight;
        Vector3 shieldPos = transform.position + Vector3.up * 1.1f * transform.localScale.y + to.normalized * 0.6f;

        if (canParry && Time.time - blockStartTime <= offHand.parryWindow)
        {
            if (hit.attacker != null) hit.attacker.Stagger(1.2f);
            FX.Text(head + Vector3.up * 0.4f, "完美格挡！", new Color(0.4f, 0.9f, 1f), 30);
            FX.Sparks(shieldPos, to, new Color(0.5f, 0.9f, 1f), 40, 1.3f);
            FX.Shockwave(transform.position, new Color(0.5f, 0.9f, 1f, 0.7f), 2.2f, 0.25f);
            FX.HitStop(0.12f);
            TopDownCamera.Shake(0.2f, 0.15f);
            return false;
        }

        // 挡下攻击掉架势（重击掉更多）
        Posture = Mathf.Max(0f, Posture - hit.guardDamage * (hit.heavy ? 1.3f : 1f));
        lastPostureHit = Time.time;
        hit.blocked = true;
        if (Posture <= 0f)
        {
            // 破盾：盾牌碎掉一段时间不能用，长硬直，接下来挨打吃额外伤害
            hit.damage *= 0.6f;
            hit.blocked = false;
            hit.hitStun = 0f;
            shieldBrokenUntil = Time.time + shieldBrokenTime;
            Stagger(guardBreakStun);
            vulnerableUntil = Time.time + guardBreakStun;
            FX.Text(head + Vector3.up * 0.4f, "破盾！", new Color(1f, 0.5f, 0.2f), 32);
            FX.Sparks(shieldPos, to, new Color(1f, 0.55f, 0.2f), 30, 1.2f);
            FX.Sparks(shieldPos, Vector3.up, offHand.color, 25, 1.0f);   // 盾牌碎片
            FX.Shockwave(transform.position, new Color(1f, 0.5f, 0.2f, 0.6f), 2f, 0.3f);
            FX.HitStop(0.1f);
            TopDownCamera.Shake(0.25f, 0.15f);
        }
        else
        {
            hit.damage *= 1f - offHand.blockDamageReduction;
            hit.knockback *= 0.4f;
            hit.hitStun = 0f;
            FX.Sparks(shieldPos, to, new Color(0.85f, 0.9f, 1f), 14);
        }
        return true;
    }

    public void OnHitTaken(HitInfo hit)
    {
        if (State == CombatState.Dead) return;
        if (spinning) return;   // 回旋中不会被打断
        if (superArmor && (State == CombatState.Windup || State == CombatState.Active)) return;
        if (hit.hitStun > 0f) Stagger(hit.hitStun * hitStunMultiplier);
    }

    // ---------- 体力 ----------
    void UseStamina(float amount)
    {
        if (infiniteStamina) return;
        Stamina = Mathf.Max(0f, Stamina - amount);
        lastStaminaUse = Time.time;
    }

    void NoStamina()
    {
        if (health.team == Team.Player)
            FX.Text(transform.position + Vector3.up * health.HeadHeight, "体力不足", new Color(0.7f, 0.7f, 0.7f), 18);
    }

    // ---------- 每帧 ----------
    void SetState(CombatState s, float duration)
    {
        State = s;
        stateTimer = stateDuration = Mathf.Max(0.0001f, duration);
    }

    void Update()
    {
        if (State == CombatState.Dead) { AnimateHands(Time.deltaTime); return; }
        float dt = Time.deltaTime;

        // 举盾的瞬间记录时间（完美格挡窗口）。连续快速按不给窗口，防止乱按
        bool nowBlocking = IsBlocking;
        if (nowBlocking && !wasBlocking)
            blockStartTime = Time.time - blockStartTime > 0.6f ? Time.time : -10f;
        wasBlocking = nowBlocking;

        if (Time.time - lastStaminaUse > staminaRegenDelay && State != CombatState.Dodging)
            Stamina = Mathf.Min(maxStamina, Stamina + staminaRegen * staminaRegenMultiplier * dt);

        // 架势：一段时间没挨打才恢复，举盾时不恢复；盾修好时架势回满
        bool broken = ShieldBroken;
        if (!broken && !nowBlocking && Time.time - lastPostureHit > postureRegenDelay)
            Posture = Mathf.Min(maxPosture, Posture + postureRegen * dt);
        if (shieldWasBroken && !broken)
        {
            Posture = maxPosture;
            if (HasShield) FX.Text(transform.position + Vector3.up * health.HeadHeight, "盾牌修复", new Color(0.7f, 0.85f, 1f), 16);
        }
        shieldWasBroken = broken;
        if (offModel && HasShield && offModel.activeSelf == broken) offModel.SetActive(!broken);

        if (bufferedHand >= 0 && Time.time - bufferTime > 0.5f) bufferedHand = -1;

        stateTimer -= dt;
        switch (State)
        {
            case CombatState.Windup:
                if (stateTimer <= 0f)
                {
                    SetState(CombatState.Active, currentWeapon.active * TimeMul);
                    if (currentWeapon.shape == AttackShape.Projectile) FireArrow();
                    else StartTrail();
                }
                break;

            case CombatState.Active:
                if (spinning || currentWeapon.shape != AttackShape.Projectile) DoHitDetection();
                if (stateTimer <= 0f)
                {
                    if (IsVerticalChop && !spinning) ChopImpact();
                    if (spinning)
                    {
                        spinning = false;
                        FX.Shockwave(transform.position, new Color(1f, 0.8f, 0.4f, 0.5f), spinRadius * 0.8f, 0.25f);
                        SetState(CombatState.Recovery, 0.15f);
                        SetTrails(false);
                        break;
                    }
                    SetState(CombatState.Recovery, currentWeapon.recovery * TimeMul);
                    SetTrails(false);
                }
                break;

            case CombatState.Recovery:
                // 后摇里有预输入：同一只手 = 接连击，另一只手 = 换手攻击
                if (bufferedHand >= 0 && stateDuration - stateTimer > 0.08f)
                {
                    bool off = bufferedHand == 1;
                    int next = off == currentIsOff ? comboIndex + 1 : 0;
                    if (StartSwing(off, next)) break;
                    bufferedHand = -1;
                }
                if (stateTimer <= 0f) { State = CombatState.Idle; comboIndex = 0; }
                break;

            case CombatState.Stunned:
                if (stateTimer <= 0f) State = CombatState.Idle;
                break;

            case CombatState.Dodging:
                if (stateTimer <= 0f) { State = CombatState.Idle; health.invulnerable = false; }
                break;
        }

        if (infiniteStamina) Stamina = maxStamina;
        ApplyRangeScale();
        if (spinning && State == CombatState.Active) SpinSparks();
        UpdateTelegraph();
        UpdateAimLine();
        AnimateRoll(dt);
        AnimateHands(dt);
    }

    // ---------- 特效 ----------
    void StartTrail()
    {
        var tr = currentIsOff ? offTrail : mainTrail;
        if (tr == null) return;
        float baseWidth = currentIsOff ? offTrailWidth : mainTrailWidth;
        Color c = trailTint ?? (currentWeapon.rarity > 0 ? currentWeapon.RarityColor : new Color(1f, 1f, 1f));
        if (IsFinisher) c = Color.Lerp(c, new Color(1f, 0.6f, 0.2f), 0.6f);
        tr.Clear();
        tr.startColor = new Color(c.r, c.g, c.b, 0.85f);
        tr.endColor = new Color(c.r, c.g, c.b, 0f);
        tr.widthMultiplier = baseWidth * (spinning ? 1.8f : IsFinisher ? 1.5f : 1f);
        tr.time = spinning ? 0.3f : IsFinisher ? 0.2f : 0.13f;
        tr.emitting = true;
    }

    // ---------- 射箭 ----------
    void FireArrow()
    {
        var w = currentWeapon;
        Vector3 pos = transform.position + Vector3.up * 1.1f * transform.localScale.y + transform.forward * 0.8f;
        var hit = new HitInfo
        {
            damage = CurrentDamage(),
            knockback = w.knockback,
            hitStun = w.hitStun,
            guardDamage = w.guardDamage,
            attacker = this,
        };
        Projectile.Fire(pos, transform.forward, w.projectileSpeed, hit, health.team, new Color(1f, 0.85f, 0.6f));
        FX.Sparks(pos, transform.forward, new Color(1f, 0.9f, 0.7f), 5, 0.4f);
    }

    // 弓箭手拉弓时：地上一条红色瞄准线，提示箭会往哪飞
    void UpdateAimLine()
    {
        bool show = telegraphAttacks && State == CombatState.Windup && currentWeapon != null && currentWeapon.shape == AttackShape.Projectile;
        if (!show) { if (aimLine) aimLine.gameObject.SetActive(false); return; }
        if (aimLine == null)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Destroy(go.GetComponent<Collider>());
            go.name = "AimLine";
            aimLine = go.transform;
            aimLine.SetParent(transform, false);
            aimMat = new Material(Shader.Find("Sprites/Default"));
            var r = go.GetComponent<Renderer>();
            r.sharedMaterial = aimMat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
        aimLine.gameObject.SetActive(true);
        float len = Mathf.Min(currentWeapon.range, 14f) / transform.localScale.z;
        aimLine.localPosition = new Vector3(0, 0.05f, len / 2f + 0.5f);
        aimLine.localScale = new Vector3(0.12f, 0.01f, len);
        float k = 1f - Mathf.Clamp01(stateTimer / stateDuration);
        aimMat.color = new Color(1f, 0.15f, 0.1f, 0.15f + 0.5f * k);
    }

    // 回旋时武器尖端甩出火花
    void SpinSparks()
    {
        if (rightHand == null || currentWeapon == null) return;
        Vector3 tip = rightHand.position + rightHand.forward * currentWeapon.length * rangeMultiplier * transform.localScale.z;
        Color c = trailTint ?? new Color(1f, 0.75f, 0.3f);
        FX.Sparks(tip, rightHand.right, c, 2, 0.5f);
    }

    void SetTrails(bool on)
    {
        if (mainTrail) mainTrail.emitting = on;
        if (offTrail) offTrail.emitting = on;
    }

    // 斧头竖劈砸地：冲击波 + 尘土
    void ChopImpact()
    {
        Vector3 p = transform.position + transform.forward * (currentWeapon.range * 0.7f * transform.localScale.z);
        FX.Shockwave(p, new Color(1f, 0.75f, 0.4f, 0.75f), currentWeapon.range * 1.1f, 0.35f);
        FX.Sparks(p + Vector3.up * 0.1f, Vector3.up, new Color(0.65f, 0.55f, 0.4f), 25, 0.7f);
        TopDownCamera.Shake(0.3f, 0.15f);
    }

    void UpdateTelegraph()
    {
        bool want = telegraphAttacks && State == CombatState.Windup;
        if (want == telegraphOn) return;
        telegraphOn = want;
        var mpb = new MaterialPropertyBlock();
        mpb.SetColor("_Color", new Color(1f, 0.15f, 0.1f));
        foreach (var r in currentIsOff ? offRenderers : mainRenderers)
            if (r && !(r is TrailRenderer)) r.SetPropertyBlock(want ? mpb : null);
        if (!want) foreach (var r in currentIsOff ? mainRenderers : offRenderers) if (r) r.SetPropertyBlock(null);
    }

    // ---------- 程序化动作 ----------
    // 翻滚：整个身体绕"翻滚方向的侧轴"转一圈
    void AnimateRoll(float dt)
    {
        if (model == null) return;
        if (State == CombatState.Dodging)
        {
            float t = 1f - Mathf.Clamp01(stateTimer / stateDuration);
            float angle = 360f * Mathf.SmoothStep(0f, 1f, t);
            Vector3 axis = Vector3.Cross(Vector3.up, dodgeDir);
            model.rotation = Quaternion.AngleAxis(angle, axis) * transform.rotation;
            float squash = 1f - 0.25f * Mathf.Sin(t * Mathf.PI);
            model.localScale = new Vector3(1f, squash, 1f);
        }
        else if (spinning && State == CombatState.Active)
        {
            // 回旋：身体绕竖轴转两圈
            float t = 1f - Mathf.Clamp01(stateTimer / stateDuration);
            model.localRotation = Quaternion.AngleAxis(-720f * t, Vector3.up);
            model.localScale = Vector3.one;
        }
        else
        {
            model.localRotation = Quaternion.Slerp(model.localRotation, Quaternion.identity, 1f - Mathf.Exp(-25f * dt));
            model.localScale = Vector3.Lerp(model.localScale, Vector3.one, 1f - Mathf.Exp(-20f * dt));
        }
    }

    void AnimateHands(float dt)
    {
        AnimateHand(rightHand, rightBase, mainHand, false, dt);
        AnimateHand(leftHand, leftBase, offHand, true, dt);
    }

    void AnimateHand(Transform hand, Vector3 basePos, WeaponData w, bool isOff, float dt)
    {
        if (hand == null) return;
        float side = isOff ? -1f : 1f;
        Vector3 pos = basePos;
        Quaternion rot = Quaternion.Euler(35f, 15f * side, 0f);   // 待机：武器斜向前下
        float follow = 15f;
        bool snap = false;
        float t = 1f - Mathf.Clamp01(stateTimer / stateDuration);
        bool attackingThis = currentWeapon != null && currentIsOff == isOff &&
                             (State == CombatState.Windup || State == CombatState.Active || State == CombatState.Recovery);

        if (State == CombatState.Stunned || State == CombatState.Dead)
        {
            rot = Quaternion.Euler(70f, 30f * side, 0f);
            follow = 10f;
        }
        else if (State == CombatState.Dodging)
        {
            rot = Quaternion.Euler(80f, 0f, 0f);   // 翻滚时收起武器
            follow = 30f;
        }
        else if (spinning)
        {
            // 回旋：双臂向两侧平伸
            rot = Quaternion.Euler(5f, 95f * side, 0f);
            pos = basePos + new Vector3(0.1f * side, 0.1f, -0.1f);
            snap = true;
        }
        else if (w != null && w.canBlock)
        {
            if (IsBlocking)
            {
                pos = new Vector3(basePos.x * 0.25f, basePos.y + 0.1f, basePos.z + 0.35f);
                rot = Quaternion.identity;
                follow = 30f;
            }
            else rot = Quaternion.Euler(0f, 45f * side, 0f);
        }
        else if (attackingThis)
        {
            var cw = currentWeapon;
            if (cw.shape == AttackShape.Projectile)
            {
                // 拉弓：弓举到身前，拉弓时往后收，放箭后回弹
                rot = Quaternion.Euler(0f, -8f * side, 0f);
                pos = State == CombatState.Windup ? basePos + new Vector3(-0.25f * side, 0.25f, 0.2f)
                                                  : basePos + new Vector3(-0.25f * side, 0.25f, 0.35f);
                follow = State == CombatState.Windup ? 12f : 30f;
            }
            else if (cw.shape == AttackShape.Thrust)
            {
                rot = Quaternion.Euler(0f, -4f * side, 0f);
                float extend = cw.range * 0.4f;
                if (State == CombatState.Windup) { pos = basePos + Vector3.back * 0.45f; follow = 20f; }
                else if (State == CombatState.Active) { pos = basePos + Vector3.forward * Mathf.Lerp(-0.45f, extend, EaseOut(t)); snap = true; }
                else { pos = basePos + Vector3.forward * (t < 0.3f ? extend : 0f); follow = 10f; }
            }
            else
            {
                float a = cw.arcAngle * 0.5f + 10f;
                bool vertical = IsVerticalChop;                          // 斧头重击：竖劈
                float d = side * (comboIndex % 2 == 0 ? 1f : -1f);       // 连击左右交替
                Quaternion start = vertical ? Quaternion.Euler(-75f, 0f, 0f) : Quaternion.Euler(5f, a * d, 0f);
                Quaternion end = vertical ? Quaternion.Euler(75f, 0f, 0f) : Quaternion.Euler(15f, -a * d, 0f);
                if (vertical) pos = new Vector3(basePos.x * 0.3f, basePos.y + 0.2f, basePos.z);

                if (State == CombatState.Windup) { rot = start; follow = 25f; }
                else if (State == CombatState.Active) { rot = Quaternion.Slerp(start, end, EaseOut(t)); snap = true; }
                else { rot = t < 0.3f ? end : rot; follow = 10f; }
            }
        }

        if (snap)
        {
            hand.localRotation = rot;
            hand.localPosition = pos;
        }
        else
        {
            float k = 1f - Mathf.Exp(-follow * dt);
            hand.localRotation = Quaternion.Slerp(hand.localRotation, rot, k);
            hand.localPosition = Vector3.Lerp(hand.localPosition, pos, k);
        }
    }

    static float EaseOut(float t) => 1f - (1f - t) * (1f - t);
}

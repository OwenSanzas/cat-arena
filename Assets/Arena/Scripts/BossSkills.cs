using System.Collections;
using UnityEngine;

// Boss 专属技能：
//   砸地：脚下出现红圈慢慢填满，填满时圈内受到重击（翻滚躲开 / 跑出圈 / 举盾挡）
//   冲锋（大 Boss）：地上出现红色直线，然后沿直线猛冲撞人
//   召唤（大 Boss）：血量降到 60% 和 30% 时叫小弟
[RequireComponent(typeof(EnemyAI))]
public class BossSkills : MonoBehaviour
{
    public string bossTitle = "Boss";
    public bool bigBoss;

    [Header("砸地")]
    public float slamCooldown = 7f;
    public float slamRadius = 4.5f;
    public float slamDamage = 35f;
    public float slamWindup = 1.1f;

    [Header("冲锋")]
    public bool canCharge;
    public float chargeCooldown = 9f;
    public float chargeDamage = 30f;
    public float chargeSpeed = 17f;

    [Header("召唤")]
    public GameObject summonPrefab;
    public int summonCount = 3;

    public static System.Action<GameObject> OnSummon;   // ArenaManager 订阅，把小弟算进本波

    EnemyAI ai;
    Combatant combatant;
    Health health;
    CharacterMotor motor;
    Health target;
    float nextSlam, nextCharge;
    bool busy;
    int summonsDone;

    void Awake()
    {
        ai = GetComponent<EnemyAI>();
        combatant = GetComponent<Combatant>();
        health = GetComponent<Health>();
        motor = GetComponent<CharacterMotor>();
    }

    void Start()
    {
        var p = FindAnyObjectByType<PlayerTopDown>();
        if (p) target = p.GetComponent<Health>();
        nextSlam = Time.time + 4f;
        nextCharge = Time.time + 6f;
        FX.Text(transform.position + Vector3.up * health.HeadHeight, bossTitle + " 登场！", new Color(1f, 0.4f, 0.3f), 34);
        TopDownCamera.Shake(0.3f, 0.4f);
    }

    void Update()
    {
        if (health.IsDead || busy || target == null || target.IsDead) return;

        // 召唤小弟
        if (summonPrefab != null)
        {
            float frac = health.hp / health.maxHp;
            if ((summonsDone == 0 && frac < 0.6f) || (summonsDone == 1 && frac < 0.3f))
            {
                summonsDone++;
                Summon();
            }
        }

        if (combatant.State != CombatState.Idle) return;
        float dist = Vector3.Distance(transform.position, target.transform.position);
        if (Time.time >= nextSlam && dist < slamRadius + 2f) StartCoroutine(Slam());
        else if (canCharge && Time.time >= nextCharge && dist > 5f && dist < 16f) StartCoroutine(Charge());
    }

    IEnumerator Slam()
    {
        busy = true;
        ai.enabled = false;
        Vector3 center = transform.position;
        TelegraphFX.Circle(center, slamRadius, slamWindup, new Color(1f, 0.15f, 0.1f));
        FX.Text(transform.position + Vector3.up * (health.HeadHeight + 0.4f), "砸地！", new Color(1f, 0.5f, 0.3f), 26);

        for (float t = 0; t < slamWindup; t += Time.deltaTime)
        {
            if (health.IsDead) yield break;
            // 蓄力：身体往上提
            if (combatant.model) combatant.model.localPosition = new Vector3(0, 0.9f + 0.4f * (t / slamWindup), 0);
            motor.Tick(Vector3.zero);
            yield return null;
        }
        if (combatant.model) combatant.model.localPosition = new Vector3(0, 0.9f, 0);

        FX.Shockwave(center, new Color(1f, 0.5f, 0.2f, 0.85f), slamRadius, 0.4f);
        FX.Sparks(center + Vector3.up * 0.2f, Vector3.up, new Color(0.7f, 0.55f, 0.4f), 50, 1.4f);
        TopDownCamera.Shake(0.5f, 0.3f);
        FX.HitStop(0.08f);

        Vector3 d = target.transform.position - center; d.y = 0;
        if (d.magnitude <= slamRadius + 0.3f)
            target.TakeHit(new HitInfo
            {
                damage = slamDamage * combatant.damageMultiplier,
                sourcePos = center, knockback = 11f, hitStun = 0.6f, guardDamage = 60f, heavy = true, attacker = combatant,
            });

        yield return Recover(0.6f);
        nextSlam = Time.time + slamCooldown;
    }

    IEnumerator Charge()
    {
        busy = true;
        ai.enabled = false;
        Vector3 dir = target.transform.position - transform.position; dir.y = 0; dir.Normalize();
        transform.rotation = Quaternion.LookRotation(dir);
        float length = 13f;
        TelegraphFX.Line(transform.position, dir, length, 1.6f, 0.8f, new Color(1f, 0.15f, 0.1f));
        FX.Text(transform.position + Vector3.up * (health.HeadHeight + 0.4f), "冲锋！", new Color(1f, 0.5f, 0.3f), 26);

        for (float t = 0; t < 0.8f; t += Time.deltaTime)
        {
            if (health.IsDead) yield break;
            motor.Tick(Vector3.zero);
            yield return null;
        }

        bool hitDone = false;
        float travelled = 0f;
        while (travelled < length && !health.IsDead)
        {
            float dt = Time.deltaTime;
            Vector3 before = transform.position;
            motor.Tick(dir * chargeSpeed);
            travelled += chargeSpeed * dt;
            if (dt > 0f && (transform.position - before).magnitude < chargeSpeed * dt * 0.2f && travelled > 1f) break;   // 撞墙
            if (Random.value < 0.5f) FX.Sparks(transform.position + Vector3.up * 0.1f, -dir, new Color(0.7f, 0.6f, 0.45f), 2, 0.5f);

            Vector3 to = target.transform.position - transform.position; to.y = 0;
            if (!hitDone && to.magnitude < 1.6f * transform.localScale.x)
            {
                hitDone = target.TakeHit(new HitInfo
                {
                    damage = chargeDamage * combatant.damageMultiplier,
                    sourcePos = transform.position, knockback = 14f, hitStun = 0.7f, guardDamage = 50f, heavy = true, attacker = combatant,
                }) || hitDone;
            }
            yield return null;
        }
        TopDownCamera.Shake(0.3f, 0.2f);
        yield return Recover(0.8f);
        nextCharge = Time.time + chargeCooldown;
    }

    IEnumerator Recover(float t)
    {
        // 放完技能有个破绽，可以趁机输出
        for (float k = 0; k < t; k += Time.deltaTime)
        {
            if (health.IsDead) yield break;
            motor.Tick(Vector3.zero);
            yield return null;
        }
        busy = false;
        if (!health.IsDead) ai.enabled = true;
    }

    void Summon()
    {
        FX.Text(transform.position + Vector3.up * (health.HeadHeight + 0.4f), "小的们，上！", new Color(1f, 0.6f, 0.3f), 28);
        for (int i = 0; i < summonCount; i++)
        {
            Vector2 c = Random.insideUnitCircle.normalized * 2.5f;
            Vector3 pos = transform.position + new Vector3(c.x, 0, c.y);
            var go = Instantiate(summonPrefab, pos, transform.rotation);
            FX.Shockwave(pos, new Color(0.8f, 0.4f, 1f, 0.7f), 1.2f, 0.3f);
            OnSummon?.Invoke(go);
        }
    }

    void OnDisable()
    {
        if (combatant && combatant.model) combatant.model.localPosition = new Vector3(0, 0.9f, 0);
    }
}

// 地面预警：圆形（中间慢慢填满）/ 直线
public class TelegraphFX : MonoBehaviour
{
    Transform fill;
    Material outerMat, fillMat;
    float duration, t;
    Vector3 fullScale;
    bool isLine;

    public static void Circle(Vector3 pos, float radius, float duration, Color c) => Make(pos, Quaternion.identity, new Vector3(radius * 2, 0.005f, radius * 2), duration, c, false);

    public static void Line(Vector3 start, Vector3 dir, float length, float width, float duration, Color c)
    {
        dir.y = 0;
        Vector3 center = start + dir.normalized * (length / 2f);
        Make(center, Quaternion.LookRotation(dir), new Vector3(width, 0.005f, length), duration, c, true);
    }

    static void Make(Vector3 pos, Quaternion rot, Vector3 scale, float duration, Color c, bool line)
    {
        var go = new GameObject("Telegraph");
        go.transform.SetPositionAndRotation(new Vector3(pos.x, 0.04f, pos.z), rot);
        var tf = go.AddComponent<TelegraphFX>();
        tf.duration = duration;
        tf.fullScale = scale;
        tf.isLine = line;
        var type = line ? PrimitiveType.Cube : PrimitiveType.Cylinder;
        tf.outerMat = new Material(Shader.Find("Sprites/Default")) { color = new Color(c.r, c.g, c.b, 0.22f) };
        tf.fillMat = new Material(Shader.Find("Sprites/Default")) { color = new Color(c.r, c.g, c.b, 0.4f) };
        Child(go.transform, type, scale, tf.outerMat);
        tf.fill = Child(go.transform, type, Vector3.zero, tf.fillMat).transform;
        tf.fill.localPosition = Vector3.up * 0.01f;
    }

    static GameObject Child(Transform parent, PrimitiveType type, Vector3 scale, Material m)
    {
        var g = GameObject.CreatePrimitive(type);
        Destroy(g.GetComponent<Collider>());
        g.transform.SetParent(parent, false);
        g.transform.localScale = scale;
        var r = g.GetComponent<Renderer>();
        r.sharedMaterial = m;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        return g;
    }

    void Update()
    {
        t += Time.deltaTime;
        float k = Mathf.Clamp01(t / duration);
        fill.localScale = isLine ? new Vector3(fullScale.x, fullScale.y, fullScale.z * k)
                                 : new Vector3(fullScale.x * k, fullScale.y, fullScale.z * k);
        if (isLine) fill.localPosition = new Vector3(0, 0.01f, -fullScale.z / 2f * (1f - k));
        float flash = 0.4f + 0.3f * Mathf.Sin(t * 20f) * k;
        var c = fillMat.color; fillMat.color = new Color(c.r, c.g, c.b, flash);
        if (t >= duration) { Destroy(outerMat); Destroy(fillMat); Destroy(gameObject); }
    }
}

using System;
using System.Collections;
using UnityEngine;

public enum Team { Player, Enemy }

// 一次攻击的信息
public struct HitInfo
{
    public float damage;
    public Vector3 sourcePos;
    public float knockback;
    public float hitStun;
    public float guardDamage;
    public bool heavy;        // 连击最后一段
    public bool blocked;      // 被盾挡住（由防御方填写）
    public Combatant attacker;
}

// 生命值：受伤、死亡、受击反馈
public class Health : MonoBehaviour
{
    public string displayName = "";
    public Team team;
    public float maxHp = 100f;
    [NonSerialized] public float hp;
    [NonSerialized] public bool invulnerable;   // 闪避无敌
    [NonSerialized] public float damageReduction;   // 0~0.9，护甲 / 角色 / 药水

    public bool IsDead => hp <= 0f;
    public float HeadHeight => 2.2f * transform.localScale.y;

    public event Action<HitInfo> OnDamaged;
    public event Action OnDied;

    Combatant combatant;
    CharacterMotor motor;

    void Awake()
    {
        hp = maxHp;
        combatant = GetComponent<Combatant>();
        motor = GetComponent<CharacterMotor>();
    }

    public void Heal(float amount)
    {
        if (IsDead) return;
        hp = Mathf.Min(maxHp, hp + amount);
        FX.Text(transform.position + Vector3.up * HeadHeight, "+" + Mathf.RoundToInt(amount), new Color(0.4f, 1f, 0.4f));
    }

    // 返回 true = 打中了（包括被格挡）
    public bool TakeHit(HitInfo hit)
    {
        if (IsDead || invulnerable) return false;

        // 先让战斗组件判断格挡/完美格挡
        if (combatant != null && !combatant.FilterIncomingHit(ref hit)) return false;
        hit.damage *= 1f - Mathf.Clamp(damageReduction, 0f, 0.9f);

        hp -= hit.damage;

        Vector3 head = transform.position + Vector3.up * HeadHeight;
        Color c = hit.blocked ? new Color(0.7f, 0.7f, 0.7f)
                : team == Team.Player ? new Color(1f, 0.35f, 0.3f)
                : hit.heavy ? new Color(1f, 0.8f, 0.2f) : Color.white;
        FX.Text(head, Mathf.RoundToInt(hit.damage).ToString(), c, hit.heavy ? 30 : 22);

        Vector3 dir = transform.position - hit.sourcePos;
        dir.y = 0f;
        if (motor != null) motor.AddImpulse(dir.normalized * hit.knockback);

        if (!hit.blocked)
        {
            // 命中火花：从被打的位置往"远离攻击者"的方向喷
            Vector3 chest = transform.position + Vector3.up * 1.1f * transform.localScale.y - dir.normalized * 0.3f;
            Color sc = team == Team.Player ? new Color(1f, 0.3f, 0.25f)
                     : hit.heavy ? new Color(1f, 0.6f, 0.2f) : new Color(1f, 0.88f, 0.5f);
            FX.Sparks(chest, dir, sc, hit.heavy ? 32 : 16, hit.heavy ? 1.3f : 1f);
            if (hit.heavy) FX.Shockwave(transform.position, new Color(1f, 0.7f, 0.3f, 0.5f), 1.4f, 0.25f);
            StartCoroutine(Flash());
        }
        OnDamaged?.Invoke(hit);

        if (hp <= 0f)
        {
            hp = 0f;
            Die();
        }
        else if (combatant != null)
        {
            combatant.OnHitTaken(hit);
        }
        return true;
    }

    void Die()
    {
        var cc = GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;
        if (combatant != null) combatant.SetDead();
        OnDied?.Invoke();
        StartCoroutine(FallOver());
    }

    IEnumerator Flash()
    {
        var rs = GetComponentsInChildren<Renderer>();
        var mpb = new MaterialPropertyBlock();
        mpb.SetColor("_Color", Color.white);
        foreach (var r in rs) if (!(r is TrailRenderer)) r.SetPropertyBlock(mpb);
        yield return new WaitForSecondsRealtime(0.08f);
        foreach (var r in rs) if (r != null) r.SetPropertyBlock(null);
    }

    IEnumerator FallOver()
    {
        Quaternion from = transform.rotation;
        Quaternion to = from * Quaternion.Euler(-90f, 0f, 0f);
        for (float t = 0; t < 1f; t += Time.deltaTime / 0.35f)
        {
            transform.rotation = Quaternion.Slerp(from, to, t * t);
            yield return null;
        }
        transform.rotation = to;
    }
}

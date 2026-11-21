using UnityEngine;

// 箭：直线飞，打到对方阵营就造成伤害（能被盾挡、翻滚无敌能躲），撞墙/柱子就停
public class Projectile : MonoBehaviour
{
    [System.NonSerialized] public HitInfo hit;
    public Team ownerTeam;
    public Vector3 velocity;
    public float lifetime = 2.5f;
    const float Radius = 0.35f;

    public static Projectile Fire(Vector3 pos, Vector3 dir, float speed, HitInfo hit, Team team, Color color)
    {
        var go = new GameObject("Arrow");
        go.transform.position = pos;
        go.transform.rotation = Quaternion.LookRotation(dir);
        var p = go.AddComponent<Projectile>();
        p.hit = hit;
        p.ownerTeam = team;
        p.velocity = dir.normalized * speed;

        // 箭杆 + 箭头 + 箭羽
        Part(go.transform, new Vector3(0, 0, 0), new Vector3(0.04f, 0.04f, 0.7f), new Color(0.55f, 0.4f, 0.25f));
        Part(go.transform, new Vector3(0, 0, 0.38f), new Vector3(0.09f, 0.03f, 0.1f), new Color(0.75f, 0.78f, 0.8f));
        Part(go.transform, new Vector3(0, 0, -0.32f), new Vector3(0.12f, 0.01f, 0.1f), new Color(0.9f, 0.9f, 0.9f));
        var tr = go.AddComponent<TrailRenderer>();
        tr.time = 0.12f;
        tr.widthMultiplier = 0.08f;
        tr.widthCurve = AnimationCurve.Linear(0, 1, 1, 0);
        tr.sharedMaterial = WeaponModel.Unlit(Color.white);
        tr.startColor = new Color(color.r, color.g, color.b, 0.7f);
        tr.endColor = new Color(color.r, color.g, color.b, 0f);
        tr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        return p;
    }

    static void Part(Transform parent, Vector3 pos, Vector3 scale, Color c)
    {
        var g = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Destroy(g.GetComponent<Collider>());
        g.transform.SetParent(parent, false);
        g.transform.localPosition = pos;
        g.transform.localScale = scale;
        g.GetComponent<Renderer>().sharedMaterial = WeaponModel.Lit(c);
    }

    void Update()
    {
        float dt = Time.deltaTime;
        if (dt <= 0f) return;
        lifetime -= dt;
        if (lifetime <= 0f) { Destroy(gameObject); return; }

        Vector3 step = velocity * dt;
        // 撞到障碍物（没有 Health 的碰撞体）
        if (Physics.Raycast(transform.position, velocity.normalized, out var rh, step.magnitude, ~0, QueryTriggerInteraction.Ignore)
            && rh.collider.GetComponentInParent<Health>() == null)
        {
            FX.Sparks(rh.point, -velocity, new Color(0.8f, 0.7f, 0.5f), 6, 0.4f);
            Destroy(gameObject);
            return;
        }
        transform.position += step;

        foreach (var col in Physics.OverlapSphere(transform.position, Radius))
        {
            var h = col.GetComponentInParent<Health>();
            if (h == null || h.IsDead || h.team == ownerTeam) continue;
            hit.sourcePos = transform.position - velocity.normalized * 3f;   // 从箭飞来的方向判断格挡
            if (h.TakeHit(hit))
            {
                Destroy(gameObject);
                return;
            }
        }
    }
}

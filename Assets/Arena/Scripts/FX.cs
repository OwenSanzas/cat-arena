using System.Collections.Generic;
using UnityEngine;

// 打击感：伤害飘字、顿帧、火花粒子、冲击光圈
public class FX : MonoBehaviour
{
    static FX inst;
    static FX I
    {
        get
        {
            if (inst == null) inst = new GameObject("[FX]").AddComponent<FX>();
            return inst;
        }
    }

    class Popup { public Vector3 pos; public string text; public Color color; public float t; public int size; }
    class Ring { public Transform tr; public Material mat; public Color color; public float t, duration, radius; }

    readonly List<Popup> popups = new();
    readonly List<Ring> rings = new();
    float hitStopEnd;
    bool stopping;
    ParticleSystem sparks;

    // ---------- 飘字 ----------
    public static void Text(Vector3 worldPos, string text, Color color, int size = 22)
    {
        worldPos += new Vector3(Random.Range(-0.3f, 0.3f), 0f, Random.Range(-0.2f, 0.2f));
        I.popups.Add(new Popup { pos = worldPos, text = text, color = color, size = size });
    }

    // ---------- 顿帧 ----------
    // 打开背包 / 搜刮时暂停
    public static bool Paused { get; private set; }
    public static void SetPaused(bool p)
    {
        Paused = p;
        Time.timeScale = p ? 0f : 1f;
    }

    public static void HitStop(float seconds)
    {
        if (Paused) return;
        I.hitStopEnd = Mathf.Max(I.hitStopEnd, Time.unscaledTime + seconds);
        I.stopping = true;
        Time.timeScale = 0.05f;
    }

    // ---------- 火花：朝 dir 方向喷出 ----------
    public static void Sparks(Vector3 pos, Vector3 dir, Color color, int count, float speed = 1f)
    {
        var ps = I.GetSparks();
        dir = dir.sqrMagnitude > 0.001f ? dir.normalized : Vector3.up;
        var ep = new ParticleSystem.EmitParams { applyShapeToPosition = false };
        for (int i = 0; i < count; i++)
        {
            ep.position = pos;
            ep.velocity = (dir + Random.insideUnitSphere * 0.9f + Vector3.up * 0.3f).normalized * Random.Range(3f, 10f) * speed;
            ep.startColor = Color.Lerp(color, Color.white, Random.value * 0.5f);
            ep.startSize = Random.Range(0.05f, 0.13f);
            ep.startLifetime = Random.Range(0.15f, 0.4f);
            ps.Emit(ep, 1);
        }
    }

    // ---------- 地面冲击光圈 ----------
    public static void Shockwave(Vector3 pos, Color color, float radius, float duration = 0.3f)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Destroy(go.GetComponent<Collider>());
        go.name = "Shockwave";
        go.transform.SetParent(I.transform);
        go.transform.position = new Vector3(pos.x, 0.03f, pos.z);
        var mat = new Material(Shader.Find("Sprites/Default")) { color = color };
        var r = go.GetComponent<Renderer>();
        r.sharedMaterial = mat;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        I.rings.Add(new Ring { tr = go.transform, mat = mat, color = color, duration = duration, radius = radius });
        go.transform.localScale = new Vector3(0.1f, 0.005f, 0.1f);
    }

    // ---------- 跟随角色的半透明旋转圆盘（回旋斩） ----------
    public static void Disc(Transform follow, Color color, float radius, float duration)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Destroy(go.GetComponent<Collider>());
        go.name = "SpinDisc";
        var mat = new Material(Shader.Find("Sprites/Default")) { color = color };
        var r = go.GetComponent<Renderer>();
        r.sharedMaterial = mat;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        var d = go.AddComponent<FollowDisc>();
        d.follow = follow; d.mat = mat; d.color = color; d.radius = radius; d.duration = duration;
    }

    ParticleSystem GetSparks()
    {
        if (sparks != null) return sparks;
        var go = new GameObject("Sparks");
        go.transform.SetParent(transform);
        sparks = go.AddComponent<ParticleSystem>();
        sparks.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = sparks.main;
        main.playOnAwake = false;
        main.loop = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.gravityModifier = 1.6f;
        main.maxParticles = 1000;
        main.useUnscaledTime = true;   // 顿帧时火花照样飞，更有冲击感

        var emission = sparks.emission;
        emission.enabled = false;
        var shape = sparks.shape;
        shape.enabled = false;

        var col = sparks.colorOverLifetime;
        col.enabled = true;
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                  new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.6f), new GradientAlphaKey(0f, 1f) });
        col.color = g;

        var size = sparks.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.2f));

        var rend = go.GetComponent<ParticleSystemRenderer>();
        rend.sharedMaterial = WeaponModel.Unlit(Color.white);
        rend.renderMode = ParticleSystemRenderMode.Stretch;
        rend.velocityScale = 0.035f;
        rend.lengthScale = 1.5f;
        rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        sparks.Play();
        return sparks;
    }

    void Update()
    {
        if (stopping && Time.unscaledTime >= hitStopEnd)
        {
            stopping = false;
            if (!Paused) Time.timeScale = 1f;
        }

        float dt = Time.unscaledDeltaTime;
        for (int i = popups.Count - 1; i >= 0; i--)
        {
            var p = popups[i];
            p.t += dt;
            p.pos += Vector3.up * (1.5f * dt);
            if (p.t > 0.9f) popups.RemoveAt(i);
        }

        for (int i = rings.Count - 1; i >= 0; i--)
        {
            var r = rings[i];
            r.t += dt;
            float k = Mathf.Clamp01(r.t / r.duration);
            float d = Mathf.Lerp(0.2f, r.radius * 2f, 1f - (1f - k) * (1f - k));
            r.tr.localScale = new Vector3(d, 0.005f, d);
            r.mat.color = new Color(r.color.r, r.color.g, r.color.b, r.color.a * (1f - k));
            if (k >= 1f)
            {
                Destroy(r.tr.gameObject);
                Destroy(r.mat);
                rings.RemoveAt(i);
            }
        }
    }

    void OnGUI()
    {
        var cam = Camera.main;
        if (cam == null) return;
        var style = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
        foreach (var p in popups)
        {
            Vector3 s = cam.WorldToScreenPoint(p.pos);
            if (s.z < 0) continue;
            float alpha = 1f - Mathf.Clamp01((p.t - 0.5f) / 0.4f);
            float pop = p.t < 0.1f ? Mathf.Lerp(1.6f, 1f, p.t / 0.1f) : 1f;
            style.fontSize = Mathf.RoundToInt(p.size * pop);
            var r = new Rect(s.x - 100, Screen.height - s.y - 20, 200, 40);
            style.normal.textColor = new Color(0, 0, 0, alpha * 0.8f);
            GUI.Label(new Rect(r.x + 2, r.y + 2, r.width, r.height), p.text, style);
            style.normal.textColor = new Color(p.color.r, p.color.g, p.color.b, alpha);
            GUI.Label(r, p.text, style);
        }
    }

    void OnDestroy()
    {
        Time.timeScale = 1f;
        Paused = false;
        if (inst == this) inst = null;
    }
}

// 回旋斩圆盘：跟着角色，先张开再淡出
public class FollowDisc : MonoBehaviour
{
    public Transform follow;
    public Material mat;
    public Color color;
    public float radius, duration, t;

    void Update()
    {
        t += Time.deltaTime;
        float k = Mathf.Clamp01(t / duration);
        if (follow) transform.position = follow.position + Vector3.up * 0.9f * follow.localScale.y;
        float d = radius * 2f * Mathf.Lerp(0.6f, 1f, Mathf.Sqrt(k));
        transform.localScale = new Vector3(d, 0.01f, d);
        mat.color = new Color(color.r, color.g, color.b, color.a * Mathf.Sin(k * Mathf.PI));
        if (k >= 1f) { Destroy(mat); Destroy(gameObject); }
    }
}

// 技能光环：脚下脉动光圈 + 上升粒子 + 点光源，持续一段时间
public class AuraFX : MonoBehaviour
{
    public Color color;
    public float duration;
    public float intensity = 1f;
    Transform ring;
    Material mat;
    Light lt;
    float t, emitTimer;

    public static AuraFX Attach(GameObject target, Color color, float duration, float intensity = 1f)
    {
        var go = new GameObject("Aura");
        go.transform.SetParent(target.transform, false);
        var a = go.AddComponent<AuraFX>();
        a.color = color; a.duration = duration; a.intensity = intensity;
        return a;
    }

    void Start()
    {
        var r = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Destroy(r.GetComponent<Collider>());
        r.transform.SetParent(transform, false);
        r.transform.localPosition = Vector3.up * 0.04f;
        mat = new Material(Shader.Find("Sprites/Default")) { color = color };
        var rr = r.GetComponent<Renderer>();
        rr.sharedMaterial = mat;
        rr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        ring = r.transform;

        lt = gameObject.AddComponent<Light>();
        lt.type = LightType.Point;
        lt.color = color;
        lt.range = 4f;
        lt.intensity = 2f * intensity;
        transform.localPosition = Vector3.up * 0.0f;

        FX.Shockwave(transform.position, new Color(color.r, color.g, color.b, 0.8f), 2.5f, 0.4f);
        FX.Sparks(transform.position + Vector3.up, Vector3.up, color, 40, 1.2f);
    }

    void Update()
    {
        float dt = Time.deltaTime;
        t += dt;
        float fade = Mathf.Clamp01((duration - t) / 0.5f);
        float pulse = 1f + 0.12f * Mathf.Sin(t * 8f);
        ring.localScale = new Vector3(2.2f * pulse, 0.005f, 2.2f * pulse);
        mat.color = new Color(color.r, color.g, color.b, 0.35f * fade);
        lt.intensity = (1.5f + 0.5f * Mathf.Sin(t * 10f)) * intensity * fade;

        emitTimer -= dt;
        if (emitTimer <= 0f && fade > 0f)
        {
            emitTimer = 0.06f / intensity;
            Vector2 c = Random.insideUnitCircle.normalized * 0.6f;
            FX.Sparks(transform.position + new Vector3(c.x, 0.2f, c.y), Vector3.up, color, 2, 0.35f);
        }
        if (t >= duration) { Destroy(mat); Destroy(gameObject); }
    }
}
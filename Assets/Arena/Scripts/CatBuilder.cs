using System.Collections.Generic;
using UnityEngine;

// 三只主角猫的外观（照着真猫的照片调的）
public class CatLookDef
{
    public Color fur;            // 主毛色
    public Color stripe;         // 虎斑条纹
    public Color white;          // 白色部分（胸口、嘴巴、爪子）
    public Color point = Color.clear;   // 重点色（脸上面罩、耳朵、腿），clear = 没有
    public Color eye, nose, pawPad, collar;
    public Color tailTip = Color.clear;
    public bool whiteChest, whitePaws, blaze, fluffy;
    public float stripeStrength = 0.8f;   // 0 = 没花纹
    public float bodyWidth = 0.85f, bodyHeight = 0.5f, headScale = 1f, tailThickness = 0.12f;
}

// 运行时拼猫：程序生成毛发贴图（虎斑条纹 + 毛发噪点），加手臂、腿、五官、尾巴
public static class CatBuilder
{
    // 重建玩家猫的外观（保留 RightHand / LeftHand 和上面的武器）
    public static void Rebuild(GameObject root, CatLookDef d)
    {
        var combat = root.GetComponent<Combatant>();
        Transform model = combat.model;
        var keep = new HashSet<Transform> { combat.rightHand, combat.leftHand };
        var old = new List<GameObject>();
        foreach (Transform ch in model) if (!keep.Contains(ch)) old.Add(ch.gameObject);
        foreach (var h in new[] { combat.rightHand, combat.leftHand })
            foreach (Transform ch in h) if (ch.name == "Paw") old.Add(ch.gameObject);
        foreach (var g in old) { g.SetActive(false); Object.Destroy(g); }

        var skin = root.GetComponent<CatSkin>();
        var tail = root.GetComponent<CatTail>();
        var arms = root.GetComponent<CatArms>() ?? root.AddComponent<CatArms>();
        Build(model, d, combat.rightHand, combat.leftHand, skin, tail, arms);
    }

    static void Build(Transform model, CatLookDef d, Transform rightHand, Transform leftHand, CatSkin skin, CatTail tail, CatArms arms)
    {
        bool hasPoint = d.point.a > 0f;
        Color legColor = hasPoint ? d.point : d.fur;
        var furM = Mat(d.fur, d.stripe, d.stripeStrength, FurPattern.Rings);
        var headM = Mat(d.fur, d.stripe, d.stripeStrength, FurPattern.Crown);
        var legM = Mat(legColor, d.stripe, Mathf.Max(d.stripeStrength, hasPoint ? 0.8f : 0f), FurPattern.Rings);
        var whiteM = Mat(d.white, d.white, 0f, FurPattern.Plain);
        var pointM = hasPoint ? Mat(d.point, d.stripe, d.stripeStrength, FurPattern.Crown) : headM;
        var noseM = WeaponModel.Lit(d.nose);
        var padM = WeaponModel.Lit(d.pawPad);
        var eyeM = WeaponModel.Lit(d.eye);
        var darkM = WeaponModel.Lit(new Color(0.05f, 0.05f, 0.06f));
        var hiM = WeaponModel.Unlit(Color.white);
        var whiskerM = WeaponModel.Unlit(new Color(1f, 1f, 1f, 0.85f));
        var innerEarM = WeaponModel.Lit(Color.Lerp(d.nose, Color.white, 0.4f));
        var collarM = WeaponModel.Lit(d.collar);

        float bw = d.bodyWidth, bh = d.bodyHeight;

        // ---- 身体 ----
        P(PrimitiveType.Capsule, model, "Body", new Vector3(0, -0.3f, 0), new Vector3(bw, bh, bw * 0.92f), furM);
        if (d.whiteChest)
            P(PrimitiveType.Sphere, model, "Chest", new Vector3(0, -0.3f, bw * 0.24f), new Vector3(bw * 0.68f, bh * 1.45f, bw * 0.55f), whiteM);
        else
            P(PrimitiveType.Sphere, model, "Chest", new Vector3(0, -0.25f, bw * 0.22f), new Vector3(bw * 0.6f, bh * 1.3f, bw * 0.5f), Mat(Color.Lerp(d.fur, d.white, 0.5f), d.stripe, 0f, FurPattern.Plain));
        if (d.fluffy)
        {
            // 长毛：脖子一圈大白围脖 + 两侧蓬起来的毛
            P(PrimitiveType.Sphere, model, "Ruff", new Vector3(0, 0.08f, 0.12f), new Vector3(0.82f, 0.5f, 0.6f), whiteM);
            P(PrimitiveType.Sphere, model, "RuffLow", new Vector3(0, -0.12f, 0.2f), new Vector3(0.66f, 0.42f, 0.5f), whiteM);
            foreach (float s in new[] { -1f, 1f })
                P(PrimitiveType.Sphere, model, "SideFluff", new Vector3(0.36f * s, -0.35f, -0.04f), new Vector3(0.32f, 0.5f, 0.5f), furM);
        }
        // 项圈（代替原来的围巾，标出是玩家）
        P(PrimitiveType.Cylinder, model, "Collar", new Vector3(0, 0.15f, 0), new Vector3(0.6f, 0.035f, 0.56f), collarM);
        P(PrimitiveType.Sphere, model, "Tag", new Vector3(0, 0.1f, 0.29f), Vector3.one * 0.07f, WeaponModel.Lit(new Color(1f, 0.82f, 0.3f)));

        // ---- 腿 + 脚 ----
        var feet = new List<Transform>();
        foreach (float s in new[] { -1f, 1f })
        {
            P(PrimitiveType.Capsule, model, "Leg", new Vector3(0.19f * s, -0.66f, 0.0f), new Vector3(0.22f, 0.2f, 0.22f), legM);
            var foot = P(PrimitiveType.Sphere, model, "Foot", new Vector3(0.2f * s, -0.83f, 0.1f), new Vector3(0.24f, 0.15f, 0.32f), d.whitePaws ? whiteM : legM).transform;
            P(PrimitiveType.Sphere, foot, "Bean", new Vector3(0, -0.3f, 0.1f), new Vector3(0.5f, 0.2f, 0.4f), padM);
            feet.Add(foot);
        }

        // ---- 手臂（CatArms 每帧把它连到手上） + 爪子 ----
        var armR = P(PrimitiveType.Capsule, model, "Arm", Vector3.zero, Vector3.one * 0.16f, legM).transform;
        var armL = P(PrimitiveType.Capsule, model, "Arm", Vector3.zero, Vector3.one * 0.16f, legM).transform;
        foreach (var h in new[] { rightHand, leftHand })
        {
            var paw = P(PrimitiveType.Sphere, h, "Paw", Vector3.zero, Vector3.one * 0.19f, d.whitePaws ? whiteM : legM);
            P(PrimitiveType.Sphere, paw.transform, "Bean", new Vector3(0, -0.1f, 0.35f), new Vector3(0.45f, 0.4f, 0.3f), padM);
        }
        arms.Setup(model, new Vector3(0.3f, 0.02f, 0.02f), armR, rightHand, armL, leftHand, bw);

        // ---- 头 ----
        var head = new GameObject("Head").transform;
        head.SetParent(model, false);
        head.localPosition = new Vector3(0, 0.48f, 0.04f);
        head.localScale = Vector3.one * d.headScale;
        // 重点色的猫：下半张脸是浅色，上半张脸（额头、眼睛一圈、头顶）是深色面罩
        var skullM = hasPoint && d.fluffy ? whiteM : headM;
        P(PrimitiveType.Sphere, head, "Skull", Vector3.zero, new Vector3(0.8f, 0.68f, 0.72f), skullM);
        if (hasPoint)
            P(PrimitiveType.Sphere, head, "Mask", new Vector3(0, 0.1f, 0f), new Vector3(0.81f, 0.56f, 0.73f), pointM);
        // 腮帮子
        float cheek = d.fluffy ? 0.36f : 0.3f;
        foreach (float s in new[] { -1f, 1f })
            P(PrimitiveType.Sphere, head, "Cheek", new Vector3(0.2f * s, -0.12f, 0.16f), new Vector3(cheek, cheek * 0.75f, cheek * 0.8f), d.fluffy || d.whiteChest ? whiteM : headM);
        // 白色口鼻（两个胡须垫 + 下巴）
        foreach (float s in new[] { -1f, 1f })
            P(PrimitiveType.Sphere, head, "WhiskerPad", new Vector3(0.065f * s, -0.12f, 0.3f), new Vector3(0.15f, 0.12f, 0.12f), whiteM);
        P(PrimitiveType.Sphere, head, "Chin", new Vector3(0, -0.2f, 0.25f), new Vector3(0.14f, 0.08f, 0.1f), whiteM);
        if (d.blaze)   // 鼻梁上的一道白
            P(PrimitiveType.Sphere, head, "Blaze", new Vector3(0, 0.03f, 0.335f), new Vector3(0.06f, 0.2f, 0.05f), whiteM, new Vector3(-20, 0, 0));
        // 鼻子（倒三角）
        P(PrimitiveType.Cube, head, "Nose", new Vector3(0, -0.06f, 0.36f), new Vector3(0.06f, 0.06f, 0.03f), noseM, new Vector3(0, 0, 45));
        // 眼睛：虹膜 + 竖瞳 + 高光
        foreach (float s in new[] { -1f, 1f })
        {
            P(PrimitiveType.Sphere, head, "EyeRim", new Vector3(0.14f * s, 0.05f, 0.325f), new Vector3(0.16f, 0.15f, 0.07f), darkM);
            P(PrimitiveType.Sphere, head, "Eye", new Vector3(0.14f * s, 0.05f, 0.34f), new Vector3(0.14f, 0.13f, 0.06f), eyeM);
            P(PrimitiveType.Cube, head, "Pupil", new Vector3(0.14f * s, 0.05f, 0.37f), new Vector3(0.028f, 0.085f, 0.01f), darkM);
            P(PrimitiveType.Sphere, head, "Shine", new Vector3(0.14f * s - 0.03f, 0.085f, 0.372f), new Vector3(0.03f, 0.03f, 0.01f), hiM);
        }
        // 耳朵
        Material earM = hasPoint ? pointM : headM;
        foreach (float s in new[] { -1f, 1f })
        {
            // 转 45° 的方块下半截埋进头里 = 三角耳
            P(PrimitiveType.Cube, head, "Ear", new Vector3(0.22f * s, 0.32f, -0.02f), new Vector3(0.24f, 0.24f, 0.08f), earM, new Vector3(0, 0, 45 + 14 * s));
            P(PrimitiveType.Cube, head, "EarInner", new Vector3(0.215f * s, 0.32f, 0.025f), new Vector3(0.14f, 0.14f, 0.03f), innerEarM, new Vector3(0, 0, 45 + 14 * s));
            if (d.fluffy)   // 长毛猫的耳朵毛
                P(PrimitiveType.Cube, head, "EarTuft", new Vector3(0.21f * s, 0.29f, 0.045f), new Vector3(0.05f, 0.12f, 0.02f), whiteM, new Vector3(0, 0, 14 * s));
            // 白色胡须
            for (int i = 0; i < 3; i++)
                P(PrimitiveType.Cube, head, "Whisker", new Vector3(0.25f * s, -0.1f - i * 0.03f, 0.27f), new Vector3(0.3f, 0.006f, 0.006f),
                  whiskerM, new Vector3(0, -12f * s, (10f - i * 9f) * s));
        }
        skin.head = head;
        skin.feet = feet.ToArray();

        // ---- 尾巴 ----
        var tailRoot = new GameObject("Tail").transform;
        tailRoot.SetParent(model, false);
        tailRoot.localPosition = new Vector3(0, -0.55f, -0.33f);
        tailRoot.localRotation = Quaternion.Euler(-60f, 0, 0);
        var segs = new Transform[5];
        Transform parent = tailRoot;
        Color tip = d.tailTip.a > 0f ? d.tailTip : (hasPoint ? d.point : d.fur);
        var tipM = Mat(tip, d.stripe, d.stripeStrength * 0.5f, FurPattern.Rings);
        for (int i = 0; i < segs.Length; i++)
        {
            var seg = new GameObject("Seg" + i).transform;
            seg.SetParent(parent, false);
            seg.localPosition = i == 0 ? Vector3.zero : new Vector3(0, 0.19f, 0);
            seg.localRotation = Quaternion.Euler(i == 0 ? 0f : 12f, 0, 0);
            float th = d.tailThickness * (d.fluffy ? 1.15f - i * 0.03f : 1f - i * 0.05f);
            P(PrimitiveType.Capsule, seg, "Fur", new Vector3(0, 0.1f, 0), new Vector3(th, 0.13f, th), i >= 4 ? tipM : (hasPoint ? legM : furM));
            segs[i] = seg;
            parent = seg;
        }
        tail.SetSegments(segs);
    }

    static GameObject P(PrimitiveType t, Transform parent, string name, Vector3 pos, Vector3 scale, Material m, Vector3 euler = default)
    {
        var go = GameObject.CreatePrimitive(t);
        go.name = name;
        Object.Destroy(go.GetComponent<Collider>());
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localRotation = Quaternion.Euler(euler);
        go.transform.localScale = scale;
        var r = go.GetComponent<Renderer>();
        r.sharedMaterial = m;
        if (scale.x < 0.1f || scale.y < 0.1f) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        return go;
    }

    // ---------------- 毛发贴图 ----------------
    enum FurPattern { Plain, Rings, Crown }
    static readonly Dictionary<string, Material> mats = new();

    static Material Mat(Color baseC, Color stripeC, float strength, FurPattern p)
    {
        string key = $"{ColorUtility.ToHtmlStringRGB(baseC)}_{ColorUtility.ToHtmlStringRGB(stripeC)}_{strength:F2}_{p}";
        if (mats.TryGetValue(key, out var m) && m != null) return m;
        m = new Material(Shader.Find("Standard")) { mainTexture = FurTexture(baseC, stripeC, strength, p), color = Color.white };
        m.SetFloat("_Glossiness", 0.12f);
        mats[key] = m;
        return m;
    }

    // 标准的 smoothstep（Unity 的 Mathf.SmoothStep 是插值，不是这个）
    static float SS(float e0, float e1, float x)
    {
        float k = Mathf.Clamp01((x - e0) / (e1 - e0));
        return k * k * (3f - 2f * k);
    }

    static Texture2D FurTexture(Color baseC, Color stripeC, float strength, FurPattern p)
    {
        const int W = 128, H = 128;
        var tex = new Texture2D(W, H, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Bilinear };
        var px = new Color[W * H];
        float seed = Random.value * 100f;
        for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                float u = x / (float)W, v = y / (float)H;
                // 毛发噪点：细颗粒 + 大块明暗
                float grain = Mathf.PerlinNoise(x * 0.45f + seed, y * 0.3f + seed) * 0.1f - 0.05f;
                float blot = Mathf.PerlinNoise(u * 4f + seed, v * 4f) * 0.1f - 0.05f;
                Color c = baseC * (1f + grain + blot);
                float s = 0f;
                if (strength > 0f)
                {
                    if (p == FurPattern.Rings)
                    {
                        // 一圈圈的虎斑（带波浪，粗细不均）
                        float wave = Mathf.Sin(u * Mathf.PI * 4f + seed) * 0.04f + Mathf.PerlinNoise(u * 6f + seed, v * 2f) * 0.06f;
                        float band = Mathf.Sin((v + wave) * Mathf.PI * 2f * 5f);
                        s = SS(0.35f, 0.75f, band);
                    }
                    else if (p == FurPattern.Crown)
                    {
                        // 头顶往下的竖条纹（额头上的虎斑线）
                        float band = Mathf.Sin((u + Mathf.PerlinNoise(v * 3f, seed) * 0.03f) * Mathf.PI * 2f * 9f);
                        float topMask = SS(0.55f, 0.8f, v);
                        s = SS(0.3f, 0.75f, band) * topMask;
                    }
                }
                c = Color.Lerp(c, stripeC * (1f + grain), s * strength);
                c.a = 1f;
                px[y * W + x] = c;
            }
        tex.SetPixels(px);
        tex.Apply(true);
        return tex;
    }
}

// 手臂：从肩膀连到手（手的位置是战斗动画控制的），每帧拉伸对齐
public class CatArms : MonoBehaviour
{
    Transform model, armR, handR, armL, handL;
    Vector3 shoulder;
    float thick;

    public void Setup(Transform model, Vector3 shoulder, Transform armR, Transform handR, Transform armL, Transform handL, float bodyWidth)
    {
        this.model = model; this.shoulder = shoulder;
        this.armR = armR; this.handR = handR; this.armL = armL; this.handL = handL;
        thick = 0.15f + (bodyWidth - 0.75f) * 0.2f;
    }

    void LateUpdate()
    {
        if (model == null) return;
        Place(armR, handR, new Vector3(shoulder.x, shoulder.y, shoulder.z));
        Place(armL, handL, new Vector3(-shoulder.x, shoulder.y, shoulder.z));
    }

    void Place(Transform arm, Transform hand, Vector3 sh)
    {
        if (arm == null || hand == null) return;
        Vector3 a = sh;
        Vector3 b = model.InverseTransformPoint(hand.position);
        Vector3 d = b - a;
        float len = Mathf.Max(0.05f, d.magnitude);
        arm.localPosition = (a + b) * 0.5f;
        arm.localRotation = Quaternion.FromToRotation(Vector3.up, d / len);
        // Unity 胶囊高 2（scale.y = 1），所以 y 缩放 = 长度 / 2，再加一点让两端埋进身体和爪子
        arm.localScale = new Vector3(thick, len * 0.5f + thick * 0.5f, thick);
    }
}

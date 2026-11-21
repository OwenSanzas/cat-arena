using System.Collections.Generic;
using UnityEngine;

// 用基础几何体拼出武器模型（以后可以换成真正的模型）
// 武器朝向挂点的 +Z 方向；攻击武器带一条刀光拖尾（TrailRenderer）
public static class WeaponModel
{
    static readonly Dictionary<Color, Material> litMats = new();
    static readonly Dictionary<Color, Material> unlitMats = new();

    public static Material Lit(Color c)
    {
        if (!litMats.TryGetValue(c, out var m) || m == null)
        {
            m = new Material(Shader.Find("Standard")) { color = c };
            m.SetFloat("_Glossiness", 0.35f);
            litMats[c] = m;
        }
        return m;
    }

    // 不受光照、支持透明和顶点颜色（拖尾、粒子、光圈用）
    public static Material Unlit(Color c)
    {
        if (!unlitMats.TryGetValue(c, out var m) || m == null)
        {
            m = new Material(Shader.Find("Sprites/Default")) { color = c };
            unlitMats[c] = m;
        }
        return m;
    }

    public static GameObject Create(WeaponData w, Transform hand)
    {
        if (w == null || hand == null) return null;

        var root = new GameObject("Model_" + w.displayName);
        root.transform.SetParent(hand, false);

        Color wood = new Color(0.45f, 0.3f, 0.18f);
        float L = w.length;
        float trailZ = 0f, trailWidth = 0f;

        switch (w.type)
        {
            case WeaponType.Sword:
                Part(root, PrimitiveType.Cube, new Vector3(0, 0, 0), new Vector3(0.06f, 0.06f, 0.22f), wood);
                Part(root, PrimitiveType.Cube, new Vector3(0, 0, 0.13f), new Vector3(0.32f, 0.05f, 0.05f), new Color(0.6f, 0.5f, 0.2f));
                Part(root, PrimitiveType.Cube, new Vector3(0, 0, 0.15f + L / 2f), new Vector3(0.09f, 0.03f, L), w.color);
                trailZ = 0.15f + L * 0.7f; trailWidth = L * 0.6f;
                break;

            case WeaponType.Spear:
                Part(root, PrimitiveType.Cylinder, new Vector3(0, 0, L / 2f - 0.5f), new Vector3(0.05f, L / 2f, 0.05f), wood, new Vector3(90, 0, 0));
                Part(root, PrimitiveType.Cube, new Vector3(0, 0, L - 0.4f), new Vector3(0.12f, 0.03f, 0.35f), w.color);
                trailZ = L - 0.35f; trailWidth = 0.22f;
                break;

            case WeaponType.Axe:
                Part(root, PrimitiveType.Cylinder, new Vector3(0, 0, L / 2f - 0.1f), new Vector3(0.06f, L / 2f, 0.06f), wood, new Vector3(90, 0, 0));
                Part(root, PrimitiveType.Cube, new Vector3(0.17f, 0, L - 0.25f), new Vector3(0.36f, 0.05f, 0.32f), w.color);
                Part(root, PrimitiveType.Cube, new Vector3(-0.06f, 0, L - 0.25f), new Vector3(0.1f, 0.06f, 0.12f), w.color);
                trailZ = L - 0.25f; trailWidth = 0.55f;
                break;

            case WeaponType.Bow:
                // 弓：几段木头拼成弧形 + 弓弦，竖着拿
                for (int i = -3; i <= 3; i++)
                {
                    float a = i * 16f;
                    var p = new Vector3(0, Mathf.Sin(a * Mathf.Deg2Rad) * 0.55f, 0.12f + Mathf.Cos(a * Mathf.Deg2Rad) * 0.25f);
                    Part(root, PrimitiveType.Cube, p, new Vector3(0.05f, 0.18f, 0.05f), i == 0 ? new Color(0.3f, 0.2f, 0.12f) : w.color, new Vector3(-a, 0, 0));
                }
                Part(root, PrimitiveType.Cube, new Vector3(0, 0, -0.02f), new Vector3(0.01f, 1.05f, 0.01f), new Color(0.9f, 0.9f, 0.85f));
                break;

            case WeaponType.Shield:
                Part(root, PrimitiveType.Cube, new Vector3(0, 0, 0.32f), new Vector3(0.75f, 0.85f, 0.08f), w.color);
                Part(root, PrimitiveType.Cube, new Vector3(0, 0, 0.36f), new Vector3(0.8f, 0.08f, 0.06f), new Color(0.55f, 0.55f, 0.6f));
                Part(root, PrimitiveType.Sphere, new Vector3(0, 0, 0.38f), new Vector3(0.2f, 0.2f, 0.12f), new Color(0.7f, 0.7f, 0.75f));
                break;
        }

        // 品质高的武器加一条发光边
        if (w.rarity > 0 && w.type != WeaponType.Shield && w.type != WeaponType.Bow)
            Part(root, PrimitiveType.Cube, new Vector3(0, 0.03f, trailZ - 0.1f), new Vector3(0.04f, 0.02f, 0.4f), w.RarityColor);

        if (trailWidth > 0f)
        {
            var tip = new GameObject("Trail");
            tip.transform.SetParent(root.transform, false);
            tip.transform.localPosition = new Vector3(0, 0, trailZ);
            var tr = tip.AddComponent<TrailRenderer>();
            tr.time = 0.14f;
            tr.minVertexDistance = 0.03f;
            tr.widthMultiplier = trailWidth;
            tr.widthCurve = AnimationCurve.Linear(0f, 1f, 1f, 0f);
            tr.sharedMaterial = Unlit(Color.white);
            tr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            tr.receiveShadows = false;
            tr.emitting = false;
        }
        return root;
    }

    static void Part(GameObject root, PrimitiveType t, Vector3 pos, Vector3 scale, Color c, Vector3 euler = default)
    {
        var go = GameObject.CreatePrimitive(t);
        Object.Destroy(go.GetComponent<Collider>());   // 武器模型不需要物理碰撞
        go.transform.SetParent(root.transform, false);
        go.transform.localPosition = pos;
        go.transform.localRotation = Quaternion.Euler(euler);
        go.transform.localScale = scale;
        go.GetComponent<Renderer>().sharedMaterial = Lit(c);
    }
}

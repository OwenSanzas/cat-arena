using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// 物品图标：用物品自己的 3D 模型，拿一台隐藏相机拍一张小图（第一次用到时生成，之后缓存）
// 武器 / 护甲 / 饰品 / 药水都有图标，不同款式、不同品质的图标也不一样
public class ItemIcons : MonoBehaviour
{
    const int Size = 128;
    const int Layer = 31;   // 专门给图标用的层，主相机看不到
    static readonly Vector3 StagePos = new(0f, -500f, 0f);

    static ItemIcons inst;
    static readonly Dictionary<string, Texture2D> cache = new();
    static readonly HashSet<string> pending = new();
    static readonly Queue<Item> queue = new();

    // 拿图标；还没生成好就返回 null（界面先显示文字，下一帧就有图了）
    public static Texture2D Get(Item it)
    {
        if (it == null || SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) return null;
        string k = Key(it);
        if (cache.TryGetValue(k, out var tex) && tex != null) return tex;
        if (pending.Add(k))
        {
            if (inst == null) inst = new GameObject("[ItemIcons]").AddComponent<ItemIcons>();
            queue.Enqueue(it);
        }
        return null;
    }

    static string Key(Item it) => it.kind switch
    {
        ItemKind.Weapon => $"W_{it.weapon.type}_{it.rarity}_{ColorUtility.ToHtmlStringRGB(it.weapon.color)}",
        ItemKind.Armor => $"A_{it.armorSlot}_{it.style}_{it.rarity}",
        _ => $"P_{it.potion}_{it.rarity}",
    };

    void LateUpdate()
    {
        // 每帧最多生成几张，避免卡顿
        for (int n = 0; n < 6 && queue.Count > 0; n++)
        {
            var it = queue.Dequeue();
            string k = Key(it);
            cache[k] = Render(it);
            pending.Remove(k);
        }
    }

    void OnDestroy()
    {
        if (inst == this) inst = null;
        pending.Clear();
        queue.Clear();
    }

    // ---------------- 拍照 ----------------
    static Texture2D Render(Item it)
    {
        var root = new GameObject("IconStage");
        root.transform.position = StagePos;
        var content = new GameObject("Content").transform;
        content.SetParent(root.transform, false);

        BuildModel(it, content);
        foreach (var tr in root.GetComponentsInChildren<TrailRenderer>()) DestroyImmediate(tr.gameObject);
        foreach (var t in root.GetComponentsInChildren<Transform>()) t.gameObject.layer = Layer;

        // 让模型刚好填满画面
        var rs = root.GetComponentsInChildren<Renderer>();
        Bounds b = new Bounds(StagePos, Vector3.one * 0.1f);
        bool first = true;
        foreach (var r in rs)
        {
            if (first) { b = r.bounds; first = false; }
            else b.Encapsulate(r.bounds);
        }

        var camGo = new GameObject("IconCam");
        var cam = camGo.AddComponent<Camera>();
        cam.enabled = false;
        cam.orthographic = true;
        cam.orthographicSize = Mathf.Max(b.extents.x, b.extents.y) * 1.12f + 0.02f;
        cam.cullingMask = 1 << Layer;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0, 0, 0, 0);
        cam.nearClipPlane = 0.1f;
        cam.farClipPlane = 50f;
        camGo.transform.position = b.center + Vector3.back * 20f;
        camGo.transform.rotation = Quaternion.identity;

        var lightGo = new GameObject("IconLight");
        var light = lightGo.AddComponent<Light>();
        light.type = LightType.Directional;
        light.cullingMask = 1 << Layer;
        light.intensity = 0.9f;
        light.shadows = LightShadows.None;
        lightGo.transform.rotation = Quaternion.Euler(35f, 25f, 0f);

        var rt = RenderTexture.GetTemporary(Size, Size, 24, RenderTextureFormat.ARGB32);
        rt.antiAliasing = 1;
        cam.targetTexture = rt;
        cam.Render();

        var prev = RenderTexture.active;
        RenderTexture.active = rt;
        var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false) { name = "Icon", filterMode = FilterMode.Bilinear };
        tex.ReadPixels(new Rect(0, 0, Size, Size), 0, 0);
        tex.Apply();
        RenderTexture.active = prev;

        cam.targetTexture = null;
        RenderTexture.ReleaseTemporary(rt);
        DestroyImmediate(camGo);
        DestroyImmediate(lightGo);
        DestroyImmediate(root);
        return tex;
    }

    static void BuildModel(Item it, Transform t)
    {
        switch (it.kind)
        {
            case ItemKind.Weapon:
                WeaponModel.Create(it.weapon, t);
                // 武器斜着放（左下 → 右上），盾牌正面朝向镜头
                t.localRotation = it.weapon.type == WeaponType.Shield
                    ? Quaternion.Euler(0f, 200f, 0f)
                    : Quaternion.Euler(0f, 0f, 135f) * Quaternion.Euler(90f, 0f, 0f) * Quaternion.Euler(0f, 0f, 20f);
                break;

            case ItemKind.Armor:
                ArmorVisuals.BuildShape(it, t, ArmorVisuals.DefaultFeet, false);
                t.localRotation = it.armorSlot switch
                {
                    ArmorSlot.Head => Quaternion.Euler(18f, 205f, 0f),
                    ArmorSlot.Body => Quaternion.Euler(12f, 205f, 0f),
                    ArmorSlot.Feet => Quaternion.Euler(20f, 235f, 0f),
                    _ => Quaternion.Euler(10f, 200f, 0f),
                };
                break;

            default:
                BuildPotion(it, t);
                t.localRotation = Quaternion.Euler(12f, 0f, -12f);
                break;
        }
    }

    // 药水瓶：血药圆瓶 / 增益药水高瓶 / 技能药水菱形瓶
    static void BuildPotion(Item it, Transform t)
    {
        Color liquid = it.PotionColor;
        Color glass = new(0.85f, 0.92f, 1f);
        Color cork = new(0.55f, 0.38f, 0.22f);
        float neckY;
        switch (it.potion)
        {
            case PotionType.Health:
                float s = it.rarity > 0 ? 0.62f : 0.5f;   // 大血药瓶子更大
                P(PrimitiveType.Sphere, t, Vector3.zero, Vector3.one * s, liquid);
                neckY = s * 0.5f;
                break;
            case PotionType.SkillUp:
                P(PrimitiveType.Cube, t, Vector3.zero, Vector3.one * 0.38f, liquid, false, new Vector3(0, 0, 45));
                P(PrimitiveType.Cube, t, Vector3.zero, Vector3.one * 0.12f, Color.white, true, new Vector3(45, 45, 0));   // 里面发光的星星
                neckY = 0.27f;
                break;
            default:
                P(PrimitiveType.Cylinder, t, new Vector3(0, -0.05f, 0), new Vector3(0.34f, 0.25f, 0.34f), liquid);
                P(PrimitiveType.Sphere, t, new Vector3(0, 0.2f, 0), new Vector3(0.34f, 0.16f, 0.34f), liquid);
                neckY = 0.27f;
                break;
        }
        P(PrimitiveType.Cylinder, t, new Vector3(0, neckY + 0.06f, 0), new Vector3(0.13f, 0.08f, 0.13f), glass);
        P(PrimitiveType.Cylinder, t, new Vector3(0, neckY + 0.17f, 0), new Vector3(0.15f, 0.05f, 0.15f), cork);
        // 高光
        P(PrimitiveType.Sphere, t, new Vector3(-0.1f, 0.06f, -0.2f), new Vector3(0.07f, 0.12f, 0.03f), new Color(1, 1, 1, 0.85f), true);
    }

    static void P(PrimitiveType type, Transform parent, Vector3 pos, Vector3 scale, Color c, bool glow = false, Vector3 euler = default)
    {
        var go = GameObject.CreatePrimitive(type);
        DestroyImmediate(go.GetComponent<Collider>());
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localRotation = Quaternion.Euler(euler);
        go.transform.localScale = scale;
        go.GetComponent<Renderer>().sharedMaterial = glow ? WeaponModel.Unlit(c) : WeaponModel.Lit(c);
    }
}

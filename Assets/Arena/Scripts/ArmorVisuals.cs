using UnityEngine;

// 把穿上的头盔 / 护甲 / 靴子 / 饰品做成模型挂到猫身上
// 模型的拼法（BuildShape）也给物品图标用
[RequireComponent(typeof(Inventory), typeof(CatSkin))]
public class ArmorVisuals : MonoBehaviour
{
    Inventory inv;
    CatSkin skin;
    Transform model;
    readonly GameObject[] built = new GameObject[System.Enum.GetValues(typeof(EquipSlot)).Length];
    readonly Item[] shown = new Item[System.Enum.GetValues(typeof(EquipSlot)).Length];
    Transform orbit;

    static readonly Color Leather = new(0.45f, 0.3f, 0.18f);
    static readonly Color LeatherDark = new(0.32f, 0.2f, 0.12f);
    static readonly Color Iron = new(0.55f, 0.58f, 0.62f);
    static readonly Color Steel = new(0.75f, 0.78f, 0.82f);
    static readonly Color Gold = new(1f, 0.8f, 0.25f);
    static readonly Color Bone = new(0.92f, 0.88f, 0.75f);

    // 图标里没有猫，靴子用默认的脚位置
    public static readonly Vector3[] DefaultFeet = { new(-0.2f, -0.83f, 0.1f), new(0.2f, -0.83f, 0.1f) };

    void Awake()
    {
        inv = GetComponent<Inventory>();
        skin = GetComponent<CatSkin>();
        model = GetComponent<Combatant>().model;
        inv.OnChanged += Rebuild;
    }

    void Start() => Rebuild();

    void Rebuild()
    {
        for (int i = (int)EquipSlot.Head; i < built.Length; i++)
        {
            var it = inv.equipped[i];
            if (it == shown[i]) continue;
            if (built[i]) Destroy(built[i]);
            built[i] = null;
            shown[i] = it;
            if (it != null) built[i] = Build((EquipSlot)i, it);
        }
    }

    void Update()
    {
        // 第二个饰品绕着猫转
        if (orbit) orbit.localRotation = Quaternion.Euler(0f, Time.time * 120f, 0f);
    }

    GameObject Build(EquipSlot slot, Item it)
    {
        var root = new GameObject("Armor_" + slot);
        Transform t = root.transform;
        switch (slot)
        {
            case EquipSlot.Head:
                t.SetParent(skin.head, false);
                break;
            case EquipSlot.Body:
            case EquipSlot.Feet:
                t.SetParent(model, false);
                break;
            case EquipSlot.Accessory1:
                t.SetParent(model, false);
                t.localPosition = new Vector3(0, 0.05f, 0.38f);   // 挂在围巾正前方
                break;
            case EquipSlot.Accessory2:
                t.SetParent(model, false);
                orbit = t;                                         // 绕身体旋转的小护符
                var holder = new GameObject("Holder").transform;
                holder.SetParent(orbit, false);
                holder.localPosition = new Vector3(0.75f, 0.3f, 0f);
                t = holder;
                break;
        }

        Vector3[] feet = DefaultFeet;
        if (slot == EquipSlot.Feet && skin.feet != null && skin.feet.Length > 0)
        {
            feet = new Vector3[skin.feet.Length];
            for (int i = 0; i < feet.Length; i++) feet[i] = skin.feet[i] ? skin.feet[i].localPosition : DefaultFeet[i % 2];
        }
        BuildShape(it, t, feet, true);

        FX.Sparks(root.transform.position, Vector3.up, it.RarityColor, 10, 0.4f);   // 换装时闪一下
        return root;
    }

    // 拼出护甲/饰品的形状，挂到 t 下面
    public static void BuildShape(Item it, Transform t, Vector3[] feet, bool lights)
    {
        Color rc = it.RarityColor;
        bool trim = it.rarity > 0;
        switch (it.armorSlot)
        {
            case ArmorSlot.Head:
            {
                Color c = it.style == 0 ? Leather : Iron;
                // 头盔盖住头顶，猫耳朵从上面钻出来
                P(PrimitiveType.Sphere, t, new Vector3(0, 0.12f, -0.02f), new Vector3(0.84f, 0.46f, 0.76f), c);
                P(PrimitiveType.Cylinder, t, new Vector3(0, 0.06f, -0.02f), new Vector3(0.86f, 0.03f, 0.78f), trim ? rc : (it.style == 0 ? LeatherDark : Steel), trim);
                if (it.style >= 1) P(PrimitiveType.Cube, t, new Vector3(0, 0.02f, 0.36f), new Vector3(0.05f, 0.18f, 0.04f), Steel);   // 护鼻
                if (it.style == 2)
                    foreach (float s in new[] { -1f, 1f })
                    {
                        P(PrimitiveType.Capsule, t, new Vector3(0.36f * s, 0.2f, 0f), new Vector3(0.08f, 0.16f, 0.08f), Bone, false, new Vector3(0, 0, -55f * s));
                        P(PrimitiveType.Sphere, t, new Vector3(0.47f * s, 0.33f, 0f), new Vector3(0.07f, 0.07f, 0.07f), Bone);
                    }
                if (it.rarity >= 3) P(PrimitiveType.Cube, t, new Vector3(0, 0.38f, -0.05f), new Vector3(0.05f, 0.12f, 0.4f), rc, true);    // 史诗：发光冠饰
                break;
            }
            case ArmorSlot.Body:
            {
                Color c = it.style switch { 0 => Leather, 1 => Iron, _ => Steel };
                P(PrimitiveType.Capsule, t, new Vector3(0, -0.28f, 0), new Vector3(0.93f, 0.46f, 0.93f), c);
                P(PrimitiveType.Cylinder, t, new Vector3(0, -0.5f, 0), new Vector3(0.95f, 0.04f, 0.95f), trim ? rc : LeatherDark, trim);  // 腰带
                if (it.style == 1)
                    for (int i = 0; i < 3; i++)   // 锁子甲的环纹
                        P(PrimitiveType.Cylinder, t, new Vector3(0, -0.15f - i * 0.12f, 0), new Vector3(0.95f, 0.01f, 0.95f), Steel);
                if (it.style == 2)
                {
                    P(PrimitiveType.Cube, t, new Vector3(0, -0.22f, 0.43f), new Vector3(0.5f, 0.42f, 0.08f), Steel);   // 胸甲
                    P(PrimitiveType.Cube, t, new Vector3(0, -0.18f, 0.47f), new Vector3(0.06f, 0.3f, 0.02f), trim ? rc : Iron, trim);
                    foreach (float s in new[] { -1f, 1f })   // 肩甲
                        P(PrimitiveType.Sphere, t, new Vector3(0.4f * s, 0.02f, 0f), new Vector3(0.32f, 0.2f, 0.34f), Steel);
                }
                break;
            }
            case ArmorSlot.Feet:
            {
                Color c = it.style == 0 ? LeatherDark : Iron;
                foreach (var f in feet)
                {
                    P(PrimitiveType.Cube, t, f + new Vector3(0, 0.04f, -0.02f), new Vector3(0.28f, 0.2f, 0.34f), c);
                    P(PrimitiveType.Cube, t, f + new Vector3(0, 0.12f, -0.02f), new Vector3(0.3f, 0.04f, 0.36f), trim ? rc : Leather, trim);
                    if (it.style == 1) P(PrimitiveType.Cube, t, f + new Vector3(0, 0.06f, 0.16f), new Vector3(0.22f, 0.12f, 0.04f), Steel);   // 铁靴护趾
                }
                break;
            }
            default:
            {
                switch (it.style)
                {
                    case 0:   // 猫铃铛
                        P(PrimitiveType.Sphere, t, Vector3.zero, Vector3.one * 0.15f, Gold);
                        P(PrimitiveType.Cube, t, new Vector3(0, -0.02f, 0.07f), new Vector3(0.1f, 0.012f, 0.01f), LeatherDark);
                        P(PrimitiveType.Cylinder, t, new Vector3(0, 0.085f, 0), new Vector3(0.04f, 0.015f, 0.04f), Gold);
                        break;
                    case 1:   // 鱼骨护符
                        P(PrimitiveType.Cube, t, Vector3.zero, new Vector3(0.22f, 0.025f, 0.025f), Bone);
                        for (int i = -1; i <= 1; i++)
                            P(PrimitiveType.Cube, t, new Vector3(i * 0.05f, 0, 0), new Vector3(0.015f, 0.1f, 0.015f), Bone);
                        P(PrimitiveType.Cube, t, new Vector3(-0.12f, 0, 0), new Vector3(0.05f, 0.08f, 0.015f), Bone, false, new Vector3(0, 0, 45));
                        P(PrimitiveType.Sphere, t, new Vector3(0.12f, 0, 0), new Vector3(0.06f, 0.05f, 0.03f), Bone);
                        break;
                    default:  // 猫眼宝石
                        P(PrimitiveType.Cube, t, Vector3.zero, Vector3.one * 0.12f, rc, true, new Vector3(45, 0, 45));
                        P(PrimitiveType.Cube, t, Vector3.zero, new Vector3(0.02f, 0.14f, 0.02f), new Color(0.1f, 0.1f, 0.1f), true);   // 竖瞳
                        break;
                }
                if (trim && lights)
                {
                    var l = t.gameObject.AddComponent<Light>();
                    l.type = LightType.Point;
                    l.color = rc;
                    l.range = 1.2f;
                    l.intensity = 0.8f + 0.4f * it.rarity;
                }
                break;
            }
        }
    }

    static GameObject P(PrimitiveType type, Transform parent, Vector3 pos, Vector3 scale, Color c, bool glow = false, Vector3 euler = default)
    {
        var go = GameObject.CreatePrimitive(type);
        Object.Destroy(go.GetComponent<Collider>());
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localRotation = Quaternion.Euler(euler);
        go.transform.localScale = scale;
        var r = go.GetComponent<Renderer>();
        r.sharedMaterial = glow ? WeaponModel.Unlit(c) : WeaponModel.Lit(c);
        if (scale.x < 0.1f || scale.y < 0.1f) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        return go;
    }
}

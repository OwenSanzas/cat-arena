using System.Collections.Generic;
using UnityEngine;

// 战利品袋：敌人死后掉在地上，玩家靠近按 F 搜刮（搜刮时游戏暂停，物品逐个"搜出来"）
public class LootBag : MonoBehaviour
{
    public readonly List<Item> items = new();
    public readonly List<bool> revealed = new();
    public bool opened;

    static readonly List<LootBag> all = new();
    const float Lifetime = 120f;   // 没搜的袋子 2 分钟后消失，避免越积越多

    Transform sack;
    Material ringMat;
    float spawnTime;

    public static LootBag Spawn(List<Item> loot, Vector3 pos, bool preRevealed = false)
    {
        if (loot == null || loot.Count == 0) return null;
        var go = new GameObject("LootBag");
        pos.y = 0f;
        go.transform.position = pos;
        var b = go.AddComponent<LootBag>();
        foreach (var it in loot) { b.items.Add(it); b.revealed.Add(preRevealed); }
        b.opened = preRevealed;
        return b;
    }

    public static LootBag FindNearest(Vector3 pos, float maxDist)
    {
        LootBag best = null;
        float bestD = maxDist * maxDist;
        foreach (var b in all)
        {
            Vector3 d = b.transform.position - pos;
            d.y = 0f;
            if (d.sqrMagnitude < bestD) { bestD = d.sqrMagnitude; best = b; }
        }
        return best;
    }

    void OnEnable() => all.Add(this);
    void OnDisable() => all.Remove(this);

    public int BestRarity
    {
        get
        {
            int r = 0;
            foreach (var it in items) r = Mathf.Max(r, it.rarity);
            return r;
        }
    }

    public void AddItem(Item it)
    {
        items.Add(it);
        revealed.Add(true);
    }

    public void Take(int index)
    {
        items.RemoveAt(index);
        revealed.RemoveAt(index);
    }

    public void CloseIfEmpty()
    {
        if (items.Count == 0) Destroy(gameObject);
    }

    void Start()
    {
        spawnTime = Time.time;
        // 小布袋：袋身 + 扎口 + 绳结
        sack = new GameObject("Sack").transform;
        sack.SetParent(transform, false);
        Part(PrimitiveType.Sphere, sack, new Vector3(0, 0.25f, 0), new Vector3(0.55f, 0.48f, 0.5f), new Color(0.55f, 0.4f, 0.25f));
        Part(PrimitiveType.Cylinder, sack, new Vector3(0, 0.52f, 0), new Vector3(0.18f, 0.06f, 0.18f), new Color(0.42f, 0.3f, 0.18f));
        Part(PrimitiveType.Sphere, sack, new Vector3(0, 0.6f, 0), new Vector3(0.22f, 0.12f, 0.22f), new Color(0.55f, 0.4f, 0.25f));
        Part(PrimitiveType.Cube, sack, new Vector3(0.08f, 0.25f, 0.24f), new Vector3(0.14f, 0.14f, 0.02f), new Color(0.42f, 0.3f, 0.18f)); // 补丁
        sack.localRotation = Quaternion.Euler(0, Random.Range(0f, 360f), Random.Range(-10f, 10f));

        var ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Destroy(ring.GetComponent<Collider>());
        ring.transform.SetParent(transform, false);
        ring.transform.localPosition = Vector3.up * 0.02f;
        ring.transform.localScale = new Vector3(1.1f, 0.005f, 1.1f);
        ringMat = new Material(Shader.Find("Sprites/Default")) { color = new Color(1, 1, 1, 0.3f) };
        var rr = ring.GetComponent<Renderer>();
        rr.sharedMaterial = ringMat;
        rr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        FX.Sparks(transform.position + Vector3.up * 0.4f, Vector3.up, new Color(0.9f, 0.8f, 0.6f), 10, 0.5f);
    }

    static GameObject Part(PrimitiveType t, Transform parent, Vector3 pos, Vector3 scale, Color c)
    {
        var go = GameObject.CreatePrimitive(t);
        Destroy(go.GetComponent<Collider>());
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localScale = scale;
        go.GetComponent<Renderer>().sharedMaterial = WeaponModel.Lit(c);
        return go;
    }

    void Update()
    {
        float age = Time.time - spawnTime;
        if (!opened && age > Lifetime) { Destroy(gameObject); return; }
        float pop = Mathf.Clamp01(age / 0.25f);
        sack.localScale = Vector3.one * Mathf.SmoothStep(0f, 1f, pop);

        // 没搜过：白色呼吸光圈；搜过：显示最高品质颜色
        Color c = opened ? WeaponData.RarityColors[BestRarity] : Color.white;
        float a = 0.2f + 0.15f * Mathf.Sin(Time.time * 3f);
        ringMat.color = new Color(c.r, c.g, c.b, a);
    }

    void OnDestroy()
    {
        if (ringMat) Destroy(ringMat);
    }

    void OnGUI()
    {
        if (FX.Paused) return;
        var cam = Camera.main;
        if (cam == null) return;
        Vector3 s = cam.WorldToScreenPoint(transform.position + Vector3.up * 1.0f);
        if (s.z < 0) return;
        var style = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 13, fontStyle = FontStyle.Bold };
        string text = opened ? $"战利品 ×{items.Count}" : "？ 未搜刮";
        var r = new Rect(s.x - 80, Screen.height - s.y - 12, 160, 24);
        style.normal.textColor = new Color(0, 0, 0, 0.8f);
        GUI.Label(new Rect(r.x + 1, r.y + 1, r.width, r.height), text, style);
        style.normal.textColor = opened ? WeaponData.RarityColors[BestRarity] : new Color(1f, 0.95f, 0.8f);
        GUI.Label(r, text, style);
    }
}

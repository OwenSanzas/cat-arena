using UnityEngine;

// Tab 背包（传统 RPG 装备栏 + 背包格子） / F 搜刮战利品袋。打开时游戏暂停
[RequireComponent(typeof(Inventory))]
public class InventoryUI : MonoBehaviour
{
    public float lootRange = 1.8f;

    public bool IsOpen => mode != Mode.Closed;
    public LootBag NearbyBag { get; private set; }

    enum Mode { Closed, Inventory, Loot }
    Mode mode;
    LootBag loot;
    float searchTimer;

    Inventory inv;
    PlayerTopDown player;
    PlayerStats stats;
    PlayerSkill skill;
    Health health;

    Item selected;            // 背包里选中的物品
    Item hovered;
    string message;
    float messageUntil;

    void Awake()
    {
        inv = GetComponent<Inventory>();
        player = GetComponent<PlayerTopDown>();
        stats = GetComponent<PlayerStats>();
        skill = GetComponent<PlayerSkill>();
        health = GetComponent<Health>();
    }

    void Update()
    {
        if (!IsOpen)
        {
            NearbyBag = player.inputEnabled && !health.IsDead ? LootBag.FindNearest(transform.position, lootRange) : null;
            if (!player.inputEnabled || health.IsDead) return;
            if (Input.GetKeyDown(KeyCode.Tab)) Open(Mode.Inventory, null);
            else if (Input.GetKeyDown(KeyCode.F) && NearbyBag != null) Open(Mode.Loot, NearbyBag);
            return;
        }

        if (Input.GetKeyDown(KeyCode.Tab) || Input.GetKeyDown(KeyCode.Escape) || (mode == Mode.Loot && Input.GetKeyDown(KeyCode.F)))
        {
            Close();
            return;
        }

        // 搜刮：未搜出来的物品一个个揭开（暂停中用真实时间）
        if (mode == Mode.Loot && loot != null)
        {
            int idx = loot.revealed.IndexOf(false);
            if (idx >= 0)
            {
                searchTimer += Time.unscaledDeltaTime;
                if (searchTimer >= SearchTime(loot.items[idx]))
                {
                    loot.revealed[idx] = true;
                    searchTimer = 0f;
                }
            }
        }
    }

    static float SearchTime(Item it) => 0.3f + 0.25f * it.rarity;

    void Open(Mode m, LootBag bag)
    {
        mode = m;
        loot = bag;
        searchTimer = 0f;
        selected = null;
        if (bag != null) bag.opened = true;
        player.inputEnabled = false;
        FX.SetPaused(true);
    }

    public void Close()
    {
        if (!IsOpen) return;
        if (loot != null) loot.CloseIfEmpty();
        loot = null;
        mode = Mode.Closed;
        selected = null;
        FX.SetPaused(false);
        player.inputEnabled = true;
    }

    void OnDestroy()
    {
        if (IsOpen) FX.SetPaused(false);
    }

    void Toast(string s)
    {
        message = s;
        messageUntil = Time.unscaledTime + 1.6f;
    }

    // ================= 界面 =================
    const float Slot = 66f;

    GUIStyle sTitle, sText, sSmall, sGlyph, sSlotName, sBtn;

    void Styles()
    {
        if (sTitle != null) return;
        sTitle = new GUIStyle(GUI.skin.label) { fontSize = 20, fontStyle = FontStyle.Bold, richText = true };
        sTitle.normal.textColor = new Color(1f, 0.88f, 0.6f);
        sText = new GUIStyle(GUI.skin.label) { fontSize = 14, richText = true, wordWrap = true };
        sText.normal.textColor = new Color(0.88f, 0.9f, 0.94f);
        sSmall = new GUIStyle(sText) { fontSize = 12 };
        sGlyph = new GUIStyle(GUI.skin.label) { fontSize = 26, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        sSlotName = new GUIStyle(GUI.skin.label) { fontSize = 10, alignment = TextAnchor.LowerCenter, richText = true, clipping = TextClipping.Clip };
        sBtn = new GUIStyle(GUI.skin.button) { fontSize = 14 };
    }

    void OnGUI()
    {
        if (!IsOpen) return;
        GUI.depth = -10;
        Styles();
        hovered = null;

        // 背景压暗
        Fill(new Rect(0, 0, Screen.width, Screen.height), new Color(0, 0, 0, 0.45f));

        float W = Mathf.Min(1060, Screen.width - 30), H = Mathf.Min(640, Screen.height - 30);
        var win = new Rect((Screen.width - W) / 2f, (Screen.height - H) / 2f, W, H);
        Fill(win, new Color(0.07f, 0.08f, 0.1f, 0.96f));
        Border(win, new Color(0.6f, 0.5f, 0.3f), 2);

        float halfW = (W - 52) / 2f;
        var left = new Rect(win.x + 16, win.y + 14, halfW, H - 190);
        var right = new Rect(left.xMax + 20, win.y + 14, halfW, H - 190);
        var bottom = new Rect(win.x + 16, win.yMax - 166, W - 32, 152);

        // 背包模式：左装备 右背包；搜刮模式：左背包 右战利品
        if (mode == Mode.Inventory) { DrawEquipment(left); DrawBag(right); }
        else { DrawBag(left); DrawLoot(right); }
        DrawDetail(bottom);

        // 提示
        GUI.Label(new Rect(win.x, win.y - 24, W, 22),
            mode == Mode.Loot ? "Ctrl + 左键：在背包和战利品之间快速转移    左键：查看    F / Tab / Esc 关闭（游戏已暂停）"
                              : "左键选中    Ctrl+左键 / 右键：快速装备、使用    选中后点装备栏装到指定栏位    点已穿的装备卸下    Tab / Esc 关闭（游戏已暂停）",
            new GUIStyle(sSmall) { alignment = TextAnchor.MiddleCenter });
        if (Time.unscaledTime < messageUntil)
        {
            var ms = new GUIStyle(sTitle) { alignment = TextAnchor.MiddleCenter, fontSize = 18 };
            ms.normal.textColor = new Color(1f, 0.6f, 0.4f);
            GUI.Label(new Rect(win.x, win.yMax + 4, W, 26), message, ms);
        }
    }

    // ---------- 左：人物装备（纸娃娃） ----------
    void DrawEquipment(Rect r)
    {
        GUI.Label(new Rect(r.x, r.y, r.width, 28), $"{stats.character?.name ?? "猫猫"}  <size=14><color=#bbbbbb>{stats.character?.title}</color></size>", sTitle);

        float cx = r.x + r.width / 2f - Slot / 2f, y0 = r.y + 40, gap = Slot + 30;
        EquipSlotBox(new Rect(cx, y0, Slot, Slot), EquipSlot.Head, "头盔");
        EquipSlotBox(new Rect(cx - gap, y0 + Slot + 22, Slot, Slot), EquipSlot.MainHand, "主手");
        EquipSlotBox(new Rect(cx, y0 + Slot + 22, Slot, Slot), EquipSlot.Body, "护甲");
        EquipSlotBox(new Rect(cx + gap, y0 + Slot + 22, Slot, Slot), EquipSlot.OffHand, "副手");
        EquipSlotBox(new Rect(cx - gap, y0 + 2 * (Slot + 22), Slot, Slot), EquipSlot.Accessory1, "饰品");
        EquipSlotBox(new Rect(cx, y0 + 2 * (Slot + 22), Slot, Slot), EquipSlot.Feet, "靴子");
        EquipSlotBox(new Rect(cx + gap, y0 + 2 * (Slot + 22), Slot, Slot), EquipSlot.Accessory2, "饰品");

        // 属性
        float sy = y0 + 3 * (Slot + 22) + 4;
        string buffs = "";
        foreach (var b in stats.buffs)
            buffs += $"<color=#{ColorUtility.ToHtmlStringRGB(b.potion.PotionColor)}>{b.potion.name} {b.endTime - Time.time:F0}s</color>  ";
        string s =
            $"生命 <b>{Mathf.CeilToInt(health.hp)}/{health.maxHp:F0}</b>    护甲 <b>{stats.Armor:F0}</b>    总减伤 <b>{stats.TotalReduction * 100:F0}%</b>\n" +
            $"攻击力 <b>+{stats.DamageBonus * 100:F0}%</b>    攻速 <b>+{stats.AttackSpeedBonus * 100:F0}%</b>    移速 <b>{stats.MoveSpeed:F1}</b>\n" +
            (skill ? $"<color=#ffd84d>[Q] {skill.skillName} Lv.{skill.level}</color>  {skill.LevelText}\n" : "") +
            (buffs.Length > 0 ? "增益：" + buffs : "");
        GUI.Label(new Rect(r.x, sy, r.width, r.yMax - sy), s, sSmall);
    }

    void EquipSlotBox(Rect rect, EquipSlot slot, string label)
    {
        var it = inv.Get(slot);
        bool canDrop = selected != null && Inventory.Fits(selected, slot);
        ItemBox(rect, it, label, canDrop ? new Color(0.3f, 0.8f, 0.4f) : (Color?)null);
        GUI.Label(new Rect(rect.x - 10, rect.y - 17, rect.width + 20, 16), label, new GUIStyle(sSmall) { alignment = TextAnchor.MiddleCenter });

        if (Clicked(rect, out int button, out _))
        {
            if (canDrop)
            {
                inv.EquipFromBag(selected, slot);
                selected = null;
            }
            else if (it != null && (button == 1 || Event.current.clickCount >= 1))
            {
                if (!inv.Unequip(slot)) Toast("背包满了");
            }
        }
    }

    // ---------- 左：战利品袋 ----------
    void DrawLoot(Rect r)
    {
        GUI.Label(new Rect(r.x, r.y, r.width, 28), "战利品", sTitle);
        if (loot == null) return;
        int cols = 5;
        float pad = 8f;
        for (int i = 0; i < loot.items.Count; i++)
        {
            var rect = new Rect(r.x + (i % cols) * (Slot + pad), r.y + 40 + (i / cols) * (Slot + pad), Slot, Slot);
            if (!loot.revealed[i])
            {
                Fill(rect, new Color(0.15f, 0.15f, 0.17f));
                Border(rect, new Color(0.4f, 0.4f, 0.4f), 1);
                bool searching = loot.revealed.IndexOf(false) == i;
                GUI.Label(rect, searching ? "…" : "?", sGlyph);
                if (searching)
                {
                    float k = searchTimer / SearchTime(loot.items[i]);
                    Fill(new Rect(rect.x + 4, rect.yMax - 10, (rect.width - 8) * k, 5), new Color(1f, 0.85f, 0.4f));
                }
                continue;
            }
            var it = loot.items[i];
            ItemBox(rect, it, null, it == selected ? new Color(1f, 0.9f, 0.4f) : (Color?)null);
            if (Clicked(rect, out int button, out bool ctrl))
            {
                if (ctrl || button == 1) TakeFromLoot(it);
                else selected = selected == it ? null : it;
                GUIUtility.ExitGUI();
            }
        }
        if (loot.items.Count == 0) GUI.Label(new Rect(r.x, r.y + 40, r.width, 24), "（空了）", sText);

        bool searchingAny = loot.revealed.Contains(false);
        GUI.Label(new Rect(r.x, r.yMax - 64, r.width, 22), searchingAny ? "搜索中……" : "搜刮完毕", sSmall);
        if (GUI.Button(new Rect(r.x, r.yMax - 40, 160, 34), "全部拿走", sBtn))
        {
            for (int i = loot.items.Count - 1; i >= 0; i--)
            {
                if (!loot.revealed[i]) continue;
                if (!inv.Add(loot.items[i])) { Toast("背包满了"); break; }
                loot.Take(i);
            }
        }
    }

    // ---------- 右：背包 ----------
    void DrawBag(Rect r)
    {
        GUI.Label(new Rect(r.x, r.y, r.width, 28), $"背包  <size=14><color=#bbbbbb>{inv.bag.Count}/{Inventory.Capacity}</color></size>", sTitle);
        int cols = 6;
        float pad = 8f;
        for (int i = 0; i < Inventory.Capacity; i++)
        {
            var rect = new Rect(r.x + (i % cols) * (Slot + pad), r.y + 40 + (i / cols) * (Slot + pad), Slot, Slot);
            var it = i < inv.bag.Count ? inv.bag[i] : null;
            ItemBox(rect, it, null, it != null && it == selected ? new Color(1f, 0.9f, 0.4f) : (Color?)null);
            if (it == null) continue;
            if (Clicked(rect, out int button, out bool ctrl))
            {
                if (ctrl && mode == Mode.Loot && loot != null) PutIntoLoot(it);
                else if (ctrl || button == 1) QuickAction(it);
                else selected = selected == it ? null : it;
                GUIUtility.ExitGUI();
            }
        }
        float hy = r.y + 40 + 4 * (Slot + pad);
        GUI.Label(new Rect(r.x, hy, r.width, 22), $"血药 ×{inv.CountPotions(PotionType.Health)}（战斗中按 1 快速喝）", sSmall);
    }

    void TakeFromLoot(Item it)
    {
        int i = loot.items.IndexOf(it);
        if (i < 0 || !loot.revealed[i]) return;
        if (!inv.Add(it)) { Toast("背包满了"); return; }
        loot.Take(i);
        if (selected == it) selected = null;
    }

    void PutIntoLoot(Item it)
    {
        if (!inv.bag.Remove(it)) return;
        loot.AddItem(it);
        if (selected == it) selected = null;
    }

    void QuickAction(Item it)
    {
        if (it.kind == ItemKind.Potion) { inv.Use(it); return; }
        inv.EquipFromBag(it, inv.DefaultSlot(it));
        if (selected == it) selected = null;
    }

    // ---------- 下：物品详情 ----------
    void DrawDetail(Rect r)
    {
        Fill(r, new Color(0.11f, 0.12f, 0.15f));
        Border(r, new Color(0.3f, 0.3f, 0.35f), 1);
        var it = hovered ?? selected;
        if (it == null)
        {
            GUI.Label(new Rect(r.x + 12, r.y + 10, r.width - 24, 24), "把鼠标移到物品上查看详情", sText);
            return;
        }
        GUI.Label(new Rect(r.x + 12, r.y + 8, r.width - 24, 28), $"{it.ColoredName}  <size=13><color=#999999>{WeaponData.RarityNames[it.rarity]} {it.TypeText}</color></size>", sTitle);
        string extra = it.kind == ItemKind.Weapon && !string.IsNullOrEmpty(it.weapon.description) ? "\n<color=#9aa>" + it.weapon.description + "</color>" : "";
        GUI.Label(new Rect(r.x + 12, r.y + 38, r.width - 300, r.height - 44), it.Description() + extra, sText);

        float bx = r.xMax - 150, by = r.y + 10;
        // 战利品里选中的物品
        if (it == selected && loot != null && loot.items.Contains(it))
        {
            if (GUI.Button(new Rect(bx, by, 138, 30), "拿取（Ctrl+左键）", sBtn)) TakeFromLoot(it);
            return;
        }
        // 按钮（只对背包里选中的物品）
        if (it != selected || !inv.bag.Contains(it)) return;
        if (mode == Mode.Loot && loot != null)
        {
            if (GUI.Button(new Rect(bx, by, 138, 30), "放进战利品袋", sBtn)) PutIntoLoot(it);
            by += 34;
        }
        if (it.kind == ItemKind.Weapon)
        {
            if (it.weapon.canMainHand && GUI.Button(new Rect(bx, by, 138, 30), "装备到主手", sBtn)) { inv.EquipFromBag(it, EquipSlot.MainHand); selected = null; }
            by += 34;
            if (it.weapon.canOffHand && GUI.Button(new Rect(bx, by, 138, 30), "装备到副手", sBtn)) { inv.EquipFromBag(it, EquipSlot.OffHand); selected = null; }
            by += 34;
        }
        else if (it.kind == ItemKind.Armor)
        {
            if (GUI.Button(new Rect(bx, by, 138, 30), "装备", sBtn)) { inv.EquipFromBag(it, inv.DefaultSlot(it)); selected = null; }
            by += 34;
        }
        else
        {
            if (GUI.Button(new Rect(bx, by, 138, 30), "使用", sBtn)) { if (inv.Use(it)) selected = null; }
            by += 34;
        }
        if (GUI.Button(new Rect(bx, by, 138, 30), "丢弃", sBtn)) { inv.Drop(it); selected = null; }
    }

    // ---------- 小工具 ----------
    void ItemBox(Rect rect, Item it, string emptyLabel, Color? highlight)
    {
        Fill(rect, new Color(0.13f, 0.14f, 0.17f));
        if (it == null)
        {
            Border(rect, highlight ?? new Color(0.3f, 0.3f, 0.34f), highlight.HasValue ? 2 : 1);
            if (emptyLabel != null)
            {
                var g = new GUIStyle(sGlyph) { fontSize = 14 };
                g.normal.textColor = new Color(0.4f, 0.4f, 0.45f);
                GUI.Label(rect, emptyLabel, g);
            }
            return;
        }
        Color rc = it.RarityColor;
        Fill(new Rect(rect.x, rect.y, rect.width, rect.height), new Color(rc.r, rc.g, rc.b, 0.12f));
        Border(rect, highlight ?? rc, highlight.HasValue ? 3 : 2);

        var icon = ItemIcons.Get(it);
        if (icon != null)
            GUI.DrawTexture(new Rect(rect.x + 5, rect.y + 3, rect.width - 10, rect.height - 16), icon, ScaleMode.ScaleToFit);
        else
        {
            var glyph = new GUIStyle(sGlyph);
            glyph.normal.textColor = it.kind == ItemKind.Potion ? it.PotionColor : Color.Lerp(rc, Color.white, 0.3f);
            GUI.Label(new Rect(rect.x, rect.y - 6, rect.width, rect.height), it.Glyph, glyph);
        }
        var nm = new GUIStyle(sSlotName);
        nm.normal.textColor = rc;
        GUI.Label(new Rect(rect.x + 2, rect.y, rect.width - 4, rect.height - 3), it.name, nm);

        if (rect.Contains(Event.current.mousePosition)) hovered = it;
    }

    static bool Clicked(Rect r, out int button, out bool ctrl)
    {
        var e = Event.current;
        button = e.button;
        ctrl = e.control || e.command;
        if (e.type == EventType.MouseDown && r.Contains(e.mousePosition))
        {
            e.Use();
            return true;
        }
        return false;
    }

    static void Fill(Rect r, Color c)
    {
        var old = GUI.color;
        GUI.color = c;
        GUI.DrawTexture(r, Texture2D.whiteTexture);
        GUI.color = old;
    }

    static void Border(Rect r, Color c, float w)
    {
        Fill(new Rect(r.x, r.y, r.width, w), c);
        Fill(new Rect(r.x, r.yMax - w, r.width, w), c);
        Fill(new Rect(r.x, r.y, w, r.height), c);
        Fill(new Rect(r.xMax - w, r.y, w, r.height), c);
    }
}

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

// 竞技场：开局选角色 → 30 波怪（每波比上一波多 5~10 只，分批刷出）→ 界面
public class ArenaManager : MonoBehaviour
{
    [Header("敌人预制体")]
    public GameObject grunt;
    public GameObject guard;
    public GameObject brute;
    public GameObject duelist;
    public GameObject archer;
    [Tooltip("每 5 波的小 Boss")]
    public GameObject miniBoss;
    [Tooltip("每 10 波的大 Boss")]
    public GameObject bigBoss;
    [Tooltip("敌人攻击速度倍率（0.5 = 慢一半）")]
    public float enemyAttackSpeed = 0.5f;

    [Header("初始武器")]
    public WeaponData sword;
    public WeaponData axe;
    public WeaponData shield;

    [Header("波次")]
    public int totalWaves = 30;
    public int addMin = 5, addMax = 10;
    [Tooltip("场上同时最多几只（剩下的排队陆续刷出）")]
    public int baseMaxAlive = 10;
    public float spawnInterval = 0.6f;
    public float delayBetweenWaves = 4f;
    public float healBetweenWaves = 40f;
    public Transform[] spawnPoints;
    public PlayerTopDown player;

    readonly List<Health> alive = new();
    readonly Queue<GameObject> queue = new();
    int waveIndex = -1, waveSize, waveTotal, killedThisWave;
    bool waveInProgress, victory;
    float nextWaveTime, nextSpawnTime;
    int totalKills;

    bool selecting = true;
    int selChar = 2;

    Health playerHealth;
    Combatant playerCombat;
    PlayerStats stats;
    PlayerSkill skill;
    Inventory inventory;
    InventoryUI inventoryUI;
    MusicPlayer music;

    void Start()
    {
        playerHealth = player.GetComponent<Health>();
        playerCombat = player.GetComponent<Combatant>();
        stats = player.GetComponent<PlayerStats>();
        skill = player.GetComponent<PlayerSkill>();
        inventory = player.GetComponent<Inventory>();
        inventoryUI = player.GetComponent<InventoryUI>();
        player.inputEnabled = false;
        music = gameObject.AddComponent<MusicPlayer>();
        music.Play(MusicPlayer.Track.Menu);
        PreviewCharacter(selChar);
        BossSkills.OnSummon = go => { ScaleEnemy(go); alive.Add(go.GetComponent<Health>()); waveTotal++; };
    }

    // ---------- 选角色 ----------
    void PreviewCharacter(int i)
    {
        selChar = i;
        var d = CharacterDef.All[i];
        CatBuilder.Rebuild(player.gameObject, d.look);   // 换成这只猫的模型
    }

    void StartGame()
    {
        if (axe == null || sword == null || shield == null) return;
        var d = CharacterDef.All[selChar];
        stats.character = d;
        skill.type = d.skill;
        skill.skillName = d.skillName;

        if (d.axe)
        {
            inventory.SetEquipped(EquipSlot.MainHand, Item.FromWeapon(axe));
            inventory.SetEquipped(EquipSlot.OffHand, null);
        }
        else if (d.dualSword)
        {
            inventory.SetEquipped(EquipSlot.MainHand, Item.FromWeapon(sword));
            inventory.SetEquipped(EquipSlot.OffHand, Item.FromWeapon(sword));
        }
        else
        {
            inventory.SetEquipped(EquipSlot.MainHand, Item.FromWeapon(sword));
            inventory.SetEquipped(EquipSlot.OffHand, Item.FromWeapon(shield));
        }
        inventory.Add(Item.Potion(PotionType.Health));
        inventory.Add(Item.Potion(PotionType.Health));

        selecting = false;
        player.inputEnabled = true;
        nextWaveTime = Time.time + 2f;
    }

    void Update()
    {
        UpdateMusic();
        if (selecting)
        {
            if (Input.GetKeyDown(KeyCode.Alpha1)) PreviewCharacter(0);
            if (Input.GetKeyDown(KeyCode.Alpha2)) PreviewCharacter(1);
            if (Input.GetKeyDown(KeyCode.Alpha3)) PreviewCharacter(2);
            if (Input.GetKeyDown(KeyCode.LeftArrow)) PreviewCharacter((selChar + 2) % 3);
            if (Input.GetKeyDown(KeyCode.RightArrow)) PreviewCharacter((selChar + 1) % 3);
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)) StartGame();
            return;
        }

        int before = alive.Count;
        alive.RemoveAll(h => h == null || h.IsDead);
        int died = before - alive.Count;
        killedThisWave += died;
        totalKills += died;

        if ((playerHealth.IsDead || victory) && Input.GetKeyDown(KeyCode.R))
        {
            FX.SetPaused(false);
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
            return;
        }
        if (playerHealth.IsDead)
        {
            if (inventoryUI.IsOpen) inventoryUI.Close();
            return;
        }
        if (victory) return;

        if (waveInProgress)
        {
            // 分批刷怪：场上数量没满就继续放
            int maxAlive = baseMaxAlive + waveIndex / 5;
            if (queue.Count > 0 && alive.Count < maxAlive && Time.time >= nextSpawnTime)
            {
                SpawnOne(queue.Dequeue());
                nextSpawnTime = Time.time + spawnInterval;
            }
            if (queue.Count == 0 && alive.Count == 0)
            {
                waveInProgress = false;
                if (waveIndex >= totalWaves - 1) victory = true;
                else
                {
                    nextWaveTime = Time.time + delayBetweenWaves;
                    playerHealth.Heal(healBetweenWaves);
                }
            }
        }
        else if (Time.time >= nextWaveTime) StartWave(++waveIndex);
    }

    // 选角 / 结束：舒缓；场上有 Boss：Boss 曲；其他：战斗曲
    void UpdateMusic()
    {
        if (music == null) return;
        if (selecting || victory || playerHealth.IsDead) { music.Play(MusicPlayer.Track.Menu); return; }
        bool boss = false;
        foreach (var h in alive) if (h != null && !h.IsDead && h.GetComponent<BossSkills>() != null) { boss = true; break; }
        music.Play(boss ? MusicPlayer.Track.Boss : MusicPlayer.Track.Battle);
    }

    void StartWave(int index)
    {
        waveSize += Random.Range(addMin, addMax + 1);
        int w = index + 1;
        killedThisWave = 0;

        // 按波次解锁兵种，后期精英更多
        var pool = new List<(GameObject prefab, float weight)> { (grunt, 4f) };
        if (w >= 2) pool.Add((guard, 3f));
        if (w >= 3) pool.Add((brute, 0.6f + 0.06f * w));
        if (w >= 4) pool.Add((duelist, 2f));
        if (w >= 3 && archer != null) pool.Add((archer, 2.2f));
        float total = 0f;
        foreach (var p in pool) total += p.weight;

        var list = new List<GameObject>();
        for (int i = 0; i < waveSize; i++)
        {
            float r = Random.value * total;
            foreach (var p in pool)
            {
                r -= p.weight;
                if (r <= 0f) { list.Add(p.prefab); break; }
            }
            if (list.Count <= i) list.Add(grunt);
        }
        // 每 10 波大 Boss，其余每 5 波小 Boss（排在最前面先出场）
        if (w % 10 == 0 && bigBoss != null) list.Insert(0, bigBoss);
        else if (w % 5 == 0 && miniBoss != null) list.Insert(0, miniBoss);

        queue.Clear();
        foreach (var g in list) queue.Enqueue(g);
        waveTotal = list.Count;
        waveInProgress = true;
        nextSpawnTime = Time.time;
    }

    void SpawnOne(GameObject prefab)
    {
        // 选一个离玩家远的刷怪点
        Transform sp = spawnPoints[Random.Range(0, spawnPoints.Length)];
        for (int k = 0; k < 3; k++)
        {
            var c = spawnPoints[Random.Range(0, spawnPoints.Length)];
            if ((c.position - player.transform.position).sqrMagnitude > (sp.position - player.transform.position).sqrMagnitude) sp = c;
        }
        Vector3 pos = sp.position + new Vector3(Random.Range(-2f, 2f), 0f, Random.Range(-2f, 2f));
        Vector3 face = player.transform.position - pos;
        face.y = 0f;
        var go = Instantiate(prefab, pos, Quaternion.LookRotation(face));
        ScaleEnemy(go);
        FX.Sparks(pos + Vector3.up, Vector3.up, new Color(0.8f, 0.5f, 1f), 12, 0.6f);
        alive.Add(go.GetComponent<Health>());
    }

    // 越往后越强：生命 +5%/波，伤害 +4%/波；敌人攻速统一 -50%
    void ScaleEnemy(GameObject go)
    {
        var h = go.GetComponent<Health>();
        h.maxHp *= 1f + 0.05f * Mathf.Max(0, waveIndex);
        h.hp = h.maxHp;
        var c = go.GetComponent<Combatant>();
        c.damageMultiplier = 1f + 0.04f * Mathf.Max(0, waveIndex);
        c.attackSpeedMultiplier = enemyAttackSpeed;
        var ai = go.GetComponent<EnemyAI>();
        ai.level = Mathf.Max(0, waveIndex);
        ai.cooldownScale = 1f / Mathf.Max(0.1f, enemyAttackSpeed);
    }

    // ================= 界面 =================
    void OnGUI()
    {
        if (grunt == null || sword == null || axe == null || shield == null)
        {
            // 场景是旧版本生成的，缺少引用
            var warn = new GUIStyle(GUI.skin.box) { fontSize = 22, wordWrap = true, alignment = TextAnchor.MiddleCenter };
            warn.normal.textColor = new Color(1f, 0.6f, 0.4f);
            GUI.Box(new Rect(Screen.width / 2f - 320, 40, 640, 110),
                "场景是旧版本，需要重新生成：\n先停止运行（再点一次 ▶），然后点菜单 Tools > 生成战斗竞技场", warn);
            return;
        }
        if (selecting) { DrawCharacterSelect(); return; }

        var label = new GUIStyle(GUI.skin.label) { fontSize = 16, richText = true };
        label.normal.textColor = Color.white;
        var center = new GUIStyle(label) { alignment = TextAnchor.MiddleCenter, fontSize = 26, fontStyle = FontStyle.Bold };
        var small = new GUIStyle(label) { fontSize = 12, alignment = TextAnchor.MiddleCenter };

        // 敌人头顶血条
        var cam = Camera.main;
        foreach (var h in alive)
        {
            if (h == null) continue;
            Vector3 s = cam.WorldToScreenPoint(h.transform.position + Vector3.up * (h.HeadHeight + 0.2f));
            if (s.z < 0) continue;
            float x = s.x - 35, y = Screen.height - s.y;
            Bar(new Rect(x, y, 70, 7), h.hp / h.maxHp, new Color(0.9f, 0.25f, 0.2f));
            GUI.Label(new Rect(x - 30, y - 20, 130, 18), h.displayName, small);
            var ec = h.GetComponent<Combatant>();
            if (ec != null && ec.HasShield) PostureBar(new Rect(x, y + 9, 70, 5), ec);
        }

        // 顶部：Boss 大血条
        float bossY = 58;
        foreach (var h in alive)
        {
            if (h == null) continue;
            var boss = h.GetComponent<BossSkills>();
            if (boss == null) continue;
            float bw = Mathf.Min(700, Screen.width - 80);
            var br = new Rect((Screen.width - bw) / 2f, bossY + 24, bw, 18);
            var bst = new GUIStyle(center) { fontSize = 18 };
            bst.normal.textColor = boss.bigBoss ? new Color(1f, 0.8f, 0.3f) : new Color(1f, 0.55f, 0.45f);
            GUI.Label(new Rect(br.x, bossY, bw, 24), $"{(boss.bigBoss ? "【大 Boss】" : "【小 Boss】")} {boss.bossTitle}", bst);
            Bar(br, h.hp / h.maxHp, boss.bigBoss ? new Color(0.85f, 0.15f, 0.5f) : new Color(0.85f, 0.3f, 0.15f));
            GUI.Label(br, $"{Mathf.CeilToInt(h.hp)} / {h.maxHp:F0}", new GUIStyle(small) { fontSize = 13 });
            bossY += 54;
        }

        // 左下：增益 / 血量 / 体力 / 架势
        float bx = 20, by = Screen.height - 80;
        string buffs = "";
        if (skill.BuffActive) buffs += $"<color=#{ColorUtility.ToHtmlStringRGB(skill.SkillColor)}>{skill.skillName} {skill.BuffRemaining:F0}s</color>   ";
        foreach (var b in stats.buffs)
            buffs += $"<color=#{ColorUtility.ToHtmlStringRGB(b.potion.PotionColor)}>{b.potion.name} {b.endTime - Time.time:F0}s</color>   ";
        if (buffs.Length > 0) GUI.Label(new Rect(bx, by - 48, 600, 24), buffs, label);
        GUI.Label(new Rect(bx, by - 24, 400, 24), $"{stats.character?.name}   生命 {Mathf.CeilToInt(playerHealth.hp)} / {playerHealth.maxHp:F0}", label);
        Bar(new Rect(bx, by, 280, 16), playerHealth.hp / playerHealth.maxHp, new Color(0.85f, 0.2f, 0.2f));
        Bar(new Rect(bx, by + 24, 280, 10), playerCombat.Stamina / playerCombat.maxStamina,
            playerCombat.infiniteStamina ? PlayerSkill.AgilityColor : new Color(0.95f, 0.8f, 0.2f));
        if (playerCombat.HasShield) PostureBar(new Rect(bx, by + 38, 280, 8), playerCombat);

        // 下方中间：Q 技能 + 血药
        DrawSkillBox(new Rect(Screen.width / 2f - 130, Screen.height - 86, 150, 70));
        var potionBox = new Rect(Screen.width / 2f + 30, Screen.height - 86, 100, 70);
        Fill(potionBox, new Color(0, 0, 0, 0.55f));
        var pst = new GUIStyle(label) { alignment = TextAnchor.MiddleCenter, fontSize = 15 };
        hpIconItem ??= new Item { kind = ItemKind.Potion, potion = PotionType.Health, name = "血药" };
        var potionIcon = ItemIcons.Get(hpIconItem);
        if (potionIcon != null) GUI.DrawTexture(new Rect(potionBox.x + 4, potionBox.y + 8, 40, 54), potionIcon, ScaleMode.ScaleToFit);
        GUI.Label(new Rect(potionBox.x + 38, potionBox.y, 62, 70), $"<color=#ff6666>[1]</color>\n×{inventory.CountPotions(PotionType.Health)}", pst);

        // 右下：主手 / 副手（带图标）
        HandSlot(new Rect(Screen.width - 168, Screen.height - 86, 70, 70), inventory.Get(EquipSlot.MainHand), "主手");
        HandSlot(new Rect(Screen.width - 90, Screen.height - 86, 70, 70), inventory.Get(EquipSlot.OffHand), "副手");

        // 搜刮提示
        if (inventoryUI.NearbyBag != null && !inventoryUI.IsOpen)
        {
            var box = new Rect(Screen.width / 2f - 90, Screen.height - 140, 180, 34);
            Fill(box, new Color(0, 0, 0, 0.65f));
            GUI.Label(box, inventoryUI.NearbyBag.opened ? "[F] 打开战利品" : "[F] 搜刮", new GUIStyle(pst) { fontSize = 17 });
        }

        // 左上：操作说明
        GUI.Label(new Rect(20, 15, 560, 140),
            "WASD 移动    鼠标 朝向    左键 主手（连点连击）    右键 副手/举盾\nAlt 翻滚    Q 技能    1 喝血药\nTab 背包    F 搜刮（打开时暂停）    M 音乐开关",
            new GUIStyle(label) { fontSize = 14 });

        // 顶部：波次
        string top;
        if (playerHealth.IsDead) top = $"你倒下了 —— 撑到第 {waveIndex + 1} 波，击败 {totalKills} 只 —— 按 R 重来";
        else if (victory) top = $"30 波全部通过！击败 {totalKills} 只 —— 按 R 重来";
        else if (!waveInProgress) top = waveIndex < 0 ? "准备……" : $"第 {waveIndex + 1} 波完成！下一波马上来（快去搜刮）";
        else top = $"第 {waveIndex + 1} / {totalWaves} 波{((waveIndex + 1) % 10 == 0 ? "（大 Boss）" : (waveIndex + 1) % 5 == 0 ? "（小 Boss）" : "")}   剩余 {waveTotal - killedThisWave}";
        GUI.Label(new Rect(0, 15, Screen.width, 40), top, center);
    }

    static Item hpIconItem;

    static void HandSlot(Rect r, Item it, string label)
    {
        Fill(r, new Color(0, 0, 0, 0.55f));
        var st = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.LowerCenter, fontSize = 11, richText = true, clipping = TextClipping.Clip };
        st.normal.textColor = new Color(0.8f, 0.8f, 0.8f);
        if (it != null)
        {
            Border(r, it.RarityColor, 2);
            var icon = ItemIcons.Get(it);
            if (icon != null) GUI.DrawTexture(new Rect(r.x + 6, r.y + 4, r.width - 12, r.height - 18), icon, ScaleMode.ScaleToFit);
            GUI.Label(new Rect(r.x + 2, r.y, r.width - 4, r.height - 2), it.ColoredName, st);
        }
        else GUI.Label(new Rect(r.x, r.y, r.width, r.height - 2), "空", st);
        var lb = new GUIStyle(st) { alignment = TextAnchor.UpperCenter };
        GUI.Label(new Rect(r.x, r.y - 16, r.width, 16), label, lb);
    }

    void DrawSkillBox(Rect r)
    {
        Fill(r, new Color(0, 0, 0, 0.55f));
        float cd = skill.CooldownRemaining;
        if (cd > 0f) Fill(new Rect(r.x, r.y + r.height * (1f - cd / skill.Cooldown), r.width, r.height * cd / skill.Cooldown), new Color(0.3f, 0.3f, 0.35f, 0.7f));
        Color c = skill.SkillColor;
        if (skill.BuffActive) Border(r, c, 3);
        else if (cd <= 0f) Border(r, new Color(c.r, c.g, c.b, 0.6f + 0.4f * Mathf.Sin(Time.time * 4f)), 2);
        var st = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 15, richText = true };
        st.normal.textColor = Color.white;
        string state = skill.BuffActive ? $"<color=#{ColorUtility.ToHtmlStringRGB(c)}>生效 {skill.BuffRemaining:F0}s</color>"
                     : cd > 0f ? $"{cd:F1}s" : "<color=#8f8>就绪</color>";
        GUI.Label(r, $"<color=#{ColorUtility.ToHtmlStringRGB(c)}>[Q] {skill.skillName}</color>  Lv{skill.level}\n{state}", st);
    }

    // ---------- 选角色界面 ----------
    void DrawCharacterSelect()
    {
        float W = Mathf.Min(1000, Screen.width - 30), H = 430;
        var panel = new Rect((Screen.width - W) / 2f, Screen.height - H - 20, W, H);
        Fill(panel, new Color(0.05f, 0.06f, 0.08f, 0.9f));

        var title = new GUIStyle(GUI.skin.label) { fontSize = 26, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        title.normal.textColor = new Color(1f, 0.85f, 0.5f);
        GUI.Label(new Rect(panel.x, panel.y + 8, W, 36), "选 择 你 的 猫 猫", title);

        var name = new GUIStyle(GUI.skin.label) { fontSize = 24, fontStyle = FontStyle.Bold, richText = true };
        name.normal.textColor = Color.white;
        var text = new GUIStyle(GUI.skin.label) { fontSize = 13, wordWrap = true, richText = true };
        text.normal.textColor = new Color(0.85f, 0.88f, 0.92f);

        float cw = (W - 64) / 3f;
        for (int i = 0; i < 3; i++)
        {
            var d = CharacterDef.All[i];
            var card = new Rect(panel.x + 16 + i * (cw + 16), panel.y + 54, cw, H - 130);
            bool sel = i == selChar;
            Fill(card, sel ? new Color(0.18f, 0.2f, 0.25f) : new Color(0.1f, 0.11f, 0.14f));
            Border(card, sel ? new Color(1f, 0.8f, 0.4f) : new Color(0.3f, 0.3f, 0.35f), sel ? 3 : 1);

            // 小头像：毛色 + 围巾色
            var face = new Rect(card.x + 14, card.y + 14, 52, 52);
            Fill(face, d.fur);
            Fill(new Rect(face.x + 6, face.y - 8, 12, 12), d.accent);
            Fill(new Rect(face.xMax - 18, face.y - 8, 12, 12), d.accent);
            Fill(new Rect(face.x + 12, face.y + 18, 8, 10), d.eye);
            Fill(new Rect(face.xMax - 20, face.y + 18, 8, 10), d.eye);
            Fill(new Rect(face.x, face.yMax - 8, face.width, 8), d.scarf);

            GUI.Label(new Rect(card.x + 78, card.y + 12, cw - 90, 30), $"{i + 1}. {d.name}", name);
            GUI.Label(new Rect(card.x + 78, card.y + 42, cw - 90, 22), $"<color=#aaaaaa>{d.title}</color>", text);
            GUI.Label(new Rect(card.x + 14, card.y + 80, cw - 28, 70), d.description, text);
            Color sc = d.skill switch { SkillType.Spin => PlayerSkill.SpinColor, SkillType.Agility => PlayerSkill.AgilityColor, _ => PlayerSkill.BerserkColor };
            GUI.Label(new Rect(card.x + 14, card.y + 150, cw - 28, 120),
                $"<color=#{ColorUtility.ToHtmlStringRGB(sc)}><b>[Q] {d.skillName}</b></color>\n{d.skillDescription}", text);

            if (Event.current.type == EventType.MouseDown && card.Contains(Event.current.mousePosition))
            {
                PreviewCharacter(i);
                Event.current.Use();
            }
        }

        GUI.backgroundColor = new Color(1f, 0.75f, 0.35f);
        if (GUI.Button(new Rect(panel.x + W / 2f - 150, panel.yMax - 62, 300, 46), $"用 {CharacterDef.All[selChar].name} 开始（Enter）",
                       new GUIStyle(GUI.skin.button) { fontSize = 20, fontStyle = FontStyle.Bold }))
            StartGame();
        GUI.backgroundColor = Color.white;
    }

    // ---------- 工具 ----------
    static void PostureBar(Rect r, Combatant c)
    {
        if (c.ShieldBroken)
            Bar(r, 1f - c.ShieldBrokenRemaining / c.shieldBrokenTime, new Color(1f, 0.5f, 0.2f));
        else
            Bar(r, c.Posture / Mathf.Max(1f, c.maxPosture), new Color(0.4f, 0.75f, 1f));
    }

    static void Bar(Rect r, float t, Color c)
    {
        Fill(new Rect(r.x - 2, r.y - 2, r.width + 4, r.height + 4), new Color(0, 0, 0, 0.6f));
        Fill(new Rect(r.x, r.y, r.width * Mathf.Clamp01(t), r.height), c);
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

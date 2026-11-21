using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// 一键生成：武器数据、材质、敌人预制体、竞技场场景
// 菜单：Tools > 生成战斗竞技场（重新运行会覆盖武器数值和场景）
public static class ArenaBuilder
{
    const string Root = "Assets/Arena";
    public const string ScenePath = "Assets/Scenes/Arena.unity";
    // 改了生成逻辑就 +1，编辑器加载时会自动重新生成场景
    public const int Version = 6;

    [MenuItem("Tools/生成战斗竞技场")]
    public static void Build()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        foreach (var d in new[] { "Weapons", "Materials", "Prefabs" }) Directory.CreateDirectory($"{Root}/{d}");
        Directory.CreateDirectory("Assets/Scenes");

        // ---------- 武器 ----------
        var sword = Weapon("Sword", w =>
        {
            w.displayName = "单手剑"; w.type = WeaponType.Sword; w.shape = AttackShape.Arc;
            w.description = "出手快，三段连击，最后一击是重击。最均衡的选择。";
            w.damage = 18; w.range = 2.0f; w.arcAngle = 110;
            w.windup = 0.12f; w.active = 0.1f; w.recovery = 0.25f; w.comboLength = 3;
            w.staminaCost = 10; w.knockback = 3; w.hitStun = 0.35f; w.guardDamage = 15; w.lunge = 3;
            w.color = new Color(0.8f, 0.84f, 0.9f); w.length = 1.0f;
        });
        var spear = Weapon("Spear", w =>
        {
            w.displayName = "长枪"; w.type = WeaponType.Spear; w.shape = AttackShape.Thrust;
            w.description = "攻击距离最远的直线突刺，适合在敌人够不着的距离戳。侧面没有判定。";
            w.damage = 22; w.range = 3.3f; w.thrustWidth = 0.6f;
            w.windup = 0.18f; w.active = 0.12f; w.recovery = 0.3f; w.comboLength = 2;
            w.staminaCost = 12; w.knockback = 4.5f; w.hitStun = 0.3f; w.guardDamage = 12; w.lunge = 4;
            w.color = new Color(0.75f, 0.78f, 0.82f); w.length = 2.6f;
        });
        var axe = Weapon("Axe", w =>
        {
            w.displayName = "战斧"; w.type = WeaponType.Axe; w.shape = AttackShape.Arc;
            w.description = "慢但伤害高，大范围横扫。第二段是竖劈砸地，削架势极多，专门破盾。";
            w.damage = 32; w.range = 1.9f; w.arcAngle = 140;
            w.windup = 0.3f; w.active = 0.12f; w.recovery = 0.45f; w.comboLength = 2;
            w.staminaCost = 20; w.knockback = 7; w.hitStun = 0.5f; w.guardDamage = 40; w.lunge = 2.5f;
            w.color = new Color(0.55f, 0.57f, 0.6f); w.length = 1.15f;
        });
        var bow = Weapon("Bow", w =>
        {
            w.displayName = "短弓"; w.type = WeaponType.Bow; w.shape = AttackShape.Projectile;
            w.description = "远程武器：拉弓后朝前方射出一支箭。箭会被墙和柱子挡住，也能被盾挡下。";
            w.canMainHand = true; w.canOffHand = false;
            w.damage = 14; w.range = 12f; w.projectileSpeed = 16f;
            w.windup = 0.45f; w.active = 0.05f; w.recovery = 0.35f; w.comboLength = 1;
            w.staminaCost = 12; w.knockback = 2; w.hitStun = 0.25f; w.guardDamage = 12; w.lunge = 0;
            w.color = new Color(0.55f, 0.38f, 0.2f); w.length = 0.6f;
        });
        var shield = Weapon("Shield", w =>
        {
            w.displayName = "盾牌"; w.type = WeaponType.Shield;
            w.description = "只能放副手。按住右键举盾挡正面攻击；刚举起时挡下 = 完美格挡，对手硬直。每次挡下都会掉架势，架势掉光就破盾：盾牌碎掉 5 秒、长硬直、挨打伤害 ×1.5。";
            w.canMainHand = false; w.canOffHand = true; w.damage = 0;
            w.canBlock = true; w.blockDamageReduction = 0.8f; w.blockAngle = 140; w.parryWindow = 0.2f; w.blockMoveMultiplier = 0.55f;
            w.color = new Color(0.5f, 0.33f, 0.2f);
        });

        // ---------- 材质 ----------
        var mGround = Mat("Ground", new Color(0.32f, 0.36f, 0.3f));
        var mWall = Mat("Wall", new Color(0.45f, 0.43f, 0.4f));
        var mCrate = Mat("Crate", new Color(0.55f, 0.4f, 0.25f));
        // 猫猫配色：毛色 / 肚皮 / 重点色（耳朵、尾巴尖、爪子）/ 眼睛 / 围巾
        var catPlayer = Cat("Player", new Color(0.95f, 0.6f, 0.25f), new Color(1f, 0.92f, 0.8f), new Color(0.8f, 0.42f, 0.15f),
                            new Color(0.35f, 0.85f, 0.4f), new Color(0.2f, 0.7f, 0.65f), true);
        var catGrunt = Cat("Grunt", new Color(0.55f, 0.56f, 0.6f), new Color(0.85f, 0.85f, 0.88f), new Color(0.38f, 0.38f, 0.42f),
                           new Color(1f, 0.7f, 0.15f), new Color(0.75f, 0.25f, 0.22f), false);
        var catGuard = Cat("Guard", new Color(0.95f, 0.95f, 0.95f), new Color(1f, 1f, 1f), new Color(0.2f, 0.2f, 0.22f),
                           new Color(0.3f, 0.6f, 1f), new Color(0.25f, 0.35f, 0.7f), false);
        var catBrute = Cat("Brute", new Color(0.13f, 0.12f, 0.14f), new Color(0.3f, 0.28f, 0.3f), new Color(0.08f, 0.08f, 0.09f),
                           new Color(1f, 0.85f, 0.1f), new Color(0.6f, 0.15f, 0.12f), false);
        var catDuelist = Cat("Duelist", new Color(0.93f, 0.86f, 0.72f), new Color(1f, 0.97f, 0.9f), new Color(0.35f, 0.24f, 0.18f),
                             new Color(0.35f, 0.65f, 1f), new Color(0.55f, 0.3f, 0.6f), false);

        var catArcher = Cat("Archer", new Color(0.6f, 0.45f, 0.3f), new Color(0.9f, 0.82f, 0.7f), new Color(0.4f, 0.28f, 0.18f),
                            new Color(0.5f, 1f, 0.4f), new Color(0.3f, 0.55f, 0.25f), true);
        var catCaptain = Cat("Captain", new Color(0.75f, 0.35f, 0.2f), new Color(0.95f, 0.8f, 0.65f), new Color(0.5f, 0.2f, 0.1f),
                             new Color(1f, 0.3f, 0.2f), new Color(0.25f, 0.25f, 0.28f), true);
        var catKing = Cat("King", new Color(0.18f, 0.16f, 0.2f), new Color(0.4f, 0.36f, 0.42f), new Color(0.75f, 0.6f, 0.2f),
                          new Color(1f, 0.2f, 0.6f), new Color(0.6f, 0.1f, 0.15f), false);

        // ---------- 敌人预制体 ----------
        var grunt = EnemyPrefab("Grunt", "剑士猫", catGrunt, 1f, 60, sword, null, ai =>
        {
            ai.moveSpeed = 3.6f; ai.comboChance = 0.2f;
        }, c => c.attackTimeMultiplier = 1.5f);

        var guard = EnemyPrefab("Guard", "盾枪猫", catGuard, 1f, 80, spear, shield, ai =>
        {
            ai.moveSpeed = 3.0f; ai.blockChance = 0.6f; ai.comboChance = 0.1f;
        }, c => c.attackTimeMultiplier = 1.5f);

        var brute = EnemyPrefab("Brute", "斧头胖猫", catBrute, 1.3f, 170, axe, null, ai =>
        {
            ai.moveSpeed = 2.8f; ai.comboChance = 0.45f; ai.attackCooldownMin = 1.2f; ai.attackCooldownMax = 2.4f;
            ai.mainDropChance = 1f; ai.rarityBonus = 1;   // 精英：必掉武器，品质 +1
            ai.armorDropChance = 0.8f; ai.potionDropChance = 0.6f; ai.buffDropChance = 0.3f; ai.skillPotionChance = 0.2f;
        }, c => { c.attackTimeMultiplier = 1.6f; c.superArmor = true; });

        var duelist = EnemyPrefab("Duelist", "双刀猫", catDuelist, 0.95f, 70, sword, sword, ai =>
        {
            ai.moveSpeed = 4.2f; ai.comboChance = 0.5f; ai.offHandAttackChance = 0.5f;
            ai.attackCooldownMin = 0.7f; ai.attackCooldownMax = 1.5f;
        }, c => c.attackTimeMultiplier = 1.4f);

        var archer = EnemyPrefab("Archer", "弓箭猫", catArcher, 0.95f, 40, bow, null, ai =>
        {
            ai.ranged = true; ai.preferredDistance = 8f; ai.moveSpeed = 3.4f;
            ai.attackCooldownMin = 1.4f; ai.attackCooldownMax = 2.6f;
            ai.mainDropChance = 0.25f;
        }, c => c.attackTimeMultiplier = 1.3f);

        // 小 Boss：铁爪队长（剑盾，砸地）
        var miniBoss = EnemyPrefab("MiniBoss", "铁爪队长", catCaptain, 1.6f, 500, sword, shield, ai =>
        {
            ai.moveSpeed = 3.2f; ai.comboChance = 0.6f; ai.blockChance = 0.5f;
            ai.mainDropChance = 1f; ai.offDropChance = 0.6f; ai.rarityBonus = 1; ai.lootRolls = 3;
            ai.armorDropChance = 1f; ai.potionDropChance = 0.8f; ai.buffDropChance = 0.5f; ai.skillPotionChance = 0.5f;
        }, c => { c.attackTimeMultiplier = 1.3f; c.superArmor = true; c.hitStunMultiplier = 0.3f; c.maxPosture = 150f; },
        go =>
        {
            var b = go.AddComponent<BossSkills>();
            b.bossTitle = "铁爪队长"; b.slamRadius = 4f; b.slamDamage = 30f; b.slamCooldown = 8f;
            // 眼罩
            var head = go.GetComponent<CatSkin>().head;
            Part(PrimitiveType.Cube, head, "EyePatch", new Vector3(0.15f, 0.04f, 0.33f), new Vector3(0.16f, 0.16f, 0.03f), Mat("Boss_Black", new Color(0.05f, 0.05f, 0.05f)));
            Part(PrimitiveType.Cube, head, "PatchStrap", new Vector3(0, 0.12f, 0.2f), new Vector3(0.8f, 0.03f, 0.03f), Mat("Boss_Black", new Color(0.05f, 0.05f, 0.05f)), new Vector3(0, 0, -15));
        });

        // 大 Boss：猫猫大王（战斧，砸地 + 冲锋 + 召唤）
        var bigBoss = EnemyPrefab("BigBoss", "猫猫大王", catKing, 2.0f, 1100, axe, null, ai =>
        {
            ai.moveSpeed = 3f; ai.comboChance = 0.6f; ai.attackCooldownMin = 1f; ai.attackCooldownMax = 2f;
            ai.mainDropChance = 1f; ai.rarityBonus = 2; ai.lootRolls = 5;
            ai.armorDropChance = 1f; ai.potionDropChance = 1f; ai.buffDropChance = 0.6f; ai.skillPotionChance = 1f;
        }, c => { c.attackTimeMultiplier = 1.4f; c.superArmor = true; c.hitStunMultiplier = 0.2f; },
        go =>
        {
            var b = go.AddComponent<BossSkills>();
            b.bossTitle = "猫猫大王"; b.bigBoss = true;
            b.slamRadius = 5.5f; b.slamDamage = 40f; b.slamCooldown = 7f;
            b.canCharge = true; b.chargeDamage = 35f;
            b.summonPrefab = grunt; b.summonCount = 3;
            // 王冠
            var head = go.GetComponent<CatSkin>().head;
            var gold = Mat("Boss_Gold", new Color(1f, 0.8f, 0.2f));
            Part(PrimitiveType.Cylinder, head, "CrownBand", new Vector3(0, 0.32f, 0), new Vector3(0.5f, 0.06f, 0.5f), gold);
            for (int i = 0; i < 5; i++)
            {
                float a = i * 72f * Mathf.Deg2Rad;
                Part(PrimitiveType.Cube, head, "CrownSpike", new Vector3(Mathf.Sin(a) * 0.22f, 0.43f, Mathf.Cos(a) * 0.22f), new Vector3(0.07f, 0.16f, 0.07f), gold, new Vector3(0, i * 72f, 45));
            }
            Part(PrimitiveType.Sphere, head, "CrownGem", new Vector3(0, 0.34f, 0.25f), new Vector3(0.09f, 0.09f, 0.05f), Mat("Boss_Gem", new Color(1f, 0.15f, 0.4f)));
        });

        // ---------- 场景 ----------
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.42f, 0.44f, 0.5f);

        var lightGo = new GameObject("Directional Light");
        var light = lightGo.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.15f;
        light.shadows = LightShadows.Soft;
        light.color = new Color(1f, 0.96f, 0.88f);
        lightGo.transform.rotation = Quaternion.Euler(55f, -35f, 0f);

        var env = new GameObject("Environment");
        var groundGo = GameObject.CreatePrimitive(PrimitiveType.Plane);
        groundGo.name = "Ground";
        groundGo.transform.SetParent(env.transform);
        groundGo.transform.localScale = new Vector3(4f, 1f, 4f);   // 40 x 40
        groundGo.GetComponent<Renderer>().sharedMaterial = mGround;

        float half = 20f;
        Block(env, "Wall_N", new Vector3(0, 0.75f, half), new Vector3(2 * half + 1, 1.5f, 1), mWall);
        Block(env, "Wall_S", new Vector3(0, 0.75f, -half), new Vector3(2 * half + 1, 1.5f, 1), mWall);
        Block(env, "Wall_E", new Vector3(half, 0.75f, 0), new Vector3(1, 1.5f, 2 * half + 1), mWall);
        Block(env, "Wall_W", new Vector3(-half, 0.75f, 0), new Vector3(1, 1.5f, 2 * half + 1), mWall);

        foreach (var p in new[] { new Vector3(-9, 0, 8), new Vector3(9, 0, 8), new Vector3(-9, 0, -8), new Vector3(9, 0, -8) })
        {
            var pillar = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pillar.name = "Pillar";
            pillar.transform.SetParent(env.transform);
            pillar.transform.position = p + Vector3.up * 1.5f;
            pillar.transform.localScale = new Vector3(1.4f, 1.5f, 1.4f);
            pillar.GetComponent<Renderer>().sharedMaterial = mWall;
        }
        Block(env, "Crate", new Vector3(4, 0.5f, 12), Vector3.one, mCrate);
        Block(env, "Crate", new Vector3(5.2f, 0.5f, 12.3f), Vector3.one, mCrate);
        Block(env, "Crate", new Vector3(-13, 0.5f, -3), Vector3.one, mCrate);
        Block(env, "Crate", new Vector3(13, 0.5f, 3), new Vector3(1.5f, 1, 1), mCrate);

        // 玩家
        var player = Character("Player", "", catPlayer, 1f, Team.Player, 150, sword, shield);
        player.transform.position = new Vector3(0, 0, -3);
        var pc = player.GetComponent<Combatant>();
        pc.canParry = true;
        pc.hitStunMultiplier = 0.5f;
        var pt = player.AddComponent<PlayerTopDown>();
        player.AddComponent<PlayerStats>();
        player.AddComponent<PlayerSkill>();
        player.AddComponent<Inventory>();
        player.AddComponent<InventoryUI>();
        player.AddComponent<ArmorVisuals>();

        // 摄像机
        var camGo = new GameObject("Main Camera");
        camGo.tag = "MainCamera";
        var cam = camGo.AddComponent<Camera>();
        cam.fieldOfView = 42f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.12f, 0.13f, 0.16f);
        var tdc = camGo.AddComponent<TopDownCamera>();
        tdc.target = player.transform;
        camGo.transform.position = player.transform.position + tdc.offset;
        camGo.transform.rotation = Quaternion.LookRotation(-tdc.offset);

        // 刷怪点 + 波次
        var spawnRoot = new GameObject("SpawnPoints");
        Vector3[] sp = { new(-14, 0, 14), new(14, 0, 14), new(14, 0, -14), new(-14, 0, -14),
                         new(0, 0, 16), new(0, 0, -16), new(16, 0, 0), new(-16, 0, 0) };
        var spawns = new Transform[sp.Length];
        for (int i = 0; i < sp.Length; i++)
        {
            spawns[i] = new GameObject("Spawn" + i).transform;
            spawns[i].SetParent(spawnRoot.transform);
            spawns[i].position = sp[i];
        }

        var am = new GameObject("ArenaManager").AddComponent<ArenaManager>();
        am.player = pt;
        am.spawnPoints = spawns;
        am.grunt = grunt; am.guard = guard; am.brute = brute; am.duelist = duelist;
        am.archer = archer; am.miniBoss = miniBoss; am.bigBoss = bigBoss;
        am.sword = sword; am.axe = axe; am.shield = shield;

        EditorSceneManager.SaveScene(scene, ScenePath);
        EditorBuildSettings.scenes = new[]
        {
            new EditorBuildSettingsScene(ScenePath, true),
            new EditorBuildSettingsScene("Assets/Scenes/Level1.unity", true),
        };
        AssetDatabase.SaveAssets();
        Debug.Log("[ArenaBuilder] 战斗竞技场已生成: " + ScenePath);
    }

    // ---------------- 工具函数 ----------------

    static WeaponData Weapon(string file, System.Action<WeaponData> setup)
    {
        string path = $"{Root}/Weapons/{file}.asset";
        var w = AssetDatabase.LoadAssetAtPath<WeaponData>(path);
        if (w == null)
        {
            w = ScriptableObject.CreateInstance<WeaponData>();
            AssetDatabase.CreateAsset(w, path);
        }
        setup(w);
        EditorUtility.SetDirty(w);
        return w;
    }

    static Material Mat(string name, Color c)
    {
        string path = $"{Root}/Materials/{name}.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            m = new Material(Shader.Find("Standard"));
            AssetDatabase.CreateAsset(m, path);
        }
        m.color = c;
        m.SetFloat("_Glossiness", 0.15f);
        EditorUtility.SetDirty(m);
        return m;
    }

    static GameObject Block(GameObject parent, string name, Vector3 pos, Vector3 size, Material mat)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(parent.transform);
        go.transform.position = pos;
        go.transform.localScale = size;
        go.GetComponent<Renderer>().sharedMaterial = mat;
        return go;
    }

    static GameObject Visual(PrimitiveType t, Transform parent, string name, Vector3 pos, Vector3 scale, Material mat)
    {
        var go = GameObject.CreatePrimitive(t);
        go.name = name;
        Object.DestroyImmediate(go.GetComponent<Collider>());
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localScale = scale;
        go.GetComponent<Renderer>().sharedMaterial = mat;
        return go;
    }

    // 一只猫的全部材质
    struct CatLook { public Material fur, belly, accent, eye, nose, white, scarf; public bool stripes; }

    static CatLook Cat(string name, Color fur, Color belly, Color accent, Color eye, Color scarf, bool stripes)
    {
        return new CatLook
        {
            fur = Mat($"Cat_{name}_Fur", fur),
            belly = Mat($"Cat_{name}_Belly", belly),
            accent = Mat($"Cat_{name}_Accent", accent),
            eye = Mat($"Cat_{name}_Eye", eye),
            scarf = Mat($"Cat_{name}_Scarf", scarf),
            nose = Mat("Cat_Nose", new Color(1f, 0.55f, 0.6f)),
            white = Mat("Cat_Dark", new Color(0.05f, 0.05f, 0.06f)),
            stripes = stripes,
        };
    }

    static GameObject Part(PrimitiveType t, Transform parent, string name, Vector3 pos, Vector3 scale, Material mat, Vector3 euler = default)
    {
        var go = Visual(t, parent, name, pos, scale, mat);
        go.transform.localRotation = Quaternion.Euler(euler);
        go.GetComponent<Renderer>().shadowCastingMode =
            scale.x < 0.1f || scale.y < 0.1f ? UnityEngine.Rendering.ShadowCastingMode.Off : UnityEngine.Rendering.ShadowCastingMode.On;
        return go;
    }

    // 猫猫模型（Model 局部坐标：脚底 y=-0.9，头顶约 y=0.9，+z 是脸朝向）
    static void BuildCat(Transform model, CatLook m, GameObject root)
    {
        // 身体：圆滚滚的
        Part(PrimitiveType.Capsule, model, "Body", new Vector3(0, -0.3f, 0), new Vector3(0.85f, 0.5f, 0.8f), m.fur);
        Part(PrimitiveType.Sphere, model, "Belly", new Vector3(0, -0.35f, 0.22f), new Vector3(0.6f, 0.65f, 0.45f), m.belly);
        // 围巾（区分阵营/兵种）
        Part(PrimitiveType.Cylinder, model, "Scarf", new Vector3(0, 0.15f, 0), new Vector3(0.7f, 0.06f, 0.66f), m.scarf);
        Part(PrimitiveType.Cube, model, "ScarfTail", new Vector3(0.18f, 0.0f, -0.3f), new Vector3(0.14f, 0.3f, 0.04f), m.scarf, new Vector3(-15, 0, 10));

        // 头
        var head = new GameObject("Head").transform;
        head.SetParent(model, false);
        head.localPosition = new Vector3(0, 0.48f, 0.04f);
        Part(PrimitiveType.Sphere, head, "Skull", Vector3.zero, new Vector3(0.78f, 0.68f, 0.7f), m.fur);
        Part(PrimitiveType.Sphere, head, "Muzzle", new Vector3(0, -0.12f, 0.27f), new Vector3(0.34f, 0.2f, 0.2f), m.belly);
        Part(PrimitiveType.Sphere, head, "Nose", new Vector3(0, -0.06f, 0.36f), new Vector3(0.08f, 0.05f, 0.05f), m.nose);
        foreach (float s in new[] { -1f, 1f })
        {
            // 耳朵：转 45° 的方块一半埋进头里 = 三角耳
            Part(PrimitiveType.Cube, head, "Ear", new Vector3(0.2f * s, 0.3f, -0.02f), new Vector3(0.2f, 0.2f, 0.07f), m.accent, new Vector3(0, 0, 45 + 12 * s));
            Part(PrimitiveType.Cube, head, "EarInner", new Vector3(0.2f * s, 0.29f, 0.015f), new Vector3(0.11f, 0.11f, 0.03f), m.nose, new Vector3(0, 0, 45 + 12 * s));
            // 眼睛：彩色虹膜 + 竖瞳
            Part(PrimitiveType.Sphere, head, "Eye", new Vector3(0.15f * s, 0.04f, 0.3f), new Vector3(0.13f, 0.15f, 0.06f), m.eye);
            Part(PrimitiveType.Cube, head, "Pupil", new Vector3(0.15f * s, 0.04f, 0.33f), new Vector3(0.025f, 0.1f, 0.01f), m.white);
            // 胡须
            for (int i = 0; i < 2; i++)
                Part(PrimitiveType.Cube, head, "Whisker", new Vector3(0.24f * s, -0.1f - i * 0.035f, 0.28f), new Vector3(0.24f, 0.008f, 0.008f),
                     m.white, new Vector3(0, -12f * s, (i == 0 ? 8f : -6f) * s));
        }
        if (m.stripes)
            for (int i = 0; i < 3; i++)
                Part(PrimitiveType.Cube, head, "Stripe", new Vector3((i - 1) * 0.09f, 0.29f, 0.12f), new Vector3(0.035f, 0.02f, 0.18f), m.accent, new Vector3(-35, 0, 0));

        // 脚
        foreach (float s in new[] { -1f, 1f })
            Part(PrimitiveType.Sphere, model, "Foot", new Vector3(0.2f * s, -0.83f, 0.1f), new Vector3(0.24f, 0.15f, 0.32f), m.belly);

        // 尾巴：4 节，从屁股往后上方翘
        var tailRoot = new GameObject("Tail").transform;
        tailRoot.SetParent(model, false);
        tailRoot.localPosition = new Vector3(0, -0.55f, -0.35f);
        tailRoot.localRotation = Quaternion.Euler(-60f, 0, 0);
        var segs = new Transform[4];
        Transform parent = tailRoot;
        for (int i = 0; i < 4; i++)
        {
            var seg = new GameObject("Seg" + i).transform;
            seg.SetParent(parent, false);
            seg.localPosition = i == 0 ? Vector3.zero : new Vector3(0, 0.22f, 0);
            seg.localRotation = Quaternion.Euler(i == 0 ? 0f : 14f, 0, 0);
            Part(PrimitiveType.Capsule, seg, "Fur", new Vector3(0, 0.11f, 0), new Vector3(0.12f, 0.14f, 0.12f), i == 3 ? m.accent : m.fur);
            segs[i] = seg;
            parent = seg;
        }
        var tail = root.AddComponent<CatTail>();
        tail.segments = segs;
    }

    static GameObject Character(string name, string displayName, CatLook look, float scale,
                                Team team, float hp, WeaponData main, WeaponData off)
    {
        var root = new GameObject(name);
        var cc = root.AddComponent<CharacterController>();
        cc.height = 1.8f;
        cc.radius = 0.4f;
        cc.center = new Vector3(0, 0.9f, 0);
        cc.minMoveDistance = 0f;   // 默认 0.001，高帧率下每帧位移太小会被忽略
        root.AddComponent<CharacterMotor>();

        var h = root.AddComponent<Health>();
        h.team = team;
        h.maxHp = hp;
        h.displayName = displayName;

        // 身体模型挂在 Model 下，中心在腰部，翻滚时整体绕它旋转
        var model = new GameObject("Model").transform;
        model.SetParent(root.transform, false);
        model.localPosition = new Vector3(0, 0.9f, 0);

        BuildCat(model, look, root);

        var rh = new GameObject("RightHand").transform;
        rh.SetParent(model, false);
        rh.localPosition = new Vector3(0.45f, -0.15f, 0.15f);
        var lh = new GameObject("LeftHand").transform;
        lh.SetParent(model, false);
        lh.localPosition = new Vector3(-0.45f, -0.15f, 0.15f);
        // 猫爪（跟着手一起动）
        Part(PrimitiveType.Sphere, rh, "Paw", Vector3.zero, Vector3.one * 0.2f, look.belly);
        Part(PrimitiveType.Sphere, lh, "Paw", Vector3.zero, Vector3.one * 0.2f, look.belly);

        // 记录各部位渲染器（运行时换毛色）和挂护甲的点
        var skin = root.AddComponent<CatSkin>();
        var rs = model.GetComponentsInChildren<Renderer>();
        Renderer[] With(Material m) => System.Array.FindAll(rs, r => r.sharedMaterial == m);
        skin.fur = With(look.fur);
        skin.belly = With(look.belly);
        skin.accent = With(look.accent);
        skin.eye = With(look.eye);
        skin.scarf = With(look.scarf);
        skin.head = model.Find("Head");
        var feet = new System.Collections.Generic.List<Transform>();
        foreach (Transform ch in model) if (ch.name == "Foot") feet.Add(ch);
        skin.feet = feet.ToArray();

        var c = root.AddComponent<Combatant>();
        c.model = model;
        c.mainHand = main;
        c.offHand = off;
        c.rightHand = rh;
        c.leftHand = lh;

        root.transform.localScale = Vector3.one * scale;
        return root;
    }

    static GameObject EnemyPrefab(string file, string displayName, CatLook look, float scale, float hp,
                                  WeaponData main, WeaponData off,
                                  System.Action<EnemyAI> aiSetup, System.Action<Combatant> combatSetup,
                                  System.Action<GameObject> extra = null)
    {
        var go = Character(file, displayName, look, scale, Team.Enemy, hp, main, off);
        var c = go.GetComponent<Combatant>();
        c.telegraphAttacks = true;
        c.maxStamina = 80f;
        c.maxPosture = 50f;
        combatSetup?.Invoke(c);
        var ai = go.AddComponent<EnemyAI>();
        aiSetup?.Invoke(ai);
        extra?.Invoke(go);

        string path = $"{Root}/Prefabs/{file}.prefab";
        var prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
        Object.DestroyImmediate(go);
        return prefab;
    }
}

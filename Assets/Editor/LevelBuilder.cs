using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// 编辑器工具：一键生成第一关
// 菜单：Tools > 生成第一关
public static class LevelBuilder
{
    const string ArtDir = "Assets/Art";
    const string ScenePath = "Assets/Scenes/Level1.unity";

    [MenuItem("Tools/生成第一关")]
    public static void Build()
    {
        Sprite square = MakeSprite("Square", 16, false);
        Sprite circle = MakeSprite("Circle", 32, true);

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // 摄像机
        var camGo = new GameObject("Main Camera");
        camGo.tag = "MainCamera";
        var cam = camGo.AddComponent<Camera>();
        cam.orthographic = true;
        cam.orthographicSize = 6f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.36f, 0.62f, 0.85f);
        camGo.transform.position = new Vector3(0, 1.5f, -10);
        var follow = camGo.AddComponent<CameraFollow>();

        // 地面和平台：(x, y, 宽, 高)
        var ground = new GameObject("Ground");
        Vector4[] platforms =
        {
            new Vector4(0, -1, 16, 1),
            new Vector4(14, -1, 8, 1),
            new Vector4(5, 2, 4, 0.5f),
            new Vector4(10, 4, 3, 0.5f),
            new Vector4(16, 2.5f, 3, 0.5f),
            new Vector4(23, 0.5f, 4, 0.5f),
        };
        for (int i = 0; i < platforms.Length; i++)
        {
            var p = platforms[i];
            var go = Block($"Platform{i}", square, new Color(0.35f, 0.7f, 0.3f), new Vector2(p.x, p.y), new Vector2(p.z, p.w));
            go.AddComponent<BoxCollider2D>();
            go.transform.SetParent(ground.transform);
        }

        // 玩家
        var player = Block("Player", square, new Color(0.95f, 0.4f, 0.3f), new Vector2(-5, 1), new Vector2(0.9f, 0.9f));
        player.GetComponent<SpriteRenderer>().sortingOrder = 10;
        var rb = player.AddComponent<Rigidbody2D>();
        rb.gravityScale = 3f;
        rb.freezeRotation = true;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        var pCol = player.AddComponent<BoxCollider2D>();
        pCol.sharedMaterial = new PhysicsMaterial2D("NoFriction") { friction = 0f };
        player.AddComponent<PlayerController>();
        follow.target = player.transform;

        // 金币
        var coins = new GameObject("Coins");
        Vector2[] coinPos =
        {
            new Vector2(-2, 0.5f), new Vector2(1, 0.5f), new Vector2(5, 3.2f),
            new Vector2(10, 5.2f), new Vector2(16, 3.7f), new Vector2(23, 1.7f),
        };
        for (int i = 0; i < coinPos.Length; i++)
        {
            var c = Block($"Coin{i}", circle, new Color(1f, 0.85f, 0.1f), coinPos[i], new Vector2(0.6f, 0.6f));
            c.GetComponent<SpriteRenderer>().sortingOrder = 5;
            var cc = c.AddComponent<CircleCollider2D>();
            cc.isTrigger = true;
            c.AddComponent<Coin>();
            c.transform.SetParent(coins.transform);
        }

        // 游戏管理器
        var gm = new GameObject("GameManager").AddComponent<GameManager>();
        gm.player = player.transform;

        Directory.CreateDirectory("Assets/Scenes");
        EditorSceneManager.SaveScene(scene, ScenePath);
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        AssetDatabase.SaveAssets();
        Debug.Log("[LevelBuilder] 第一关已生成: " + ScenePath);
    }

    static GameObject Block(string name, Sprite sprite, Color color, Vector2 pos, Vector2 size)
    {
        var go = new GameObject(name);
        go.transform.position = pos;
        go.transform.localScale = new Vector3(size.x, size.y, 1);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.color = color;
        return go;
    }

    // 生成一张白色方块/圆形贴图并作为 Sprite 导入（1 单位大小）
    static Sprite MakeSprite(string name, int size, bool circle)
    {
        Directory.CreateDirectory(ArtDir);
        string path = $"{ArtDir}/{name}.png";
        if (!File.Exists(path))
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            float r = size / 2f;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                bool inside = !circle || new Vector2(x + 0.5f - r, y + 0.5f - r).magnitude <= r;
                tex.SetPixel(x, y, inside ? Color.white : Color.clear);
            }
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path);
        }

        var imp = (TextureImporter)AssetImporter.GetAtPath(path);
        imp.textureType = TextureImporterType.Sprite;
        imp.spritePixelsPerUnit = size;
        imp.filterMode = FilterMode.Point;
        imp.alphaIsTransparency = true;
        imp.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }
}

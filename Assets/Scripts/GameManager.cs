using System;
using UnityEngine;
using UnityEngine.SceneManagement;

// 游戏管理：计分、判断胜利、掉下去重来
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    // 自定义事件：分数变化时通知所有订阅者（比如以后的 UI、音效）
    public static event Action<int> OnScoreChanged;

    public Transform player;
    public float deathY = -10f;

    int score;
    int totalCoins;
    bool won;

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        totalCoins = FindObjectsByType<Coin>().Length;
    }

    void Update()
    {
        // 掉出地图就重新开始
        if (player != null && player.position.y < deathY)
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);

        if (won && Input.GetKeyDown(KeyCode.R))
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void AddScore(int amount)
    {
        score += amount;
        OnScoreChanged?.Invoke(score);
        if (score >= totalCoins) won = true;
    }

    // 最简单的屏幕文字显示（以后可以换成正式 UI）
    void OnGUI()
    {
        var style = new GUIStyle(GUI.skin.label) { fontSize = 28 };
        style.normal.textColor = Color.white;
        GUI.Label(new Rect(20, 15, 400, 40), $"金币: {score} / {totalCoins}", style);
        GUI.Label(new Rect(20, 50, 600, 30), "A/D 或 ←/→ 移动，空格跳跃",
                  new GUIStyle(style) { fontSize = 16 });

        if (won)
        {
            var big = new GUIStyle(style) { fontSize = 48, alignment = TextAnchor.MiddleCenter };
            GUI.Label(new Rect(0, Screen.height / 2f - 60, Screen.width, 60), "全部收集！你赢了！", big);
            GUI.Label(new Rect(0, Screen.height / 2f, Screen.width, 40), "按 R 重新开始",
                      new GUIStyle(big) { fontSize = 22 });
        }
    }
}

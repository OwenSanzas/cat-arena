using UnityEngine;

// 金币：玩家碰到就加分并消失
// 需要一个勾选了 Is Trigger 的 Collider2D
public class Coin : MonoBehaviour
{
    public int value = 1;
    public float spinSpeed = 180f;

    void Update()
    {
        // 简单的旋转动画
        transform.Rotate(0f, spinSpeed * Time.deltaTime, 0f);
    }

    // 事件回调：有东西进入触发区时，Unity 自动调用
    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.GetComponent<PlayerController>() == null) return;

        GameManager.Instance.AddScore(value);
        Destroy(gameObject);
    }
}

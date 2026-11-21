using UnityEngine;

// 玩家控制：左右移动 + 跳跃
// 挂在玩家物体上，需要 Rigidbody2D 和 Collider2D
[RequireComponent(typeof(Rigidbody2D))]
public class PlayerController : MonoBehaviour
{
    [Header("移动")]
    public float moveSpeed = 6f;
    public float jumpForce = 14f;

    [Header("地面检测")]
    public LayerMask groundLayer = ~0;   // 默认检测所有层
    public float groundCheckDistance = 0.1f;

    Rigidbody2D rb;
    Collider2D col;
    float inputX;
    bool jumpPressed;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        col = GetComponent<Collider2D>();
    }

    // Update：每帧调用，用来读取输入
    void Update()
    {
        inputX = Input.GetAxisRaw("Horizontal");           // A/D 或 ←/→
        if (Input.GetButtonDown("Jump") && IsGrounded())   // 空格
            jumpPressed = true;
    }

    // FixedUpdate：按固定间隔调用，物理相关的操作放这里
    void FixedUpdate()
    {
        rb.linearVelocity = new Vector2(inputX * moveSpeed, rb.linearVelocity.y);

        if (jumpPressed)
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);
            jumpPressed = false;
        }
    }

    // 检查脚底下一小块区域里有没有别的碰撞体（排除自己和触发器，比如金币）
    bool IsGrounded()
    {
        Bounds b = col.bounds;
        Vector2 center = new Vector2(b.center.x, b.min.y - groundCheckDistance * 0.5f);
        Vector2 size = new Vector2(b.size.x * 0.9f, groundCheckDistance);

        foreach (Collider2D hit in Physics2D.OverlapBoxAll(center, size, 0f, groundLayer))
        {
            if (hit != col && !hit.isTrigger)
                return true;
        }
        return false;
    }
}

using UnityEngine;

// 角色移动底层：处理重力、击退，玩家和敌人共用
[RequireComponent(typeof(CharacterController))]
public class CharacterMotor : MonoBehaviour
{
    public float gravity = 25f;
    public float knockbackDamping = 9f;

    CharacterController cc;
    Vector3 external;   // 击退等外力
    float vy;

    void Awake() => cc = GetComponent<CharacterController>();

    public void AddImpulse(Vector3 v)
    {
        v.y = 0f;
        external += v;
    }

    // 每帧由控制脚本调用一次，传入想要的水平速度
    public void Tick(Vector3 horizontalVelocity)
    {
        if (!cc.enabled) return;
        float dt = Time.deltaTime;

        if (cc.isGrounded && vy < 0f) vy = -2f;
        vy -= gravity * dt;

        Vector3 v = horizontalVelocity + external;
        v.y = vy;
        cc.Move(v * dt);

        external = Vector3.Lerp(external, Vector3.zero, 1f - Mathf.Exp(-knockbackDamping * dt));
    }
}

using UnityEngine;

// 玩家输入：WASD 移动、朝向鼠标、左键主手、右键副手、Alt 翻滚、Q 技能、1 喝血药（Tab 背包 / F 搜刮在 InventoryUI）
[RequireComponent(typeof(CharacterMotor), typeof(Combatant), typeof(Health))]
public class PlayerTopDown : MonoBehaviour
{
    public float moveSpeed = 5.5f;
    [Tooltip("兵器库打开时关闭操作")]
    public bool inputEnabled = true;

    public Vector3 AimPoint { get; private set; }

    CharacterMotor motor;
    Combatant combatant;
    Health health;
    Camera cam;
    PlayerSkill skill;
    Inventory inventory;

    void Awake()
    {
        motor = GetComponent<CharacterMotor>();
        combatant = GetComponent<Combatant>();
        health = GetComponent<Health>();
        skill = GetComponent<PlayerSkill>();
        inventory = GetComponent<Inventory>();
    }

    void Start() => cam = Camera.main;

    void Update()
    {
        if (health.IsDead) return;
        if (!inputEnabled)
        {
            combatant.SetBlock(false);
            motor.Tick(Vector3.zero);
            return;
        }
        float dt = Time.deltaTime;

        // 移动方向：相对镜头
        Vector3 input = new Vector3(Input.GetAxisRaw("Horizontal"), 0f, Input.GetAxisRaw("Vertical"));
        Vector3 f = cam.transform.forward; f.y = 0f; f.Normalize();
        Vector3 r = cam.transform.right; r.y = 0f; r.Normalize();
        Vector3 move = f * input.z + r * input.x;
        if (move.sqrMagnitude > 1f) move.Normalize();

        // 鼠标指向地面的点
        var ray = cam.ScreenPointToRay(Input.mousePosition);
        var ground = new Plane(Vector3.up, new Vector3(0f, transform.position.y, 0f));
        if (ground.Raycast(ray, out float dist)) AimPoint = ray.GetPoint(dist);

        Vector3 look = AimPoint - transform.position;
        look.y = 0f;
        if (combatant.CanRotate && look.sqrMagnitude > 0.01f)
        {
            float turn = combatant.State == CombatState.Idle ? 25f : 10f;
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(look), 1f - Mathf.Exp(-turn * dt));
        }

        // 战斗输入
        if (Input.GetMouseButtonDown(0)) combatant.TryAttack(false);

        bool offIsShield = combatant.offHand != null && combatant.offHand.canBlock;
        combatant.SetBlock(offIsShield && Input.GetMouseButton(1));
        if (!offIsShield && Input.GetMouseButtonDown(1)) combatant.TryAttack(true);

        if (Input.GetKeyDown(KeyCode.LeftAlt) || Input.GetKeyDown(KeyCode.RightAlt)) combatant.TryDodge(move);

        if (Input.GetKeyDown(KeyCode.Q) && skill != null) skill.TryActivate();
        if (Input.GetKeyDown(KeyCode.Alpha1) && inventory != null) inventory.QuickHeal();

        motor.Tick(move * moveSpeed * combatant.MoveMultiplier + combatant.ExtraVelocity);
    }
}

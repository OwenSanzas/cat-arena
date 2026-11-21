using UnityEngine;

// 斜俯视镜头：跟随玩家，往鼠标方向稍微偏一点，支持震屏
public class TopDownCamera : MonoBehaviour
{
    public static TopDownCamera Instance { get; private set; }

    public Transform target;
    public Vector3 offset = new Vector3(0f, 11.5f, -7.8f);
    public float smooth = 8f;
    public float lookAhead = 2.5f;

    PlayerTopDown player;
    Vector3 basePos;
    float shakeAmp, shakeTime;

    void Awake() => Instance = this;

    void Start()
    {
        if (target) player = target.GetComponent<PlayerTopDown>();
        transform.rotation = Quaternion.LookRotation(-offset);
        basePos = target ? target.position + offset : transform.position;
    }

    public static void Shake(float amplitude, float duration)
    {
        if (Instance == null) return;
        Instance.shakeAmp = Mathf.Max(Instance.shakeAmp, amplitude);
        Instance.shakeTime = Mathf.Max(Instance.shakeTime, duration);
    }

    void LateUpdate()
    {
        if (target == null) return;
        float dt = Time.unscaledDeltaTime;

        Vector3 focus = target.position;
        if (player != null)
        {
            Vector3 a = player.AimPoint - target.position;
            a.y = 0f;
            focus += Vector3.ClampMagnitude(a * 0.25f, lookAhead);
        }

        basePos = Vector3.Lerp(basePos, focus + offset, 1f - Mathf.Exp(-smooth * dt));
        Vector3 shake = Vector3.zero;
        if (shakeTime > 0f)
        {
            shakeTime -= dt;
            shake = Random.insideUnitSphere * shakeAmp;
            if (shakeTime <= 0f) shakeAmp = 0f;
        }
        transform.position = basePos + shake;
    }
}

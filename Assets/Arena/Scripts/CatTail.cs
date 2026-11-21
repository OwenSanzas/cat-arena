using UnityEngine;

// 猫尾巴：一节节摆动，移动越快摆得越厉害
public class CatTail : MonoBehaviour
{
    public Transform[] segments;
    public float swing = 18f;
    public float speed = 3f;

    Quaternion[] baseRot;
    Vector3 lastPos;
    float phase, excite;

    void Start()
    {
        SetSegments(segments);
        lastPos = transform.position;
        phase = Random.value * 10f;
    }

    // 换了尾巴模型时重新记录每节的初始角度
    public void SetSegments(Transform[] segs)
    {
        segments = segs;
        baseRot = new Quaternion[segs.Length];
        for (int i = 0; i < segs.Length; i++) baseRot[i] = segs[i].localRotation;
    }

    void Update()
    {
        float dt = Time.deltaTime;
        if (dt <= 0f) return;
        float v = (transform.position - lastPos).magnitude / dt;
        lastPos = transform.position;
        excite = Mathf.Lerp(excite, Mathf.Clamp01(v / 5f), 1f - Mathf.Exp(-5f * dt));
        phase += dt * speed * (1f + excite * 1.5f);

        for (int i = 0; i < segments.Length; i++)
        {
            if (segments[i] == null) continue;
            float a = Mathf.Sin(phase - i * 0.7f) * swing * (1f + excite) * (0.5f + i * 0.25f);
            segments[i].localRotation = baseRot[i] * Quaternion.Euler(excite * 12f, 0f, a);
        }
    }
}

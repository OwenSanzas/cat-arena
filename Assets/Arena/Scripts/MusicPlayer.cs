using System.Collections.Generic;
using UnityEngine;

// 背景音乐：全部用代码合成（不需要音乐文件），三段可无缝循环的曲子
//   选角：舒缓的弹拨 + 柔和铺底
//   战斗：鼓点 + 贝斯 + 快速琶音
//   Boss：更快更重的小调
// M 键静音；打开背包/搜刮时音量降低
public class MusicPlayer : MonoBehaviour
{
    public enum Track { Menu, Battle, Boss }

    [Range(0, 1)] public float volume = 0.4f;
    public static bool Muted;

    const int Rate = 32000;
    readonly Dictionary<Track, AudioClip> clips = new();
    AudioSource a, b;        // 两个音源交叉淡入淡出
    Track current;
    bool started;

    void Awake()
    {
        // 场景里没有"耳朵"就给主相机加一个
        if (FindAnyObjectByType<AudioListener>() == null && Camera.main != null) Camera.main.gameObject.AddComponent<AudioListener>();
        a = gameObject.AddComponent<AudioSource>();
        b = gameObject.AddComponent<AudioSource>();
        foreach (var s in new[] { a, b }) { s.loop = true; s.playOnAwake = false; s.volume = 0f; s.ignoreListenerPause = true; }
        clips[Track.Menu] = Make("Menu", 76, new[] { Am(57), F(53), C(48), G(55), Am(57), F(53), Dm(50), E(52) }, Style.Menu);
        clips[Track.Battle] = Make("Battle", 128, new[] { Am(57), F(53), C(48), G(55), Am(57), F(53), G(55), E(52) }, Style.Battle);
        clips[Track.Boss] = Make("Boss", 144, new[] { Dm(50), Bb(46), Gm(55), A(57), Dm(50), Bb(46), C(48), A(57) }, Style.Boss);
    }

    public void Play(Track t)
    {
        if (started && t == current) return;
        started = true;
        current = t;
        var tmp = a; a = b; b = tmp;   // a = 新的，b = 淡出的
        a.clip = clips[t];
        a.volume = 0f;
        a.Play();
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.M)) Muted = !Muted;
        float target = Muted ? 0f : volume * (FX.Paused ? 0.45f : 1f);
        float k = 1f - Mathf.Exp(-2.5f * Time.unscaledDeltaTime);
        a.volume = Mathf.Lerp(a.volume, target, k);
        b.volume = Mathf.Lerp(b.volume, 0f, k * 1.5f);
        if (b.isPlaying && b.volume < 0.005f) b.Stop();
    }

    // ================= 合成 =================
    enum Style { Menu, Battle, Boss }

    static int[] Am(int r) => new[] { r, r + 3, r + 7 };
    static int[] Dm(int r) => Am(r);
    static int[] Gm(int r) => Am(r);
    static int[] F(int r) => new[] { r, r + 4, r + 7 };
    static int[] C(int r) => F(r);
    static int[] G(int r) => F(r);
    static int[] E(int r) => F(r);
    static int[] A(int r) => F(r);
    static int[] Bb(int r) => F(r);

    static float Freq(int midi) => 440f * Mathf.Pow(2f, (midi - 69) / 12f);

    static AudioClip Make(string name, int bpm, int[][] chords, Style style)
    {
        float beat = 60f / bpm;
        int bars = chords.Length;
        int n = Mathf.RoundToInt(bars * 4 * beat * Rate);
        var buf = new float[n];
        var rng = new System.Random(name.GetHashCode());

        for (int bar = 0; bar < bars; bar++)
        {
            var ch = chords[bar];
            float t0 = bar * 4 * beat;
            int root = ch[0];
            while (root > 52) root -= 12;   // 贝斯放低八度

            // ---- 铺底和弦 ----
            float padVol = style == Style.Menu ? 0.05f : style == Style.Boss ? 0.035f : 0.025f;
            foreach (int m in ch) Note(buf, t0, 4 * beat, Freq(m + 12), padVol, Inst.Pad);

            // ---- 贝斯 ----
            if (style == Style.Menu)
            {
                Note(buf, t0, 2 * beat, Freq(root), 0.16f, Inst.Bass);
                Note(buf, t0 + 2 * beat, 2 * beat, Freq(root + 7), 0.12f, Inst.Bass);
            }
            else
            {
                for (int i = 0; i < 8; i++)
                {
                    int m = style == Style.Boss ? root - 12 + (i % 4 == 3 ? 12 : 0) : root + (i % 2 == 1 ? 12 : 0);
                    Note(buf, t0 + i * beat / 2, beat / 2 * 0.9f, Freq(m), style == Style.Boss ? 0.2f : 0.17f, Inst.Bass);
                }
            }

            // ---- 琶音 ----
            int steps = style == Style.Menu ? 8 : 16;
            float stepLen = 4 * beat / steps;
            int[] order = style == Style.Boss ? new[] { 0, 1, 2, 1, 0, 2, 1, 2 } : new[] { 0, 1, 2, 3, 2, 1, 0, 1 };
            for (int i = 0; i < steps; i++)
            {
                int idx = order[i % order.Length];
                int m = idx == 3 ? ch[0] + 12 : ch[idx];
                m += style == Style.Menu ? 12 : 24;
                if (style == Style.Menu && i % 2 == 1 && rng.NextDouble() < 0.3) continue;   // 舒缓的偶尔空一拍
                float vol = (style == Style.Menu ? 0.09f : 0.06f) * (i % 4 == 0 ? 1.2f : 1f);
                Note(buf, t0 + i * stepLen, stepLen * (style == Style.Menu ? 3f : 1.5f), Freq(m), vol, Inst.Pluck);
            }

            // ---- 主旋律（战斗/Boss 每两小节一句） ----
            if (style != Style.Menu && bar % 2 == 0)
            {
                int[] mel = style == Style.Boss ? new[] { 0, 2, 1, 2, 0, -1 } : new[] { 2, 1, 0, 1, 2, 2 };
                float[] at = { 0f, 1f, 1.5f, 2f, 3f, 3.5f };
                for (int i = 0; i < mel.Length; i++)
                {
                    int m = mel[i] < 0 ? ch[0] + 10 : ch[mel[i]];
                    Note(buf, t0 + at[i] * beat, beat * 0.45f, Freq(m + 12), 0.07f, Inst.Lead);
                }
            }

            // ---- 鼓 ----
            if (style != Style.Menu)
            {
                for (int i = 0; i < 4; i++)
                {
                    float bt = t0 + i * beat;
                    if (style == Style.Boss || i % 2 == 0) Note(buf, bt, 0.25f, 0, 0.45f, Inst.Kick);
                    if (i % 2 == 1) Note(buf, bt, 0.2f, 0, 0.22f, Inst.Snare);
                }
                int hats = style == Style.Boss ? 16 : 8;
                for (int i = 0; i < hats; i++)
                    Note(buf, t0 + i * 4 * beat / hats, 0.05f, 0, (i % 2 == 0 ? 0.06f : 0.035f) * (0.8f + 0.4f * (float)rng.NextDouble()), Inst.Hat);
                if (bar % 4 == 3)   // 每四小节加个过门
                    for (int i = 0; i < 4; i++) Note(buf, t0 + 3 * beat + i * beat / 4, 0.12f, 0, 0.12f + i * 0.03f, Inst.Snare);
            }
        }

        // 归一化 + 软削波
        float peak = 0.001f;
        foreach (var s in buf) peak = Mathf.Max(peak, Mathf.Abs(s));
        float g = 0.9f / peak;
        for (int i = 0; i < n; i++) buf[i] = (float)System.Math.Tanh(buf[i] * g * 1.2f) * 0.85f;

        var clip = AudioClip.Create(name, n, 1, Rate, false);
        clip.SetData(buf, 0);
        return clip;
    }

    enum Inst { Pad, Bass, Pluck, Lead, Kick, Snare, Hat }

    // 写入一个音符（超出结尾的部分绕回开头，保证无缝循环）
    static void Note(float[] buf, float start, float dur, float f, float vol, Inst inst)
    {
        int n = buf.Length;
        int s0 = Mathf.RoundToInt(start * Rate);
        float tail = inst == Inst.Pad ? 0.3f : inst == Inst.Pluck ? 0.25f : 0.05f;
        int len = Mathf.RoundToInt((dur + tail) * Rate);
        float phase = 0f, lp = 0f;
        uint noise = (uint)(s0 * 7919 + 1);
        for (int i = 0; i < len; i++)
        {
            float t = i / (float)Rate;
            float v;
            switch (inst)
            {
                case Inst.Pad:
                {
                    float env = Mathf.Min(1f, t / 0.25f) * (t > dur ? Mathf.Max(0f, 1f - (t - dur) / tail) : 1f);
                    v = (Mathf.Sin(2 * Mathf.PI * f * t) + 0.5f * Mathf.Sin(2 * Mathf.PI * f * 1.003f * t) + 0.25f * Mathf.Sin(4 * Mathf.PI * f * t)) * env * 0.6f;
                    break;
                }
                case Inst.Bass:
                {
                    phase += f / Rate; phase -= Mathf.Floor(phase);
                    float tri = 4f * Mathf.Abs(phase - 0.5f) - 1f;
                    float sq = phase < 0.5f ? 1f : -1f;
                    float env = Mathf.Min(1f, t / 0.005f) * Mathf.Exp(-t * 3f) * (t > dur ? Mathf.Max(0f, 1f - (t - dur) / tail) : 1f);
                    lp += ((tri * 0.8f + sq * 0.3f) - lp) * 0.25f;
                    v = lp * env;
                    break;
                }
                case Inst.Pluck:
                {
                    phase += f / Rate; phase -= Mathf.Floor(phase);
                    float saw = 2f * phase - 1f;
                    lp += (saw - lp) * Mathf.Lerp(0.5f, 0.08f, Mathf.Min(1f, t * 6f));   // 越到后面越闷
                    v = lp * Mathf.Min(1f, t / 0.003f) * Mathf.Exp(-t * 7f);
                    break;
                }
                case Inst.Lead:
                {
                    phase += f * (1f + 0.004f * Mathf.Sin(t * 30f)) / Rate; phase -= Mathf.Floor(phase);
                    float sq = phase < 0.5f ? 1f : -1f;
                    lp += (sq - lp) * 0.2f;
                    float env = Mathf.Min(1f, t / 0.01f) * (t > dur ? Mathf.Max(0f, 1f - (t - dur) / tail) : Mathf.Exp(-t * 1.5f));
                    v = lp * env;
                    break;
                }
                case Inst.Kick:
                {
                    float pf = 45f + 90f * Mathf.Exp(-t * 30f);
                    phase += pf / Rate;
                    v = Mathf.Sin(2 * Mathf.PI * phase) * Mathf.Exp(-t * 14f);
                    break;
                }
                case Inst.Snare:
                {
                    noise ^= noise << 13; noise ^= noise >> 17; noise ^= noise << 5;
                    float nz = (noise / (float)uint.MaxValue) * 2f - 1f;
                    v = (nz * 0.8f + Mathf.Sin(2 * Mathf.PI * 190f * t) * 0.4f) * Mathf.Exp(-t * 22f);
                    break;
                }
                default: // Hat
                {
                    noise ^= noise << 13; noise ^= noise >> 17; noise ^= noise << 5;
                    float nz = (noise / (float)uint.MaxValue) * 2f - 1f;
                    float hp = nz - lp; lp = nz;   // 简单高通
                    v = hp * Mathf.Exp(-t * 70f);
                    break;
                }
            }
            buf[(s0 + i) % n] += v * vol;
        }
    }
}

using System;
using UnityEngine;

/// <summary>
/// 오디오 파일 없이 코드로 만든 8비트 효과음.
/// </summary>
public class SoundFx : MonoBehaviour
{
    const int Rate = 44100;
    static readonly System.Random Rng = new System.Random(7);

    AudioSource src;
    AudioClip eat, hit, swipe, tap, click, stage;

    void Awake()
    {
        src = gameObject.AddComponent<AudioSource>();
        src.playOnAwake = false;
        src.spatialBlend = 0f;
        src.volume = 0.8f;

        eat = Make("eat", 0.16f, t => Square(t < 0.06f ? 660f : 990f, t) * Env(t, 0.16f) * 0.30f);
        hit = Make("hit", 0.5f, t => (Noise() * 0.55f + Saw(Mathf.Lerp(260f, 55f, t / 0.5f), t) * 0.45f) * Env(t, 0.5f) * 0.5f);
        swipe = Make("swipe", 0.16f, t => Noise() * Mathf.Sin(Mathf.PI * t / 0.16f) * 0.22f);
        tap = Make("tap", 0.05f, t => Sine(600f, t) * Env(t, 0.05f) * 0.25f);
        click = Make("click", 0.09f, t => Square(880f, t) * Env(t, 0.09f) * 0.22f);
        stage = Make("stage", 0.36f, t =>
            Square(t < 0.12f ? 523.25f : (t < 0.24f ? 659.25f : 783.99f), t) * Env(t % 0.12f, 0.12f) * 0.22f);
    }

    public void Eat() => Play(eat);
    public void Hit() => Play(hit);
    public void Swipe() => Play(swipe);
    public void Tap() => Play(tap);
    public void Click() => Play(click);
    public void Stage() => Play(stage);

    void Play(AudioClip c)
    {
        if (c != null && src != null) src.PlayOneShot(c);
    }

    static float Sine(float f, float t) => Mathf.Sin(2f * Mathf.PI * f * t);
    static float Square(float f, float t) => Sine(f, t) >= 0f ? 1f : -1f;
    static float Saw(float f, float t) => 2f * Mathf.Repeat(f * t, 1f) - 1f;
    static float Noise() => (float)(Rng.NextDouble() * 2.0 - 1.0);
    static float Env(float t, float dur) => Mathf.Clamp01(t / 0.004f) * Mathf.Clamp01(1f - t / dur);

    static AudioClip Make(string name, float dur, Func<float, float> fn)
    {
        int n = Mathf.CeilToInt(dur * Rate);
        var data = new float[n];
        for (int i = 0; i < n; i++) data[i] = fn(i / (float)Rate);
        var clip = AudioClip.Create(name, n, 1, Rate, false);
        clip.SetData(data, 0);
        return clip;
    }
}

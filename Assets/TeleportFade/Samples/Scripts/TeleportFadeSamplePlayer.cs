using System.Collections.Generic;
using UnityEngine;

public class TeleportFadeSamplePlayer : MonoBehaviour
{
    enum State { None, FadeOut, FadeIn }

    [Header("Targets")]
    public GameObject fadeObject;
    public MeshRenderer[] fadeMeshes;
    public SkinnedMeshRenderer[] fadeSkinnedMeshes;

    [Header("Particles")]
    public ParticleSystem fadeOutParticle;
    public ParticleSystem fadeInParticle;

    [Header("Time")]
    [SerializeField] private bool _ignoreTimeScale = true;

    [Header("Fade Params (editable)")]
    [SerializeField, Min(0.01f)] private float fadeSpeed = 1.0f;
    [SerializeField, Range(0f, 2f)] private float risePower = 0.2f;
    [SerializeField, Range(0f, 10f)] private float twistPower = 3.0f;
    [SerializeField, Range(0f, 2f)] private float spreadPower = 0.6f;

    float fadeTime;
    State state;

    readonly List<Renderer> _renderers = new();
    MaterialPropertyBlock _mpb;

    static readonly int ID_BasePos = Shader.PropertyToID("_ObjectBasePos");
    static readonly int ID_FadeRate = Shader.PropertyToID("_FadeRate");
    static readonly int ID_RisePower = Shader.PropertyToID("_RisePower");
    static readonly int ID_TwistPower = Shader.PropertyToID("_TwistPower");
    static readonly int ID_SpreadPower = Shader.PropertyToID("_SpreadPower");

    void Awake()
    {
        _mpb = new MaterialPropertyBlock();

        // collect renderers once
        if (fadeMeshes != null)
            foreach (var r in fadeMeshes) if (r) _renderers.Add(r);

        if (fadeSkinnedMeshes != null)
            foreach (var r in fadeSkinnedMeshes) if (r) _renderers.Add(r);
    }

    void Update()
    {
        if (state == State.None) return;

        fadeTime += _ignoreTimeScale ? Time.unscaledDeltaTime : Time.deltaTime;

        float fadeDuration = 2.0f / fadeSpeed;
        float fadeStartDelay = 0.9f / fadeSpeed;

        float fadeRate = 0.0f;
        switch (state)
        {
            case State.FadeOut:
                fadeRate = Mathf.Clamp((fadeTime - fadeStartDelay) / fadeDuration, 0f, 1f);
                break;
            case State.FadeIn:
                fadeRate = 1f - Mathf.Clamp((fadeTime - fadeStartDelay) / fadeDuration, 0f, 1f);
                break;
        }

        Vector4 basePos = Vector4.zero;
        if (fadeObject) basePos = fadeObject.transform.position;

        // apply via MPB (no material instancing)
        foreach (var r in _renderers)
        {
            if (!r) continue;

            r.GetPropertyBlock(_mpb);
            _mpb.SetVector(ID_BasePos, basePos);
            _mpb.SetFloat(ID_FadeRate, fadeRate);
            _mpb.SetFloat(ID_RisePower, risePower);
            _mpb.SetFloat(ID_TwistPower, twistPower);
            _mpb.SetFloat(ID_SpreadPower, spreadPower);
            r.SetPropertyBlock(_mpb);
        }
    }

    public void StartFadeOut()
    {
        fadeTime = 0f;
        state = State.FadeOut;
        PlayParticle(fadeOutParticle);
    }

    public void StartFadeIn()
    {
        fadeTime = 0f;
        state = State.FadeIn;
        PlayParticle(fadeInParticle);
    }

    public void StopFade()
    {
        state = State.None;
        fadeTime = 0f;
    }

    void PlayParticle(ParticleSystem ps)
    {
        if (!ps) return;

        var main = ps.main;
        main.simulationSpeed = fadeSpeed;

        foreach (var child in ps.GetComponentsInChildren<ParticleSystem>())
        {
            var m = child.main;
            m.simulationSpeed = fadeSpeed;
        }

        ps.Play(true);
    }

    // duration helper (same logic as yours)
    public float GetTotalFadeSeconds()
    {
        float fadeDuration = 2.0f / fadeSpeed;
        float fadeStartDelay = 0.9f / fadeSpeed;
        return fadeStartDelay + fadeDuration;
    }

    // runtime setters (optional)
    public void SetFadeParams(float speed, float rise, float twist, float spread)
    {
        fadeSpeed = Mathf.Max(0.01f, speed);
        risePower = rise;
        twistPower = twist;
        spreadPower = spread;
    }

    public float FadeSpeed => fadeSpeed;
}

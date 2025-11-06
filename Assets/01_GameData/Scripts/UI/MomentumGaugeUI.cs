using DG.Tweening;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Cysharp.Threading.Tasks;
using System;
using System.Threading;

public class MomentumGaugeUI : MonoBehaviour
{
    [SerializeField] private Slider _momentumSlider;
    [SerializeField] private float _tweenDuration = 0.3f;
    [SerializeField] private Ease _ease = Ease.OutQuad;
    [SerializeField] private bool _animateWhilePaused = true;

    private Tween _valueTween;
    private float _lastValue = -999f;
    private bool _isBound = false;
    private CancellationTokenSource _cts;

    private void Awake()
    {
        if (_momentumSlider == null) _momentumSlider = GetComponent<Slider>();
    }

    private void OnEnable()
    {
        _cts = new CancellationTokenSource();
        SceneManager.sceneLoaded += OnSceneLoaded;
        BindWhenReadyAsync(_cts.Token).Forget();
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        _cts?.Cancel(); _cts?.Dispose(); _cts = null;
        Unbind();
        _valueTween?.Kill(); _valueTween = null;
    }

    private void OnDestroy()
    {
        Unbind();
        _valueTween?.Kill(); _valueTween = null;
    }

    private void OnSceneLoaded(Scene s, LoadSceneMode m)
    {
        // Scene changes can recreate managers; rebind.
        _cts?.Cancel();
        _cts = new CancellationTokenSource();
        BindWhenReadyAsync(_cts.Token).Forget();
    }

    private async UniTaskVoid BindWhenReadyAsync(CancellationToken token)
    {
        // Wait until both UI and manager exist
        await UniTask.WaitUntil(() => MomentumManager.Instance != null && _momentumSlider != null, cancellationToken: token);

        // Small extra delay to avoid race during scene activation
        await UniTask.Yield(PlayerLoopTiming.Update, token);

        if (token.IsCancellationRequested) return;

        var mm = MomentumManager.Instance;
        if (mm == null) return;

        Unbind(); // clean previous
        _momentumSlider.wholeNumbers = false;
        _momentumSlider.minValue = 0f;
        _momentumSlider.maxValue = mm.MaxMomentum;

        mm.onMomentumChanged.AddListener(OnMomentumChanged);
        _isBound = true;

        // Force initial update
        _lastValue = -999f;
        OnMomentumChanged(mm.CurrentMomentum);
    }

    private void Unbind()
    {
        if (_isBound && MomentumManager.Instance != null)
            MomentumManager.Instance.onMomentumChanged.RemoveListener(OnMomentumChanged);
        _isBound = false;
    }

    private void OnMomentumChanged(float m)
    {
        if (_momentumSlider == null) return;

        float target = Mathf.Clamp(m, 0f, _momentumSlider.maxValue);

        // Avoid spam
        if (Mathf.Approximately(_lastValue, target)) return;
        _lastValue = target;

        _valueTween?.Kill();
        _valueTween = _momentumSlider
            .DOValue(target, _tweenDuration)
            .SetEase(_ease)
            .SetUpdate(_animateWhilePaused)   // works while paused
            .SetLink(_momentumSlider.gameObject, LinkBehaviour.KillOnDestroy);
    }
}

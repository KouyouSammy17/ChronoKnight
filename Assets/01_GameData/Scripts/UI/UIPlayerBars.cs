using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using UnityEngine.SceneManagement;
using Cysharp.Threading.Tasks;
using System.Threading;

public class UIPlayerBars : MonoBehaviour
{
    [SerializeField] private Slider _hpSlider;
    [SerializeField] private Slider _staminaSlider;
    [SerializeField] private float _tweenDuration = 0.3f;
    [SerializeField] private Ease _ease = Ease.OutQuad;
    [SerializeField] private bool _animateWhilePaused = true; // run even when Time.timeScale==0

    private PlayerStats _stats;
    private CancellationTokenSource _cts;

    private Tween _hpTween;
    private Tween _staminaTween;

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
        _cts = new CancellationTokenSource();
        BindWhenReadyAsync(_cts.Token).Forget();
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        _cts?.Cancel(); _cts?.Dispose(); _cts = null;
        Unsubscribe();
        KillTweens();
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // Scene changed: rebind because player/stats may be a new instance
        _cts?.Cancel();
        _cts = new CancellationTokenSource();
        BindWhenReadyAsync(_cts.Token).Forget();
    }

    private void KillTweens()
    {
        _hpTween?.Kill(); _hpTween = null;
        _staminaTween?.Kill(); _staminaTween = null;
    }

    private void Unsubscribe()
    {
        if (_stats != null)
        {
            _stats.onHealthChanged.RemoveListener(UpdateHP);
            _stats.onStaminaChanged.RemoveListener(UpdateStamina);
            _stats = null;
        }
    }

    private async UniTaskVoid BindWhenReadyAsync(CancellationToken token)
    {
        Unsubscribe();
        KillTweens();

        // Wait until PlayerStats exists (spawned & active)
        PlayerStats stats = null;

        // First try GameManager -> Player
        for (int i = 0; i < 60 && !token.IsCancellationRequested; i++) // ~1 sec at 60fps
        {
            var gm = GameManager.Instance;
            var player = gm != null ? gm.GetPlayer() : null;
            if (player != null)
            {
                stats = player.GetComponent<PlayerStats>();
                if (stats != null) break;
            }
            await UniTask.NextFrame(token);
        }

        // Fallback: scene scan (handles cases without GameManager or different order)
#if UNITY_6000_0_OR_NEWER
        stats ??= Object.FindFirstObjectByType<PlayerStats>();
#else
        stats ??= Object.FindObjectOfType<PlayerStats>();
#endif
        if (token.IsCancellationRequested || stats == null) return;

        _stats = stats;

        // Match slider ranges (null-checks just in case)
        if (_hpSlider) _hpSlider.maxValue = _stats.MaxHP;
        if (_staminaSlider) _staminaSlider.maxValue = _stats.MaxStamina;

        // Snap to current values before subscribing
        UpdateHP(_stats.CurrentHP);
        UpdateStamina(_stats.CurrentStamina);

        // Subscribe to changes
        _stats.onHealthChanged.AddListener(UpdateHP);
        _stats.onStaminaChanged.AddListener(UpdateStamina);
    }

    private void UpdateHP(int hp)
    {
        if (!_hpSlider) return;
        _hpTween?.Kill();
        _hpTween = _hpSlider
            .DOValue(hp, _tweenDuration)
            .SetEase(_ease)
            .SetUpdate(_animateWhilePaused) // animate during pause
            .SetLink(_hpSlider.gameObject, LinkBehaviour.KillOnDestroy);
    }

    private void UpdateStamina(int sta)
    {
        if (!_staminaSlider) return;
        _staminaTween?.Kill();
        _staminaTween = _staminaSlider
            .DOValue(sta, _tweenDuration)
            .SetEase(_ease)
            .SetUpdate(_animateWhilePaused) // animate during pause
            .SetLink(_staminaSlider.gameObject, LinkBehaviour.KillOnDestroy);
    }
}

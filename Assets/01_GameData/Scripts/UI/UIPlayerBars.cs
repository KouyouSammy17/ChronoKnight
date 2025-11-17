using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using UnityEngine.SceneManagement;
using MoreMountains.Tools;   // ← FEEL ProgressBar

public class UIPlayerBars : MonoBehaviour
{
    [Header("HP (FEEL MMProgressBar)")]
    [SerializeField] private MMProgressBar _hpBar;      // real HP bar

    [Header("Intro HP Slider (only for animation)")]
    [SerializeField] private Slider _hpIntroSlider;     // overlay HP slider just for intro
    [SerializeField] private float _hpIntroDuration = 0.4f;

    [Header("Stamina (Simple Slider)")]
    [SerializeField] private Slider _staminaSlider;
    [SerializeField] private float _tweenDuration = 0.3f;

    [Header("Intro Fill")]
    [SerializeField, Tooltip("If true, stamina starts at 0 and waits for PlayIntroAnimation()")]
    private bool _useIntroFill = true;

    [SerializeField, Tooltip("How long the stamina intro tween takes")]
    private float _introFillDuration = 0.7f;

    private PlayerStats _stats;
    private bool _introPlayed = false;

    private Tween _hpIntroTween;
    private Tween _staminaIntroTween;

    // ─────────────────────────────────────────────────────────────────────
    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
        BindToStats();
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        Unsubscribe();
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // When scene is reloaded / changed, player instance may change → rebind
        BindToStats();
    }

    // ─────────────────────────────────────────────────────────────────────
    private void BindToStats()
    {
        Unsubscribe();

        // find current PlayerStats in the scene
        _stats = Object.FindFirstObjectByType<PlayerStats>();
        if (_stats == null) return;

        // initialize UI to current values (or 0 if using intro)
        InitBars();

        // subscribe to events
        _stats.onHealthChanged.AddListener(UpdateHP);
        _stats.onStaminaChanged.AddListener(UpdateStamina);
    }

    private void Unsubscribe()
    {
        if (_stats == null) return;

        _stats.onHealthChanged.RemoveListener(UpdateHP);
        _stats.onStaminaChanged.RemoveListener(UpdateStamina);
        _stats = null;
    }

    private void InitBars()
    {
        if (_stats == null) return;

        // ── HP (real MMProgressBar) ───────────────────────────────────
        if (_hpBar != null)
        {
            // Always show real HP bar at current value
            _hpBar.SetBar(_stats.CurrentHP, 0f, _stats.MaxHP);
        }

        // ── Intro HP Slider (overlay) ────────────────────────────────
        if (_hpIntroSlider != null)
        {
            _hpIntroSlider.maxValue = _stats.MaxHP;

            if (_useIntroFill && !_introPlayed)
            {
                // start empty, will animate 0 → full in PlayIntroAnimation()
                _hpIntroSlider.value = 0f;
                _hpIntroSlider.gameObject.SetActive(true);   // visible during intro
            }
            else
            {
                // after intro played, keep it hidden
                _hpIntroSlider.gameObject.SetActive(false);
            }
        }

        // ── Stamina ──────────────────────────────
        if (_staminaSlider != null)
        {
            _staminaSlider.maxValue = _stats.MaxStamina;

            if (_useIntroFill && !_introPlayed)
            {
                _staminaSlider.value = 0f;
            }
            else
            {
                _staminaSlider.value = _stats.CurrentStamina;
            }
        }
    }

    // ─────────────────────────────────────────────────────────────────────
    // Called from PlayerStats events (gameplay)
    private void UpdateHP(int hp)
    {
        if (_stats == null || _hpBar == null) return;

        // FEEL handles delayed bar / bump / easing.
        _hpBar.UpdateBar(hp, 0f, _stats.MaxHP);
    }

    private void UpdateStamina(int sta)
    {
        if (_staminaSlider == null) return;

        // Simple DOTween for stamina, no delayed bar
        _staminaSlider.DOValue(sta, _tweenDuration).SetEase(Ease.OutQuad);
    }

    // ─────────────────────────────────────────────────────────────────────
    // Call this when the camera work / intro is finished.
    // Example: from your camera script, timeline signal, or GameManager.
    public void PlayIntroAnimation()
    {
        if (_stats == null) return;

        _introPlayed = true;

        // ── HP intro overlay: 0 → full HP, then disable ──────────────
        if (_hpIntroSlider != null)
        {
            _hpIntroSlider.gameObject.SetActive(true);
            _hpIntroSlider.maxValue = _stats.MaxHP;
            _hpIntroSlider.value = 0f;

            _hpIntroTween?.Kill();
            _hpIntroTween = _hpIntroSlider
                .DOValue(_stats.CurrentHP, _hpIntroDuration)
                .SetEase(Ease.OutQuad)
                .OnComplete(() =>
                {
                    // hide the intro slider after it finishes
                    _hpIntroSlider.gameObject.SetActive(false);
                });
        }

        // ── Stamina intro: 0 → current ───────────────────────────────
        if (_staminaSlider != null)
        {
            _staminaSlider.maxValue = _stats.MaxStamina;
            _staminaSlider.DOKill();
            _staminaSlider.value = 0f;

            _staminaIntroTween?.Kill();
            _staminaIntroTween = _staminaSlider
                .DOValue(_stats.CurrentStamina, _introFillDuration)
                .SetEase(Ease.OutQuad);
        }
    }
}

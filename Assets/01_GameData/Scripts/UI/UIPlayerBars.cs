// プレイヤーのHPバーとスタミナバーの表示・アニメーションを管理するスクリプト
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
    [SerializeField] private float _hpIntroDuration = 0.4f; // HPイントロアニメーションの時間

    [Header("Stamina (Simple Slider)")]
    [SerializeField] private Slider _staminaSlider;         // スタミナを表示するスライダー
    [SerializeField] private float _tweenDuration = 0.3f;   // スタミナ変動時のTween時間

    [Header("Intro Fill")]
    [SerializeField, Tooltip("If true, stamina starts at 0 and waits for PlayIntroAnimation()")]
    private bool _useIntroFill = true; // イントロアニメーションを使用するかどうか

    [SerializeField, Tooltip("How long the stamina intro tween takes")]
    private float _introFillDuration = 0.7f; // スタミナイントロTweenの時間

    private PlayerStats _stats;        // 現在バインドされているPlayerStats
    private bool _introPlayed = false; // イントロアニメーションが再生済みかどうか

    private Tween _hpIntroTween;      // HPイントロ用Tween
    private Tween _staminaIntroTween; // スタミナイントロ用Tween

    // ─────────────────────────────────────────────────────────────────────
    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded; // シーンロード時に再バインド
        BindToStats();
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        Unsubscribe(); // イベントリスナーを解除してリークを防ぐ
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // When scene is reloaded / changed, player instance may change → rebind
        BindToStats();
    }

    // ─────────────────────────────────────────────────────────────────────
    private void BindToStats()
    {
        Unsubscribe(); // 既存のバインドを解除してから再接続

        // find current PlayerStats in the scene
        _stats = Object.FindFirstObjectByType<PlayerStats>();
        if (_stats == null) return; // PlayerStatsが見つからなければ何もしない

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
            _hpBar.SetBar(_stats.CurrentHP, 0f, _stats.MaxHP); // HPバーを現在値で初期化
        }

        // ── Intro HP Slider (overlay) ────────────────────────────────
        if (_hpIntroSlider != null)
        {
            _hpIntroSlider.maxValue = _stats.MaxHP; // スライダーの最大値をHPに合わせる

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
            _staminaSlider.maxValue = _stats.MaxStamina; // スタミナスライダーの最大値を設定

            if (_useIntroFill && !_introPlayed)
            {
                _staminaSlider.value = 0f; // イントロ前は0から始める
            }
            else
            {
                _staminaSlider.value = _stats.CurrentStamina; // イントロ済みなら現在値を表示
            }
        }
    }

    // ─────────────────────────────────────────────────────────────────────
    // Called from PlayerStats events (gameplay)
    private void UpdateHP(int hp)
    {
        if (_stats == null || _hpBar == null) return;

        // FEEL handles delayed bar / bump / easing.
        _hpBar.UpdateBar(hp, 0f, _stats.MaxHP); // FEELのMMProgressBarにHP変化を通知
    }

    private void UpdateStamina(int sta)
    {
        if (_staminaSlider == null) return;

        // Simple DOTween for stamina, no delayed bar
        _staminaSlider.DOValue(sta, _tweenDuration).SetEase(Ease.OutQuad); // スタミナ変化をDOTweenでアニメーション
    }

    // ─────────────────────────────────────────────────────────────────────
    // Call this when the camera work / intro is finished.
    // Example: from your camera script, timeline signal, or GameManager.
    public void PlayIntroAnimation()
    {
        if (_stats == null) return;

        _introPlayed = true; // 以降のInitBarsでイントロスライダーを非表示にするためのフラグ

        // ── HP intro overlay: 0 → full HP, then disable ──────────────
        if (_hpIntroSlider != null)
        {
            _hpIntroSlider.gameObject.SetActive(true);
            _hpIntroSlider.maxValue = _stats.MaxHP;
            _hpIntroSlider.value = 0f; // 0から最大HPへアニメーションするため初期値を0にする

            _hpIntroTween?.Kill();
            _hpIntroTween = _hpIntroSlider
                .DOValue(_stats.CurrentHP, _hpIntroDuration)
                .SetEase(Ease.OutQuad)
                .OnComplete(() =>
                {
                    // hide the intro slider after it finishes
                    _hpIntroSlider.gameObject.SetActive(false); // アニメーション完了後にオーバーレイスライダーを非表示
                });
        }

        // ── Stamina intro: 0 → current ───────────────────────────────
        if (_staminaSlider != null)
        {
            _staminaSlider.maxValue = _stats.MaxStamina;
            _staminaSlider.DOKill(); // 既存のスタミナTweenを停止
            _staminaSlider.value = 0f;

            _staminaIntroTween?.Kill();
            _staminaIntroTween = _staminaSlider
                .DOValue(_stats.CurrentStamina, _introFillDuration)
                .SetEase(Ease.OutQuad); // 0から現在スタミナへ滑らかに表示
        }
    }
}

// プレイヤーのHPバーとスタミナバーの表示・アニメーションを管理するスクリプト
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using UnityEngine.SceneManagement;
using MoreMountains.Tools;   // ← FEEL ProgressBar

public class UIPlayerBars : MonoBehaviour
{
    [Header("HP (FEEL MMProgressBar)")]
    [SerializeField] private MMProgressBar _hpBar;      // 実際のHPバー

    [Header("Intro HP Slider (only for animation)")]
    [SerializeField] private Slider _hpIntroSlider;     // イントロ専用のオーバーレイHPスライダー
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
        // シーンがリロード・変更された場合、プレイヤーインスタンスが変わる可能性があるため再バインドする
        BindToStats();
    }

    // ─────────────────────────────────────────────────────────────────────
    private void BindToStats()
    {
        Unsubscribe(); // 既存のバインドを解除してから再接続

        // シーン内の現在のPlayerStatsを検索する
        _stats = Object.FindFirstObjectByType<PlayerStats>();
        if (_stats == null) return; // PlayerStatsが見つからなければ何もしない

        // UIを現在値（またはイントロ使用時は0）で初期化する
        InitBars();

        // イベントを購読する
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

        // ── HP（実際のMMProgressBar）─────────────────────────────────
        if (_hpBar != null)
        {
            // 実際のHPバーを常に現在値で表示する
            _hpBar.SetBar(_stats.CurrentHP, 0f, _stats.MaxHP); // HPバーを現在値で初期化
        }

        // ── イントロHPスライダー（オーバーレイ）────────────────────────
        if (_hpIntroSlider != null)
        {
            _hpIntroSlider.maxValue = _stats.MaxHP; // スライダーの最大値をHPに合わせる

            if (_useIntroFill && !_introPlayed)
            {
                // 空から開始し、PlayIntroAnimation()で0→満タンへアニメーションする
                _hpIntroSlider.value = 0f;
                _hpIntroSlider.gameObject.SetActive(true);   // イントロ中は表示
            }
            else
            {
                // イントロ再生後は非表示のままにする
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
    // PlayerStatsのイベントから呼び出される（ゲームプレイ中）
    private void UpdateHP(int hp)
    {
        if (_stats == null || _hpBar == null) return;

        // FEELがバーの遅延・バンプ・イージングを管理する
        _hpBar.UpdateBar(hp, 0f, _stats.MaxHP); // FEELのMMProgressBarにHP変化を通知
    }

    private void UpdateStamina(int sta)
    {
        if (_staminaSlider == null) return;

        // スタミナはシンプルなDOTween（遅延バーなし）
        _staminaSlider.DOValue(sta, _tweenDuration).SetEase(Ease.OutQuad); // スタミナ変化をDOTweenでアニメーション
    }

    // ─────────────────────────────────────────────────────────────────────
    // カメラ演出・イントロが終わったら呼び出す。
    // 例：カメラスクリプト、タイムラインシグナル、GameManagerなどから呼び出す。
    public void PlayIntroAnimation()
    {
        if (_stats == null) return;

        _introPlayed = true; // 以降のInitBarsでイントロスライダーを非表示にするためのフラグ

        // ── HPイントロオーバーレイ：0 → 最大HP → 非表示 ──────────────
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
                    // アニメーション完了後にイントロスライダーを非表示にする
                    _hpIntroSlider.gameObject.SetActive(false); // アニメーション完了後にオーバーレイスライダーを非表示
                });
        }

        // ── スタミナイントロ：0 → 現在値 ───────────────────────────────
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

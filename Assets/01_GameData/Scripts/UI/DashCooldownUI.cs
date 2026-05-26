// ダッシュのクールダウン状態をUIで表示・アニメーションするスクリプト
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

public class DashCooldownUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerMotor _motor;          // NEW: assign in inspector (recommended)
    [SerializeField] private Image _cooldownFill;         // radial image on top of icon
    [SerializeField] private RectTransform _icon;         // dash icon

    [Header("Scales")]
    [SerializeField] private float _readyScale = 1.5f;       // 準備完了時のアイコンスケール
    [SerializeField] private float _cooldownScale = 1f;      // クールダウン中のアイコンスケール
    [SerializeField] private float _punchScale = 0.3f;       // ダッシュ時のパンチエフェクト強さ
    [SerializeField] private float _punchDuration = 0.2f;    // パンチエフェクトの時間

    [Header("Cooldown UI Timing")]
    [SerializeField] private float _delayBeforeFill = 0.4f; // delay measured in SAME units as cooldown seconds

    [Header("Settings")]
    [SerializeField] private bool _useUnscaledTime = true;  // only affects tweens (punch/scale), not cooldown timer

    private Tween _iconTween; // アイコンアニメーション用Tween

    private bool _cooldownActive; // クールダウン中かどうか
    private bool _showFill;       // フィル表示フェーズかどうか
    private bool _readyFired;     // 準備完了通知を送信済みかどうか

    private float _cooldownDuration;   // full cooldown seconds at dash start
    private float _uiDuration;         // cooldownDuration - delayBeforeFill

    private void Awake()
    {
        if (_cooldownFill) _cooldownFill.fillAmount = 0f; // 0 = ready
        if (_icon) _icon.localScale = Vector3.one * _readyScale; // 準備完了スケールで初期化
    }

    private void OnEnable()
    {
        TryResolveMotor(); // PlayerMotorを取得
        Subscribe(true);   // ダッシュイベントを購読

        // If we enabled mid-cooldown (scene load), sync visuals
        SyncFromMotor(); // シーンロード中にクールダウンが発生していた場合に同期
    }

    private void OnDisable()
    {
        Subscribe(false); // イベントリスナーを解除
        KillTweens();
    }

    private void Update()
    {
        if (_motor == null)
        {
            TryResolveMotor(); // Updateでも毎回再探索を試みる
            if (_motor == null) return;
        }

        if (!_cooldownActive)
        {
            // keep stable ready visuals
            if (_cooldownFill) _cooldownFill.fillAmount = 0f; // 準備完了状態を維持
            return;
        }

        float remaining = _motor.DashCooldownRemaining; // 残りクールダウン時間を取得

        // Delay phase: keep fill at 1 until remaining <= uiDuration
        if (!_showFill)
        {
            float threshold = Mathf.Max(0f, _cooldownDuration - _delayBeforeFill);
            if (remaining <= threshold)
            {
                BeginFillVisuals(); // 遅延フェーズが終わったらフィル表示を開始
                _showFill = true;
            }
        }

        if (_cooldownFill)
        {
            if (!_showFill)
            {
                _cooldownFill.fillAmount = 1f; // 遅延フェーズ中はフィル満タンを維持
            }
            else
            {
                float uiRemaining = Mathf.Clamp(remaining, 0f, _uiDuration);
                _cooldownFill.fillAmount = (_uiDuration <= 0.01f) ? 0f : (uiRemaining / _uiDuration); // 残り時間に応じてフィルを減らす
            }
        }

        // Ready
        if (remaining <= 0.001f && !_readyFired)
        {
            _readyFired = true;
            _cooldownActive = false;
            _showFill = false;

            if (_cooldownFill) _cooldownFill.fillAmount = 0f; // 準備完了状態にリセット
            OnCooldownComplete(); // 準備完了アニメーションを再生
        }
    }

    // ─────────────────────────────────────────────────────────────

    private void TryResolveMotor()
    {
        if (_motor != null) return;

        // Best: assign in Inspector. Fallback: find one in scene.
#if UNITY_2023_1_OR_NEWER
        _motor = Object.FindFirstObjectByType<PlayerMotor>();
#else
        _motor = Object.FindObjectOfType<PlayerMotor>();
#endif
    }

    private void Subscribe(bool on)
    {
        if (_motor == null) return;

        if (on) _motor.DashStarted += HandleDashStarted;   // ダッシュ開始イベントを購読
        else _motor.DashStarted -= HandleDashStarted;       // ダッシュ開始イベントを解除
    }

    private void SyncFromMotor()
    {
        if (_motor == null) return;

        float remaining = _motor.DashCooldownRemaining;
        if (remaining > 0.001f)
        {
            _cooldownActive = true;  // クールダウン中として状態を復元
            _readyFired = false;

            _cooldownDuration = Mathf.Max(0.01f, _motor.DashCooldownDuration);
            _uiDuration = Mathf.Max(0.01f, _cooldownDuration - _delayBeforeFill);

            // show fill immediately if we are past the delay window already
            float threshold = Mathf.Max(0f, _cooldownDuration - _delayBeforeFill);
            _showFill = remaining <= threshold; // 遅延ウィンドウを過ぎているかチェック

            if (_icon) _icon.localScale = Vector3.one * (_showFill ? _cooldownScale : _readyScale);
            if (_cooldownFill) _cooldownFill.fillAmount = _showFill ? Mathf.Clamp01(remaining / _uiDuration) : 1f;
        }
        else
        {
            _cooldownActive = false; // クールダウンなし→準備完了状態
            _showFill = false;
            _readyFired = false;

            if (_cooldownFill) _cooldownFill.fillAmount = 0f;
            if (_icon) _icon.localScale = Vector3.one * _readyScale;
        }
    }

    private void KillTweens()
    {
        _iconTween?.Kill();
        _iconTween = null;
    }

    private void HandleDashStarted(float cooldownSeconds)
    {
        KillTweens(); // 前のTweenを停止してから新しいクールダウンを開始

        _cooldownActive = true;
        _showFill = false;       // 遅延フェーズから開始
        _readyFired = false;

        _cooldownDuration = Mathf.Max(0.01f, cooldownSeconds);
        _uiDuration = Mathf.Max(0.01f, _cooldownDuration - _delayBeforeFill); // UI表示時間から遅延を引く

        // Start full
        if (_cooldownFill) _cooldownFill.fillAmount = 1f; // 即座にフィルを満タンにする

        // Start from ready size
        if (_icon) _icon.localScale = Vector3.one * _readyScale;

        // Punch on dash
        if (_icon)
        {
            _iconTween = _icon
                .DOPunchScale(Vector3.one * _punchScale, _punchDuration, 1, 0.5f) // ダッシュ時にアイコンをパンチ
                .SetUpdate(_useUnscaledTime)
                .SetLink(gameObject);
        }
    }

    private void BeginFillVisuals()
    {
        // Scale down during cooldown
        if (_icon)
        {
            _iconTween?.Kill();
            _iconTween = _icon
                .DOScale(_cooldownScale, 0.25f)
                .SetEase(Ease.OutQuad) // クールダウン中の小さいスケールへアニメーション
                .SetUpdate(_useUnscaledTime)
                .SetLink(gameObject);
        }
    }

    private void OnCooldownComplete()
    {
        // Grow big when ready
        if (_icon)
        {
            _iconTween?.Kill();
            _iconTween = _icon
                .DOScale(_readyScale, 0.25f)
                .SetEase(Ease.OutBack) // 準備完了時のポップスケールアニメーション
                .SetUpdate(_useUnscaledTime)
                .SetLink(gameObject);
        }
    }

    // Optional: let other scripts bind player at runtime (good for DontDestroy UI)
    public void Bind(PlayerMotor motor)
    {
        if (_motor == motor) return; // 同じモーターなら再バインド不要

        Subscribe(false); // 旧モーターのイベントを解除
        _motor = motor;
        Subscribe(true);  // 新モーターのイベントを購読

        SyncFromMotor(); // 新しいモーターの状態に同期
    }
}

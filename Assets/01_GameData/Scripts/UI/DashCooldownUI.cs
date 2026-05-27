// ダッシュのクールダウン状態をUIで表示・アニメーションするスクリプト
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

public class DashCooldownUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerMotor _motor;          // インスペクターで割り当て推奨
    [SerializeField] private Image _cooldownFill;         // アイコン上部の放射状フィル画像
    [SerializeField] private RectTransform _icon;         // ダッシュアイコン

    [Header("Scales")]
    [SerializeField] private float _readyScale = 1.5f;       // 準備完了時のアイコンスケール
    [SerializeField] private float _cooldownScale = 1f;      // クールダウン中のアイコンスケール
    [SerializeField] private float _punchScale = 0.3f;       // ダッシュ時のパンチエフェクト強さ
    [SerializeField] private float _punchDuration = 0.2f;    // パンチエフェクトの時間

    [Header("Cooldown UI Timing")]
    [SerializeField] private float _delayBeforeFill = 0.4f; // クールダウン秒数と同じ単位で計測する遅延

    [Header("Settings")]
    [SerializeField] private bool _useUnscaledTime = true;  // TweenのみへのTimeScale無視設定（クールダウンタイマーには影響しない）

    private Tween _iconTween; // アイコンアニメーション用Tween

    private bool _cooldownActive; // クールダウン中かどうか
    private bool _showFill;       // フィル表示フェーズかどうか
    private bool _readyFired;     // 準備完了通知を送信済みかどうか

    private float _cooldownDuration;   // ダッシュ開始時のクールダウン秒数（全体）
    private float _uiDuration;         // クールダウン時間から遅延を引いたUI表示時間

    private void Awake()
    {
        if (_cooldownFill) _cooldownFill.fillAmount = 0f; // 0 = ready
        if (_icon) _icon.localScale = Vector3.one * _readyScale; // 準備完了スケールで初期化
    }

    private void OnEnable()
    {
        TryResolveMotor(); // PlayerMotorを取得
        Subscribe(true);   // ダッシュイベントを購読

        // シーンロード中にクールダウンが始まっていた場合にビジュアルを同期する
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
            // 準備完了のビジュアルを維持する
            if (_cooldownFill) _cooldownFill.fillAmount = 0f; // 準備完了状態を維持
            return;
        }

        float remaining = _motor.DashCooldownRemaining; // 残りクールダウン時間を取得

        // 遅延フェーズ：remaining <= uiDurationになるまでフィルを1に維持する
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

        // 準備完了判定
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

        // 推奨：インスペクターで割り当てる。フォールバック：シーン内から検索する。
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

            // 遅延ウィンドウを既に過ぎている場合は即座にフィルを表示する
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

        // フィルを満タンから開始する
        if (_cooldownFill) _cooldownFill.fillAmount = 1f; // 即座にフィルを満タンにする

        // 準備完了サイズから開始する
        if (_icon) _icon.localScale = Vector3.one * _readyScale;

        // ダッシュ時にパンチエフェクトを再生する
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
        // クールダウン中はアイコンを縮小する
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
        // 準備完了時にアイコンを大きくする
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

    // 任意：実行時に他スクリプトからプレイヤーをバインドできる（DontDestroyなUIに便利）
    public void Bind(PlayerMotor motor)
    {
        if (_motor == motor) return; // 同じモーターなら再バインド不要

        Subscribe(false); // 旧モーターのイベントを解除
        _motor = motor;
        Subscribe(true);  // 新モーターのイベントを購読

        SyncFromMotor(); // 新しいモーターの状態に同期
    }
}

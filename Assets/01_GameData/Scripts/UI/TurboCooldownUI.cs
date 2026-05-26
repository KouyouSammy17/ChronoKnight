// ターボのクールダウン状態をUIで表示・アニメーションするスクリプト
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

public class TurboCooldownUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Image _cooldownFill;         // radial fill image on top
    [SerializeField] private RectTransform _icon;         // ONLY rotates
    [SerializeField] private RectTransform _background;   // frame / ring that scales & punches

    [Header("Cooldown Settings")]
    [SerializeField] private float _cooldownDuration = 6f;           // クールダウンの秒数
    [SerializeField] private float _rotationSpeed = 180f;   // degrees per second
    [SerializeField] private bool _useUnscaledTime = true;  // UI should ignore timeScale

    [Header("Scale Settings")]
    [SerializeField] private float _lockedScale = 1.0f;     // momentum < 25%
    [SerializeField] private float _readyScale = 1.25f;     // momentum >= 25% and not cooling
    [SerializeField] private float _cooldownScale = 0.8f;   // during cooldown

    [Header("Punch Settings")]
    [SerializeField] private float _punchScale = 0.25f;   // パンチエフェクトの強さ
    [SerializeField] private float _punchDuration = 0.2f; // パンチエフェクトの時間

    private Tween _cooldownTween;   // フィルアニメーション用Tween
    private Tween _rotateTween;     // アイコン回転アニメーション用Tween
    private Tween _bgScaleTween;    // 背景スケールアニメーション用Tween
    private Tween _bgPunchTween;    // 背景パンチエフェクト用Tween
    private Tween _lockDelayTween;  // ロック遅延用Tween

    // "Unlocked" = have ≥25% momentum *when idle*
    private bool _unlocked = true;        // 現在のアンロック状態（モメンタム+チュートリアル両方考慮）
    private bool _isCoolingDown = false;  // クールダウン中かどうか

    // Tutorial gate: Turbo is not usable until tutorial unlocks it
    private bool _tutorialUnlocked = true; // チュートリアルによるアンロック状態

    // Keep momentum-based unlocked separate (>=25%)
    private bool _momentumUnlocked = true; // モメンタム25%以上かどうか

    // Effective unlock = tutorial unlocked AND momentum unlocked
    private bool EffectiveUnlocked => _tutorialUnlocked && _momentumUnlocked; // 実効アンロック（両条件の論理積）


    // Pending lock flag (used to avoid snapping when Turbo starts right after cost)
    private bool _lockPending = false; // ロック遅延処理が保留中かどうか

    // ───────────────────────────────────────────────────────────────────
    private void Awake()
    {
        if (_cooldownFill != null)
            _cooldownFill.fillAmount = 0f; // default ready

        if (_background != null)
            _background.localScale = Vector3.one * _readyScale; // 準備完了スケールで初期化
    }

    private void OnEnable()
    {
        // Turbo events
        if (TurboModeManager.Instance != null)
        {
            TurboModeManager.Instance.onTurboStart.AddListener(OnTurboStart); // ターボ開始イベントを購読
            TurboModeManager.Instance.onTurboEnd.AddListener(OnTurboEnd);     // ターボ終了イベントを購読
        }

        // Momentum events
        if (MomentumManager.Instance != null)
        {
            MomentumManager.Instance.onMomentumChanged.AddListener(HandleMomentumChanged); // モメンタム変化イベントを購読
            InitializeFromMomentum(); // 現在のモメンタム状態でUIを初期化
        }
        else
        {
            _unlocked = true;
            _isCoolingDown = false;
            if (_cooldownFill != null) _cooldownFill.fillAmount = 0f;
            if (_background != null) _background.localScale = Vector3.one * _readyScale;
        }
    }

    private void OnDisable()
    {
        if (TurboModeManager.Instance != null)
        {
            TurboModeManager.Instance.onTurboStart.RemoveListener(OnTurboStart); // イベントリスナーを解除
            TurboModeManager.Instance.onTurboEnd.RemoveListener(OnTurboEnd);
        }

        if (MomentumManager.Instance != null)
        {
            MomentumManager.Instance.onMomentumChanged.RemoveListener(HandleMomentumChanged);
        }

        KillTweens(); // 全Tweenを停止してリソースを解放
    }

    private void KillTweens()
    {
        _cooldownTween?.Kill(); _cooldownTween = null;
        _rotateTween?.Kill(); _rotateTween = null;
        _bgScaleTween?.Kill(); _bgScaleTween = null;
        _bgPunchTween?.Kill(); _bgPunchTween = null;
        _lockDelayTween?.Kill(); _lockDelayTween = null;
        _lockPending = false; // 保留中のロックフラグをクリア
    }

    private void ClearPendingLock()
    {
        _lockPending = false; // 保留ロックをキャンセル
        if (_lockDelayTween != null)
        {
            _lockDelayTween.Kill();
            _lockDelayTween = null;
        }
    }

    // ───────────────────────────────────────────────────────────────────
    private void InitializeFromMomentum()
    {
        var mm = MomentumManager.Instance;
        if (mm == null)
            return;

        float percent = (mm.MaxMomentum <= 0f)
            ? 0f
            : (mm.CurrentMomentum / mm.MaxMomentum) * 100f; // モメンタムのパーセンテージを計算

        _momentumUnlocked = percent >= 25f;              // 25%以上でモメンタムアンロック
        _unlocked = EffectiveUnlocked; // keep existing var for your cooldown logic
        _isCoolingDown = false;
        KillTweens();

        if (_icon != null)
            _icon.localRotation = Quaternion.identity; // アイコンの回転をリセット

        bool eff = EffectiveUnlocked;

        if (_cooldownFill != null)
            _cooldownFill.fillAmount = eff ? 0f : 1f; // アンロック時は0（準備完了）、ロック時は1（使用不可）

        if (_background != null)
            _background.localScale = Vector3.one * (eff ? _readyScale : _lockedScale); // 状態に応じたスケールを適用
    }


    private void HandleMomentumChanged(float currentMomentum)
    {
        var mm = MomentumManager.Instance;
        if (mm == null) return;

        float percent = (mm.MaxMomentum <= 0f)
            ? 0f
            : (currentMomentum / mm.MaxMomentum) * 100f; // 現在モメンタムのパーセンテージ

        bool newMomentumUnlocked = percent >= 25f; // 25%以上かどうかを再評価
        _momentumUnlocked = newMomentumUnlocked;

        bool newEffective = EffectiveUnlocked;
        if (newEffective == _unlocked) return; // 実効アンロック状態に変化がなければスキップ

        // Always keep the logical flag updated (this flag now means EFFECTIVE unlock)
        _unlocked = newEffective;

        // IMPORTANT:
        // If we are in the middle of a cooldown animation,
        // DO NOT change visuals here. Just remember the new _unlocked value.
        // OnCooldownComplete() will look at _unlocked and decide:
        //   - !_unlocked → go locked
        //   -  _unlocked → go ready
        if (_isCoolingDown)
            return; // クールダウン中はビジュアルを変更しない（完了後に処理する）

        // Not cooling: normal behavior
        if (_unlocked)
        {
            OnMomentumUnlocked(); // アンロック状態のビジュアルに切り替え
        }
        else
        {
            OnMomentumLocked(); // ロック状態のビジュアルに切り替え
        }
    }

    private void OnMomentumUnlocked()
    {
        _isCoolingDown = false;
        ClearPendingLock(); // 保留中のロック処理をキャンセル

        _cooldownTween?.Kill(); _cooldownTween = null;
        _bgScaleTween?.Kill(); _bgScaleTween = null;
        _bgPunchTween?.Kill(); _bgPunchTween = null;

        if (_background != null)
        {
            _bgScaleTween = _background
                .DOScale(_readyScale, 0.25f)
                .SetEase(Ease.OutBack) // バネのようなスケールアニメーション
                .SetUpdate(_useUnscaledTime)
                .SetLink(gameObject);
        }

        if (_cooldownFill != null)
            _cooldownFill.fillAmount = 0f; // unlocked ⇒ ready
    }

    private void OnMomentumLocked()
    {
        // If turbo is active or the UI is in cooldown,
        // don't snap to locked visuals right now.
        // We'll go locked in OnCooldownComplete() instead.
        bool turboActive = TurboModeManager.Instance != null && TurboModeManager.Instance.IsActive;

        if (_isCoolingDown || turboActive)
        {
            // Just update _unlocked; visuals will be handled later.
            return;
        }

        // Here is the tricky case: this is often called RIGHT AFTER Turbo consumed momentum,
        // but BEFORE TurboModeManager sets IsActive and fires onTurboStart.
        // So instead of snapping, schedule a small delayed lock that can be canceled
        // if Turbo actually starts or cooldown starts in this frame.
        ClearPendingLock();
        _lockPending = true; // ロック遅延を保留状態にする

        _lockDelayTween = DOVirtual.DelayedCall(0.01f, () =>
        {
            _lockDelayTween = null;
            if (!_lockPending) return; // キャンセルされた場合は何もしない

            bool turboNow = TurboModeManager.Instance != null && TurboModeManager.Instance.IsActive;
            if (_isCoolingDown || turboNow) return; // クールダウン中またはターボ中なら処理しない

            ApplyLockedVisuals(); // 少し遅らせてロック状態のビジュアルを適用
        })
        .SetUpdate(_useUnscaledTime)
        .SetLink(gameObject);
    }

    private void ApplyLockedVisuals()
    {
        _isCoolingDown = false;
        ClearPendingLock();

        KillTweens(); // 全Tweenを停止してから状態をリセット

        if (_icon != null)
            _icon.localRotation = Quaternion.identity; // 回転をリセット

        if (_cooldownFill != null)
            _cooldownFill.fillAmount = 1f; // locked = full mask

        if (_background != null)
        {
            _bgScaleTween = _background
                .DOScale(_lockedScale, 0.25f)
                .SetEase(Ease.OutQuad) // ロック状態の小さいスケールへアニメーション
                .SetUpdate(_useUnscaledTime)
                .SetLink(gameObject);
        }
    }

    public void SetTutorialUnlocked(bool unlocked)
    {
        if (_tutorialUnlocked == unlocked) return; // 状態が変わっていなければ何もしない
        _tutorialUnlocked = unlocked;

        // Re-evaluate visuals immediately (don't kill cooldown mid animation)
        if (_isCoolingDown) return; // クールダウン中はビジュアルを変更しない

        if (EffectiveUnlocked) OnMomentumUnlocked();
        else ApplyLockedVisuals(); // locked by tutorial OR momentum
    }

    // ───────────────────────────────────────────────────────────────────
    private void OnTurboStart()
    {
        if (!_tutorialUnlocked) return;
        // Once Turbo really starts, we know this is NOT a "idle → locked" case.
        // Cancel any pending lock from the momentum cost.
        ClearPendingLock(); // ターボ開始が確定したので保留中のロックをキャンセル

        // We ALWAYS show Turbo use (even if momentum is now <25 because of the cost).
        PlayRotation();    // アイコン回転を開始
        PlayStartPunch();  // 背景に開始パンチエフェクトを再生
    }

    private void OnTurboEnd()
    {
        if (!_tutorialUnlocked) return;
        // Always run cooldown when Turbo ends, even if momentum <25.
        StopRotation();  // アイコン回転を停止
        StartCooldown(); // クールダウンアニメーションを開始
    }

    private void PlayStartPunch()
    {
        if (_background == null) return;

        _bgPunchTween?.Kill();

        _bgPunchTween = _background
            .DOPunchScale(Vector3.one * _punchScale, _punchDuration, 1, 0.5f) // ターボ開始時の弾力パンチ演出
            .SetUpdate(_useUnscaledTime)
            .SetLink(gameObject);
    }

    private void StartCooldown()
    {
        if (_cooldownFill == null) return;

        _isCoolingDown = true;
        ClearPendingLock(); // クールダウン開始時に保留ロックをキャンセル

        _cooldownTween?.Kill(); _cooldownTween = null;
        _bgScaleTween?.Kill(); _bgScaleTween = null;

        // Fill instantly to 100, then animate down.
        _cooldownFill.fillAmount = 1f; // クールダウン開始直後はフィルを満タンにする

        // Background to cooldown scale
        if (_background != null)
        {
            _bgScaleTween = _background
                .DOScale(_cooldownScale, 0.2f)
                .SetEase(Ease.OutQuad) // クールダウン中の縮小スケールへアニメーション
                .SetUpdate(_useUnscaledTime)
                .SetLink(gameObject);
        }

        _cooldownTween = _cooldownFill
            .DOFillAmount(0f, _cooldownDuration)
            .SetEase(Ease.Linear)          // 線形にフィルを減らしてクールダウンを視覚化
            .SetUpdate(_useUnscaledTime)
            .SetLink(gameObject)
            .OnComplete(OnCooldownComplete); // クールダウン完了時にコールバック
    }

    private void OnCooldownComplete()
    {
        _cooldownTween = null;
        _isCoolingDown = false;

        if (!_unlocked)
        {
            // Case: you used Turbo at exactly 25% → momentum dropped <25
            // or lost momentum during cooldown ⇒ AFTER cooldown ends, go locked.
            ApplyLockedVisuals(); // クールダウン後もモメンタムが足りなければロック状態へ
            return;
        }

        // Still have enough momentum ⇒ show ready state.
        if (_background != null)
        {
            _bgScaleTween?.Kill();
            _bgScaleTween = _background
                .DOScale(_readyScale, 0.25f)
                .SetEase(Ease.OutBack) // 準備完了時のポップスケールアニメーション
                .SetUpdate(_useUnscaledTime)
                .SetLink(gameObject);
        }

        if (_cooldownFill != null)
            _cooldownFill.fillAmount = 0f; // 準備完了状態にリセット
    }

    // ───────────────────────────────────────────────────────────────────
    private void PlayRotation()
    {
        if (_icon == null) return;

        _rotateTween?.Kill();

        _rotateTween = _icon
            .DORotate(new Vector3(0, 0, -360f), 360f / _rotationSpeed, RotateMode.FastBeyond360)
            .SetEase(Ease.Linear)
            .SetLoops(-1, LoopType.Restart) // 無限ループで回転し続ける
            .SetSpeedBased()
            .SetUpdate(_useUnscaledTime)
            .SetLink(gameObject);
    }

    public void StopRotation()
    {
        if (_rotateTween != null)
        {
            _rotateTween.Kill(); // 回転Tweenを停止
            _rotateTween = null;
        }

        if (_icon != null)
            _icon.localRotation = Quaternion.identity; // 回転をリセット
    }
}

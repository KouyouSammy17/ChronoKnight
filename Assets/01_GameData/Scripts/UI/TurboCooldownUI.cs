// ターボのクールダウン状態をUIで表示・アニメーションするスクリプト
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

public class TurboCooldownUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Image _cooldownFill;         // アイコン上部の放射状フィル画像
    [SerializeField] private RectTransform _icon;         // 回転のみを行うアイコン
    [SerializeField] private RectTransform _background;   // スケール・パンチ演出するフレーム/リング

    [Header("Cooldown Settings")]
    [SerializeField] private float _cooldownDuration = 6f;           // クールダウンの秒数
    [SerializeField] private float _rotationSpeed = 180f;   // 回転速度（度/秒）
    [SerializeField] private bool _useUnscaledTime = true;  // UIはtimeScaleを無視する

    [Header("Scale Settings")]
    [SerializeField] private float _lockedScale = 1.0f;     // モメンタム25%未満のスケール
    [SerializeField] private float _readyScale = 1.25f;     // モメンタム25%以上かつクールダウンなしのスケール
    [SerializeField] private float _cooldownScale = 0.8f;   // クールダウン中のスケール

    [Header("Punch Settings")]
    [SerializeField] private float _punchScale = 0.25f;   // パンチエフェクトの強さ
    [SerializeField] private float _punchDuration = 0.2f; // パンチエフェクトの時間

    private Tween _cooldownTween;   // フィルアニメーション用Tween
    private Tween _rotateTween;     // アイコン回転アニメーション用Tween
    private Tween _bgScaleTween;    // 背景スケールアニメーション用Tween
    private Tween _bgPunchTween;    // 背景パンチエフェクト用Tween
    private Tween _lockDelayTween;  // ロック遅延用Tween

    // 「アンロック済み」= アイドル中にモメンタムが25%以上
    private bool _unlocked = true;        // 現在のアンロック状態（モメンタム+チュートリアル両方考慮）
    private bool _isCoolingDown = false;  // クールダウン中かどうか

    // チュートリアルがアンロックするまでターボは使用不可
    private bool _tutorialUnlocked = true; // チュートリアルによるアンロック状態

    // モメンタムベースのアンロックを個別に管理（25%以上）
    private bool _momentumUnlocked = true; // モメンタム25%以上かどうか

    // 実効アンロック = チュートリアルアンロック AND モメンタムアンロック
    private bool EffectiveUnlocked => _tutorialUnlocked && _momentumUnlocked; // 実効アンロック（両条件の論理積）


    // 保留中のロックフラグ（コスト消費直後にターボが開始される場合のスナップを防ぐ）
    private bool _lockPending = false; // ロック遅延処理が保留中かどうか

    // ───────────────────────────────────────────────────────────────────
    private void Awake()
    {
        if (_cooldownFill != null)
            _cooldownFill.fillAmount = 0f; // デフォルトは準備完了状態

        if (_background != null)
            _background.localScale = Vector3.one * _readyScale; // 準備完了スケールで初期化
    }

    private void OnEnable()
    {
        // ターボイベント
        if (TurboModeManager.Instance != null)
        {
            TurboModeManager.Instance.onTurboStart.AddListener(OnTurboStart); // ターボ開始イベントを購読
            TurboModeManager.Instance.onTurboEnd.AddListener(OnTurboEnd);     // ターボ終了イベントを購読
        }

        // モメンタムイベント
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

        // 論理フラグを常に最新の値に保つ（このフラグは実効アンロックを意味する）
        _unlocked = newEffective;

        // 重要：
        // クールダウンアニメーション中はここでビジュアルを変更しない。
        // 新しい_unlocked値を記憶するだけにする。
        // OnCooldownComplete()が_unlockedを参照して以下を判断する：
        //   - !_unlocked → ロック状態へ
        //   -  _unlocked → 準備完了状態へ
        if (_isCoolingDown)
            return; // クールダウン中はビジュアルを変更しない（完了後に処理する）

        // クールダウンなし：通常の処理
        if (_unlocked)
        {
            OnMomentumUnlocked(); // アンロック状態のビジュアルに切り替える
        }
        else
        {
            OnMomentumLocked(); // ロック状態のビジュアルに切り替える
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
            _cooldownFill.fillAmount = 0f; // アンロック済み → 準備完了
    }

    private void OnMomentumLocked()
    {
        // ターボ使用中またはクールダウン中は即座にロックビジュアルへ切り替えない。
        // OnCooldownComplete()でロック状態に移行する。
        bool turboActive = TurboModeManager.Instance != null && TurboModeManager.Instance.IsActive;

        if (_isCoolingDown || turboActive)
        {
            // _unlockedだけ更新し、ビジュアルは後で処理する。
            return;
        }

        // 注意：ターボがモメンタムを消費した直後にこのメソッドが呼ばれることが多い。
        // ただし、TurboModeManagerがIsActiveをセットしてonTurboStartを発火させる前に呼ばれる。
        // そのため、スナップではなく少し遅延したロックをスケジュールし、
        // このフレーム内でターボやクールダウンが開始された場合はキャンセルできるようにする。
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
            _cooldownFill.fillAmount = 1f; // ロック = フル表示（マスク）

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

        // アニメーション途中でクールダウンをキャンセルしないよう即座にビジュアルを再評価する
        if (_isCoolingDown) return; // クールダウン中はビジュアルを変更しない

        if (EffectiveUnlocked) OnMomentumUnlocked();
        else ApplyLockedVisuals(); // チュートリアルまたはモメンタムによりロック
    }

    // ───────────────────────────────────────────────────────────────────
    private void OnTurboStart()
    {
        if (!_tutorialUnlocked) return;
        // ターボが実際に開始した場合、これは「アイドル → ロック」ではないことが確定する。
        // モメンタムコスト消費による保留中のロックをキャンセルする。
        ClearPendingLock(); // ターボ開始が確定したので保留中のロックをキャンセル

        // コスト消費でモメンタムが25%未満になっていても、常にターボ使用を表示する。
        PlayRotation();    // アイコン回転を開始
        PlayStartPunch();  // 背景に開始パンチエフェクトを再生
    }

    private void OnTurboEnd()
    {
        if (!_tutorialUnlocked) return;
        // モメンタムが25%未満でも、ターボ終了時は必ずクールダウンを実行する。
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

        // 即座に100%にして、そこからアニメーションで減らす。
        _cooldownFill.fillAmount = 1f; // クールダウン開始直後はフィルを満タンにする

        // 背景をクールダウンスケールへ縮小する
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
            // ケース：ターボをちょうど25%で使用 → モメンタムが25%未満に
            // またはクールダウン中にモメンタムを失った → クールダウン後にロック状態へ
            ApplyLockedVisuals(); // クールダウン後もモメンタムが足りなければロック状態へ
            return;
        }

        // まだ十分なモメンタムがある → 準備完了状態を表示する
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
            .SetLoops(-1, LoopType.Restart) // 無限ループで回転し続ける（-1 = 無制限）
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

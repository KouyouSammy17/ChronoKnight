// 点滅するレーザートラップの視覚エフェクト・コライダー・ダメージを管理するスクリプト
using System.Collections;
using UnityEngine;
using SciFiArsenal;

[DisallowMultipleComponent]
public class LaserTrap : MonoBehaviour
{
    [Header("Beam FX (SciFi Arsenal)")]
    [SerializeField] private GameObject _beamFxRoot;                 // ビームFXの親オブジェクト（SciFiArsenalBeamStaticをここにアタッチ推奨：子オブジェクト）
    [SerializeField] private SciFiArsenalBeamStatic _beamFx;         // ビームFXコンポーネント（nullなら_beamFxRootから自動取得）

    [Header("Loop Timing")]
    [SerializeField] private bool _startOn = true;              // 開始時にレーザーをオン状態にするか
    [SerializeField] private float _onDuration = 0.75f;         // レーザーが点灯している時間（秒）
    [SerializeField] private float _offDuration = 0.75f;        // レーザーが消灯している時間（秒）
    [SerializeField] private float _initialDelay = 0f;          // 最初に動作するまでの待機時間
    [SerializeField] private bool _useUnscaledTime = false;     // タイムスケールを無視した時間を使うか

    [Header("Damage")]
    [SerializeField] private Collider _damageTrigger;                // ダメージ判定に使うトリガーコライダー（BoxCollider推奨）
    [SerializeField] private int _damage = 20;                       // プレイヤーに与えるダメージ量

    [Tooltip("If > 0: deals damage repeatedly while inside the trigger (laser burn). If = 0: only on enter.")]
    [SerializeField] private float _damageTickInterval = 0.2f;  // 継続ダメージの間隔（0以下なら進入時のみ）

    [Header("Auto Fit Damage Box (Optional)")]
    [Tooltip("If true and damage trigger is BoxCollider, it will resize to match the beam length (raycast hit / max length).")]
    [SerializeField] private bool _autoFitDamageBox = true;     // ビーム長にダメージボックスを自動調整するか

    private Coroutine _loopCo;  // 点滅ループのコルーチン参照
    private bool _isOn;         // 現在レーザーがオンかどうか

    // シングルプレイヤー向けのティックゲート
    private float _nextDamageTime = 0f; // 次にダメージを与えられる時刻

    private void Awake()
    {
        // ビームFX参照の補完
        if (_beamFxRoot == null && _beamFx != null) _beamFxRoot = _beamFx.gameObject;
        if (_beamFx == null && _beamFxRoot != null) _beamFx = _beamFxRoot.GetComponent<SciFiArsenalBeamStatic>(); // 相互参照を補完

        // ダメージトリガー参照の補完
        if (_damageTrigger == null) _damageTrigger = GetComponent<Collider>();  // 自身のコライダーをフォールバックで使用
        if (_damageTrigger != null) _damageTrigger.isTrigger = true;            // トリガーモードに設定

        // ループが判断するまで非表示にする（エディタで常時オンになる誤作動を防ぐ）
        ApplyState(_startOn, immediate: true);
    }

    private void OnEnable()
    {
        if (_loopCo != null) StopCoroutine(_loopCo);
        _loopCo = StartCoroutine(LoopRoutine());    // 有効化時に点滅ループを開始
    }

    private void OnDisable()
    {
        if (_loopCo != null)
        {
            StopCoroutine(_loopCo);
            _loopCo = null;
        }

        // 安全のため、無効化時はすべてをオフにする
        ApplyState(false, immediate: true); // 無効化時にレーザーを強制オフ
    }

    private IEnumerator LoopRoutine()
    {
        if (_initialDelay > 0f)
        {
            // 初期遅延の待機（タイムスケール設定に応じて切り替え）
            if (_useUnscaledTime) yield return new WaitForSecondsRealtime(_initialDelay);
            else yield return new WaitForSeconds(_initialDelay);
        }

        // 初期状態をここでも一度適用する
        ApplyState(_startOn, immediate: true);  // 初期状態を適用

        while (true)
        {
            float wait = _isOn ? _onDuration : _offDuration;   // 現在の状態に応じた待機時間を選択
            if (wait < 0f) wait = 0f;

            if (_useUnscaledTime) yield return new WaitForSecondsRealtime(wait);
            else yield return new WaitForSeconds(wait);

            ApplyState(!_isOn, immediate: false);   // 状態を反転（オン→オフ or オフ→オン）
        }
    }

    private void ApplyState(bool on, bool immediate)
    {
        _isOn = on;

        // 1) ビームFXのオン／オフ
        if (_beamFxRoot != null)
        {
            // 視覚的に「スポーン・デスポーン」を擬似的に実現する：
            // - 初回アクティブ化時にSciFiArsenalBeamStatic.Start()が実行されてビームがスポーンされる。
            // - その後は表示・非表示を切り替えるだけ。
            _beamFxRoot.SetActive(on);  // ビームFXの表示・非表示を切り替え
        }

        // 2) ダメージトリガーのオン／オフ
        if (_damageTrigger != null)
            _damageTrigger.enabled = on;    // ダメージトリガーのコライダーを有効・無効化

        // オン時にすぐダメージを与えられるようティックゲートをリセット
        if (on) _nextDamageTime = 0f;  // オンになった瞬間にすぐダメージを与えられるようにリセット

        // オン時に即座にスナップフィットを実行（コライダーをビーム長に合わせる）
        if (on && _autoFitDamageBox)
            FitDamageBoxToBeam();   // 即座にビーム長に合わせてコライダーを調整
    }

    private void FixedUpdate()
    {
        if (!_isOn) return;
        if (_autoFitDamageBox) FitDamageBoxToBeam();    // 毎フィジクスフレームでコライダーをビーム長に追従
    }

    private void FitDamageBoxToBeam()
    {
        if (_beamFx == null) return;

        // BoxColliderのフィットのみ対応
        if (!(_damageTrigger is BoxCollider box)) return;   // BoxCollider以外は対応しない

        float maxLen = Mathf.Max(0.01f, _beamFx.beamLength);   // ビームの最大長
        Vector3 origin = _beamFx.transform.position;
        Vector3 dir = _beamFx.transform.forward;

        float distance = maxLen;

        if (_beamFx.beamCollides)
        {
            if (Physics.Raycast(origin, dir, out RaycastHit hit, maxLen))
            {
                // SciFiArsenalBeamStaticのエンドオフセットロジックに合わせる
                distance = Mathf.Clamp(hit.distance - _beamFx.beamEndOffset, 0.01f, maxLen);  // 衝突位置とオフセットに基づいてビーム長を決定
            }
        }

        // ボックスのローカルZ軸がビームの前方向を向いていることを前提とする。
        // サイズと中心はローカル空間で指定する。
        Vector3 size = box.size;
        Vector3 center = box.center;

        size.z = distance;          // Z軸方向のサイズをビーム長に合わせる
        center.z = distance * 0.5f; // ボックスの中心をビームの中点に配置

        box.size = size;
        box.center = center;
    }

    // タイムスケール設定に応じて現在時刻を返す
    private float Now() => _useUnscaledTime ? Time.unscaledTime : Time.time;

    private void DealDamage(Collider other)
    {
        if (!_isOn) return;
        if (other.CompareTag("Player"))
        {
            var stats = other.GetComponent<PlayerStats>();
            if (stats != null)
                stats.TakeDamage(_damage);  // プレイヤーにダメージを与える
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!_isOn) return;

        if (_damageTickInterval <= 0f)
        {
            DealDamage(other);  // ティックなしの場合は進入時に即ダメージ
            return;
        }

        // ティックモード：進入時に即ヒットを許可
        _nextDamageTime = 0f;   // 進入時は待機時間をリセットして即ダメージを許可
        TryTickDamage(other);
        Debug.Log("LaserTrap OnTriggerEnter: ダメージを与えた");
    }

    private void OnTriggerStay(Collider other)
    {
        if (!_isOn) return;
        if (_damageTickInterval <= 0f) return;  // ティックダメージ無効なら継続判定しない

        TryTickDamage(other);   // 滞在中は一定間隔でダメージを与える
    }

    private void TryTickDamage(Collider other)
    {
        float now = Now();
        if (now < _nextDamageTime) return;  // まだダメージを与える時刻でなければスキップ

        _nextDamageTime = now + Mathf.Max(0.01f, _damageTickInterval); // 次のダメージ時刻を設定
        DealDamage(other);
    }
}

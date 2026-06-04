using System.Collections;
using UnityEngine;

/// <summary>
/// Turbo Mode中のVFXを管理するクラス。
/// Playerのルートにアタッチし、TurboModeManagerの
/// onTurboStart / onTurboEnd に Inspector から接続して使用する。
/// </summary>
public class TurboVFXController : MonoBehaviour
{
    // ────────────────────────────────────────────────────────────────
    // Inspector設定
    // ────────────────────────────────────────────────────────────────

    [Header("発動時VFX")]
    [SerializeField] private GameObject _activationBurstPrefab;
    [SerializeField] private GameObject _activationBeamPrefab;

    [Header("発動中VFX")]
    [SerializeField] private GameObject _loopAuraPrefab;
    [SerializeField] private Vector3 _loopAuraRotationEuler = Vector3.zero;

    [Header("終了時VFX")]
    [SerializeField] private GameObject _endShockwavePrefab;

    [Header("時間設定")]
    [SerializeField, Range(0f, 0.5f)] private float _loopStartDelay = 0.2f;
    [SerializeField, Range(0.5f, 5f)] private float _endVFXLifetime = 2f;

    // ────────────────────────────────────────────────────────────────
    // 実行中の状態
    // ────────────────────────────────────────────────────────────────

    private GameObject _activeAura;
    private Coroutine _loopDelayCoroutine;

    // ────────────────────────────────────────────────────────────────
    // TurboModeManagerのイベントから呼ぶ処理
    // ────────────────────────────────────────────────────────────────

    /// <summary>
    /// TurboModeManager.onTurboStart に接続する。
    /// </summary>
    public void OnTurboStart()
    {
        // 発動時の単発VFXをワールド座標に生成する。
        SpawnOneShot(_activationBurstPrefab, transform.position);
        SpawnOneShot(_activationBeamPrefab, transform.position);

        // 少し遅らせて、発動中のオーラを表示する。
        _loopDelayCoroutine = StartCoroutine(Co_StartLoopAfterDelay(_loopStartDelay));
    }

    /// <summary>
    /// TurboModeManager.onTurboEnd に接続する。
    /// </summary>
    public void OnTurboEnd()
    {
        // Turboがすぐ終了した場合、遅延生成を止める。
        if (_loopDelayCoroutine != null)
        {
            StopCoroutine(_loopDelayCoroutine);
            _loopDelayCoroutine = null;
        }

        // 発動中のオーラを停止する。
        StopAndDestroy(ref _activeAura);

        // 終了時の単発VFXをワールド座標に生成する。
        if (_endShockwavePrefab != null)
        {
            var vfx = Instantiate(_endShockwavePrefab, transform.position, Quaternion.identity);
            ForceUnscaledTime(vfx);
            Destroy(vfx, _endVFXLifetime);
        }
    }

    private void LateUpdate()
    {
        // オーラはプレイヤーの位置だけ追従させる。
        // プレイヤーの左右向き回転を継承すると、円形エフェクトの向きが崩れやすい。
        if (_activeAura == null) return;

        _activeAura.transform.position = transform.position;
        _activeAura.transform.rotation = Quaternion.Euler(_loopAuraRotationEuler);
    }

    // ────────────────────────────────────────────────────────────────
    // 補助処理
    // ────────────────────────────────────────────────────────────────

    private IEnumerator Co_StartLoopAfterDelay(float delay)
    {
        // Time.timeScale の影響を受けないように、リアルタイムで待つ。
        yield return new WaitForSecondsRealtime(delay);

        _activeAura = SpawnLooped(_loopAuraPrefab);
        _loopDelayCoroutine = null;
    }

    /// <summary>
    /// 単発VFXをワールド座標に生成する。
    /// </summary>
    private void SpawnOneShot(GameObject prefab, Vector3 worldPos)
    {
        if (prefab == null) return;

        var vfx = Instantiate(prefab, worldPos, Quaternion.identity);
        ForceUnscaledTime(vfx);

        // パーティクルが終わるまで残してから削除する。
        float lifetime = GetMaxDuration(vfx);
        Destroy(vfx, Mathf.Max(lifetime, 3f));
    }

    /// <summary>
    /// 発動中に表示するループVFXを生成する。
    /// 親子付けしないことで、プレイヤーの向き回転を継承しないようにする。
    /// </summary>
    private GameObject SpawnLooped(GameObject prefab)
    {
        if (prefab == null) return null;

        var vfx = Instantiate(prefab, transform.position, Quaternion.Euler(_loopAuraRotationEuler));
        ForceUnscaledTime(vfx);
        return vfx;
    }

    /// <summary>
    /// ループVFXの放出を止め、少し待ってから削除する。
    /// </summary>
    private void StopAndDestroy(ref GameObject instance)
    {
        if (instance == null) return;

        var systems = instance.GetComponentsInChildren<ParticleSystem>();
        foreach (var ps in systems)
        {
            var emission = ps.emission;
            emission.enabled = false;
        }

        Destroy(instance, 1.5f);
        instance = null;
    }

    /// <summary>
    /// Time.timeScale が下がっていても、パーティクルをリアルタイムで再生する。
    /// </summary>
    private static void ForceUnscaledTime(GameObject root)
    {
        foreach (var ps in root.GetComponentsInChildren<ParticleSystem>(includeInactive: true))
        {
            var main = ps.main;
            main.useUnscaledTime = true;
        }
    }

    /// <summary>
    /// 子オブジェクトを含めて、一番長いパーティクル再生時間を取得する。
    /// </summary>
    private static float GetMaxDuration(GameObject root)
    {
        float max = 0f;

        foreach (var ps in root.GetComponentsInChildren<ParticleSystem>())
        {
            float duration = ps.main.duration + ps.main.startLifetime.constantMax;
            if (duration > max) max = duration;
        }

        return max;
    }

    // ────────────────────────────────────────────────────────────────
    // 破棄時の安全処理
    // ────────────────────────────────────────────────────────────────

    private void OnDestroy()
    {
        if (_activeAura != null)
        {
            Destroy(_activeAura);
        }
    }
}

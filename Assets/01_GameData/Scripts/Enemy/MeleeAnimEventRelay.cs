// Animator と同じ GameObject（モデル）にアタッチして使用する。
// Animation Event から呼ばれ、親の MeleeRobotAI に処理を転送する。
using UnityEngine;

/// <summary>
/// Animation Event の橋渡しコンポーネント。<br/>
/// モデル側の Animator と、親の <see cref="MeleeRobotAI"/> の間を繋ぐ。<br/>
/// <br/>
/// 使い方:<br/>
/// 1. このスクリプトを Animator と同じ GameObject にアタッチする。<br/>
/// 2. Attack クリップの Animation Event の Function に<br/>
///    <c>ActivateHitbox</c> / <c>DeactivateHitbox</c> を指定する。
/// </summary>
public class MeleeAnimEventRelay : MonoBehaviour
{
    // 親の MeleeRobotAI への参照（自動取得）
    private MeleeRobotAI _ai;

    private void Awake()
    {
        // 親階層を遡って MeleeRobotAI を探す
        _ai = GetComponentInParent<MeleeRobotAI>();

        if (_ai == null)
            Debug.LogWarning($"[MeleeAnimEventRelay] 親に MeleeRobotAI が見つかりません。({gameObject.name})", this);
    }

    // ─────────────────────────────────────────────────────────────
    //  Animation Event から呼ばれるメソッド
    // ─────────────────────────────────────────────────────────────

    /// <summary>左拳が出るフレームの Animation Event に設定する（Attack 1・3）。</summary>
    public void ActivateLeftHitbox()  => _ai?.ActivateLeftHitbox();

    /// <summary>右拳が出るフレームの Animation Event に設定する（Attack 2）。</summary>
    public void ActivateRightHitbox() => _ai?.ActivateRightHitbox();

    /// <summary>拳が引くフレームの Animation Event に設定する（全 Attack クリップ共通）。</summary>
    public void DeactivateHitbox()    => _ai?.DeactivateHitbox();
}

// プレイヤーの頭上にUI要素をスムーズに追従させるスクリプト
using UnityEngine;

public class UIFollowPlayer : MonoBehaviour
{
    [SerializeField] private Transform _target;                          // 追従対象のTransform
    [SerializeField] private Vector3 _offset = new Vector3(0f, 2f, 0f); // 対象からのオフセット位置
    [SerializeField] private float _smoothSpeed = 10f;                  // 追従の補間速度

    private void LateUpdate()
    {
        if (_target == null) return; // ターゲットが未設定なら何もしない

        Vector3 targetPos = _target.position + _offset; // オフセットを加算した目標位置を計算
        transform.position = Vector3.Lerp(transform.position, targetPos, _smoothSpeed * Time.deltaTime); // 現在位置から目標位置へ滑らかに移動
        transform.rotation = Quaternion.LookRotation(Camera.main.transform.forward); // always face camera
    }

    public void SetTarget(Transform newTarget)
    {
        _target = newTarget; // 追従対象を動的に変更する
    }
}

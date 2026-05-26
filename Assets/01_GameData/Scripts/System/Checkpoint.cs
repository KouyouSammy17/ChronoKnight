// プレイヤーが通過するとリスポーン地点を更新するチェックポイントスクリプト
using UnityEngine;

[DisallowMultipleComponent]
public class Checkpoint : MonoBehaviour
{
    [Header("Optional")]
    [SerializeField] private bool _oneShot = true;              // trueなら一度だけ有効になる
    [SerializeField] private GameObject _activatedVisual;       // 通過後に表示するエフェクト（発光など）

    private bool _activated; // このチェックポイントが通過済みかどうか

    private void Awake()
    {
        // 起動時は通過エフェクトを非表示にしておく
        if (_activatedVisual != null) _activatedVisual.SetActive(false);
    }

    private void OnTriggerEnter(Collider other)
    {
        // 一度きり設定で既に通過済みなら処理をスキップする
        if (_activated && _oneShot) return;
        // プレイヤー以外のコライダーは無視する
        if (!other.CompareTag("Player")) return;

        if (GameManager.Instance != null)
        {
            // GameManagerにこのチェックポイントをリスポーン地点として登録する
            GameManager.Instance.SetCheckpoint(transform);
            _activated = true;
            // 通過エフェクトを有効にする
            if (_activatedVisual != null) _activatedVisual.SetActive(true);
        }
    }
}

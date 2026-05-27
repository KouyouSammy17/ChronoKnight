// 敵の撃破に連動して開くフォースフィールド（バリア）を制御するスクリプト
using UnityEngine;

public class ForceField : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private EnemyStats _enemy;          // このゲートのロックを解除する敵
    [SerializeField] private Collider _blockCollider;    // プレイヤーの通行を遮断するコライダー（未設定時は自動取得）
    [SerializeField] private GameObject _visualRoot;     // フォースフィールドのビジュアル・VFXルート（未設定時は自身）

    [Header("Behavior")]
    [SerializeField] private bool _disableObjectAtEnd = true;   // 開いた後にゲームオブジェクトを非表示にするか

    private bool _opened;   // すでに開いたかどうかのフラグ（二重実行防止）

    private void Awake()
    {
        if (_blockCollider == null) _blockCollider = GetComponent<Collider>();  // 自身のコライダーをフォールバックで使用
        if (_visualRoot == null) _visualRoot = gameObject;                      // 自身をビジュアルルートとして使用
    }

    private void OnEnable()
    {
        if (_enemy != null) _enemy.OnDied.AddListener(Open);    // 敵の死亡イベントを購読
    }

    private void OnDisable()
    {
        if (_enemy != null) _enemy.OnDied.RemoveListener(Open); // 死亡イベントの購読を解除
    }

    private void Open()
    {
        if (_opened) return;    // 二重実行を防ぐ
        _opened = true;

        // 即座にプレイヤーの通行を遮断するのを停止する
        if (_blockCollider != null) _blockCollider.enabled = false; // バリアのコライダーを無効化してプレイヤーが通れるようにする

        // 最もシンプルな「消滅」処理
        if (_disableObjectAtEnd)
        {
            _visualRoot.SetActive(false);   // ビジュアルを非表示にして消滅させる
        }
    }
}

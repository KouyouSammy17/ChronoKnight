// 敵の撃破に連動して開くフォースフィールド（バリア）を制御するスクリプト
using UnityEngine;

public class ForceField : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private EnemyStats _enemy;          // the enemy that unlocks this gate   // このゲートのロックを解除する敵
    [SerializeField] private Collider _blockCollider;    // barrier collider (optional auto-find)  // プレイヤーの通行を遮断するコライダー
    [SerializeField] private GameObject _visualRoot;     // mesh/VFX root (optional: this object)  // フォースフィールドのビジュアル・VFXルート

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

        // stop blocking the player immediately
        if (_blockCollider != null) _blockCollider.enabled = false; // バリアのコライダーを無効化してプレイヤーが通れるようにする

        // simplest 'disappear'
        if (_disableObjectAtEnd)
        {
            _visualRoot.SetActive(false);   // ビジュアルを非表示にして消滅させる
        }
    }
}

// ゴール判定を行い、プレイヤー接触時にクリアを通知するスクリプト
using UnityEngine;

/// <summary>
/// ゴールGameObject（トリガーコライダーが必要）にアタッチする。
/// プレイヤーが入ったとき、GameManager.Instance.WinLevel()を呼び出す。
/// </summary>
public class Goal : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        // プレイヤータグを持つコライダーのみ反応する
        // プレイヤーのコライダーに"Player"タグ（または使用しているタグ）が設定されていることを確認する
        if (other.CompareTag("Player"))
        {
            // GameManagerにクリアを通知する
            GameManager.Instance.WinLevel(this);
        }
    }
}

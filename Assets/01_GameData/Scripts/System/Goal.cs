// ゴール判定を行い、プレイヤー接触時にクリアを通知するスクリプト
using UnityEngine;

/// <summary>
/// Attach this to the Goal GameObject (which should have a Trigger collider).
///  When the player enters, it calls GameManager.Instance.WinLevel().
/// </summary>
public class Goal : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        // プレイヤータグを持つコライダーのみ反応する
        // Make sure the player's collider has the tag "Player" (or whatever tag you use)
        if (other.CompareTag("Player"))
        {
            // GameManagerにクリアを通知する
            GameManager.Instance.WinLevel(this);
        }
    }
}

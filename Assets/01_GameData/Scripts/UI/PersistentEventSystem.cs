// シーンをまたいで唯一のEventSystemを維持するスクリプト
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

public class PersistentEventSystem : MonoBehaviour
{
    private void Awake()
    {
        // If another EventSystem already exists, destroy this one
        var all = FindObjectsByType<EventSystem>(FindObjectsSortMode.None); // シーン内の全EventSystemを検索
        if (all.Length > 1)
        {
            Destroy(gameObject); // 既に別のEventSystemが存在する場合はこのオブジェクトを削除
            return;
        }

        DontDestroyOnLoad(gameObject); // 唯一のEventSystemとしてシーン間で保持する
    }
}

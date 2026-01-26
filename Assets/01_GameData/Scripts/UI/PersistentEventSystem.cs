using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

public class PersistentEventSystem : MonoBehaviour
{
    private void Awake()
    {
        // If another EventSystem already exists, destroy this one
        var all = FindObjectsByType<EventSystem>(FindObjectsSortMode.None);
        if (all.Length > 1)
        {
            Destroy(gameObject);
            return;
        }

        DontDestroyOnLoad(gameObject);
    }
}

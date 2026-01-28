using UnityEngine;

[DisallowMultipleComponent]
public class Checkpoint : MonoBehaviour
{
    [Header("Optional")]
    [SerializeField] private bool _oneShot = true;
    [SerializeField] private GameObject _activatedVisual; // glow, light, etc.

    private bool _activated;

    private void Awake()
    {
        if (_activatedVisual != null) _activatedVisual.SetActive(false);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (_activated && _oneShot) return;
        if (!other.CompareTag("Player")) return;

        if (GameManager.Instance != null)
        {
            GameManager.Instance.SetCheckpoint(transform);
            _activated = true;
            if (_activatedVisual != null) _activatedVisual.SetActive(true);
        }
    }
}

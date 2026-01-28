using UnityEngine;

public class ForceField : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private EnemyStats _enemy;          // the enemy that unlocks this gate
    [SerializeField] private Collider _blockCollider;    // barrier collider (optional auto-find)
    [SerializeField] private GameObject _visualRoot;     // mesh/VFX root (optional: this object)

    [Header("Behavior")]
    [SerializeField] private bool _disableObjectAtEnd = true;

    private bool _opened;

    private void Awake()
    {
        if (_blockCollider == null) _blockCollider = GetComponent<Collider>();
        if (_visualRoot == null) _visualRoot = gameObject;
    }

    private void OnEnable()
    {
        if (_enemy != null) _enemy.OnDied.AddListener(Open);
    }

    private void OnDisable()
    {
        if (_enemy != null) _enemy.OnDied.RemoveListener(Open);
    }

    private void Open()
    {
        if (_opened) return;
        _opened = true;

        // stop blocking the player immediately
        if (_blockCollider != null) _blockCollider.enabled = false;

        // simplest ÅgdisappearÅh
        if (_disableObjectAtEnd)
        {
            _visualRoot.SetActive(false);
        }
    }
}

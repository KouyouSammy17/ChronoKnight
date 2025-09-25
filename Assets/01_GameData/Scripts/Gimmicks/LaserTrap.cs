using UnityEngine;
using DG.Tweening;

[RequireComponent(typeof(Collider))]
public class LaserTrap : MonoBehaviour
{
    [Header("Blink Settings")]
    [SerializeField] private float _onDuration = 0.5f;   // active laser time
    [SerializeField] private float _offDuration = 0.5f;  // inactive time
    [SerializeField] private Transform _laserBeam;       // the visual beam mesh
    [SerializeField] private float _popDuration = 0.2f;  // how fast it pops down
    [SerializeField] private int _damage = 20;

    private Collider _col;
    private float _timer;
    private bool _isOn;

    private void Awake()
    {
        _col = GetComponent<Collider>();
        _col.isTrigger = true;

        if (_laserBeam != null)
        {
            // start hidden (scaled to zero in Y)
            _laserBeam.localScale = new Vector3(0.5f, 0f, 0.5f);
        }

        _col.enabled = false;
    }

    private void Update()
    {
        _timer -= Time.deltaTime;

        if (_timer <= 0f)
        {
            _isOn = !_isOn;
            if (_isOn)
                ActivateLaser();
            else
                DeactivateLaser();
        }
    }

    private void ActivateLaser()
    {
        _col.enabled = true;
        _laserBeam?.DOScaleY(4f, _popDuration).From(0f).SetEase(Ease.OutCubic);
        _timer = _onDuration;
    }

    private void DeactivateLaser()
    {
        _col.enabled = false;
        _laserBeam?.DOScaleY(0f, _popDuration).SetEase(Ease.InCubic);
        _timer = _offDuration;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!_isOn) return;

        if (other.CompareTag("Player"))
        {
            var stats = other.GetComponent<PlayerStats>();
            if (stats != null)
                stats.TakeDamage(_damage);
        }
    }
}

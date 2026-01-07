using System.Collections;
using UnityEngine;
using SciFiArsenal;

[DisallowMultipleComponent]
public class LaserTrap : MonoBehaviour
{
    [Header("Beam FX (SciFi Arsenal)")]
    [SerializeField] private GameObject _beamFxRoot;                 // Put SciFiArsenalBeamStatic on this object (recommended: child object)
    [SerializeField] private SciFiArsenalBeamStatic _beamFx;         // Auto-found from _beamFxRoot if null

    [Header("Loop Timing")]
    [SerializeField] private bool _startOn = true;
    [SerializeField] private float _onDuration = 0.75f;
    [SerializeField] private float _offDuration = 0.75f;
    [SerializeField] private float _initialDelay = 0f;
    [SerializeField] private bool _useUnscaledTime = false;

    [Header("Damage")]
    [SerializeField] private Collider _damageTrigger;                // Trigger collider used for damage (BoxCollider recommended)
    [SerializeField] private int _damage = 20;

    [Tooltip("If > 0: deals damage repeatedly while inside the trigger (laser burn). If = 0: only on enter.")]
    [SerializeField] private float _damageTickInterval = 0.2f;

    [Header("Auto Fit Damage Box (Optional)")]
    [Tooltip("If true and damage trigger is BoxCollider, it will resize to match the beam length (raycast hit / max length).")]
    [SerializeField] private bool _autoFitDamageBox = true;

    private Coroutine _loopCo;
    private bool _isOn;

    // single-player friendly tick gate
    private float _nextDamageTime = 0f;

    private void Awake()
    {
        // Beam fx refs
        if (_beamFxRoot == null && _beamFx != null) _beamFxRoot = _beamFx.gameObject;
        if (_beamFx == null && _beamFxRoot != null) _beamFx = _beamFxRoot.GetComponent<SciFiArsenalBeamStatic>();

        // Damage trigger ref
        if (_damageTrigger == null) _damageTrigger = GetComponent<Collider>();
        if (_damageTrigger != null) _damageTrigger.isTrigger = true;

        // Start hidden until loop decides (prevents accidental always-on in editor)
        ApplyState(_startOn, immediate: true);
    }

    private void OnEnable()
    {
        if (_loopCo != null) StopCoroutine(_loopCo);
        _loopCo = StartCoroutine(LoopRoutine());
    }

    private void OnDisable()
    {
        if (_loopCo != null)
        {
            StopCoroutine(_loopCo);
            _loopCo = null;
        }

        // Safety: turn everything off when disabled
        ApplyState(false, immediate: true);
    }

    private IEnumerator LoopRoutine()
    {
        if (_initialDelay > 0f)
        {
            if (_useUnscaledTime) yield return new WaitForSecondsRealtime(_initialDelay);
            else yield return new WaitForSeconds(_initialDelay);
        }

        // Ensure the starting state is applied once here too
        ApplyState(_startOn, immediate: true);

        while (true)
        {
            float wait = _isOn ? _onDuration : _offDuration;
            if (wait < 0f) wait = 0f;

            if (_useUnscaledTime) yield return new WaitForSecondsRealtime(wait);
            else yield return new WaitForSeconds(wait);

            ApplyState(!_isOn, immediate: false);
        }
    }

    private void ApplyState(bool on, bool immediate)
    {
        _isOn = on;

        // 1) Beam FX ON/OFF
        if (_beamFxRoot != null)
        {
            // This effectively gspawns on/offh visually:
            // - When activated the first time, SciFiArsenalBeamStatic.Start() runs and spawns the beam.
            // - Afterwards it just toggles visibility.
            _beamFxRoot.SetActive(on);
        }

        // 2) Damage trigger ON/OFF
        if (_damageTrigger != null)
            _damageTrigger.enabled = on;

        // reset tick gate on each ON so it can deal damage immediately
        if (on) _nextDamageTime = 0f;

        // Optional: snap-fit once immediately (so collider matches as soon as it turns on)
        if (on && _autoFitDamageBox)
            FitDamageBoxToBeam();
    }

    private void FixedUpdate()
    {
        if (!_isOn) return;
        if (_autoFitDamageBox) FitDamageBoxToBeam();
    }

    private void FitDamageBoxToBeam()
    {
        if (_beamFx == null) return;

        // Only supports BoxCollider fitting
        if (!(_damageTrigger is BoxCollider box)) return;

        float maxLen = Mathf.Max(0.01f, _beamFx.beamLength);
        Vector3 origin = _beamFx.transform.position;
        Vector3 dir = _beamFx.transform.forward;

        float distance = maxLen;

        if (_beamFx.beamCollides)
        {
            if (Physics.Raycast(origin, dir, out RaycastHit hit, maxLen))
            {
                // Match SciFiArsenalBeamStatic end offset logic
                distance = Mathf.Clamp(hit.distance - _beamFx.beamEndOffset, 0.01f, maxLen);
            }
        }

        // We assume your box local Z axis points along the beam forward direction.
        // Size/center are in local space.
        Vector3 size = box.size;
        Vector3 center = box.center;

        size.z = distance;
        center.z = distance * 0.5f;

        box.size = size;
        box.center = center;
    }

    private float Now() => _useUnscaledTime ? Time.unscaledTime : Time.time;

    private void DealDamage(Collider other)
    {
        if (!_isOn) return;
        if (other.CompareTag("Player"))
        {
            var stats = other.GetComponent<PlayerStats>();
            if (stats != null)
                stats.TakeDamage(_damage);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!_isOn) return;

        if (_damageTickInterval <= 0f)
        {
            DealDamage(other);
            return;
        }

        // tick mode: allow immediate hit on enter
        _nextDamageTime = 0f;
        TryTickDamage(other);
        Debug.Log("LaserTrap OnTriggerEnter: dealt damage");
    }

    private void OnTriggerStay(Collider other)
    {
        if (!_isOn) return;
        if (_damageTickInterval <= 0f) return;

        TryTickDamage(other);
    }

    private void TryTickDamage(Collider other)
    {
        float now = Now();
        if (now < _nextDamageTime) return;

        _nextDamageTime = now + Mathf.Max(0.01f, _damageTickInterval);
        DealDamage(other);
    }
}

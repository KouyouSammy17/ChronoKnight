using UnityEngine;
using UnityEngine.Events;

[System.Serializable]
public class FloatEvent : UnityEvent<float> { }

public class MomentumManager : MonoBehaviour
{
    public static MomentumManager Instance { get; private set; }

    [Header("Momentum Settings")]
    [SerializeField] private float _maxMomentum = 100f;
    [SerializeField] private float _decayRatePerSecond = 5f;
    [SerializeField] private bool _pauseGain = false;

    [Header("Events")]
    public FloatEvent onMomentumChanged;  // Sends currentMomentum [0–max]
    public UnityEvent onMaxReached;       // Called once when 100% reached

    private float _currentMomentum;

    private MomentumState _currentState = MomentumState.None;
    public MomentumState CurrentState => _currentState;

    public void SetGainPaused(bool paused) => _pauseGain = paused;


    private bool _maxLock = false; // True once 100% is hit (until damage)


    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        _currentMomentum = 0f;
        _currentState = MomentumState.None;
    }

    private void Update()
    {
        if (_currentMomentum <= 0f || _maxLock)
            return;

        if (!IsPlayerMoving())
        {
            _currentMomentum = Mathf.Max(0f, _currentMomentum - _decayRatePerSecond * Time.deltaTime);
            UpdateMomentumEvents();
        }
    }

    public void AddMomentum(float amount)
    {
        if (_pauseGain && amount > 0f) return;
        _currentMomentum = Mathf.Clamp(_currentMomentum + amount, 0f, _maxMomentum);
        UpdateMomentumEvents();
    }

    public void ResetAll()
    {
        _currentMomentum = 0f;
        _maxLock = false;
        _currentState = MomentumState.None;
        UpdateMomentumEvents();
    }

    private void UpdateMomentumEvents()
    {
        onMomentumChanged.Invoke(_currentMomentum);

        MomentumState newState = GetMomentumStateFromPercent((_currentMomentum / _maxMomentum) * 100f);

        if (newState != _currentState)
        {
            if (newState == MomentumState.Max && !_maxLock)
            {
                _maxLock = true;
                onMaxReached?.Invoke();
            }

            _currentState = newState;
        }
    }

    public void BreakMaxLock()
    {
        _maxLock = false;
        UpdateMomentumEvents(); // re-evaluate decay eligibility
    }

    private MomentumState GetMomentumStateFromPercent(float percent)
    {
        if (percent >= 100f) return MomentumState.Max;
        if (percent >= 75f) return MomentumState.Tier3;
        if (percent >= 50f) return MomentumState.Tier2;
        if (percent >= 25f) return MomentumState.Tier1;
        return MomentumState.None;
    }

    private bool IsPlayerMoving()
    {
        if (GameManager.Instance == null) return true;
        var player = GameManager.Instance.GetPlayer();
        return player != null && player.IsMoving;
    }

    public float CurrentMomentum => _currentMomentum;
    public float MaxMomentum => _maxMomentum;
}
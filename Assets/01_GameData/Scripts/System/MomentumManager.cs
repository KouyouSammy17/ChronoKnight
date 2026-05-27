// プレイヤーのモメンタム値を管理し、段階変化イベントを発行するシングルトン
using UnityEngine;
using UnityEngine.Events;

[System.Serializable]
public class FloatEvent : UnityEvent<float> { }
public class MomentumStateEvent : UnityEvent<MomentumState> { }

public class MomentumManager : MonoBehaviour
{
    public static MomentumManager Instance { get; private set; }

    [Header("Momentum Settings")]
    [SerializeField] private float _maxMomentum = 100f;       // モメンタムの最大値
    [SerializeField] private float _decayRatePerSecond = 5f;  // 静止時の毎秒減衰量
    [SerializeField] private bool _pauseGain = false;          // trueの間はモメンタム加算を停止する

    [Header("Events")]
    public FloatEvent onMomentumChanged;  // 現在のモメンタム値[0〜max]を送信する
    public UnityEvent onMaxReached;       // 100%到達時に一度だけ呼ばれる
    public MomentumStateEvent onStateChanged; // 段階・状態が変化したときのみ発火する

    private float _currentMomentum; // 現在のモメンタム値

    private MomentumState _currentState = MomentumState.None; // 現在の段階
    public MomentumState CurrentState => _currentState;

    public void SetGainPaused(bool paused) => _pauseGain = paused;


    private bool _maxLock = false; // 100%到達後、ダメージを受けるまでtrueになる


    private void Awake()
    {
        // シングルトンの初期化：既存のインスタンスがある場合は自身を破棄する
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

        // 重要：イベントがnullにならないよう確実に初期化する
        onMomentumChanged ??= new FloatEvent();
        onMaxReached ??= new UnityEvent();
        onStateChanged ??= new MomentumStateEvent();

        _currentMomentum = 0f;
        _currentState = MomentumState.None;
    }

    private void Update()
    {
        // モメンタムが0またはMax固定中は減衰しない
        if (_currentMomentum <= 0f || _maxLock)
            return;

        // プレイヤーが静止中のみ減衰させる
        if (!IsPlayerMoving())
        {
            _currentMomentum = Mathf.Max(0f, _currentMomentum - _decayRatePerSecond * Time.deltaTime);
            UpdateMomentumEvents();
        }
    }

    public void AddMomentum(float amount)
    {
        // 加算停止フラグが立っている場合は正の加算を無視する
        if (_pauseGain && amount > 0f) return;
        _currentMomentum = Mathf.Clamp(_currentMomentum + amount, 0f, _maxMomentum);
        UpdateMomentumEvents();
    }

    // モメンタムを最大値に対するパーセントで直接セットする（_pauseGainを無視する）
    public void SetMomentumPercent(float percent)
    {
        // 念のため0〜100の範囲に収める
        float clamped = Mathf.Clamp(percent, 0f, 100f);
        // パーセントから実際の値に換算して直接セットする
        _currentMomentum = _maxMomentum * (clamped / 100f);
        UpdateMomentumEvents();
    }

    public void ResetAll()
    {
        // モメンタムと関連フラグをすべて初期状態に戻す
        _currentMomentum = 0f;
        _maxLock = false;
        _currentState = MomentumState.None;
        UpdateMomentumEvents();
    }

    private void UpdateMomentumEvents()
    {
        // 現在値を通知する
        onMomentumChanged.Invoke(_currentMomentum);

        // 現在のパーセントから段階を算出する
        MomentumState newState = GetMomentumStateFromPercent((_currentMomentum / _maxMomentum) * 100f);

        if (newState != _currentState)
        {
            // 状態変化を最初に発火する（リスナーが新しい段階をすぐ受け取れるようにする）
            onStateChanged?.Invoke(newState);

            // 初めてMaxに到達した場合のみonMaxReachedを発火しロックをかける
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
        // Maxロックを解除し、減衰対象に戻す
        _maxLock = false;
        UpdateMomentumEvents(); // 減衰対象かどうかを再評価する
    }

    // パーセント値から対応するMomentumStateを返す
    private MomentumState GetMomentumStateFromPercent(float percent)
    {
        if (percent >= 100f) return MomentumState.Max;
        if (percent >= 75f) return MomentumState.Tier3;
        if (percent >= 50f) return MomentumState.Tier2;
        if (percent >= 25f) return MomentumState.Tier1;
        return MomentumState.None;
    }

    // GameManagerを介してプレイヤーが移動中かどうかを確認する
    private bool IsPlayerMoving()
    {
        if (GameManager.Instance == null) return true;
        var player = GameManager.Instance.GetPlayer();
        return player != null && player.IsMoving;
    }

    public float CurrentMomentum => _currentMomentum; // 現在のモメンタム値（読み取り専用）
    public float MaxMomentum => _maxMomentum;          // モメンタムの上限値（読み取り専用）
}

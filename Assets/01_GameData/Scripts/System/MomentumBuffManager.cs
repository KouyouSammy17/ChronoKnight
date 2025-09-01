using UnityEngine;
using Cysharp.Threading.Tasks;

/// <summary>
/// Handles momentum-based buffs for the player. Listens to the MomentumManager's
/// onMomentumChanged event and applies or removes buffs based on the player's
/// momentum tier. Buffs include damage multiplier, move speed, extra jumps,
/// air dash, and attack speed. This is a local copy of the upstream
/// MomentumBuffsManager.cs from the ChronoKnight project. It is needed for
/// testing Turbo Mode interactions with momentum buffs.
/// </summary>
[RequireComponent(typeof(CombatController), typeof(PlayerController))]
public class MomentumBuffsManager : MonoBehaviour
{
    private CombatController _combat;
    private PlayerController _ctrl;

    // Base move speed used when resetting from buffs
    [SerializeField] private float baseMoveSpeed = 6f;

    private MomentumState _activeState = MomentumState.None;

    private void Start()
    {
        _combat = GetComponent<CombatController>();
        _ctrl = GetComponent<PlayerController>();
    }

    private void OnEnable()
    {
        SubscribeAsync().Forget();
    }

    private void OnDisable()
    {
        var mm = MomentumManager.Instance;
        if (mm != null)
            mm.onMomentumChanged.RemoveListener(OnMomentumChanged);
    }

    private async UniTaskVoid SubscribeAsync()
    {
        // Wait until the MomentumManager singleton is available
        await UniTask.WaitUntil(() => MomentumManager.Instance != null);
        MomentumManager.Instance.onMomentumChanged.AddListener(OnMomentumChanged);
    }

    private void OnMomentumChanged(float _)
    {
        var newState = MomentumManager.Instance.CurrentState;
        if (newState == _activeState) return;

        // Remove old buffs if downgrading (except from Max unless forced)
        if (newState < _activeState)
        {
            RemoveBuffs(_activeState);
        }

        // Apply new buffs if upgrading
        if (newState > _activeState)
        {
            ApplyBuffs(newState);
        }

        _activeState = newState;
    }

    private void ApplyBuffs(MomentumState state)
    {
        switch (state)
        {
            case MomentumState.Tier1:
                _combat.SetDamageMultiplier(1.1f);
                _ctrl.SetMoveSpeed(6.5f);
                break;

            case MomentumState.Tier2:
                _ctrl.EnableExtraJump(1);
                _combat.SetDamageMultiplier(1.25f);
                _ctrl.SetMoveSpeed(7.5f);
                break;

            case MomentumState.Tier3:
                _ctrl.EnableAirDash();
                _ctrl.SetMoveSpeed(9f);
                break;

            case MomentumState.Max:
                _combat.SetDamageMultiplier(1.5f);
                _combat.SetAttackSpeedBuff(1.2f);
                _ctrl.SetMoveSpeed(11f);
                break;
        }
    }

    private void RemoveBuffs(MomentumState state)
    {
        switch (state)
        {
            case MomentumState.Tier1:
                _combat.SetDamageMultiplier(1f);
                _ctrl.SetMoveSpeed(baseMoveSpeed);
                break;

            case MomentumState.Tier2:
                _ctrl.EnableExtraJump(0);
                _combat.SetDamageMultiplier(1f);
                _ctrl.SetMoveSpeed(6f);
                break;

            case MomentumState.Tier3:
                _ctrl.DisableAirDash();
                _ctrl.SetMoveSpeed(7.5f);
                break;

            case MomentumState.Max:
                _combat.SetAttackSpeedBuff(1f);
                _ctrl.SetMoveSpeed(9f);
                break;
        }
    }

    /// <summary>
    /// Called when the player takes damage to forcibly remove the MAX tier buff.
    /// </summary>
    public void RemoveMaxBuffIfActive()
    {
        if (_activeState == MomentumState.Max)
        {
            RemoveBuffs(MomentumState.Max);
            _activeState = MomentumManager.Instance.CurrentState; // update to new valid state
            // Check if Tier2 was also lost
            if (_activeState < MomentumState.Tier2)
                _ctrl.EnableExtraJump(0);
        }
    }
}
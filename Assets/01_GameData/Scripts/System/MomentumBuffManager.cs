// モメンタム段階に応じたバフ（ダメージ・移動速度・追加ジャンプ等）を付与・除去するスクリプト
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
[RequireComponent(typeof(CombatController), typeof(PlayerMotor))]
public class MomentumBuffsManager : MonoBehaviour
{
    private CombatController _combat; // ダメージ倍率・攻撃速度を操作する戦闘コントローラー
    private PlayerMotor _ctrl;        // 移動速度・追加ジャンプ等を操作するプレイヤーモーター

    // Base move speed used when resetting from buffs
    [SerializeField] private float baseMoveSpeed = 6f; // バフなしの基本移動速度

    private MomentumState _activeState = MomentumState.None; // 現在適用中のバフ段階

    private void Start()
    {
        _combat = GetComponent<CombatController>();
        _ctrl = GetComponent<PlayerMotor>();
    }

    private void OnEnable()
    {
        // MomentumManagerが準備できてからイベントを購読する
        SubscribeAsync().Forget();
    }

    private void OnDisable()
    {
        // 無効化時にイベントリスナーを解除してメモリリークを防ぐ
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
        // 段階に変化がなければ何もしない
        if (newState == _activeState) return;

        // Remove old buffs if downgrading (except from Max unless forced)
        // 段階が下がった場合は古いバフを除去する
        if (newState < _activeState)
        {
            RemoveBuffs(_activeState);
        }

        // Apply new buffs if upgrading（段階が上がった場合は新しいバフを付与する）
        if (newState > _activeState)
        {
            ApplyBuffs(newState);
        }

        _activeState = newState; // 現在の段階を更新する
    }

    // 指定した段階のバフを付与する
    private void ApplyBuffs(MomentumState state)
    {
        switch (state)
        {
            case MomentumState.Tier1:
                // 攻撃力10%アップ・移動速度小幅増加
                _combat.SetDamageMultiplier(1.1f);
                _ctrl.SetMoveSpeed(6.5f);
                break;

            case MomentumState.Tier2:
                // 追加ジャンプ解放・攻撃力25%アップ・移動速度中程度増加
                _ctrl.EnableExtraJump(1);
                _combat.SetDamageMultiplier(1.25f);
                _ctrl.SetMoveSpeed(7.5f);
                break;

            case MomentumState.Tier3:
                // 空中ダッシュ解放・移動速度大幅増加
                _ctrl.EnableAirDash();
                _ctrl.SetMoveSpeed(9f);
                break;

            case MomentumState.Max:
                // 攻撃力50%アップ・攻撃速度20%アップ・最高速度に設定
                _combat.SetDamageMultiplier(1.5f);
                // At maximum momentum, increase attack speed by 20% (1.2×).  When
                // Turbo Mode is active, the Turbo attack‑speed buff overrides
                // this momentum buff (see CombatController), so we avoid stacking
                // the two multipliers.  Previously this used a 1.25× bonus, but
                // game design now specifies a 1.2× increase at max momentum.
                _combat.SetAttackSpeedBuff(1.2f);
                _ctrl.SetMoveSpeed(11f);
                break;
        }
    }

    // 指定した段階のバフを除去し、一段階下の値に戻す
    private void RemoveBuffs(MomentumState state)
    {
        switch (state)
        {
            case MomentumState.Tier1:
                // ダメージ倍率と移動速度をベースに戻す
                _combat.SetDamageMultiplier(1f);
                _ctrl.SetMoveSpeed(baseMoveSpeed);
                break;

            case MomentumState.Tier2:
                // 追加ジャンプを無効化し、ダメージ倍率と速度をTier1相当に戻す
                _ctrl.EnableExtraJump(0);
                _combat.SetDamageMultiplier(1f);
                _ctrl.SetMoveSpeed(6f);
                break;

            case MomentumState.Tier3:
                // 空中ダッシュを無効化し、移動速度をTier2相当に戻す
                _ctrl.DisableAirDash();
                _ctrl.SetMoveSpeed(7.5f);
                break;

            case MomentumState.Max:
                // 攻撃速度バフをリセットし、移動速度をTier3相当に戻す
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
        // ダメージを受けたときにMaxバフが残っていれば強制的に除去する
        if (_activeState == MomentumState.Max)
        {
            RemoveBuffs(MomentumState.Max);
            _activeState = MomentumManager.Instance.CurrentState; // update to new valid state
            // Check if Tier2 was also lost（Tier2も失われていれば追加ジャンプを無効化する）
            if (_activeState < MomentumState.Tier2)
                _ctrl.EnableExtraJump(0);
        }
    }
}

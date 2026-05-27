// モメンタム段階に応じたバフ（ダメージ・移動速度・追加ジャンプ等）を付与・除去するスクリプト
using UnityEngine;
using Cysharp.Threading.Tasks;

/// <summary>
/// プレイヤーのモメンタム段階に応じたバフを管理する。
/// MomentumManagerのonMomentumChangedイベントを購読し、段階に応じてバフの付与・除去を行う。
/// バフの種類はダメージ倍率・移動速度・追加ジャンプ・空中ダッシュ・攻撃速度。
/// ChronoKnightプロジェクトのMomentumBuffsManager.csをローカルにコピーしたもので、
/// ターボモードとモメンタムバフの相互作用をテストするために使用する。
/// </summary>
[RequireComponent(typeof(CombatController), typeof(PlayerMotor))]
public class MomentumBuffsManager : MonoBehaviour
{
    private CombatController _combat; // ダメージ倍率・攻撃速度を操作する戦闘コントローラー
    private PlayerMotor _ctrl;        // 移動速度・追加ジャンプ等を操作するプレイヤーモーター

    // バフをリセットする際に使用するベースの移動速度
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
        // MomentumManagerシングルトンが利用可能になるまで待機する
        await UniTask.WaitUntil(() => MomentumManager.Instance != null);
        MomentumManager.Instance.onMomentumChanged.AddListener(OnMomentumChanged);
    }

    private void OnMomentumChanged(float _)
    {
        var newState = MomentumManager.Instance.CurrentState;
        // 段階に変化がなければ何もしない
        if (newState == _activeState) return;

        // 段階が下がった場合は古いバフを除去する（Maxからの降格も強制でなければ含む）
        if (newState < _activeState)
        {
            RemoveBuffs(_activeState);
        }

        // 段階が上がった場合は新しいバフを付与する
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
                // 最大モメンタム時は攻撃速度を20%（1.2倍）アップする。
                // ターボモード中はターボの攻撃速度バフが優先されるため（CombatController参照）、
                // 二重掛けを避ける。以前は1.25倍だったが、現在のゲームデザインでは1.2倍に変更された。
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
    /// ダメージを受けたときに呼ばれ、Maxバフを強制的に除去する。
    /// </summary>
    public void RemoveMaxBuffIfActive()
    {
        // ダメージを受けたときにMaxバフが残っていれば強制的に除去する
        if (_activeState == MomentumState.Max)
        {
            RemoveBuffs(MomentumState.Max);
            _activeState = MomentumManager.Instance.CurrentState; // 現在の有効な段階に更新する
            // Tier2も失われていれば追加ジャンプを無効化する
            if (_activeState < MomentumState.Tier2)
                _ctrl.EnableExtraJump(0);
        }
    }
}

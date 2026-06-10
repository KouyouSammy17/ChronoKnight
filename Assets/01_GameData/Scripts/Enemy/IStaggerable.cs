/// <summary>
/// ダメージを受けてよろめく（スタッガー）能力を持つ敵が実装するインターフェース。
/// EnemyStats.TakeDamage から呼ばれる。
/// </summary>
public interface IStaggerable
{
    /// <summary>ダメージ時によろめき処理を発生させる。</summary>
    void Stagger();
}

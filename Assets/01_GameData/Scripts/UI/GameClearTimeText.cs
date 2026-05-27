// ゲームクリア時にクリアタイムとベストタイムを交互に点滅表示するスクリプト
using TMPro;
using UnityEngine;
using DG.Tweening;

public class GameClearTimeText : MonoBehaviour
{
    [Header("TMPを1つだけ使用（TIME/BESTを交互に表示）")]
    [SerializeField] private TMP_Text _timeText; // ここに単一のTMPを割り当てる

    [Header("Blink Settings")]
    [SerializeField] private float _fadeIn = 0.12f;   // フェードイン時間
    [SerializeField] private float _hold = 0.90f;     // 表示を維持する時間
    [SerializeField] private float _fadeOut = 0.12f;  // フェードアウト時間
    [SerializeField] private float _gap = 0.10f;      // 次の表示までの間隔

    [Header("Format")]
    [SerializeField] private string _timePrefix = "TIME"; // クリアタイム表示のプレフィックス
    [SerializeField] private string _bestPrefix = "BEST"; // ベストタイム表示のプレフィックス

    private Sequence _seq; // 点滅アニメーションのSequence

    /// <summary>クリア画面が開いたときに呼び出す。TIME → BEST → TIME... と無限ループする。</summary>
    public void PlayBlink(float runTime, float bestTime)
    {
        if (_timeText == null) return;

        string timeStr = $"{_timePrefix}  {TimeAttackManager.Format(runTime)}"; // クリアタイムの文字列を生成

        string bestStr;
        bool hasBest = bestTime < float.MaxValue * 0.5f; // 有効なベストタイムが存在するか確認
        bestStr = hasBest ? $"{_bestPrefix}  {TimeAttackManager.Format(bestTime)}"
                          : $"{_bestPrefix}  --:--.--"; // ベストタイムがなければダッシュで表示

        _seq?.Kill(); // 既存のSequenceを停止してから新しいものを開始

        // 表示状態から開始する
        _timeText.alpha = 1f;

        _seq = DOTween.Sequence()
            .SetUpdate(true) // 非スケール時間
            .SetLink(gameObject, LinkBehaviour.KillOnDestroy);

        // 無限ループ
        _seq.AppendCallback(() => _timeText.text = timeStr); // クリアタイムのテキストを設定
        _seq.Append(_timeText.DOFade(1f, _fadeIn));          // フェードイン
        _seq.AppendInterval(_hold);                           // 表示を維持
        _seq.Append(_timeText.DOFade(0f, _fadeOut));          // フェードアウト
        _seq.AppendInterval(_gap);                            // 次の表示まで待機

        _seq.AppendCallback(() => _timeText.text = bestStr); // ベストタイムのテキストに切り替え
        _seq.Append(_timeText.DOFade(1f, _fadeIn));          // フェードイン
        _seq.AppendInterval(_hold);                           // 表示を維持
        _seq.Append(_timeText.DOFade(0f, _fadeOut));          // フェードアウト
        _seq.AppendInterval(_gap);

        _seq.SetLoops(-1, LoopType.Restart); // クリアタイムとベストタイムを無限ループで交互表示
        _seq.Play();
    }

    public void StopBlink()
    {
        _seq?.Kill(); // 点滅アニメーションを停止
        _seq = null;

        if (_timeText != null)
        {
            _timeText.alpha = 1f; // 停止後はテキストを不透明に戻す
        }
    }

    /// <summary>ゲームクリアUIが表示されたときに点滅アニメーションを開始するために呼び出す</summary>
    public void OnGameClearUIShown(float runTime, float bestTime)
    {
        PlayBlink(runTime, bestTime); // クリアUI表示時に点滅を開始
    }

    private void OnDisable()
    {
        // 無効化されたオブジェクトでTweenが動き続けないようにする
        StopBlink(); // 無効化時に点滅を確実に停止する
    }
}

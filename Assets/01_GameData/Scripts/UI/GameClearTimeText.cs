using TMPro;
using UnityEngine;
using DG.Tweening;

public class GameClearTimeText : MonoBehaviour
{
    [Header("One TMP only (alternates TIME/BEST)")]
    [SerializeField] private TMP_Text _timeText; // put your single TMP here

    [Header("Blink Settings")]
    [SerializeField] private float _fadeIn = 0.12f;
    [SerializeField] private float _hold = 0.90f;
    [SerializeField] private float _fadeOut = 0.12f;
    [SerializeField] private float _gap = 0.10f;

    [Header("Format")]
    [SerializeField] private string _timePrefix = "TIME";
    [SerializeField] private string _bestPrefix = "BEST";

    private Sequence _seq;

    /// <summary>Call on clear screen open. This will loop TIME -> BEST -> TIME...</summary>
    public void PlayBlink(float runTime, float bestTime)
    {
        if (_timeText == null) return;

        string timeStr = $"{_timePrefix}  {TimeAttackManager.Format(runTime)}";
        string bestStr;

        bool hasBest = bestTime < float.MaxValue * 0.5f;
        bestStr = hasBest ? $"{_bestPrefix}  {TimeAttackManager.Format(bestTime)}"
                          : $"{_bestPrefix}  --:--.--";

        _seq?.Kill();

        // start visible
        _timeText.alpha = 1f;

        _seq = DOTween.Sequence()
            .SetUpdate(true) // unscaled
            .SetLink(gameObject, LinkBehaviour.KillOnDestroy);

        // loop forever
        _seq.AppendCallback(() => _timeText.text = timeStr);
        _seq.Append(_timeText.DOFade(1f, _fadeIn));
        _seq.AppendInterval(_hold);
        _seq.Append(_timeText.DOFade(0f, _fadeOut));
        _seq.AppendInterval(_gap);

        _seq.AppendCallback(() => _timeText.text = bestStr);
        _seq.Append(_timeText.DOFade(1f, _fadeIn));
        _seq.AppendInterval(_hold);
        _seq.Append(_timeText.DOFade(0f, _fadeOut));
        _seq.AppendInterval(_gap);

        _seq.SetLoops(-1, LoopType.Restart);
        _seq.Play();
    }

    public void StopBlink()
    {
        _seq?.Kill();
        _seq = null;

        if (_timeText != null)
        {
            _timeText.alpha = 1f;
        }
    }

    /// <summary>Call this when game clear UI is displayed to start the blink animation</summary>
    public void OnGameClearUIShown(float runTime, float bestTime)
    {
        PlayBlink(runTime, bestTime);
    }

    private void OnDisable()
    {
        // avoid tweens running on disabled object
        StopBlink();
    }
}

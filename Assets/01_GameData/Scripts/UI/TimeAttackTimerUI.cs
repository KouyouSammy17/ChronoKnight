using TMPro;
using UnityEngine;

public class TimeAttackTimerUI : MonoBehaviour
{
    [SerializeField] private TMP_Text _text;
    [SerializeField] private GameObject _root; // optional (assign self or parent)

    private void Awake()
    {
        if (_root == null) _root = gameObject;
        if (_text == null) _text = GetComponentInChildren<TMP_Text>(true);
    }

    private void Update()
    {
        var ta = TimeAttackManager.Instance;
        // Show if TimeAttack is enabled OR if it's running (accounts for scene transitions)
        bool show = ta != null && (ta.Enabled || ta.IsRunning);

        if (_root != null)
        {
            if (_root.activeSelf != show)
            {
                _root.SetActive(show);
            }
        }

        if (!show || _text == null) return;

        _text.text = TimeAttackManager.Format(ta.Elapsed);
    }

    /// <summary>Public method to force-show the timer (called by TutorialManager)</summary>
    public void EnsureVisible()
    {
        if (_root != null)
            _root.SetActive(true);
    }
}

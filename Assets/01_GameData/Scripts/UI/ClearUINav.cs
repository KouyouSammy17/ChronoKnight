using UnityEngine;
using UnityEngine.UI;

public class ClearUINav : MonoBehaviour
{
    [Header("Left is always Restart (or Back)")]
    [SerializeField] private Selectable _leftBtn;   // Restart button

    [Header("Right is dynamic (Next / Title)")]
    [SerializeField] private Selectable _rightBtn;  // assign default (optional)

    [Tooltip("left of first = last, right of last = first")]
    [SerializeField] private bool _wrap = true;

    private void Awake()
    {
        Apply();
    }

    /// <summary>
    /// Call this before focusing first selected.
    /// </summary>
    public void Configure(Selectable leftRestart, Selectable rightAction, bool wrap)
    {
        _leftBtn = leftRestart;
        _rightBtn = rightAction;
        _wrap = wrap;
        Apply();
    }

    public void Apply()
    {
        if (!_leftBtn || !_rightBtn) return;

        // Explicit horizontal links
        LinkLR(_leftBtn, left: _wrap ? _rightBtn : null, right: _rightBtn);
        LinkLR(_rightBtn, left: _leftBtn, right: _wrap ? _leftBtn : null);

        // Disable up/down to avoid vertical drift
        DisableUD(_leftBtn);
        DisableUD(_rightBtn);
    }

    private static void LinkLR(Selectable s, Selectable left, Selectable right)
    {
        var nav = s.navigation;
        nav.mode = Navigation.Mode.Explicit;
        nav.selectOnLeft = left;
        nav.selectOnRight = right;
        nav.selectOnUp = null;
        nav.selectOnDown = null;
        s.navigation = nav;
    }

    private static void DisableUD(Selectable s)
    {
        var nav = s.navigation;
        nav.selectOnUp = null;
        nav.selectOnDown = null;
        s.navigation = nav;
    }
}

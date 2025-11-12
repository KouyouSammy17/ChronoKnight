using UnityEngine;
using UnityEngine.UI;

public class PauseMenuNav : MonoBehaviour
{
    [Header("Order left Å® right")]
    public Selectable settingsBtn;
    public Selectable playBtn;
    public Selectable restartBtn;
    public Selectable exitBtn;

    public bool wrap = true; // left of first = last, right of last = first

    private void Awake()
    {
        if (!settingsBtn || !playBtn || !restartBtn || !exitBtn) return;

        // Explicit horizontal links
        LinkLR(settingsBtn, left: wrap ? exitBtn : null, right: playBtn);
        LinkLR(playBtn, left: settingsBtn, right: restartBtn);
        LinkLR(restartBtn, left: playBtn, right: exitBtn);
        LinkLR(exitBtn, left: restartBtn, right: wrap ? settingsBtn : null);

        // Disable up/down to avoid vertical drift
        DisableUD(settingsBtn);
        DisableUD(playBtn);
        DisableUD(restartBtn);
        DisableUD(exitBtn);
    }

    private static void LinkLR(Selectable s, Selectable left, Selectable right)
    {
        var nav = s.navigation;
        nav.mode = Navigation.Mode.Explicit;
        nav.selectOnLeft = left;
        nav.selectOnRight = right;
        // Keep vertical empty
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

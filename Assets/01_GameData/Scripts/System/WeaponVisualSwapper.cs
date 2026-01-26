using UnityEngine;

public class WeaponVisualSwapper : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private GameObject _swordInSheath;
    [SerializeField] private GameObject _swordInHand;

    [Header("State")]
    public bool IsDrawn { get; private set; }

    void Awake()
    {
        SetDrawn(true);
    }

    public void SetDrawn(bool drawn)
    {
        IsDrawn = drawn;
        if (_swordInSheath) _swordInSheath.SetActive(!drawn);
        if (_swordInHand) _swordInHand.SetActive(drawn);
    }

    // Animation Event
    public void AnimEvent_UnsheathSwap()
    {
        SetDrawn(true);
    }

    // Animation Event
    public void AnimEvent_SheathSwap()
    {
        SetDrawn(false);
    }
}

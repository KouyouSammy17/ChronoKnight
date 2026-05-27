// 鞘と手持ちの剣オブジェクトを切り替えて武器の抜刀・収刀を表現するスクリプト
using UnityEngine;

public class WeaponVisualSwapper : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private GameObject _swordInSheath; // 収刀状態の剣オブジェクト
    [SerializeField] private GameObject _swordInHand;   // 抜刀状態の剣オブジェクト

    [Header("State")]
    public bool IsDrawn { get; private set; } // 現在抜刀中かどうか

    void Awake()
    {
        // 起動時は抜刀状態にしておく
        SetDrawn(true);
    }

    public void SetDrawn(bool drawn)
    {
        IsDrawn = drawn;
        // 抜刀状態に応じて各オブジェクトの表示を切り替える
        if (_swordInSheath) _swordInSheath.SetActive(!drawn); // 抜刀中は鞘の剣を非表示
        if (_swordInHand) _swordInHand.SetActive(drawn);      // 抜刀中は手持ちの剣を表示
    }

    // アニメーションイベントから呼ばれる：抜刀の瞬間に剣を切り替える
    public void AnimEvent_UnsheathSwap()
    {
        SetDrawn(true);
    }

    // アニメーションイベントから呼ばれる：収刀の瞬間に剣を切り替える
    public void AnimEvent_SheathSwap()
    {
        SetDrawn(false);
    }
}

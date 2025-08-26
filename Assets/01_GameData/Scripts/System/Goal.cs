using UnityEngine;

/// <summary>
/// Attach this to the Goal GameObject (which should have a Trigger collider).
///  When the player enters, it calls GameManager.Instance.WinLevel().
/// </summary>
public class Goal : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        // Make sure the playerÅfs collider has the tag ÅgPlayerÅh (or whatever tag you use)
        if (other.CompareTag("Player"))
        {
            GameManager.Instance.WinLevel();
        }
    }
}

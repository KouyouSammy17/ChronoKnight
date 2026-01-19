using Unity.AppUI.UI;
using UnityEngine;

public class DamageZone : MonoBehaviour
{
    [SerializeField] private int _damage = 10;
    private void OnTriggerEnter(Collider other)
    {

        if (other.CompareTag("Player"))
        {
            var stats = other.GetComponent<PlayerStats>();
            if (stats != null)
                // pass the trap world position so the player can face/knockback correctly
                stats.TakeEnemyDamage(_damage, transform.position);
        }
    }
}

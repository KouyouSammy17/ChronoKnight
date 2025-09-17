using UnityEngine;

public class SuperFastPlatform : MonoBehaviour
{
    [SerializeField] private Transform pointA;
    [SerializeField] private Transform pointB;
    [SerializeField] private float moveSpeed = 50f; // super fast

    private Vector3 _target;

    void Start()
    {
        if (pointA == null || pointB == null)
        {
            Debug.LogError("Need 2 points for platform!");
            enabled = false;
            return;
        }
        _target = pointB.position;
    }

    void Update()
    {
        // Movement depends on timeScale Å® slows down in Turbo Mode
        transform.position = Vector3.MoveTowards(
            transform.position,
            _target,
            moveSpeed * Time.deltaTime
        );

        if (Vector3.Distance(transform.position, _target) < 0.05f)
            _target = (_target == pointA.position) ? pointB.position : pointA.position;
    }

    private void OnDrawGizmos()
    {
        if (pointA && pointB)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(pointA.position, pointB.position);
        }
    }
    void OnCollisionEnter(Collision col)
    {
        if (col.gameObject.CompareTag("Player"))
            col.transform.SetParent(transform);
    }
    void OnCollisionExit(Collision col)
    {
        if (col.gameObject.CompareTag("Player"))
            col.transform.SetParent(null);
    }

}

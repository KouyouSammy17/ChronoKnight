using UnityEngine;

public class UIFollowPlayer : MonoBehaviour
{
    [SerializeField] private Transform _target;
    [SerializeField] private Vector3 _offset = new Vector3(0f, 2f, 0f);
    [SerializeField] private float _smoothSpeed = 10f;

    private void LateUpdate()
    {
        if (_target == null) return;

        Vector3 targetPos = _target.position + _offset;
        transform.position = Vector3.Lerp(transform.position, targetPos, _smoothSpeed * Time.deltaTime);
        transform.rotation = Quaternion.LookRotation(Camera.main.transform.forward); // always face camera
    }

    public void SetTarget(Transform newTarget)
    {
        _target = newTarget;
    }
}

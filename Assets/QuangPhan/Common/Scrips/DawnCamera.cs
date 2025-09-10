using UnityEngine;

public class DawnCamera : MonoBehaviour
{
    public Transform target;
    public float smoothSpeed = 0.125f;
    public Vector3 offset = new Vector3(0, 1.2f, -1.2f);

    // Variables for camera control
    public float rotateSpeed = 5.0f;
    public float rotateMinDistance = 0.5f;
    public float maxZoom = 4f; // Set the maximum zoom distance to 4
    public float minZoom = 0.1f; // Set the minimum zoom distance to 0.1
    public float zoomSpeed = 5f;
    public float minAngle = 10f;
    public float maxAngle = 80f;

    // Internal variables for tracking camera position and rotation
    private float currentRotationX = 0.0f;
    private float currentRotationY = 0.0f;
    private float currentDistance = 2.0f;
    private float currentAngle = 0.0f;

    private bool isRotating;

    private void Start()
    {
        // Set the camera to look at the character's head by default
        Vector3 lookAtPosition = target.position + new Vector3(0, 1.2f, 0);
        transform.LookAt(lookAtPosition);
    }

    private void LateUpdate()
    {
        // Handle zooming in and out with mouse scroll wheel
        float zoomDelta = Input.GetAxis("Mouse ScrollWheel") * zoomSpeed;
        currentDistance -= zoomDelta;

        // Clamp the distance to avoid getting too close or too far
        currentDistance = Mathf.Clamp(currentDistance, rotateMinDistance, maxZoom);

        // Handle rotation around character with left mouse button
        if (Input.GetMouseButtonDown(0))
        {
            isRotating = true;
        }

        if (Input.GetMouseButtonUp(0))
        {
            isRotating = false;
        }

        if (isRotating)
        {
            float rotateDeltaX = Input.GetAxis("Mouse X") * rotateSpeed;
            float rotateDeltaY = Input.GetAxis("Mouse Y") * rotateSpeed;

            currentRotationX += rotateDeltaX;
            currentRotationY -= rotateDeltaY;

            currentRotationY = Mathf.Clamp(currentRotationY, -80f, 80f);
        }

        // Calculate the desired position and rotation of the camera
        Quaternion rotation = Quaternion.Euler(currentRotationY, currentRotationX, currentAngle);
        Vector3 desiredPosition = target.position + rotation * offset - currentDistance * transform.forward;

        // Smoothly move the camera to the desired position
        Vector3 smoothedPosition = Vector3.Lerp(transform.position, desiredPosition, smoothSpeed);
        transform.position = smoothedPosition;

        // Make the camera always look at the character's head
        Vector3 lookAtPosition = target.position + new Vector3(0, 1.2f, 0);
        transform.LookAt(lookAtPosition);
    }
}
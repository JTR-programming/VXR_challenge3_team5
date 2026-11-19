using UnityEngine;

public class copyRotation : MonoBehaviour
{
    public Transform cameraTransform;
    private Vector3 initialOffset;
    private float initialCameraY;

    void Start()
    {
        if (cameraTransform == null && Camera.main != null)
        {
            cameraTransform = Camera.main.transform;
        }

        if (cameraTransform != null)
        {
            // Record the object's starting offset and the camera's starting Y angle
            initialOffset = transform.position - cameraTransform.position;
            initialCameraY = cameraTransform.eulerAngles.y;
        }
    }

    void LateUpdate()
    {
        if (cameraTransform == null) return;

        // Calculate how many degrees the camera has turned on Y since Start
        float yDelta = Mathf.DeltaAngle(initialCameraY, cameraTransform.eulerAngles.y);
        Quaternion deltaRotation = Quaternion.Euler(0f, yDelta, 0f);

        // Rotate the starting offset around the camera
        transform.position = cameraTransform.position + (deltaRotation * initialOffset);

        // Also match the camera's Y rotation
        Vector3 currentEuler = transform.eulerAngles;
        transform.rotation = Quaternion.Euler(currentEuler.x, cameraTransform.eulerAngles.y, currentEuler.z);
    }
}

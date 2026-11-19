
using UnityEngine;

public class QuadrupedBodyFollow : MonoBehaviour
{
    public Transform headCamera;

    void LateUpdate()
    {
        if (headCamera == null) return;

        // 1. Lock position directly under the camera on the floor
        Vector3 targetPos = headCamera.position;
        targetPos.y = transform.parent != null ? transform.parent.position.y : headCamera.root.position.y;
        transform.position = targetPos;

        // 2. Lock Y-rotation directly to the camera's horizontal facing direction (zero lag)
        Vector3 flatForward = Vector3.ProjectOnPlane(headCamera.forward, Vector3.up).normalized;
        if (flatForward.sqrMagnitude > 0.001f)
        {
            transform.rotation = Quaternion.LookRotation(flatForward, Vector3.up);
        }
    }
}
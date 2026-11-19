using UnityEngine;

public class ScaleTargetMovement : MonoBehaviour
{
    [Range(0.05f, 1f)]
    public float movementScale = 0.25f;

    private Transform controller;
    private Vector3 startControllerLocalPos;
    private Vector3 startTargetLocalPos;

    void Start()
    {
        controller = transform.parent;
        startControllerLocalPos = controller.localPosition;
        startTargetLocalPos = transform.localPosition;
    }

    void Update()
    {
        // Measure how much the controller moved from its starting spot
        Vector3 controllerDelta = controller.localPosition - startControllerLocalPos;

        // Apply only a percentage of that movement to the target
        transform.localPosition = startTargetLocalPos + (controllerDelta * movementScale);
    }
}
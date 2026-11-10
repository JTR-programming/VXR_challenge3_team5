
using UnityEngine;

public class CatPawMovement : MonoBehaviour
{
    [Header("VR References")]
    public Transform xrOrigin;
    public Transform headset;
    public Transform leftPaw;
    public Transform rightPaw;

    [Header("Movement Settings")]
    public float stepDistance = 0.2f;
    public float liftHeight = 0.12f;
    public float landingHeight = 0.04f;

    private float leftRestHeight;
    private float rightRestHeight;

    private bool leftRaised = false;
    private bool rightRaised = false;

    // -1 = neither paw has stepped yet
    //  0 = left paw
    //  1 = right paw
    private int lastPaw = -1;

    void Start()
    {
        if (!xrOrigin || !headset || !leftPaw || !rightPaw)
        {
            Debug.LogError("Missing XR references!");
            enabled = false;
            return;
        }

        leftRestHeight = leftPaw.position.y - headset.position.y;
        rightRestHeight = rightPaw.position.y - headset.position.y;
    }

    void Update()
    {
        CheckPaw(leftPaw, ref leftRaised, leftRestHeight, 0);
        CheckPaw(rightPaw, ref rightRaised, rightRestHeight, 1);
    }

    void CheckPaw(
        Transform paw,
        ref bool raised,
        float restHeight,
        int pawIndex)
    {
        float currentHeight = paw.position.y - headset.position.y;
        float difference = currentHeight - restHeight;

        if (!raised && difference >= liftHeight)
        {
            raised = true;
            Debug.Log(pawIndex == 0 ? "Left paw raised" : "Right paw raised");
        }

        if (raised && difference <= landingHeight)
        {
            raised = false;

            // Walking must alternate between paws.
            if (pawIndex != lastPaw)
            {
                TakeStep(pawIndex);
            }
        }
    }

    void TakeStep(int pawIndex)
    {
        Vector3 direction = headset.forward;
        direction.y = 0f;
        direction.Normalize();

        xrOrigin.position += direction * stepDistance;

        lastPaw = pawIndex;

        Debug.Log(pawIndex == 0 ? "LEFT STEP!" : "RIGHT STEP!");
    }
}

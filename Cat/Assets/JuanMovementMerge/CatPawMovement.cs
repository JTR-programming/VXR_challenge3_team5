
using UnityEngine;

public class CatPawMovement : MonoBehaviour
{
    [Header("VR References")]
    public Transform xrOrigin;
    public Transform headset;
    public Transform leftPaw;
    public Transform rightPaw;

    [Header("Walking")]
    public float stepDistance = 0.25f;
    public float movementSpeed = 1.5f;
    public float forwardReach = 0.05f;
    public float backwardDrag = 0.06f;

    [Header("Jumping")]
    public float jumpHeight = 0.65f;
    public float jumpLiftDistance = 0.20f;
    public float jumpResetDistance = 0.06f;
    public float jumpCooldown = 0.5f;
    public float jumpSyncWindow = 0.45f;

    [Header("Ground Movement")]
    public float gravity = -9.81f;
    public float stepOffset = 0.25f;

    [Header("Debugging")]
    public bool showDebugLogs = true;
    public bool showTrackingLogs = false;

    private CharacterController controller;

    private class PawState
    {
        public Vector3 previousPosition;

        public float restingHeight;
        public bool raisedForJump;
        public float raiseTime = -999f;

        public bool reachedForward;
        public float forwardDistance;
        public float backwardDistance;
    }

    private PawState left = new PawState();
    private PawState right = new PawState();

    private bool rightPawTurn = true;
    private bool jumpArmed = true;

    private float lastJumpTime = -999f;
    private float verticalVelocity = 0f;
    private float nextDebugTime = 0f;

    private Vector3 remainingMovement;

    void Start()
    {
        if (!xrOrigin || !headset ||
            !leftPaw || !rightPaw)
        {
            Debug.LogError("Missing VR references!");
            enabled = false;
            return;
        }

        controller =
            xrOrigin.GetComponent<CharacterController>();

        if (controller == null || !controller.enabled)
        {
            Debug.LogError(
                "XR Origin requires an enabled CharacterController!"
            );

            enabled = false;
            return;
        }

        controller.stepOffset = Mathf.Min(
            stepOffset,
            controller.height
        );

        controller.slopeLimit = 60f;

        InitializePaw(leftPaw, left);
        InitializePaw(rightPaw, right);

        if (showDebugLogs)
        {
            Debug.Log(
                "CAT MOVEMENT READY - RIGHT PAW FIRST"
            );
        }
    }

    void InitializePaw(Transform paw, PawState state)
    {
        Vector3 position = GetLocalPosition(paw);

        state.previousPosition = position;
        state.restingHeight = position.y;

        ResetGesture(state);
    }

    Vector3 GetLocalPosition(Transform paw)
    {
        // Removes XR Origin translation from paw tracking.
        return xrOrigin.InverseTransformPoint(paw.position);
    }

    Vector3 GetLocalForward()
    {
        Vector3 forward =
            xrOrigin.InverseTransformDirection(
                headset.forward
            );

        forward.y = 0f;

        if (forward.sqrMagnitude < 0.001f)
            return Vector3.forward;

        return forward.normalized;
    }

    void Update()
    {
        // Read controller positions every frame.
        Vector3 leftPosition =
            GetLocalPosition(leftPaw);

        Vector3 rightPosition =
            GetLocalPosition(rightPaw);

        Vector3 forward = GetLocalForward();

        // Controller motion since the previous frame.
        Vector3 leftDelta =
            leftPosition - left.previousPosition;

        Vector3 rightDelta =
            rightPosition - right.previousPosition;

        // Detect vertical movement for jumping.
        UpdateJumpPaw(leftPosition, left);
        UpdateJumpPaw(rightPosition, right);

        bool jumped = CheckJump();

        // Walking gestures should not be counted
        // while airborne or triggering a jump.
        if (!jumped && controller.isGrounded)
        {
            if (rightPawTurn)
            {
                CheckPawDrag(rightDelta, right, true, forward);
            }
            else
            {
                CheckPawDrag(leftDelta, left, false, forward);
            }
        }

        // Optional tracking diagnostics.
        if (showTrackingLogs &&
            Time.time >= nextDebugTime)
        {
            Debug.Log(
                $"Right local: {rightPosition} | " +
                $"Left local: {leftPosition} | " +
                $"Grounded: {controller.isGrounded} | " +
                $"Next: {(rightPawTurn ? "RIGHT" : "LEFT")}"
            );

            nextDebugTime = Time.time + 0.5f;
        }

        // Save positions for the next frame.
        left.previousPosition = leftPosition;
        right.previousPosition = rightPosition;

        ApplyMovement();
    }

    void CheckPawDrag(
        Vector3 delta,
        PawState state,
        bool isRightPaw,
        Vector3 forward)
    {
        // Project controller motion onto the
        // direction the player is facing.
        float movement = Vector3.Dot(
            delta,
            forward
        );

        // PHASE 1: Move controller forward.
        if (!state.reachedForward)
        {
            // Accumulate forward movement.
            // Backward motion resets the reach progress.
            state.forwardDistance = Mathf.Max(
                0f,
                state.forwardDistance + movement
            );

            if (state.forwardDistance >= forwardReach)
            {
                state.reachedForward = true;
                state.backwardDistance = 0f;

                if (showDebugLogs)
                {
                    Debug.Log(
                        isRightPaw
                        ? "RIGHT PAW FORWARD"
                        : "LEFT PAW FORWARD"
                    );
                }
            }

            return;
        }

        // PHASE 2: Drag controller backward.
        if (movement < 0f)
        {
            state.backwardDistance += -movement;
        }
        else
        {
            // Forward motion reduces accumulated drag.
            state.backwardDistance = Mathf.Max(
                0f,
                state.backwardDistance - movement
            );
        }

        if (state.backwardDistance >= backwardDrag)
        {
            CompleteStep(isRightPaw);
        }
    }

    void CompleteStep(bool isRightPaw)
    {
        Vector3 direction = headset.forward;
        direction.y = 0f;

        if (direction.sqrMagnitude < 0.001f)
            return;

        direction.Normalize();

        // Queue a short forward step.
        remainingMovement +=
            direction * stepDistance;

        // Alternate between right and left.
        rightPawTurn = !isRightPaw;

        // Clear the completed gesture and
        // prepare the opposite paw.
        ResetGesture(left);
        ResetGesture(right);

        if (showDebugLogs)
        {
            Debug.Log(
                isRightPaw
                ? "RIGHT STEP! NEXT: LEFT"
                : "LEFT STEP! NEXT: RIGHT"
            );
        }
    }

    void ResetGesture(PawState state)
    {
        state.reachedForward = false;
        state.forwardDistance = 0f;
        state.backwardDistance = 0f;
    }

    void UpdateJumpPaw(
        Vector3 position,
        PawState state)
    {
        float height = position.y;

        // Keep track of the lowest controller height.
        if (height < state.restingHeight)
        {
            state.restingHeight = height;
        }

        float lift =
            height - state.restingHeight;

        if (!state.raisedForJump &&
            lift >= jumpLiftDistance)
        {
            state.raisedForJump = true;
            state.raiseTime = Time.time;

            if (showDebugLogs)
                Debug.Log("PAW RAISED FOR JUMP");
        }

        if (state.raisedForJump &&
            lift <= jumpResetDistance)
        {
            state.raisedForJump = false;
            state.raiseTime = -999f;
        }
    }

    bool CheckJump()
    {
        // Both hands must return down
        // to rearm the next jump.
        if (!left.raisedForJump &&
            !right.raisedForJump)
        {
            jumpArmed = true;
        }

        if (!jumpArmed)
            return false;

        if (!controller.isGrounded)
            return false;

        if (!left.raisedForJump ||
            !right.raisedForJump)
            return false;

        // Require both paws to be lifted
        // within a short time of one another.
        float timeDifference = Mathf.Abs(
            left.raiseTime - right.raiseTime
        );

        if (timeDifference > jumpSyncWindow)
            return false;

        if (Time.time - lastJumpTime < jumpCooldown)
            return false;

        // Jump velocity from desired jump height.
        verticalVelocity = Mathf.Sqrt(
            jumpHeight * -2f * gravity
        );

        lastJumpTime = Time.time;
        jumpArmed = false;

        // Prevent walking gestures from completing
        // during the jump.
        ResetGesture(left);
        ResetGesture(right);

        remainingMovement = Vector3.zero;

        if (showDebugLogs)
        {
            Debug.Log("BOTH PAWS UP - CAT JUMP!");
        }

        return true;
    }

    void ApplyMovement()
    {
        if (controller == null || !controller.enabled)
            return;

        // Smoothly consume queued walking distance.
        Vector3 stepMovement = Vector3.MoveTowards(
            Vector3.zero,
            remainingMovement,
            movementSpeed * Time.deltaTime
        );

        remainingMovement -= stepMovement;

        // Reset falling velocity on the ground.
        if (controller.isGrounded &&
            verticalVelocity < 0f)
        {
            verticalVelocity = -2f;
        }

        // Apply gravity every frame.
        verticalVelocity +=
            gravity * Time.deltaTime;

        Vector3 movement =
            stepMovement +
            Vector3.up *
            verticalVelocity *
            Time.deltaTime;

        CollisionFlags flags =
            controller.Move(movement);

        // Stop upward motion if we hit a ceiling.
        if ((flags & CollisionFlags.Above) != 0 &&
            verticalVelocity > 0f)
        {
            verticalVelocity = 0f;
        }
    }
}

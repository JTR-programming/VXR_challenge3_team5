
using UnityEngine;

public class CatPawMovement : MonoBehaviour
{
    [Header("VR References")]
    public Transform xrOrigin;
    public Transform headset;
    public Transform leftPaw;
    public Transform rightPaw;

    [Header("Walking")]
    public float stepDistance = 0.20f;
    public float movementSpeed = 1.5f;

    // NEW: Forward and backward walking gestures.
    public float forwardReach = 0.08f;
    public float backwardDrag = 0.06f;

    [Header("Jumping")]
    public float jumpHeight = 0.65f;
    public float jumpLiftDistance = 0.16f;
    public float jumpResetDistance = 0.08f;
    public float jumpSyncWindow = 0.40f;
    public float jumpCooldown = 0.50f;

    [Header("Forward Lunge")]
    public float lungeDistance = 0.85f;
    public float lungeSpeed = 2.5f;

    [Header("Carpet / Small Obstacles")]
    public float stepOffset = 0.25f;
    public float slopeLimit = 60f;

    [Header("Debugging")]
    public bool showDebugLogs = true;
    public bool showTrackingLogs = false;

    private CharacterController controller;

    // WALKING STATE
    private class PawState
    {
        public bool initialized = false;
        public bool reachedForward = false;

        public Vector3 previousPosition;

        public float forwardMovement = 0f;
        public float backwardMovement = 0f;
    }

    private PawState left = new PawState();
    private PawState right = new PawState();

    // Right paw starts first.
    private bool rightPawTurn = true;

    private Vector3 remainingMovement;
    private Vector3 remainingLunge;

    private float verticalVelocity;
    private const float gravity = -9.81f;

    // ORIGINAL JUMPING STATE
    private class JumpPawState
    {
        public bool initialized = false;
        public bool raised = false;

        public float lowestHeight;
        public float peakHeight;
        public float raiseTime;
    }

    private JumpPawState leftJump = new JumpPawState();
    private JumpPawState rightJump = new JumpPawState();

    private bool jumpLocked = false;
    private float lastJumpTime = -999f;

    private float nextTrackingLog = 0f;

    void Start()
    {
        if (!xrOrigin || !headset ||
            !leftPaw || !rightPaw)
        {
            Debug.LogError("Missing VR references!");
            enabled = false;
            return;
        }

        controller = GetComponent<CharacterController>();

        if (controller == null || !controller.enabled)
        {
            Debug.LogError(
                "XR Origin needs an enabled CharacterController!"
            );

            enabled = false;
            return;
        }

        // Keep the carpet traversal settings.
        controller.stepOffset = Mathf.Clamp(
            stepOffset,
            0f,
            Mathf.Max(0f, controller.height - 0.01f)
        );

        controller.slopeLimit = slopeLimit;

        // Initialize walking positions.
        InitializeWalkingPaw(leftPaw, left);
        InitializeWalkingPaw(rightPaw, right);

        Debug.Log(
            "CAT MOVEMENT READY - RIGHT PAW FIRST"
        );
    }

    void Update()
    {
        // Check for jumping first.
        bool startedJump = CheckJump();

        // Always update both controller positions.
        // This prevents outdated movement values.
        Vector3 rightPosition =
            GetLocalPosition(rightPaw);

        Vector3 leftPosition =
            GetLocalPosition(leftPaw);

        Vector3 rightDelta =
            rightPosition - right.previousPosition;

        Vector3 leftDelta =
            leftPosition - left.previousPosition;

        // Walking is only paused during jumping.
        if (!jumpLocked && !startedJump)
        {
            if (rightPawTurn)
            {
                CheckPaw(
                    rightDelta,
                    right,
                    true
                );
            }
            else
            {
                CheckPaw(
                    leftDelta,
                    left,
                    false
                );
            }
        }

        // Save positions for the next frame.
        right.previousPosition = rightPosition;
        left.previousPosition = leftPosition;

        // Optional movement diagnostics.
        if (showTrackingLogs &&
            Time.time >= nextTrackingLog)
        {
            Debug.Log(
                $"RIGHT: {rightPosition} | " +
                $"LEFT: {leftPosition} | " +
                $"GROUNDED: {controller.isGrounded} | " +
                $"NEXT: {(rightPawTurn ? "RIGHT" : "LEFT")}"
            );

            nextTrackingLog = Time.time + 0.5f;
        }

        // Keep original movement physics.
        ApplyMovement();
    }

    // NEW: Initialize paw position.
    void InitializeWalkingPaw(
        Transform paw,
        PawState state)
    {
        state.previousPosition =
            GetLocalPosition(paw);

        state.initialized = true;

        ResetWalkingGesture(state);
    }

    // Get position relative to XR Origin.
    Vector3 GetLocalPosition(Transform paw)
    {
        return xrOrigin.InverseTransformPoint(
            paw.position
        );
    }

    // Get horizontal facing direction
    // relative to XR Origin.
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

    // NEW WALKING GESTURE DETECTION
    void CheckPaw(
        Vector3 movementDelta,
        PawState state,
        bool isRightPaw)
    {
        Vector3 forward = GetLocalForward();

        // Determine whether the controller
        // moved forward or backward.
        float movement = Vector3.Dot(
            movementDelta,
            forward
        );

        // PHASE 1: Move paw forward.
        if (!state.reachedForward)
        {
            state.forwardMovement = Mathf.Max(
                0f,
                state.forwardMovement + movement
            );

            if (state.forwardMovement >= forwardReach)
            {
                state.reachedForward = true;
                state.backwardMovement = 0f;

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

        // PHASE 2: Move paw backward.
        state.backwardMovement = Mathf.Max(
            0f,
            state.backwardMovement - movement
        );

        if (state.backwardMovement >= backwardDrag)
        {
            ResetWalkingGesture(state);

            CompleteStep(isRightPaw);
        }
    }

    void ResetWalkingGesture(PawState state)
    {
        state.reachedForward = false;
        state.forwardMovement = 0f;
        state.backwardMovement = 0f;
    }

    // ORIGINAL SMOOTH STEP MECHANISM
    void CompleteStep(bool isRightPaw)
    {
        Vector3 direction = headset.forward;
        direction.y = 0f;

        if (direction.sqrMagnitude < 0.001f)
            return;

        direction.Normalize();

        // Queue one smooth forward step.
        remainingMovement +=
            direction * stepDistance;

        // Alternate paws.
        rightPawTurn = !isRightPaw;

        // Reset both gesture states.
        ResetWalkingGesture(left);
        ResetWalkingGesture(right);

        if (showDebugLogs)
        {
            Debug.Log(
                isRightPaw
                ? "RIGHT STEP! NEXT: LEFT"
                : "LEFT STEP! NEXT: RIGHT"
            );
        }
    }

    // ORIGINAL JUMP DETECTION
    void UpdateJumpPaw(
        Transform paw,
        JumpPawState state)
    {
        float height =
            xrOrigin.InverseTransformPoint(
                paw.position
            ).y;

        if (!state.initialized)
        {
            state.lowestHeight = height;
            state.peakHeight = height;
            state.initialized = true;
            return;
        }

        if (!state.raised)
        {
            state.lowestHeight = Mathf.Min(
                state.lowestHeight,
                height
            );

            float lift =
                height - state.lowestHeight;

            if (lift >= jumpLiftDistance)
            {
                state.raised = true;
                state.peakHeight = height;
                state.raiseTime = Time.time;
            }
        }
        else
        {
            state.peakHeight = Mathf.Max(
                state.peakHeight,
                height
            );

            float drop =
                state.peakHeight - height;

            if (drop >= jumpResetDistance)
            {
                state.raised = false;
                state.lowestHeight = height;
                state.peakHeight = height;
            }
        }
    }

    // ORIGINAL TWO-PAW JUMP
    bool CheckJump()
    {
        UpdateJumpPaw(leftPaw, leftJump);
        UpdateJumpPaw(rightPaw, rightJump);

        // Resume walking after landing
        // and lowering both paws.
        if (jumpLocked)
        {
            if (controller.isGrounded &&
                verticalVelocity <= 0f &&
                !leftJump.raised &&
                !rightJump.raised)
            {
                jumpLocked = false;

                ResetWalkingGesture(left);
                ResetWalkingGesture(right);

                if (showDebugLogs)
                {
                    Debug.Log("CAT READY TO WALK");
                }
            }

            return false;
        }

        if (!leftJump.raised || !rightJump.raised)
            return false;

        float timeDifference = Mathf.Abs(
            leftJump.raiseTime - rightJump.raiseTime
        );

        if (timeDifference > jumpSyncWindow)
            return false;

        if (!controller.isGrounded)
            return false;

        if (Time.time - lastJumpTime < jumpCooldown)
            return false;

        PerformJump();
        return true;
    }

    // ORIGINAL UPWARD JUMP + FORWARD LUNGE
    void PerformJump()
    {
        verticalVelocity = Mathf.Sqrt(
            jumpHeight * -2f * gravity
        );

        Vector3 direction = headset.forward;
        direction.y = 0f;

        if (direction.sqrMagnitude > 0.001f)
        {
            direction.Normalize();

            remainingLunge =
                direction * lungeDistance;
        }
        else
        {
            remainingLunge = Vector3.zero;
        }

        lastJumpTime = Time.time;
        jumpLocked = true;

        // Stop unfinished walking.
        remainingMovement = Vector3.zero;

        ResetWalkingGesture(left);
        ResetWalkingGesture(right);

        if (showDebugLogs)
        {
            Debug.Log(
                "BOTH PAWS UP - CAT LUNGE!"
            );
        }
    }

    // ORIGINAL MOVEMENT PHYSICS
    void ApplyMovement()
    {
        if (controller == null ||
            !controller.enabled)
            return;

        // Smooth walking.
        Vector3 stepMovement =
            Vector3.MoveTowards(
                Vector3.zero,
                remainingMovement,
                movementSpeed * Time.deltaTime
            );

        remainingMovement -= stepMovement;

        // Forward lunge.
        Vector3 lungeMovement =
            Vector3.MoveTowards(
                Vector3.zero,
                remainingLunge,
                lungeSpeed * Time.deltaTime
            );

        remainingLunge -= lungeMovement;

        // Grounding.
        if (controller.isGrounded &&
            verticalVelocity < 0f)
        {
            verticalVelocity = -2f;
        }

        verticalVelocity +=
            gravity * Time.deltaTime;

        // Combine walking, jumping, lunging.
        Vector3 movement =
            stepMovement +
            lungeMovement +
            Vector3.up *
            verticalVelocity *
            Time.deltaTime;

        CollisionFlags flags =
            controller.Move(movement);

        // Stop upward motion at ceiling.
        if ((flags & CollisionFlags.Above) != 0 &&
            verticalVelocity > 0f)
        {
            verticalVelocity = 0f;
        }

        // Stop lunge after landing.
        if ((flags & CollisionFlags.Below) != 0 &&
            verticalVelocity < 0f &&
            jumpLocked)
        {
            remainingLunge = Vector3.zero;
        }

        // Stop lunge when hitting a wall.
        if ((flags & CollisionFlags.Sides) != 0)
        {
            remainingLunge = Vector3.zero;
        }
    }
}

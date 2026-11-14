
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
    public float forwardReach = 0.10f;
    public float backwardDrag = 0.12f;

    [Header("Jumping")]
    public float jumpHeight = 0.65f;
    public float jumpLiftDistance = 0.14f;
    public float jumpResetDistance = 0.06f;
    public float jumpCooldown = 0.5f;

    [Header("Ground Movement")]
    public float gravity = -9.81f;
    public float stepOffset = 0.25f;

    [Header("Debugging")]
    public bool showDebugLogs = true;

    private CharacterController controller;

    private class PawState
    {
        public bool initialized;
        public bool reachedForward;

        public Vector3 startPosition;
        public Vector3 forwardDirection;
        public float furthestForward;

        public float restingHeight;
        public bool jumpRaised;
    }

    private PawState left = new PawState();
    private PawState right = new PawState();

    private bool rightPawTurn = true;
    private bool jumpArmed = true;

    private float lastJumpTime = -999f;
    private float verticalVelocity;

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

        controller = xrOrigin.GetComponent<CharacterController>();

        if (controller == null || !controller.enabled)
        {
            Debug.LogError(
                "XR Origin needs an enabled CharacterController!"
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

        Debug.Log("CAT MOVEMENT READY - RIGHT PAW FIRST");
    }

    void Update()
    {
        // Detect both paws being lifted.
        UpdateJumpPaw(leftPaw, left);
        UpdateJumpPaw(rightPaw, right);

        bool jumped = CheckJump();

        // Do not count walking steps during a jump.
        if (!jumped && jumpArmed &&
            controller.isGrounded)
        {
            CheckWalking();
        }

        ApplyMovement();
    }

    void InitializePaw(Transform paw, PawState state)
    {
        Vector3 localPosition =
            xrOrigin.InverseTransformPoint(paw.position);

        state.startPosition = localPosition;
        state.restingHeight = localPosition.y;
        state.initialized = true;
    }

    Vector3 GetLocalForward()
    {
        Vector3 direction =
            xrOrigin.InverseTransformDirection(headset.forward);

        direction.y = 0f;

        if (direction.sqrMagnitude > 0.001f)
            direction.Normalize();

        return direction;
    }

    void CheckWalking()
    {
        if (rightPawTurn)
        {
            CheckPawDrag(rightPaw, right, true);
            ResetWalkingPaw(leftPaw, left);
        }
        else
        {
            CheckPawDrag(leftPaw, left, false);
            ResetWalkingPaw(rightPaw, right);
        }
    }

    void CheckPawDrag(
        Transform paw,
        PawState state,
        bool isRightPaw)
    {
        Vector3 currentPosition =
            xrOrigin.InverseTransformPoint(paw.position);

        if (!state.initialized)
        {
            InitializePaw(paw, state);
            return;
        }

        // PHASE 1: Reach controller forward.
        if (!state.reachedForward)
        {
            Vector3 forward = GetLocalForward();

            if (forward.sqrMagnitude < 0.001f)
                return;

            float reach = Vector3.Dot(
                currentPosition - state.startPosition,
                forward
            );

            if (reach >= forwardReach)
            {
                state.reachedForward = true;
                state.forwardDirection = forward;
                state.furthestForward = reach;

                if (showDebugLogs)
                    Debug.Log(isRightPaw
                        ? "RIGHT PAW REACHED FORWARD"
                        : "LEFT PAW REACHED FORWARD");
            }

            return;
        }

        // PHASE 2: Drag controller backward.
        float currentReach = Vector3.Dot(
            currentPosition - state.startPosition,
            state.forwardDirection
        );

        state.furthestForward = Mathf.Max(
            state.furthestForward,
            currentReach
        );

        float drag =
            state.furthestForward - currentReach;

        if (drag >= backwardDrag)
        {
            ResetWalkingPaw(paw, state);
            CompleteStep(isRightPaw);
        }
    }

    void ResetWalkingPaw(Transform paw, PawState state)
    {
        state.startPosition =
            xrOrigin.InverseTransformPoint(paw.position);

        state.reachedForward = false;
        state.furthestForward = 0f;
    }

    void CompleteStep(bool isRightPaw)
    {
        Vector3 direction = headset.forward;
        direction.y = 0f;

        if (direction.sqrMagnitude < 0.001f)
            return;

        direction.Normalize();

        remainingMovement += direction * stepDistance;

        rightPawTurn = !isRightPaw;

        // Prepare the next paw to start from its
        // current position, not an old resting position.
        if (rightPawTurn)
            ResetWalkingPaw(rightPaw, right);
        else
            ResetWalkingPaw(leftPaw, left);

        if (showDebugLogs)
        {
            Debug.Log(isRightPaw
                ? "RIGHT STEP! NEXT: LEFT"
                : "LEFT STEP! NEXT: RIGHT");
        }
    }

    void UpdateJumpPaw(Transform paw, PawState state)
    {
        float height =
            xrOrigin.InverseTransformPoint(paw.position).y;

        // Allow recalibration if the paw rests lower.
        if (height < state.restingHeight)
            state.restingHeight = height;

        float lift = height - state.restingHeight;

        // Hysteresis prevents jitter around the threshold.
        if (lift >= jumpLiftDistance)
            state.jumpRaised = true;
        else if (lift <= jumpResetDistance)
            state.jumpRaised = false;
    }

    bool CheckJump()
    {
        // Both paws must be lowered to rearm jumping.
        if (!left.jumpRaised && !right.jumpRaised)
            jumpArmed = true;

        if (!jumpArmed)
            return false;

        if (!left.jumpRaised || !right.jumpRaised)
            return false;

        if (!controller.isGrounded)
            return false;

        if (Time.time - lastJumpTime < jumpCooldown)
            return false;

        // Standard jump velocity for the chosen height.
        verticalVelocity = Mathf.Sqrt(
            jumpHeight * -2f * gravity
        );

        lastJumpTime = Time.time;
        jumpArmed = false;

        remainingMovement = Vector3.zero;

        ResetWalkingPaw(leftPaw, left);
        ResetWalkingPaw(rightPaw, right);

        if (showDebugLogs)
            Debug.Log("BOTH PAWS UP - CAT JUMP!");

        return true;
    }

    void ApplyMovement()
    {
        if (controller == null || !controller.enabled)
            return;

        Vector3 stepMovement = Vector3.MoveTowards(
            Vector3.zero,
            remainingMovement,
            movementSpeed * Time.deltaTime
        );

        remainingMovement -= stepMovement;

        // Only reset downward velocity on the ground.
        if (controller.isGrounded &&
            verticalVelocity < 0f)
        {
            verticalVelocity = -2f;
        }

        verticalVelocity += gravity * Time.deltaTime;

        Vector3 movement =
            stepMovement +
            Vector3.up * verticalVelocity * Time.deltaTime;

        CollisionFlags flags = controller.Move(movement);

        if ((flags & CollisionFlags.Above) != 0 &&
            verticalVelocity > 0f)
        {
            verticalVelocity = 0f;
        }
    }
}


using UnityEngine;

public class CatMovementTest : MonoBehaviour
{
    public Transform headset;

    [Header("Testing")]
    public bool testForward = false;
    public bool testJump = false;
    public float speed = 1f;

    private CharacterController controller;
    private float verticalVelocity;
    private float nextLogTime;

    void Start()
    {
        controller = GetComponent<CharacterController>();

        if (controller == null)
        {
            Debug.LogError(
                "NO CHARACTER CONTROLLER FOUND!"
            );
            enabled = false;
            return;
        }

        Debug.Log("MOVEMENT TEST READY");
    }

    void Update()
    {
        Vector3 direction = Vector3.zero;

        if (testForward)
        {
            direction = headset != null
                ? headset.forward : transform.forward;

            direction.y = 0f;
            direction.Normalize();
        }

        if (testJump)
        {
            verticalVelocity = 4f;
            testJump = false;
        }

        if (controller.isGrounded &&
            verticalVelocity < 0f)
        {
            verticalVelocity = -2f;
        }

        verticalVelocity += -9.81f * Time.deltaTime;

        Vector3 motion =
            direction * speed * Time.deltaTime +
            Vector3.up * verticalVelocity * Time.deltaTime;

        Vector3 before = transform.position;

        CollisionFlags flags = controller.Move(motion);

        Vector3 actualMovement =
            transform.position - before;

        if (Time.time >= nextLogTime)
        {
            Debug.Log(
                $"Requested: {motion} | " +
                $"Actual: {actualMovement} | " +
                $"Position: {transform.position} | " +
                $"Grounded: {controller.isGrounded} | " +
                $"Collisions: {flags}"
            );

            nextLogTime = Time.time + 0.5f;
        }
    }
}

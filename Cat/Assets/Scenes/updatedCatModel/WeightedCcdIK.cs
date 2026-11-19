using UnityEngine;

public class WeightedCcdIK : MonoBehaviour
{
    public Transform[] joints;       // thigh, lower leg, hock
    public Transform endBone;        // paw bone
    public Transform target;         // desired paw position
    public Transform hint; // place near the desired knee-bend side
    [Range(0f, 1f)] public float[] weights; // e.g. 0.15, 0.35, 1.0
    public int iterations = 12;
    public float tolerance = 0.01f;

    void ApplyHint()
    {
        if (hint == null || joints == null || joints.Length == 0) return;

        Transform root = joints[0];
        Transform knee = joints.Length > 1 ? joints[1] : null;
        if (knee == null) return;

        Vector3 axis = (target.position - root.position).normalized;
        Vector3 current = Vector3.ProjectOnPlane(knee.position - root.position, axis);
        Vector3 desired = Vector3.ProjectOnPlane(hint.position - root.position, axis);

        if (current.sqrMagnitude < 0.000001f ||
            desired.sqrMagnitude < 0.000001f) return;

        float angle = Vector3.SignedAngle(current, desired, axis);
        root.rotation = Quaternion.AngleAxis(angle, axis) * root.rotation;
    }

    void LateUpdate()
    {
        if (joints == null || endBone == null || target == null) return;

        // Solve from the hock back toward the thigh.
        for (int iteration = 0; iteration < iterations; iteration++)
        {
            if (Vector3.Distance(endBone.position, target.position) <= tolerance)
                break;

            for (int i = joints.Length - 1; i >= 0; i--)
            {
                Transform joint = joints[i];
                Vector3 toEnd = endBone.position - joint.position;
                Vector3 toTarget = target.position - joint.position;

                if (toEnd.sqrMagnitude < 0.000001f ||
                    toTarget.sqrMagnitude < 0.000001f)
                    continue;

                Quaternion fullTurn =
                Quaternion.FromToRotation(toEnd, toTarget) * joint.rotation;

                float weight = weights != null && i < weights.Length
                ? weights[i]
                : 1f;

                joint.rotation = Quaternion.Slerp(
                    joint.rotation, fullTurn, Mathf.Clamp01(weight));
            }
        }
        ApplyHint();
    }
}

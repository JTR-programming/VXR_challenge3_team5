using UnityEngine;

public class CopyTransform : MonoBehaviour
{
    [SerializeField] private Transform source;
    [SerializeField] private Transform reference;
    [SerializeField, Range(0f,2f)] private float positionScale = 1f;
    [SerializeField] private Vector3 positionOffset;
    [SerializeField] private Vector3 rotationOffset;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    private void LateUpdate(){
        if(source == null || reference == null) return;

        Vector3 relativePosition = reference.InverseTransformPoint(source.position);
        transform.position = reference.TransformPoint(relativePosition * positionScale) + reference.TransformDirection(positionOffset);
        transform.rotation = source.rotation * Quaternion.Euler(rotationOffset);
    }
}

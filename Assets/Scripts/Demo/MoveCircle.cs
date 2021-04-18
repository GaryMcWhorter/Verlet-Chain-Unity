using UnityEngine;

public class MoveCircle : MonoBehaviour
{
    [SerializeField] float radius = 1f;
    [SerializeField] float angularSpeed = 1f;
    float angle = 0;
    Vector3 center;

    void Awake()
    {
        center = transform.position;
    }

    void Update()
    {
        angle += Time.deltaTime * angularSpeed;
        Vector3 dir = Quaternion.AngleAxis(angle, Vector3.forward) * Vector3.up;
        transform.position = center + dir * radius;
    }
}

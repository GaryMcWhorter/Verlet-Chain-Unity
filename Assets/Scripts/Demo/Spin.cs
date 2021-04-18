using UnityEngine;

public class Spin : MonoBehaviour
{
    [SerializeField] float spinSpeed = 1f;
    void Update()
    {
        transform.Rotate(0, 0, spinSpeed * Time.deltaTime);
    }
}

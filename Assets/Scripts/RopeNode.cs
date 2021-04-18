using UnityEngine;

public class RopeNode : MonoBehaviour
{
    public Vector3 PreviousPosition;

    void Awake()
    {
        this.enabled = false;
    }
}

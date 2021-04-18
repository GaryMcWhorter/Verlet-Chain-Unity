using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
public class Rope : MonoBehaviour
{
    [Header("Instanced Mesh Details")]
    [SerializeField, Tooltip("The Mesh of chain link to render")] Mesh link;
    [SerializeField, Tooltip("The chain link material, must have gpu instancing enabled!")] Material linkMaterial;

    [Space]

    [Header("Demo Parameters")]
    [SerializeField, Min(0), Tooltip("The distance to project the mouse into world space")] float mouseOffset = 10f;

    [Space]

    [Header("Verlet Parameters")]

    [SerializeField, Tooltip("The distance between each link in the chain")] float nodeDistance = 0.35f;
    [SerializeField, Tooltip("The radius of the sphere collider used for each chain link")] float nodeColliderRadius = 0.2f;

    [SerializeField, Tooltip("Works best with a lower value")] float gravityStrength = 2;

    [SerializeField, Tooltip("The number of chain links. Decreases performance with high values and high iteration")] int totalNodes = 100;

    [SerializeField, Range(0, 1), Tooltip("Modifier to dampen velocity so the simulation can stabilize")] float velocityDampen = 0.95f;

    [SerializeField, Range(0, 0.99f), Tooltip("The stiffness of the simulation. Set to lower values for more elasticity")] float stiffness = 0.8f;

    [SerializeField, Tooltip("Setting this will test collisions for every n iterations. Possibly more performance but less stable collisions")] int iterateCollisionsEvery = 1;

    [SerializeField, Tooltip("Iterations for the simulation. More iterations is more expensive but more stable")] int iterations = 100;

    [SerializeField, Tooltip("How many colliders to test against for every node.")] int colliderBufferSize = 1;

    RaycastHit[] raycastHitBuffer;
    Collider[] colliderHitBuffer;
    Camera cam;

    // Need a better way of stepping through collisions for high Gravity
    // And high Velocity
    Vector3 gravity;

    Vector3 startLock;
    Vector3 endLock;

    bool isStartLocked = false;
    bool isEndLocked = false;

    [Space]

    // For Debug Drawing the chain/rope
    [Header("Line Renderer")]
    [SerializeField, Tooltip("Width for the line renderer")] float ropeWidth = 0.1f;

    LineRenderer lineRenderer;
    Vector3[] linePositions;

    Vector3[] previousNodePositions;

    Vector3[] currentNodePositions;
    Quaternion[] currentNodeRotations;

    SphereCollider nodeCollider;
    GameObject nodeTester;
    Matrix4x4[] matrices;


    void Awake()
    {
        currentNodePositions = new Vector3[totalNodes];
        previousNodePositions = new Vector3[totalNodes];
        currentNodeRotations = new Quaternion[totalNodes];

        raycastHitBuffer = new RaycastHit[colliderBufferSize];
        colliderHitBuffer = new Collider[colliderBufferSize];
        gravity = new Vector3(0, -gravityStrength, 0);
        cam = Camera.main;
        lineRenderer = this.GetComponent<LineRenderer>();

        // using a single dynamically created GameObject to test collisions on every node
        nodeTester = new GameObject();
        nodeTester.name = "Node Tester";
        nodeTester.layer = 8;
        nodeCollider = nodeTester.AddComponent<SphereCollider>();
        nodeCollider.radius = nodeColliderRadius;


        matrices = new Matrix4x4[totalNodes];

        Vector3 startPosition = Vector3.zero;
        for (int i = 0; i < totalNodes; i++)
        {

            currentNodePositions[i] = startPosition;
            currentNodeRotations[i] = Quaternion.identity;

            previousNodePositions[i] = startPosition;

            matrices[i] = Matrix4x4.TRS(startPosition, Quaternion.identity, Vector3.one);

            startPosition.y -= nodeDistance;

        }

        // for line renderer data
        linePositions = new Vector3[totalNodes];
    }


    void Update()
    {
        // Attach rope end to mouse click position
        if (Input.GetMouseButtonDown(0))
        {
            if (!isStartLocked)
            {
                isStartLocked = true;
            }
            else if(!isEndLocked)
            {
                isEndLocked = true;
            }
        }
        else if (!isStartLocked && !isEndLocked)
        {
            startLock = cam.ScreenToWorldPoint(Input.mousePosition + new Vector3(0, 0, mouseOffset));
        }
        else if (isStartLocked && !isEndLocked)
        {
            endLock = cam.ScreenToWorldPoint(Input.mousePosition + new Vector3(0, 0, mouseOffset));
        }

        DrawRope();

        // Instanced drawing here is really performant over using GameObjects
        Graphics.DrawMeshInstanced(link, 0, linkMaterial, matrices, totalNodes);
    }

    private void FixedUpdate()
    {
        Simulate();

        for (int i = 0; i < iterations; i++)
        {
            ApplyConstraint();

            if(i % iterateCollisionsEvery == 0)
            {
                AdjustCollisions();
            }
        }

        SetAngles();
        TranslateMatrices();
    }

    private void Simulate()
    {
        var fixedDt = Time.fixedDeltaTime;
        for (int i = 0; i < totalNodes; i++)
        {
            Vector3 velocity = currentNodePositions[i] - previousNodePositions[i];
            velocity *= velocityDampen;

            previousNodePositions[i] = currentNodePositions[i];

            // calculate new position
            Vector3 newPos = currentNodePositions[i] + velocity;
            newPos += gravity * fixedDt;
            Vector3 direction = currentNodePositions[i] - newPos;

            currentNodePositions[i] = newPos;
        }
    }
    
    private void AdjustCollisions()
    {
        for (int i = 0; i < totalNodes; i++)
        {
            if(i % 2 == 0) continue;

            int result = -1;
            result = Physics.OverlapSphereNonAlloc(currentNodePositions[i], nodeColliderRadius + 0.01f, colliderHitBuffer, ~(1 << 8));

            // if (result > 0)
            // {
                for (int n = 0; n < result; n++)
                {
                    // if (colliderHitBuffer[n].gameObject.layer != 8)
                    {
                        Vector3 colliderPosition = colliderHitBuffer[n].transform.position;
                        Quaternion colliderRotation = colliderHitBuffer[n].gameObject.transform.rotation;

                        Vector3 dir;
                        float distance;

                        Physics.ComputePenetration(nodeCollider, currentNodePositions[i], Quaternion.identity, colliderHitBuffer[n], colliderPosition, colliderRotation, out dir, out distance);
                        
                        currentNodePositions[i] += dir * distance;
                    }
                }
            // }
        }    
    }

    private void ApplyConstraint()
    {
        currentNodePositions[0] = startLock;
        if(isStartLocked)
        {
            currentNodePositions[totalNodes - 1] = endLock;
        }

        for (int i = 0; i < totalNodes - 1; i++)
        {
            var node1 = currentNodePositions[i];
            var node2 = currentNodePositions[i + 1];

            // Get the current distance between rope nodes
            float currentDistance = (node1 - node2).magnitude;
            float difference = Mathf.Abs(currentDistance - nodeDistance);
            Vector3 direction = Vector3.zero;

            // determine what direction we need to adjust our nodes
            if (currentDistance > nodeDistance)
            {
                direction = (node1 - node2).normalized;
            }
            else if (currentDistance < nodeDistance)
            {
                direction = (node2 - node1).normalized;
            }

            // calculate the movement vector
            Vector3 movement = direction * difference;

            // apply correction
            currentNodePositions[i] -= (movement * stiffness);
            currentNodePositions[i + 1] += (movement * stiffness);
        }
    }

    void SetAngles()
    {
        for (int i = 0; i < totalNodes - 1; i++)
        {
            var node1 = currentNodePositions[i];
            var node2 = currentNodePositions[i + 1];

            var dir = (node2 - node1).normalized;
            if(dir != Vector3.zero)
            {
                if( i > 0)
                {
                    Quaternion desiredRotation = Quaternion.LookRotation(dir, Vector3.right);
                    currentNodeRotations[i + 1] = desiredRotation;
                }
                else if( i < totalNodes - 1)
                {
                    Quaternion desiredRotation = Quaternion.LookRotation(dir, Vector3.right);
                    currentNodeRotations[i + 1] = desiredRotation;
                }
                else
                {
                    Quaternion desiredRotation = Quaternion.LookRotation(dir, Vector3.right);
                    currentNodeRotations[i] = desiredRotation;
                }
            }

            if( i % 2 == 0 && i != 0)
            {
                currentNodeRotations[i + 1] *= Quaternion.Euler(0, 0, 90);
            }
        }
    }

    void TranslateMatrices()
    {
        for(int i = 0; i < totalNodes; i++)
        {
            matrices[i].SetTRS(currentNodePositions[i], currentNodeRotations[i], Vector3.one);
        }
    }

    private void DrawRope()
    {
        lineRenderer.startWidth = ropeWidth;
        lineRenderer.endWidth = ropeWidth;

        for (int n = 0; n < totalNodes; n++)
        {
            linePositions[n] = currentNodePositions[n];
        }

        lineRenderer.positionCount = linePositions.Length;
        lineRenderer.SetPositions(linePositions);
    }

}

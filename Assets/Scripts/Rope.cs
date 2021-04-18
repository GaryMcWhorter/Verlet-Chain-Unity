using System.Collections.Generic;
using UnityEngine;

public class Rope : MonoBehaviour
{
    LineRenderer lineRenderer;
    Vector3[] linePositions;

    RopeNode[] ropeNodes;
    Vector3[] previousNodePositions;
    
    // It is slightly faster to iterate over an array than a List.
    // private List<RopeNode> RopeNodes = new List<RopeNode>();

    [SerializeField] GameObject ropeNodePrefab;
    [SerializeField] GameObject ropeNodeRotatedPrefab;

    // Distance Between Nodes
    [SerializeField] float nodeDistance = 0.2f;

    [SerializeField] int totalNodes = 50;

    [SerializeField, Range(0, 1)] float velocityDampen = 0.99f;

    // Higher iterations == More stability of the simulation
    [SerializeField] int iterations = 50;

    // Only check collisions over every n iteration
    [SerializeField] int iterateCollisionsEvery = 1;

    // For Debug Drawing the chain/rope
    private float ropeWidth = 0.1f;

    // For projecting the mouse into 3d space for this example
    [SerializeField, Min(0)] float mouseOffset = 3f;

    [SerializeField, Range(0, 0.99f)] float stiffness = 0.8f;

    Camera cam;

    [SerializeField] int colliderBufferSize = 10;
    RaycastHit[] raycastHitBuffer;
    Collider[] colliderHitBuffer;

    // Need a better way of stepping through collisions for high Gravity
    // And high Velocity
    [SerializeField] float gravityStrength;
    Vector3 gravity;

    Vector3 startLock;
    Vector3 endLock;

    bool isStartLocked = false;
    bool isEndLocked = false;

    [Header("Experimental")]
    [SerializeField] float nodeColliderRadius = 0.2f;
    [SerializeField] Mesh link;
    [SerializeField] Material linkMaterial;

    Vector3[] currentNodePositions;
    Quaternion[] currentNodeRotations;

    SphereCollider nodeCollider;
    GameObject nodeTester;
    Matrix4x4[] matrices;


    void Awake()
    {
        ropeNodes = new RopeNode[totalNodes];
        currentNodePositions = new Vector3[totalNodes];
        previousNodePositions = new Vector3[totalNodes];
        currentNodeRotations = new Quaternion[totalNodes];

        raycastHitBuffer = new RaycastHit[colliderBufferSize];
        colliderHitBuffer = new Collider[colliderBufferSize];
        gravity = new Vector3(0, -gravityStrength, 0);
        cam = Camera.main;
        lineRenderer = this.GetComponent<LineRenderer>();

        // using a dynamically created GameObject to test collisions on every node
        nodeTester = new GameObject();
        nodeTester.name = "Node Tester";
        nodeTester.layer = 8;
        nodeCollider = nodeTester.AddComponent<SphereCollider>();
        nodeCollider.radius = nodeColliderRadius;


        matrices = new Matrix4x4[totalNodes];

        // Generate some rope nodes based on properties
        Vector3 startPosition = Vector3.zero;
        for (int i = 0; i < totalNodes; i++)
        {

            // RopeNode node;            
            // if(i % 2 == 0)
            // {
            //     // node.transform.rotation = node.transform.rotation * Quaternion.Euler(0, 0, 90);
            //     node = (GameObject.Instantiate(ropeNodePrefab)).GetComponent<RopeNode>();
            // }
            // else
            // {
            //     node = (GameObject.Instantiate(ropeNodePrefab)).GetComponent<RopeNode>();
            // }
            // node.transform.position = startPosition;

            // node.PreviousPosition = startPosition;
            currentNodePositions[i] = startPosition;
            previousNodePositions[i] = startPosition;
            currentNodeRotations[i] = Quaternion.identity;
            // RopeNodes.Add(node);
            // ropeNodes[i] = node;

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
                // startLock = cam.ScreenToWorldPoint(Input.mousePosition + new Vector3(0, 0, mouseOffset));
            }
            else if(!isEndLocked)
            {
                isEndLocked = true;
                // endLock = cam.ScreenToWorldPoint(Input.mousePosition + new Vector3(0, 0, mouseOffset));
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
            // if(i % 2 == 0)
        }

        SetAngles();
        TranslateMatrices();
    }

    private void Simulate()
    {
        // step each node in rope
        for (int i = 0; i < totalNodes; i++)
        {
            // RopeNode node = this.ropeNodes[i];
            // derive the velocity from previous frame
            // Vector3 velocity = ropeNodes[i].transform.position - ropeNodes[i].PreviousPosition;
            Vector3 velocity = currentNodePositions[i] - previousNodePositions[i];
            velocity *= velocityDampen;

            // Attempt to fix high velocity clipping, unsuccessfully
            // if(velocity.magnitude > 0.1f)
            // {
            //     AdjustCollisions();
            // }
            // ropeNodes[i].PreviousPosition = ropeNodes[i].transform.position;
            previousNodePositions[i] = currentNodePositions[i];

            // calculate new position
            Vector3 newPos = currentNodePositions[i] + velocity;
            newPos += gravity * Time.fixedDeltaTime;
            Vector3 direction = currentNodePositions[i] - newPos;
            
            // cast ray towards this position to check for a collision
            // int result = -1;
            // result = Physics.SphereCastNonAlloc(currentNodePositions[i], nodeColliderRadius + 0.01f, -direction.normalized, raycastHitBuffer, 0.22f, ~(1 << 8));

            // if (result > 0)
            // {
            //     for (int n = 0; n < result; n++)
            //     {                    
            //         if (raycastHitBuffer[n].collider.gameObject.layer == 9)
            //         {
            //             Vector3 colliderPosition = colliderHitBuffer[n].transform.position;
            //             Quaternion colliderRotation = colliderHitBuffer[n].gameObject.transform.rotation;

            //             Vector3 dir;
            //             float distance;

            //             Physics.ComputePenetration(nodeCollider, currentNodePositions[i], Quaternion.identity, colliderHitBuffer[n], colliderPosition, colliderRotation, out dir, out distance);
            //             newPos += dir * distance;
                        
            //         }
            //     }
            // }

            // ropeNodes[i].transform.position = newPos;
            currentNodePositions[i] = newPos;
        }
    }
    
    private void AdjustCollisions()
    {
        // Loop rope nodes and check if currently colliding
        for (int i = 0; i < totalNodes; i++)
        {
            // RopeNode node = this.ropeNodes[i];

            int result = -1;
            result = Physics.OverlapSphereNonAlloc(currentNodePositions[i], nodeColliderRadius + 0.01f, colliderHitBuffer, ~(1 << 8));

            if (result > 0)
            {
                for (int n = 0; n < result; n++)
                {
                    if (colliderHitBuffer[n].gameObject.layer != 8)
                    {
                        Vector3 colliderPosition = colliderHitBuffer[n].transform.position;
                        Quaternion colliderRotation = colliderHitBuffer[n].gameObject.transform.rotation;

                        Vector3 dir;
                        float distance;

                        Physics.ComputePenetration(nodeCollider, currentNodePositions[i], Quaternion.identity, colliderHitBuffer[n], colliderPosition, colliderRotation, out dir, out distance);
                        
                        currentNodePositions[i] += dir * distance;
                    }
                }
            }
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
            // else
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

            if( i > 0)
            {
                Quaternion desiredRotation = Quaternion.LookRotation((node2 - node1).normalized, Vector3.right);
                // node2.transform.rotation = Quaternion.RotateTowards(node2.transform.rotation, desiredRotation, 15f);
                // node2.transform.rotation = desiredRotation;
                currentNodeRotations[i + 1] = desiredRotation;
                // matrices[i + 1] *= Matrix4x4.Rotate(desiredRotation);
            }
            else if( i < totalNodes - 1)
            {
                Quaternion desiredRotation = Quaternion.LookRotation((node2 - node1).normalized, Vector3.right);
                // node2.transform.rotation = Quaternion.RotateTowards(node2.transform.rotation, desiredRotation, 15f);
                // node2.transform.rotation = desiredRotation;
                currentNodeRotations[i + 1] = desiredRotation;
                // matrices[i + 1] *= Matrix4x4.Rotate(desiredRotation);
            }
            else
            {
                Quaternion desiredRotation = Quaternion.LookRotation((node2 - node1).normalized, Vector3.right);
                // node1.transform.rotation = Quaternion.RotateTowards(node1.transform.rotation, desiredRotation, 15f);               
                // node1.transform.rotation = desiredRotation;
                currentNodeRotations[i] = desiredRotation;
                // matrices[i] *= Matrix4x4.Rotate(desiredRotation);
            }

            if( i % 2 == 0 && i != 0)
            {
                currentNodeRotations[i + 1] *= Quaternion.Euler(0, 0, 90);
                // node1.transform.rotation *= Quaternion.Euler(0, 0, 90);
                // matrices[i] *= Matrix4x4.Rotate(Quaternion.Euler(0, 0, 90));
            }
        }
    }

    void TranslateMatrices()
    {
        for(int i = 0; i < totalNodes; i++)
        {
            matrices[i].SetTRS(currentNodePositions[i], currentNodeRotations[i], Vector3.one);
            // matrices[i].m03 = currentNodePositions[i].x;
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

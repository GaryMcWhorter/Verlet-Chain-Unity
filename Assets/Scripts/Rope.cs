using System.Collections.Generic;
using UnityEngine;

public class Rope : MonoBehaviour
{
    LineRenderer lineRenderer;
    Vector3[] linePositions;

    private RopeNode[] ropeNodes;
    
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


    void Awake()
    {
        ropeNodes = new RopeNode[totalNodes];
        raycastHitBuffer = new RaycastHit[colliderBufferSize];
        colliderHitBuffer = new Collider[colliderBufferSize];
        gravity = new Vector3(0, -gravityStrength, 0);
        cam = Camera.main;

        lineRenderer = this.GetComponent<LineRenderer>();

        // Generate some rope nodes based on properties
        Vector3 startPosition = Vector3.zero;
        for (int i = 0; i < totalNodes; i++)
        {
            RopeNode node;            
            if(i % 2 == 0)
            {
                // node.transform.rotation = node.transform.rotation * Quaternion.Euler(0, 0, 90);
                node = (GameObject.Instantiate(ropeNodePrefab)).GetComponent<RopeNode>();
            }
            else
            {
                node = (GameObject.Instantiate(ropeNodeRotatedPrefab)).GetComponent<RopeNode>();
            }
            node.transform.position = startPosition;
            node.PreviousPosition = startPosition;
            // RopeNodes.Add(node);
            ropeNodes[i] = node;

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
    }

    private void FixedUpdate()
    {
        Simulate();
        // Higher iteration results in stiffer ropes and stable simulation
        for (int i = 0; i < iterations; i++)
        {

            ApplyConstraint();

            if(i % 2 == 0)
            AdjustCollisions();
        }
        SetAngles();
    }

    private void Simulate()
    {
        // step each node in rope
        for (int i = 0; i < totalNodes; i++)
        {
            RopeNode node = this.ropeNodes[i];
            // derive the velocity from previous frame
            Vector3 velocity = ropeNodes[i].transform.position - ropeNodes[i].PreviousPosition;
            velocity *= velocityDampen;

            // Attempt to fix high velocity clipping, unsuccessfully
            // if(velocity.magnitude > 0.1f)
            // {
            //     AdjustCollisions();
            // }
            ropeNodes[i].PreviousPosition = ropeNodes[i].transform.position;

            // calculate new position
            Vector3 newPos = ropeNodes[i].transform.position + velocity;
            newPos += gravity * Time.fixedDeltaTime;
            Vector3 direction = ropeNodes[i].transform.position - newPos;
            
            // cast ray towards this position to check for a collision
            int result = -1;
            result = Physics.SphereCastNonAlloc(ropeNodes[i].transform.position, 0.21f, -direction.normalized, raycastHitBuffer, 0.22f, ~(1 << 8));

            if (result > 0)
            {
                for (int n = 0; n < result; n++)
                {                    
                    if (raycastHitBuffer[n].collider.gameObject.layer == 9)
                    {
                        Vector3 colliderPosition = colliderHitBuffer[n].transform.position;
                        Quaternion colliderRotation = colliderHitBuffer[n].gameObject.transform.rotation;

                        Vector3 dir;
                        float distance;

                        Physics.ComputePenetration(node.GetComponent<SphereCollider>(), node.transform.position, node.transform.rotation, colliderHitBuffer[n], colliderPosition, colliderRotation, out dir, out distance);
                        newPos += dir * distance;
                        
                    }
                }
            }

            ropeNodes[i].transform.position = newPos;
        }
    }
    
    private void AdjustCollisions()
    {
        // Loop rope nodes and check if currently colliding
        for (int i = 0; i < totalNodes - 1; i++)
        {
            RopeNode node = this.ropeNodes[i];

            int result = -1;
            result = Physics.OverlapSphereNonAlloc(node.transform.position, 0.21f, colliderHitBuffer, ~(1 << 8));

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

                        Physics.ComputePenetration(node.GetComponent<SphereCollider>(), node.transform.position, node.transform.rotation, colliderHitBuffer[n], colliderPosition, colliderRotation, out dir, out distance);
                        
                        node.transform.position += dir * distance;
                    }
                }
            }
        }    
    }

    private void ApplyConstraint()
    {
        ropeNodes[0].transform.position = startLock;
        if(isStartLocked)
        {
            ropeNodes[totalNodes - 1].transform.position = endLock;
        }

        for (int i = 0; i < totalNodes - 1; i++)
        {
            RopeNode node1 = this.ropeNodes[i];
            RopeNode node2 = this.ropeNodes[i + 1];

            // Get the current distance between rope nodes
            float currentDistance = (node1.transform.position - node2.transform.position).magnitude;
            float difference = Mathf.Abs(currentDistance - nodeDistance);
            Vector3 direction = Vector3.zero;

            // determine what direction we need to adjust our nodes
            if (currentDistance > nodeDistance)
            {
                direction = (node1.transform.position - node2.transform.position).normalized;
            }
            else if (currentDistance < nodeDistance)
            // else
            {
                direction = (node2.transform.position - node1.transform.position).normalized;
            }

            // calculate the movement vector
            Vector3 movement = direction * difference;

            // apply correction
            node1.transform.position -= (movement * stiffness);
            node2.transform.position += (movement * stiffness);
        }
    }

    void SetAngles()
    {
        for (int i = 0; i < totalNodes - 1; i++)
        {
            RopeNode node1 = this.ropeNodes[i];
            RopeNode node2 = this.ropeNodes[i + 1];

            if( i > 0)
            {
                Quaternion desiredRotation = Quaternion.LookRotation((node2.transform.position - node1.transform.position).normalized, node1.transform.up);
                // node2.transform.rotation = Quaternion.RotateTowards(node2.transform.rotation, desiredRotation, 15f);
                node2.transform.rotation = desiredRotation;
            }
            else if( i < totalNodes - 1)
            {
                Quaternion desiredRotation = Quaternion.LookRotation((node2.transform.position - node1.transform.position).normalized, node2.transform.up);
                // node2.transform.rotation = Quaternion.RotateTowards(node2.transform.rotation, desiredRotation, 15f);
                node2.transform.rotation = desiredRotation;
            }
            else
            {
                Quaternion desiredRotation = Quaternion.LookRotation((node1.transform.position - node2.transform.position).normalized, node1.transform.up);
                // node1.transform.rotation = Quaternion.RotateTowards(node1.transform.rotation, desiredRotation, 15f);               
                node1.transform.rotation = desiredRotation;

            }
        }
    }

    private void DrawRope()
    {
        lineRenderer.startWidth = ropeWidth;
        lineRenderer.endWidth = ropeWidth;

        for (int n = 0; n < totalNodes; n++)
        {
            linePositions[n] = ropeNodes[n].transform.position;
        }

        lineRenderer.positionCount = linePositions.Length;
        lineRenderer.SetPositions(linePositions);
    }

}

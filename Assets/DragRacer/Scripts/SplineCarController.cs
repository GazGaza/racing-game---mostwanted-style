using UnityEngine;

public class SplineCarController : MonoBehaviour
{
    [Header("Spline Setup")]
    public LaneSpline[] lanes; // Assign lane splines in order: Left → Right
    public LayerMask roadLayer;

    [Header("Movement Settings")]
    public float acceleration = 20f;
    public float maxSpeed = 50f;
    public float brakeForce = 40f;
    public float laneChangeSpeed = 3f;
    public float rayDistance = 10f;

    private Rigidbody rb;
    private int currentLane = 0;
    private int targetLane = 0;
    private float t = 0f; // spline position (0–1)
    private float speed = 0f;

    // side offset control
    private float laneOffset = 0f;
    private float targetOffset = 0f;
    public float laneWidth = 2.5f; // distance between lanes


    // Try to detect which lane is closest to the car at start
    void DetectStartLane()
    {
        if (lanes == null || lanes.Length == 0) return;

        float closestDist = float.MaxValue;
        int closestIndex = 0;

        // sample each spline at multiple points along its length
        for (int i = 0; i < lanes.Length; i++)
        {
            for (float s = 0f; s <= 1f; s += 0.05f)   // every 5% of the lane
            {
                Vector3 testPos = lanes[i].GetPoint(s);
                float dist = (transform.position - testPos).sqrMagnitude;
                if (dist < closestDist)
                {
                    closestDist = dist;
                    closestIndex = i;
                }
            }
        }

        currentLane = targetLane = closestIndex;

        // offset from road centre so lanes are spaced correctly
        float mid = (lanes.Length - 1) * 0.5f;
        targetOffset = laneOffset = (closestIndex - mid) * laneWidth;

        Debug.Log($"Detected starting lane = {closestIndex}");
    
    }

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        rb.useGravity = false;

        if (lanes == null || lanes.Length == 0)
        {
            Debug.LogError("Assign LaneSpline references in the inspector!");
            return;
        }

        DetectStartLane();
        t = 0f;                // start at beginning of spline
        speed = 0f;            // no momentum from physics
    }

    void Update()
    {
        HandleInput();
        DrawDebugRay();
        Debug.Log($"Current lane index: {currentLane}");
    }

    void FixedUpdate()
    {
        
        FollowSpline();
        StickToRoad();
        rb.linearVelocity = rb.transform.forward * speed;
    }

    void HandleInput()
    {

        // forward / brake
        if (Input.GetKey(KeyCode.W))
            speed = Mathf.MoveTowards(speed, maxSpeed, acceleration * Time.deltaTime);
        else if (Input.GetKey(KeyCode.S))
            speed = Mathf.MoveTowards(speed, 0, brakeForce * Time.deltaTime);
        else
            speed = Mathf.MoveTowards(speed, 0, (acceleration / 2f) * Time.deltaTime);

        // lane change input (no reset)
        if (Input.GetKeyDown(KeyCode.A))
            targetOffset = Mathf.Max(targetOffset - laneWidth, 0);
        else if (Input.GetKeyDown(KeyCode.D))
            targetOffset = Mathf.Min(targetOffset + laneWidth, laneWidth * (lanes.Length - 1));

        float maxOffset = laneWidth * (lanes.Length - 1) / 2f;
        targetOffset = Mathf.Clamp(targetOffset, -maxOffset, maxOffset);
        laneOffset = Mathf.Clamp(laneOffset, -maxOffset, maxOffset);
    }

    void FollowSpline()
    {
        if (lanes == null || lanes.Length == 0) return;

        // forward progress
        t = Mathf.Clamp01(t + (speed / 1000f) * Time.fixedDeltaTime);

        // center spline as base path
        int centerIndex = Mathf.FloorToInt((lanes.Length - 1) * 0.5f);
        LaneSpline mainSpline = lanes[centerIndex];

        Vector3 basePoint = mainSpline.GetPoint(t);
        Vector3 baseDir = mainSpline.GetDirection(t);
        Vector3 baseRight = Vector3.Cross(Vector3.up, baseDir).normalized;

        // smooth lateral offset
        laneOffset = Mathf.Lerp(laneOffset, targetOffset, Time.fixedDeltaTime * laneChangeSpeed);

        // recenter offset so that lane 0 is far left and last lane is far right
        float mid = (lanes.Length - 1) * 0.5f;
        Vector3 finalPos = basePoint + baseRight * (laneOffset - mid * laneWidth) + baseDir.normalized * 0.01f;


        finalPos.y = rb.position.y;
        rb.MovePosition(finalPos);
        rb.MoveRotation(Quaternion.Slerp(
            rb.rotation,
            Quaternion.LookRotation(baseDir, Vector3.up),
            Time.fixedDeltaTime * 5f
        ));
    }


    void StickToRoad()
    {
        // Cast several rays to "sample" the ground surface under the car
        Vector3 origin = transform.position + Vector3.up * 1f;
        Vector3[] rayOffsets = new Vector3[]
        {
        Vector3.zero,                                 // center
        transform.right * 0.6f,                       // right
        -transform.right * 0.6f,                      // left
        transform.forward * 0.6f,                     // front
        -transform.forward * 0.6f                     // back
        };

        Vector3 avgNormal = Vector3.zero;
        float avgY = 0f;
        int hitCount = 0;

        foreach (Vector3 offset in rayOffsets)
        {
            Vector3 rayOrigin = origin + offset;
            if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, rayDistance, roadLayer))
            {
                avgNormal += hit.normal;
                avgY += hit.point.y;
                hitCount++;

                // Debug rays (green if hit)
                Debug.DrawRay(rayOrigin, Vector3.down * hit.distance, Color.green);
            }
            else
            {
                // Red ray (no ground)
                Debug.DrawRay(rayOrigin, Vector3.down * rayDistance, Color.red);
            }
        }

        if (hitCount == 0)
        {
            // No ground detected, apply gentle gravity to fall back
            rb.AddForce(Vector3.down * 30f, ForceMode.Acceleration);
            return;
        }

        // Average ground position and normal
        avgNormal.Normalize();
        float targetY = (avgY / hitCount) + 0.05f; // 5 cm above average ground

        // Smooth spring-damper vertical stabilization
        float heightError = targetY - rb.position.y;
        float springStrength = 200f;   // stiffness (raise for tighter contact)
        float damper = 25f;            // damping factor
        float lift = (heightError * springStrength) - (rb.linearVelocity.y * damper);
        rb.AddForce(Vector3.up * lift, ForceMode.Acceleration);

        //// Align car rotation to ground normal
        //Quaternion alignRot = Quaternion.FromToRotation(transform.up, avgNormal) * rb.rotation;
        //rb.MoveRotation(Quaternion.Slerp(rb.rotation, alignRot, Time.deltaTime * 10f));

        // align only rotation, not vertical force
        if (Physics.Raycast(transform.position + Vector3.up, Vector3.down, out RaycastHit groundHit, rayDistance, roadLayer))
        {
            Quaternion alignRot = Quaternion.FromToRotation(transform.up, groundHit.normal) * rb.rotation;
            rb.MoveRotation(Quaternion.Slerp(rb.rotation, alignRot, Time.deltaTime * 5f));
        }

        // Safety clamp: ensure car never sinks below ground
        Vector3 pos = rb.position;
        float minHeight = targetY - 0.01f;
        if (pos.y < minHeight)
        {
            pos.y = minHeight;
            rb.position = pos;
        }

        // Optional aerodynamic downforce (improves high-speed grip)
        rb.AddForce(-transform.up * speed * 2f, ForceMode.Acceleration);

        

    }

    void DrawDebugRay()
    {
        Debug.DrawRay(transform.position + Vector3.up, Vector3.down * rayDistance, Color.green);
    }
}


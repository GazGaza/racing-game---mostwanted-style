using UnityEngine;

public class HybridSplineCarController : MonoBehaviour
{
    [Header("Spline Setup")]
    public LaneSpline[] lanes;             // assign spline lanes in inspector
    public float laneChangeSpeed = 3f;
    public float laneWidth = 3.5f;

    [Header("Car Physics")]
    public WheelCollider frontLeftCollider;
    public WheelCollider frontRightCollider;
    public WheelCollider rearLeftCollider;
    public WheelCollider rearRightCollider;

    public Transform frontLeftMesh;
    public Transform frontRightMesh;
    public Transform rearLeftMesh;
    public Transform rearRightMesh;

    public float motorTorque = 1500f;
    public float brakeTorque = 2000f;
    public float maxSteerAngle = 5f;

    [Header("Free Drive Movement")]
    public float acceleration = 10f;
    public float maxSpeed = 60f;
    public float brakeForce = 30f;
    public float stabilizationForce = 500f;

    [Header("Spline Assist Settings")]
    public float alignStrength = 25f;      // how fast it aligns with spline direction
    public float laneSnapStrength = 3f;    // how tight it stays centered
    public float verticalFollowSpeed = 5f; // for hills and slopes

    [Header("Burnout Settings")]
    public float rpm = 0f;
    public float maxRpm = 8000f;
    public float burnoutThreshold = 3000f;

    [Header("Ground Snap Settings")]
    public LayerMask groundMask;
    public float rideHeight = 0.3f;

    private Rigidbody rb;
    private int currentLane = 0;
    private int targetLane = 0;
    private bool raceStarted = false;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        rb.centerOfMass = new Vector3(0f, -0.35f, 0.05f);
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
    }

    void FixedUpdate()
    {
        HandleInput();
        HandleBurnout();

        if (raceStarted)
            ApplySplineAssist();

        HandleFreeDrive();
        UpdateWheels();
        StickToRoad();
    }

    void HandleInput()
    {
        if (Input.GetKeyDown(KeyCode.A) && targetLane > 0)
            targetLane--;
        if (Input.GetKeyDown(KeyCode.D) && targetLane < lanes.Length - 1)
            targetLane++;
    }

    void HandleFreeDrive()
    {
        float motorInput = 0f;
        float brakeInput = 0f;

        if (Input.GetKey(KeyCode.W))
            motorInput = 1f;
        if (Input.GetKey(KeyCode.S))
            brakeInput = 1f;

        // apply motor torque to rear wheels
        rearLeftCollider.motorTorque = motorInput * motorTorque;
        rearRightCollider.motorTorque = motorInput * motorTorque;

        // apply brake torque
        frontLeftCollider.brakeTorque = brakeInput * brakeTorque;
        frontRightCollider.brakeTorque = brakeInput * brakeTorque;
        rearLeftCollider.brakeTorque = brakeInput * brakeTorque;
        rearRightCollider.brakeTorque = brakeInput * brakeTorque;

        // small downward stabilization
        rb.AddForce(-transform.up * stabilizationForce * Time.fixedDeltaTime);
    }

    void HandleBurnout()
    {
        bool burnout = Input.GetKey(KeyCode.W) && Input.GetKey(KeyCode.S);
        float targetRpm = burnout ? maxRpm : Mathf.Lerp(0f, maxRpm, rb.linearVelocity.magnitude / maxSpeed);
        rpm = Mathf.MoveTowards(rpm, targetRpm, Time.deltaTime * 2000f);

        Debug.Log($"RPM: {(int)rpm} | Burnout: {burnout}");

        if (burnout)
        {
            // keep car in place but spin rear wheels visually
            rearLeftCollider.motorTorque = 0f;
            rearRightCollider.motorTorque = 0f;
            rb.linearVelocity = Vector3.zero;
        }
    }

    void ApplySplineAssist()
    {
        if (lanes == null || lanes.Length == 0) return;

        var mainSpline = lanes[targetLane];
        float t = mainSpline.FindNearestPoint(transform.position);
        Vector3 splinePoint = mainSpline.GetPoint(t);
        Vector3 splineDir = mainSpline.GetDirection(t);

        // steer alignment
        Quaternion targetRot = Quaternion.LookRotation(splineDir, Vector3.up);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.fixedDeltaTime * alignStrength);

        // lane centering
        Vector3 laneCenter = splinePoint;
        Vector3 desiredPos = new Vector3(laneCenter.x, transform.position.y, laneCenter.z);
        Vector3 lateralOffset = (desiredPos - transform.position) * laneSnapStrength * Time.fixedDeltaTime;
        rb.MovePosition(transform.position + lateralOffset);

        // follow road height
        Vector3 rayOrigin = splinePoint + Vector3.up * 2f;
        if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, 10f, groundMask))
        {
            float targetY = Mathf.Lerp(transform.position.y, hit.point.y + rideHeight, Time.fixedDeltaTime * verticalFollowSpeed);
            rb.MovePosition(new Vector3(rb.position.x, targetY, rb.position.z));
        }
    }

    void StickToRoad()
    {
        Vector3 rayOrigin = transform.position + Vector3.up * 1f;
        if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, 3f, groundMask))
        {
            Vector3 pos = rb.position;
            pos.y = Mathf.Lerp(pos.y, hit.point.y + rideHeight, Time.deltaTime * 8f);
            rb.MovePosition(pos);
        }
    }

    void UpdateWheels()
    {
        UpdateSingleWheel(frontLeftCollider, frontLeftMesh);
        UpdateSingleWheel(frontRightCollider, frontRightMesh);
        UpdateSingleWheel(rearLeftCollider, rearLeftMesh);
        UpdateSingleWheel(rearRightCollider, rearRightMesh);
    }

    void UpdateSingleWheel(WheelCollider col, Transform mesh)
    {
        col.GetWorldPose(out Vector3 pos, out Quaternion rot);
        mesh.position = pos;
        mesh.rotation = rot;
    }

    // Triggered by StartCheckpoint
    public void StartRace()
    {
        raceStarted = true;
        Debug.Log("Race Started! Spline assist enabled.");
    }
}

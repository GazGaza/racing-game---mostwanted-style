using UnityEngine;
using System.Collections.Generic;

public class HybridSplineCarController : MonoBehaviour
{
    [Header("Spline Setup")]
    public LaneSpline[] lanes;
    public float laneWidth = 3.5f;
    public float laneChangeSpeed = 3f;

    [Header("Car Physics")]
    public WheelCollider frontLeft;
    public WheelCollider frontRight;
    public WheelCollider rearLeft;
    public WheelCollider rearRight;

    public Transform frontLeftMesh;
    public Transform frontRightMesh;
    public Transform rearLeftMesh;
    public Transform rearRightMesh;

    public float motorTorque = 800f;
    public float brakeTorque = 1000f;
    public float maxSteerAngle = 5f; // small since spline controls direction

    [Header("Spline Control")]
    public float followStrength = 5f;   // how strongly car aligns with spline
    private Rigidbody rb;

    [Header("Movement Settings")]
    public float acceleration = 20f;
    public float maxSpeed = 60f;
    public float brakeForce = 30f;

    private int currentLane = 0;
    private int targetLane = 0;
    private float t = 0f;
    private float laneOffset = 0f;
    private float targetOffset = 0f;
    private float speed = 0f;

    [Header("Ground Snap")]
    public LayerMask groundMask;           // include the road's layer(s)
    public float rideHeight = 0.30f;       // meters from road surface to your car's pivot

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        rb.centerOfMass = new Vector3(0f, -0.35f, 0.05f);

        // find center lane index
        int centerIndex = Mathf.FloorToInt((lanes.Length - 1) * 0.5f);
        currentLane = centerIndex;
        targetLane = centerIndex;

        LaneSpline mainSpline = lanes[currentLane];

        // place the car exactly at the spline start
        t = 0f; // start of spline
        Vector3 startPoint = mainSpline.GetPoint(t);
        Vector3 startDir = mainSpline.GetDirection(t);

        // lift slightly above road surface so wheels rest on ground
        Vector3 spawnPos = startPoint + Vector3.up * 0.3f;
        transform.position = spawnPos;

        // rotate to spline direction
        transform.rotation = Quaternion.LookRotation(startDir, Vector3.up);

        // reset lane offset
        laneOffset = 0f;
        targetOffset = 0f;

        Debug.Log($"Car spawned at spline start: {spawnPos}");

        
    }

    void FixedUpdate()
    {
        HandleInput();
        FollowSplinePhysics();
        StickToRoad();
        rb.position = Vector3.Lerp(rb.position, rb.position + Vector3.down * 0.0005f, 0.5f);
        ResetWheelColliderRotation(); // 🔥 new line
        UpdateWheels();
    }

    void HandleInput()
    {
        // Accelerate
        if (Input.GetKey(KeyCode.W))
            speed = Mathf.MoveTowards(speed, maxSpeed, acceleration * Time.deltaTime);
        // Brake / Reverse
        else if (Input.GetKey(KeyCode.S))
            speed = Mathf.MoveTowards(speed, 0, brakeForce * Time.deltaTime);
        else
            speed = Mathf.MoveTowards(speed, 0, (acceleration / 3f) * Time.deltaTime);

        // Lane switching
        if (Input.GetKeyDown(KeyCode.A) && targetLane > 0)
            targetLane--;
        if (Input.GetKeyDown(KeyCode.D) && targetLane < lanes.Length - 1)
            targetLane++;

        // Calculate target offset
        targetOffset = (targetLane - ((lanes.Length - 1) * 0.5f)) * laneWidth;
    }

    void FollowSplinePhysics()
    {
        if (lanes == null || lanes.Length == 0) return;

        if (speed > 0f)
            t = Mathf.Clamp01(t + (speed / 200f) * Time.fixedDeltaTime);

        var baseSpline = lanes[currentLane];
        Vector3 basePoint = baseSpline.GetPoint(t);
        Vector3 baseDir = baseSpline.GetDirection(t);
        Vector3 baseRight = Vector3.Cross(baseDir, Vector3.up).normalized;

        laneOffset = Mathf.Lerp(laneOffset, targetOffset, Time.fixedDeltaTime * laneChangeSpeed);

        // target on XZ only
        Vector3 lateral = basePoint - baseRight * laneOffset;
        Vector3 target = new Vector3(lateral.x, transform.position.y, lateral.z);

        transform.position = Vector3.Lerp(transform.position, target, Time.fixedDeltaTime * 10f);
        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            Quaternion.LookRotation(baseDir, Vector3.up),
            Time.fixedDeltaTime * 5f
        );

    }

    void ResetWheelColliderRotation()
    {
        WheelCollider[] allWheels = {
        frontLeft, frontRight,
        rearLeft, rearRight
    };

        foreach (var wc in allWheels)
        {
            if (wc == null) continue;
            // Preserve position but reset rotation to upright (world up)
            var t = wc.transform;
            t.rotation = Quaternion.Euler(0f, t.rotation.eulerAngles.y, 0f);
        }
    }

    void UpdateWheels()
    {
        UpdateWheelPosition(frontLeft, frontLeftMesh);
        UpdateWheelPosition(frontRight, frontRightMesh);
        UpdateWheelPosition(rearLeft, rearLeftMesh);
        UpdateWheelPosition(rearRight, rearRightMesh);
    }

    void UpdateWheelPosition(WheelCollider col, Transform mesh)
    {
        if (col == null || mesh == null) return;

        Vector3 pos;
        Quaternion rot;

        // Gets current wheel world pose from collider
        col.GetWorldPose(out pos, out rot);

        // Smoothly interpolate mesh movement to avoid jitter
        mesh.position = Vector3.Lerp(mesh.position, pos, Time.deltaTime * 20f);
        mesh.rotation = Quaternion.Lerp(mesh.rotation, rot, Time.deltaTime * 20f);
    }

    void StickToRoad()
    {
        // Raycast just to get average ground height (not rotation)
        Vector3 origin = transform.position + Vector3.up * 1.0f;
        if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 5f, groundMask))
        {
            // Smooth vertical follow (without touching car rotation)
            float targetY = hit.point.y + 0.05f;
            Vector3 pos = rb.position;
            pos.y = Mathf.Lerp(pos.y, targetY, Time.deltaTime * 6f);
            rb.MovePosition(pos);

            // Aerodynamic downforce only (no rotation adjustment)
            rb.AddForce(-transform.up * speed * 2f, ForceMode.Acceleration);
        }
        else
        {
            rb.AddForce(Vector3.down * 20f, ForceMode.Acceleration);
        }

        // Very light angular damping to stop shake
        rb.angularVelocity *= 0.92f;
    }

}

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

    [Header("Engine Audio")]
    public AudioSource engineAudioSource;
    public float idleEnginePitch = 0.85f;
    public float maxEnginePitch = 2f;
    public float idleEngineVolume = 0.2f;
    public float maxEngineVolume = 0.85f;
    public float enginePitchResponse = 5f;
    public float engineVolumeResponse = 5f;

    private Rigidbody rb;
    private float currentLaneIndex = 0f;
    private int targetLane = 0;
    private float throttleInput = 0f;
    private float brakeInput = 0f;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        rb.centerOfMass = new Vector3(0f, -0.35f, 0.05f);
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

        if (lanes != null && lanes.Length > 0)
        {
            targetLane = Mathf.Clamp(targetLane, 0, lanes.Length - 1);
            currentLaneIndex = targetLane;
        }
        else
        {
            targetLane = 0;
            currentLaneIndex = 0f;
        }

        if (engineAudioSource != null)
        {
            engineAudioSource.loop = true;
            engineAudioSource.playOnAwake = false;
            engineAudioSource.pitch = idleEnginePitch;
            engineAudioSource.volume = idleEngineVolume;
        }
    }

    void FixedUpdate()
    {
        HandleInput();
        UpdateLaneTarget();
        HandleBurnout();

        ApplySplineAssist();

        HandleFreeDrive();
        HandleEngineAudio();
        UpdateWheels();
        StickToRoad();
    }

    void HandleInput()
    {
        if (lanes == null || lanes.Length == 0)
            return;

        if (Input.GetKeyDown(KeyCode.A) && targetLane > 0)
            targetLane--;
        if (Input.GetKeyDown(KeyCode.D) && targetLane < lanes.Length - 1)
            targetLane++;
    }

    void UpdateLaneTarget()
    {
        if (lanes == null || lanes.Length == 0) return;

        currentLaneIndex = Mathf.MoveTowards(currentLaneIndex, targetLane, laneChangeSpeed * Time.fixedDeltaTime);
    }

    void HandleFreeDrive()
    {
        float motorInput = 0f;
        float braking = 0f;

        if (Input.GetKey(KeyCode.W))
            motorInput = 1f;
        if (Input.GetKey(KeyCode.S))
            braking = 1f;

        throttleInput = motorInput;
        brakeInput = braking;

        // apply motor torque to rear wheels
        rearLeftCollider.motorTorque = motorInput * motorTorque;
        rearRightCollider.motorTorque = motorInput * motorTorque;

        // apply brake torque
        frontLeftCollider.brakeTorque = braking * brakeTorque;
        frontRightCollider.brakeTorque = braking * brakeTorque;
        rearLeftCollider.brakeTorque = braking * brakeTorque;
        rearRightCollider.brakeTorque = braking * brakeTorque;

        // small downward stabilization
        rb.AddForce(-transform.up * stabilizationForce * Time.fixedDeltaTime);
    }

    void HandleBurnout()
    {
        bool burnout = Input.GetKey(KeyCode.W) && Input.GetKey(KeyCode.S);
        float targetRpm = burnout ? maxRpm : Mathf.Lerp(0f, maxRpm, rb.linearVelocity.magnitude / maxSpeed);
        rpm = Mathf.MoveTowards(rpm, targetRpm, Time.deltaTime * 2000f);

        // Debug.Log($"RPM: {(int)rpm} | Burnout: {burnout}");

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

        int lowerLaneIndex = Mathf.Clamp(Mathf.FloorToInt(currentLaneIndex), 0, lanes.Length - 1);
        int upperLaneIndex = Mathf.Clamp(Mathf.CeilToInt(currentLaneIndex), 0, lanes.Length - 1);
        float laneBlend = Mathf.Clamp01(currentLaneIndex - lowerLaneIndex);

        int referenceLaneIndex = Mathf.Clamp(Mathf.RoundToInt(currentLaneIndex), 0, lanes.Length - 1);
        LaneSpline referenceLane = lanes[referenceLaneIndex];
        float t = referenceLane.FindNearestPoint(transform.position);

        LaneSpline lowerLane = lanes[lowerLaneIndex];
        LaneSpline upperLane = lanes[upperLaneIndex];

        Vector3 lowerPoint = lowerLane.GetPoint(t);
        Vector3 upperPoint = upperLane.GetPoint(t);
        Vector3 splinePoint = Vector3.Lerp(lowerPoint, upperPoint, laneBlend);

        Vector3 lowerDir = lowerLane.GetDirection(t);
        Vector3 upperDir = upperLane.GetDirection(t);
        Vector3 splineDir = Vector3.Slerp(lowerDir, upperDir, laneBlend).normalized;

        // steer alignment
        Quaternion targetRot = Quaternion.LookRotation(splineDir, Vector3.up);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.fixedDeltaTime * alignStrength);

        // lane centering
        Vector3 laneCenter = splinePoint;
        Vector3 desiredPos = new Vector3(laneCenter.x, transform.position.y, laneCenter.z);
        Vector3 targetPosition = Vector3.MoveTowards(transform.position, desiredPos, laneSnapStrength * Time.fixedDeltaTime);

        // follow road height
        Vector3 rayOrigin = splinePoint + Vector3.up * 2f;
        if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, 10f, groundMask))
        {
            float targetY = Mathf.Lerp(transform.position.y, hit.point.y + rideHeight, Time.fixedDeltaTime * verticalFollowSpeed);
            targetPosition.y = targetY;
        }

        rb.MovePosition(targetPosition);
    }

    void HandleEngineAudio()
    {
        if (engineAudioSource == null || engineAudioSource.clip == null)
            return;

        if (!engineAudioSource.isPlaying)
            engineAudioSource.Play();

        float speedPercent = Mathf.Clamp01(rb.linearVelocity.magnitude / Mathf.Max(0.01f, maxSpeed));
        float accelInfluence = Mathf.Max(throttleInput, speedPercent);

        float targetPitch = Mathf.Lerp(idleEnginePitch, maxEnginePitch, accelInfluence);
        if (brakeInput > 0f && throttleInput <= 0f)
            targetPitch = Mathf.Lerp(targetPitch, idleEnginePitch * 0.9f, brakeInput);

        float targetVolume = Mathf.Lerp(idleEngineVolume, maxEngineVolume, accelInfluence);
        if (brakeInput > 0f && throttleInput <= 0f)
            targetVolume = Mathf.Lerp(targetVolume, idleEngineVolume, brakeInput);

        engineAudioSource.pitch = Mathf.MoveTowards(engineAudioSource.pitch, targetPitch, Time.fixedDeltaTime * enginePitchResponse);
        engineAudioSource.volume = Mathf.MoveTowards(engineAudioSource.volume, targetVolume, Time.fixedDeltaTime * engineVolumeResponse);
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

    // Engine audio setup guidance:
    // 1. Create an empty child GameObject on the car and add an AudioSource component.
    // 2. Assign a looping engine clip (44.1 kHz WAV or OGG Vorbis are Unity-friendly formats).
    // 3. Drag that AudioSource into the engineAudioSource field in the inspector.
    // 4. Balance spatial blend and doppler settings to suit your camera setup.
}

using System.Collections.Generic;
using System.Reflection;
using EasyRoads3Dv3;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class HybridSplineCarController : MonoBehaviour
{
    [Header("EasyRoads Binding")]
    public ERModularRoad road;
    [Tooltip("Automatically search the parents for an ERModularRoad when none is assigned.")]
    public bool autoAssignRoad = true;
    [Tooltip("Fallback lane count when the EasyRoads road has no lane data.")]
    public int fallbackLaneCount = 3;
    [Tooltip("Override lane width in metres. Leave at 0 to use the width from the EasyRoads road settings.")]
    public float laneWidthOverride = 0f;
    [Tooltip("Fallback width when EasyRoads has no lane data and no override is provided.")]
    public float defaultLaneWidth = 3.5f;
    public float laneChangeSpeed = 3f;

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

    [Header("Lane Assist Settings")]
    public float alignStrength = 25f;
    public float laneSnapStrength = 3f;
    public float verticalFollowStrength = 5f;
    public float rideHeight = 0.3f;

    [Header("Burnout Settings")]
    public float rpm = 0f;
    public float maxRpm = 8000f;
    public float burnoutThreshold = 3000f;

    [Header("Ground Detection")]
    public LayerMask groundMask;

    [Header("Engine Audio")]
    public AudioSource engineAudioSource;
    public float idleEnginePitch = 0.85f;
    public float maxEnginePitch = 2f;
    public float idleEngineVolume = 0.2f;
    public float maxEngineVolume = 0.85f;
    public float enginePitchResponse = 5f;
    public float engineVolumeResponse = 5f;

    private struct RoadFrame
    {
        public Vector3 center;
        public Vector3 forward;
        public Vector3 right;
        public Vector3 up;
        public float distance;
    }

    private readonly List<RoadFrame> roadFrames = new List<RoadFrame>();
    private readonly HashSet<string> loggedMissingMethods = new HashSet<string>();

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

    void Awake()
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

    void Start()
    {
        EnsureRoadReady();
        targetLane = Mathf.Clamp(targetLane, 0, Mathf.Max(0, resolvedLaneCount - 1));
        currentLaneIndex = targetLane;

        if (engineAudioSource != null)
        {
            engineAudioSource.loop = true;
            engineAudioSource.playOnAwake = false;
            engineAudioSource.pitch = idleEnginePitch;
            engineAudioSource.volume = idleEngineVolume;
        }
    }

    void OnValidate()
    {
        fallbackLaneCount = Mathf.Max(1, fallbackLaneCount);
        laneChangeSpeed = Mathf.Max(0f, laneChangeSpeed);
        alignStrength = Mathf.Max(0f, alignStrength);
        laneSnapStrength = Mathf.Max(0f, laneSnapStrength);
        verticalFollowStrength = Mathf.Max(0f, verticalFollowStrength);
        defaultLaneWidth = Mathf.Max(0.5f, defaultLaneWidth);
        stabilizationForce = Mathf.Max(0f, stabilizationForce);
        rideHeight = Mathf.Max(0f, rideHeight);
        maxSpeed = Mathf.Max(0.1f, maxSpeed);
        motorTorque = Mathf.Max(0f, motorTorque);
        brakeTorque = Mathf.Max(0f, brakeTorque);

        if (!Application.isPlaying)
        {
            if (autoAssignRoad && road == null)
                road = GetComponentInParent<ERModularRoad>();
            roadDataDirty = true;
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
    }

    void HandleInput(bool roadReady)
    {
        if (lanes == null || lanes.Length == 0)
            return;

        if (Input.GetKeyDown(KeyCode.A) && targetLane > 0)
            targetLane--;
        if (roadReady && Input.GetKeyDown(KeyCode.D) && targetLane < laneLimit - 1)
            targetLane++;

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

        throttleInput = motorInput;
        brakeInput = braking;
    }

        // apply brake torque
        frontLeftCollider.brakeTorque = braking * brakeTorque;
        frontRightCollider.brakeTorque = braking * brakeTorque;
        rearLeftCollider.brakeTorque = braking * brakeTorque;
        rearRightCollider.brakeTorque = braking * brakeTorque;

        rb.AddForce(-transform.up * stabilizationForce * Time.fixedDeltaTime);
    }

    void HandleBurnout()
    {
        bool burnout = Input.GetKey(KeyCode.W) && Input.GetKey(KeyCode.S);
        float targetRpm = burnout ? maxRpm : Mathf.Lerp(0f, maxRpm, rb.velocity.magnitude / Mathf.Max(0.01f, maxSpeed));
        rpm = Mathf.MoveTowards(rpm, targetRpm, Time.deltaTime * 2000f);

        if (burnout)
        {
            rearLeftCollider.motorTorque = 0f;
            rearRightCollider.motorTorque = 0f;
            rb.velocity = Vector3.zero;
        }
    }

    void ApplyRoadAssist()
    {
        if (roadFrames.Count == 0)
            return;

        int closestIndex = FindClosestFrameIndex(transform.position);
        RoadFrame frame = roadFrames[closestIndex];

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

        Vector3 lowerPos = frame.center + frame.right * GetLaneOffset(lowerLane);
        Vector3 upperPos = frame.center + frame.right * GetLaneOffset(upperLane);
        Vector3 blendedPos = Vector3.Lerp(lowerPos, upperPos, laneBlend);

        Quaternion targetRot = Quaternion.LookRotation(frame.forward, frame.up);
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

    void HandleEngineAudio()
    {
        if (engineAudioSource == null || engineAudioSource.clip == null)
            return;

        if (!engineAudioSource.isPlaying)
            engineAudioSource.Play();

        float speedPercent = Mathf.Clamp01(rb.velocity.magnitude / Mathf.Max(0.01f, maxSpeed));
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

    void UpdateWheels()
    {
        UpdateSingleWheel(frontLeftCollider, frontLeftMesh);
        UpdateSingleWheel(frontRightCollider, frontRightMesh);
        UpdateSingleWheel(rearLeftCollider, rearLeftMesh);
        UpdateSingleWheel(rearRightCollider, rearRightMesh);
    }

    void UpdateSingleWheel(WheelCollider col, Transform mesh)
    {
        if (col == null || mesh == null)
            return;

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

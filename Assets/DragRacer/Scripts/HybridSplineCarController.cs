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

    private Rigidbody rb;
    private float currentLaneIndex = 0f;
    private int targetLane = 0;
    private float throttleInput = 0f;
    private float brakeInput = 0f;
    private int resolvedLaneCount = 1;
    private int lastClosestIndex = 0;
    private bool roadDataDirty = true;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.centerOfMass = new Vector3(0f, -0.35f, 0.05f);
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
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
        bool roadReady = EnsureRoadReady();

        HandleInput(roadReady);
        UpdateLaneTarget(roadReady);
        HandleBurnout();

        if (roadReady)
            ApplyRoadAssist();

        HandleFreeDrive();
        HandleEngineAudio();
        UpdateWheels();
    }

    void HandleInput(bool roadReady)
    {
        int laneLimit = Mathf.Max(1, resolvedLaneCount);

        if (roadReady && Input.GetKeyDown(KeyCode.A) && targetLane > 0)
            targetLane--;
        if (roadReady && Input.GetKeyDown(KeyCode.D) && targetLane < laneLimit - 1)
            targetLane++;

        float motorInput = 0f;
        float braking = 0f;

        if (Input.GetKey(KeyCode.W))
            motorInput = 1f;
        if (Input.GetKey(KeyCode.S))
            braking = 1f;

        throttleInput = motorInput;
        brakeInput = braking;
    }

    void UpdateLaneTarget(bool roadReady)
    {
        if (!roadReady)
        {
            currentLaneIndex = 0f;
            targetLane = 0;
            return;
        }

        int laneLimit = Mathf.Max(1, resolvedLaneCount);
        targetLane = Mathf.Clamp(targetLane, 0, laneLimit - 1);
        currentLaneIndex = Mathf.MoveTowards(currentLaneIndex, targetLane, laneChangeSpeed * Time.fixedDeltaTime);
    }

    void HandleFreeDrive()
    {
        rearLeftCollider.motorTorque = throttleInput * motorTorque;
        rearRightCollider.motorTorque = throttleInput * motorTorque;

        float brake = brakeInput * brakeTorque;
        frontLeftCollider.brakeTorque = brake;
        frontRightCollider.brakeTorque = brake;
        rearLeftCollider.brakeTorque = brake;
        rearRightCollider.brakeTorque = brake;

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

        float clampedLaneIndex = Mathf.Clamp(currentLaneIndex, 0f, Mathf.Max(1, resolvedLaneCount) - 1);
        int lowerLane = Mathf.Clamp(Mathf.FloorToInt(clampedLaneIndex), 0, resolvedLaneCount - 1);
        int upperLane = Mathf.Clamp(Mathf.CeilToInt(clampedLaneIndex), 0, resolvedLaneCount - 1);
        float laneBlend = Mathf.Clamp01(clampedLaneIndex - lowerLane);

        Vector3 lowerPos = frame.center + frame.right * GetLaneOffset(lowerLane);
        Vector3 upperPos = frame.center + frame.right * GetLaneOffset(upperLane);
        Vector3 blendedPos = Vector3.Lerp(lowerPos, upperPos, laneBlend);

        Quaternion targetRot = Quaternion.LookRotation(frame.forward, frame.up);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.fixedDeltaTime * alignStrength);

        Vector3 horizontalTarget = Vector3.Lerp(rb.position, new Vector3(blendedPos.x, rb.position.y, blendedPos.z), Time.fixedDeltaTime * laneSnapStrength);
        float targetY = Mathf.Lerp(rb.position.y, (blendedPos + frame.up * rideHeight).y, Time.fixedDeltaTime * verticalFollowStrength);
        Vector3 finalTarget = new Vector3(horizontalTarget.x, targetY, horizontalTarget.z);

        rb.MovePosition(finalTarget);
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

    bool EnsureRoadReady()
    {
        if (road == null && autoAssignRoad)
            road = GetComponentInParent<ERModularRoad>();

        if (road == null)
        {
            resolvedLaneCount = Mathf.Max(1, fallbackLaneCount);
            return false;
        }

        if (roadDataDirty || roadFrames.Count == 0)
            CacheRoadData();

        resolvedLaneCount = Mathf.Max(1, road.totalLanes > 0 ? road.totalLanes : fallbackLaneCount);
        targetLane = Mathf.Clamp(targetLane, 0, resolvedLaneCount - 1);

        return roadFrames.Count > 0;
    }

    void CacheRoadData()
    {
        roadFrames.Clear();
        lastClosestIndex = 0;

        if (road == null)
            return;

        Vector3[] centers = InvokeSplineSample("GetSplinePointsCenter", true);
        if (centers == null || centers.Length == 0)
            return;

        Vector3[] lefts = InvokeSplineSample("GetSplinePointsLeftSide");
        Vector3[] rights = InvokeSplineSample("GetSplinePointsRightSide");

        float cumulative = 0f;
        roadFrames.Capacity = centers.Length;

        for (int i = 0; i < centers.Length; i++)
        {
            Vector3 center = centers[i];
            Vector3 prev = centers[Mathf.Max(0, i - 1)];
            Vector3 next = centers[Mathf.Min(centers.Length - 1, i + 1)];
            Vector3 forward = (next - prev).normalized;
            if (forward.sqrMagnitude < 0.0001f)
                forward = (next - center).sqrMagnitude > 0.0001f ? (next - center).normalized : transform.forward;

            Vector3 right = Vector3.zero;
            if (lefts != null && rights != null && i < lefts.Length && i < rights.Length)
            {
                Vector3 lateral = rights[i] - lefts[i];
                if (lateral.sqrMagnitude > 0.0001f)
                    right = lateral.normalized;
            }

            if (right.sqrMagnitude < 0.0001f)
            {
                Vector3 projectedForward = new Vector3(forward.x, 0f, forward.z);
                if (projectedForward.sqrMagnitude < 0.0001f)
                    projectedForward = Vector3.forward;
                right = Quaternion.AngleAxis(90f, Vector3.up) * projectedForward.normalized;
            }

            Vector3 up = Vector3.Cross(forward, right).normalized;
            if (up.sqrMagnitude < 0.0001f)
                up = Vector3.up;

            right = Vector3.Cross(up, forward).normalized;

            if (i > 0)
                cumulative += Vector3.Distance(center, centers[i - 1]);

            roadFrames.Add(new RoadFrame
            {
                center = center,
                forward = forward,
                right = right,
                up = up,
                distance = cumulative
            });
        }

        roadDataDirty = false;
    }

    Vector3[] InvokeSplineSample(string methodName, bool logIfMissing = false)
    {
        if (road == null)
            return null;

        MethodInfo method = road.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (method == null || method.ReturnType != typeof(Vector3[]) || method.GetParameters().Length != 0)
        {
            if (logIfMissing && !loggedMissingMethods.Contains(methodName))
            {
                Debug.LogWarning($"[{nameof(HybridSplineCarController)}] {methodName} is unavailable on road '{road.name}'. Lane binding disabled until spline data is generated.");
                loggedMissingMethods.Add(methodName);
            }
            return null;
        }

        try
        {
            return method.Invoke(road, null) as Vector3[];
        }
        catch (System.Exception ex)
        {
            if (!loggedMissingMethods.Contains(methodName))
            {
                Debug.LogWarning($"[{nameof(HybridSplineCarController)}] Failed to invoke {methodName} on road '{road.name}': {ex.Message}");
                loggedMissingMethods.Add(methodName);
            }
            return null;
        }
    }

    int FindClosestFrameIndex(Vector3 position)
    {
        if (roadFrames.Count == 0)
            return 0;

        int closest = Mathf.Clamp(lastClosestIndex, 0, roadFrames.Count - 1);
        float bestSqr = (position - roadFrames[closest].center).sqrMagnitude;

        for (int i = 0; i < roadFrames.Count; i++)
        {
            float sqr = (position - roadFrames[i].center).sqrMagnitude;
            if (sqr < bestSqr)
            {
                bestSqr = sqr;
                closest = i;
            }
        }

        lastClosestIndex = closest;
        return closest;
    }

    float GetLaneOffset(int laneIndex)
    {
        int laneLimit = Mathf.Max(1, resolvedLaneCount);
        laneIndex = Mathf.Clamp(laneIndex, 0, laneLimit - 1);

        float width = Mathf.Max(0.01f, ResolveLaneWidth());
        float startOffset = -((laneLimit - 1) * width) * 0.5f;
        return startOffset + laneIndex * width;
    }

    float ResolveLaneWidth()
    {
        if (laneWidthOverride > 0f)
            return laneWidthOverride;

        if (road != null && road.laneWidth > 0f)
            return road.laneWidth;

        return defaultLaneWidth;
    }
}

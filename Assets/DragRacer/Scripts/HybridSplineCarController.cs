using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class HybridSplineCarController : MonoBehaviour
{
    [Header("Lane System")]
    public Transform[] laneCenters;
    public int currentLane = 1;
    public float laneWidth = 3.5f;
    public float laneChangeSpeed = 3f;
    public float moveSpeed = 20f;

    [Header("Car Physics")]
    public WheelCollider frontLeftCollider;
    public WheelCollider frontRightCollider;
    public WheelCollider rearLeftCollider;
    public WheelCollider rearRightCollider;

    public Transform frontLeftMesh;
    public Transform frontRightMesh;
    public Transform rearLeftMesh;
    public Transform rearRightMesh;

    [Header("Driving")]
    public float motorTorque = 1500f;
    public float brakeTorque = 2000f;
    public float rideHeight = 0.3f;
    public LayerMask groundMask;

    [Header("Burnout / RPM")]
    public float rpm = 0f;
    public float maxRpm = 8000f;

    [Header("Engine Audio")]
    public AudioSource engineAudioSource;
    public float idlePitch = 0.85f;
    public float maxPitch = 2f;
    public float idleVolume = 0.2f;
    public float maxVolume = 0.9f;
    public float pitchSmooth = 5f;
    public float volumeSmooth = 5f;

    private Rigidbody rb;
    private float targetLaneOffset = 0f;
    private float currentLaneOffset = 0f;
    private int totalLanes;
    private float forwardPos = 0f;

    private float throttleInput;
    private float brakeInput;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.centerOfMass = new Vector3(0f, -0.35f, 0.05f);
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

        totalLanes = laneCenters.Length;

        if (engineAudioSource != null)
        {
            engineAudioSource.loop = true;
            engineAudioSource.playOnAwake = false;
            engineAudioSource.pitch = idlePitch;
            engineAudioSource.volume = idleVolume;
        }
    }

    private void Start()
    {
        if (engineAudioSource != null && !engineAudioSource.isPlaying)
            engineAudioSource.Play();
    }

    private void FixedUpdate()
    {
        HandleInput();
        HandleBurnout();
        HandleDrive();
        FollowLanes();
        HandleEngineAudio();
        UpdateWheels();
    }

    // ---------------- Input ----------------
    private void HandleInput()
    {
        if (Input.GetKeyDown(KeyCode.A) && currentLane > 0)
        {
            currentLane--;
            targetLaneOffset = (currentLane - (totalLanes - 1) / 2f) * laneWidth;
        }

        if (Input.GetKeyDown(KeyCode.D) && currentLane < totalLanes - 1)
        {
            currentLane++;
            targetLaneOffset = (currentLane - (totalLanes - 1) / 2f) * laneWidth;
        }

        currentLaneOffset = Mathf.Lerp(currentLaneOffset, targetLaneOffset, Time.deltaTime * laneChangeSpeed);

        throttleInput = Input.GetKey(KeyCode.W) ? 1f : 0f;
        brakeInput = Input.GetKey(KeyCode.S) ? 1f : 0f;
    }

    // ---------------- Lane Follow ----------------
    private void FollowLanes()
    {
        if (laneCenters.Length < 2) return;

        forwardPos += moveSpeed * Time.fixedDeltaTime;
        if (forwardPos >= (laneCenters.Length - 1))
            forwardPos = laneCenters.Length - 1;

        int index = Mathf.FloorToInt(forwardPos);
        int next = Mathf.Min(index + 1, laneCenters.Length - 1);
        float t = forwardPos - index;

        Vector3 basePos = Vector3.Lerp(laneCenters[index].position, laneCenters[next].position, t);
        Vector3 forward = (laneCenters[next].position - laneCenters[index].position).normalized;
        Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;

        Vector3 laneTarget = basePos + right * currentLaneOffset;
        Quaternion targetRot = Quaternion.LookRotation(forward, Vector3.up);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.fixedDeltaTime * 5f);

        Vector3 targetPos = new Vector3(laneTarget.x, transform.position.y, laneTarget.z);
        rb.MovePosition(Vector3.Lerp(transform.position, targetPos, Time.fixedDeltaTime * 6f));

        if (Physics.Raycast(transform.position + Vector3.up, Vector3.down, out RaycastHit hit, 5f, groundMask))
        {
            float targetY = Mathf.Lerp(transform.position.y, hit.point.y + rideHeight, Time.fixedDeltaTime * 8f);
            rb.MovePosition(new Vector3(rb.position.x, targetY, rb.position.z));
        }

        Debug.DrawLine(transform.position, laneTarget, Color.cyan);
    }

    // ---------------- Drive ----------------
    private void HandleDrive()
    {
        rearLeftCollider.motorTorque = throttleInput * motorTorque;
        rearRightCollider.motorTorque = throttleInput * motorTorque;

        float brakeForce = brakeInput * brakeTorque;
        frontLeftCollider.brakeTorque = brakeForce;
        frontRightCollider.brakeTorque = brakeForce;
        rearLeftCollider.brakeTorque = brakeForce;
        rearRightCollider.brakeTorque = brakeForce;
    }

    // ---------------- Burnout / RPM ----------------
    private void HandleBurnout()
    {
        bool burnout = Input.GetKey(KeyCode.W) && Input.GetKey(KeyCode.S);
        float targetRpm = burnout ? maxRpm : Mathf.Lerp(1000f, maxRpm, rb.linearVelocity.magnitude / 60f);
        rpm = Mathf.MoveTowards(rpm, targetRpm, Time.deltaTime * 2000f);

        if (burnout)
        {
            rearLeftCollider.motorTorque = 0f;
            rearRightCollider.motorTorque = 0f;
            rb.linearVelocity = Vector3.zero;
        }
    }

    // ---------------- Engine Audio ----------------
    private void HandleEngineAudio()
    {
        if (engineAudioSource == null)
            return;

        float speedPercent = Mathf.Clamp01(rb.linearVelocity.magnitude / moveSpeed);
        float throttleInfluence = Mathf.Max(throttleInput, speedPercent);
        float brakeInfluence = brakeInput;

        // Calculate target pitch
        float targetPitch = Mathf.Lerp(idlePitch, maxPitch, throttleInfluence);
        if (brakeInfluence > 0f && throttleInfluence <= 0.1f)
            targetPitch = Mathf.Lerp(targetPitch, idlePitch * 0.8f, brakeInfluence);

        // Calculate target volume
        float targetVolume = Mathf.Lerp(idleVolume, maxVolume, throttleInfluence);
        if (brakeInfluence > 0f && throttleInfluence <= 0.1f)
            targetVolume = Mathf.Lerp(targetVolume, idleVolume, brakeInfluence);

        engineAudioSource.pitch = Mathf.Lerp(engineAudioSource.pitch, targetPitch, Time.deltaTime * pitchSmooth);
        engineAudioSource.volume = Mathf.Lerp(engineAudioSource.volume, targetVolume, Time.deltaTime * volumeSmooth);
    }

    // ---------------- Wheels ----------------
    private void UpdateWheels()
    {
        UpdateWheel(frontLeftCollider, frontLeftMesh);
        UpdateWheel(frontRightCollider, frontRightMesh);
        UpdateWheel(rearLeftCollider, rearLeftMesh);
        UpdateWheel(rearRightCollider, rearRightMesh);
    }

    private void UpdateWheel(WheelCollider col, Transform mesh)
    {
        if (col == null || mesh == null) return;
        col.GetWorldPose(out Vector3 pos, out Quaternion rot);
        mesh.position = pos;
        mesh.rotation = rot;
    }
}

using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class CarController : MonoBehaviour
{
    [Header("Steering (Flat Track)")]
    public float wheelbase = 2.6f;        // m
    public float maxSteerDegSlow = 35f;   // steering at 0 m/s
    public float maxSteerDegFast = 6f;    // steering at >= vSteerFade
    public float vSteerFade = 25f;        // m/s (~90 km/h)
    public float yawGain = 6f;            // how fast we chase target yaw rate
    public float lateralGrip = 8f;        // higher = sticks to lane, lower = slide

    private float steerInput;             // -1..1 (A/D or arrows)

    public VehicleData data;
    public bool autoShift = false;

    [Header("Debug")]
    public bool debugLogs = true;       // toggle logs
    public float logInterval = 0.25f;   // seconds between logs
    private float logTimer;

    private Rigidbody rb;
    private EngineModel engine;

    private int gear = 1;               // start in 1st (avoid neutral creep confusion)
    private float clutch = 1f;          // 0 open, 1 locked
    private float shiftTimer = 0f;

    private float throttle;             // 0..1
    private bool shiftUpReq;

    private float normalLoad;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.mass = data.massKg;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        engine = new EngineModel(data);
        normalLoad = data.massKg * 9.81f; // simple single-axle load
    }

    void Update()
    {
        // TEMP keyboard input (legacy)
        throttle = Input.GetKey(KeyCode.W) ? 1f : 0f;
        if (Input.GetKeyDown(KeyCode.Space)) shiftUpReq = true;

        if (autoShift && engine.RPM > data.redlineRpm * 0.98f) shiftUpReq = true;

        // Left/Right steering (legacy input; we'll wire Input System later)
        steerInput = 0f;
        if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) steerInput = -1f;
        if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) steerInput = 1f;
    }

    void FixedUpdate()
    {
        float dt = Time.fixedDeltaTime;

        // Shift handling (torque cut during shift)
        if (shiftTimer > 0f) shiftTimer -= dt;

        if (shiftUpReq && gear < data.gearRatios.Length)
        {
            gear++;
            shiftTimer = data.shiftTime;
            shiftUpReq = false;
        }

        float v = Vector3.Dot(rb.linearVelocity, transform.forward); // car forward speed (m/s)
        

        // Ratio
        float ratio = (gear > 0) ? data.gearRatios[gear - 1] * data.finalDrive : 0f;

        // Driveline kinematics: engine target RPM from car speed (if clutch locked)
        float wheelAngularFromCar = (v / Mathf.Max(0.01f, data.wheelRadius)) * Mathf.Rad2Deg / 60f; // as RPM
        float engineRpmFromCar = (ratio > 0f) ? wheelAngularFromCar * ratio : data.idleRpm;

        // Clutch blend: engine RPM tends toward driveline RPM
        float rpm = Mathf.Lerp(engine.RPM, engineRpmFromCar, data.clutchEngageSpeed * dt * clutch);
        engine.SetRPM(rpm);

        // Engine torque (cut during shift)
        float loadTorque = 0f; // we back-fill below, use 0 for integration step
        float engineTorque = (shiftTimer > 0f) ? 0f : engine.Update(throttle, loadTorque, dt);

        // Map to wheel torque & available force
        float wheelTorque = (ratio > 0f) ? engineTorque * ratio * clutch : 0f;          // Nm at wheels
        float maxAvailableForce = Mathf.Abs(wheelTorque) / Mathf.Max(0.01f, data.wheelRadius);

        // Wheel linear speed implied by engine RPM (free-running side)
        float wheelAngularEngine = (ratio > 0f) ? (engine.RPM / ratio) * (Mathf.PI * 2f / 60f) : 0f; // rad/s
        float wheelLinearFromEngine = wheelAngularEngine * data.wheelRadius;

        // Tire force with BOTH caps (traction & available)
        float driveForce = TireLongitudinal.ComputeDriveForce(
            data, normalLoad, wheelLinearFromEngine, v, maxAvailableForce, out float slip);

        // Back-calc load torque for next engine integration (reaction from tire)
        loadTorque = driveForce * data.wheelRadius / Mathf.Max(1f, ratio);

        // Apply forces
        rb.AddForce(transform.forward * driveForce, ForceMode.Force);

        // Aero drag
        
        float aero = 0.5f * 1.2f * data.dragCoeff * data.frontalArea * v * v;
        rb.AddForce(-Mathf.Sign(v) * transform.forward * aero, ForceMode.Force);

        // ===== Steering (flat ground) =====
        // Speed-based max steering
        float t = Mathf.InverseLerp(0f, vSteerFade, Mathf.Abs(v)); // v is forward speed (m/s)
        float maxSteerDeg = Mathf.Lerp(maxSteerDegSlow, maxSteerDegFast, t);
        float steerDeg = steerInput * maxSteerDeg;
        float steerRad = steerDeg * Mathf.Deg2Rad;

        // Target yaw rate from basic bicycle model: r = v/L * tan(delta)
        float targetYawRate = (v / Mathf.Max(1f, wheelbase)) * Mathf.Tan(steerRad);

        // Current yaw rate about world up (flat track -> Vector3.up)
        float yawRateNow = Vector3.Dot(rb.angularVelocity, Vector3.up);

        // Torque to chase target yaw rate (Acceleration mode -> mass independent)
        float yawAccel = (targetYawRate - yawRateNow) * yawGain;
        rb.AddTorque(Vector3.up * yawAccel, ForceMode.Acceleration);

        // Lateral grip: damp sideways velocity relative to car forward
        Vector3 vel = rb.linearVelocity;
        Vector3 fwd = transform.forward;
        Vector3 lateral = vel - fwd * Vector3.Dot(vel, fwd); // sideways component
        rb.AddForce(-lateral * lateralGrip * rb.mass, ForceMode.Force);

        // (Optional) debug peek
        if (debugLogs && logTimer <= 0f)
        {
            Debug.Log($"[Steer] steer={steerInput:F2} deg={steerDeg:F1} v={v * 3.6f:F0}km/h yawNow={yawRateNow:F2} targ={targetYawRate:F2}");
        }

        // Debug logging
        if (debugLogs)
        {
            logTimer -= dt;
            if (logTimer <= 0f)
            {
                logTimer = Mathf.Max(0.05f, logInterval);
                Debug.Log(
                    $"[Car] gear={gear} thr={throttle:F2} rpm={engine.RPM:F0} " +
                    $"engTq={engineTorque:F0}Nm wheelTq={wheelTorque:F0}Nm " +
                    $"availF={maxAvailableForce:F0}N slip={slip:F2} " +
                    $"v={v * 3.6f:F1}km/h driveF={driveForce:F0}N aeroF={aero:F0}N");
            }
        }
    }
}

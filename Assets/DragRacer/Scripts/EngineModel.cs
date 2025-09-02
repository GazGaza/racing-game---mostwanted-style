using UnityEngine;


public class EngineModel
{
    private VehicleData data;
    private float rpm;
    private float limiterTimer;


    public float RPM => rpm;



    public EngineModel(VehicleData d)
    {
        data = d;
        rpm = d.idleRpm;

        // Safety: if no keys, inject a placeholder curve and log a warning
        if (data.torqueCurveNm == null || data.torqueCurveNm.length < 2)
        {
            Debug.LogWarning($"[EngineModel] Torque curve is empty on '{data.name}'. Injecting placeholder.");
            var c = new AnimationCurve();
            c.AddKey(1000f, 120f);
            c.AddKey(2500f, 220f);
            c.AddKey(4000f, 300f);
            c.AddKey(5500f, 320f);
            c.AddKey(6500f, 290f);
            c.AddKey(data.redlineRpm, 0f);
            data.torqueCurveNm = c;
        }
    }





    public float Update(float throttle01, float loadTorque, float dt)
    {
        // torque from curve
        float torque = data.torqueCurveNm.Evaluate(rpm);


        // simple rev limiter cut
        bool cut = rpm >= data.redlineRpm && limiterTimer <= 0f;
        if (cut) limiterTimer = data.revLimiterCutMs * 0.001f;
        if (limiterTimer > 0f) { limiterTimer -= dt; torque = 0f; }


        // scale by throttle
        torque *= Mathf.Clamp01(throttle01);


        // integrate angular accel: alpha = (Tengine - Tload)/I
        float I = data.drivelineInertia;
        float alpha = (torque - loadTorque) / Mathf.Max(0.01f, I);
        rpm += alpha * dt * Mathf.Rad2Deg / 60f; // rough mapping rad/s -> RPM
        rpm = Mathf.Max(data.idleRpm, rpm);


        return torque;
    }


    public void SetRPM(float newRpm) => rpm = Mathf.Max(data.idleRpm, newRpm);
}

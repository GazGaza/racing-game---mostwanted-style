using UnityEngine;

public static class TireLongitudinal
{
    // slip = (wheelLinear - carLinear)/max(|carLinear|, eps)
    public static float ComputeDriveForce(
        VehicleData v,
        float normalForce,
        float wheelLinearSpeed,
        float carLinearSpeed,
        float maxAvailableForce,   // NEW: cap by drivetrain torque
        out float slip)
    {
        float eps = 0.2f;
        slip = (wheelLinearSpeed - carLinearSpeed) / Mathf.Max(Mathf.Abs(carLinearSpeed), eps);

        float slip01 = Mathf.Clamp01(Mathf.Abs(slip));
        float mu = v.baseMu * v.muSlipCurve.Evaluate(slip01);
        float maxTraction = mu * normalForce;

        float sign = Mathf.Sign(wheelLinearSpeed - carLinearSpeed);
        float cap = Mathf.Min(maxTraction, Mathf.Max(0f, maxAvailableForce));
        return sign * cap;
    }
}

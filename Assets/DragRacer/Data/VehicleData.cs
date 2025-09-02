using UnityEngine;


[CreateAssetMenu(menuName = "DragRacer/VehicleData")]
public class VehicleData : ScriptableObject
{
    [Header("Meta")]
    public string displayName;


    [Header("Mass & Aero")]
    public float massKg = 1400f; // curb mass
    public float dragCoeff = 0.34f; // Cd approx
    public float frontalArea = 2.2f; // m^2


    [Header("Tires & Grip")]
    public float baseMu = 1.4f; // peak μ on VHT track
    public AnimationCurve muSlipCurve = AnimationCurve.EaseInOut(0, 0, 1, 1); // shape for slip->mu


    [Header("Engine & Drivetrain")]
    public AnimationCurve torqueCurveNm; // x: RPM, y: torque (Nm)
    public float idleRpm = 1000f;
    public float redlineRpm = 8000f;
    public float revLimiterCutMs = 120f;


    [Tooltip("Gear ratios from 1..N; include final drive separately")]
    public float[] gearRatios = new float[] { 3.2f, 2.1f, 1.5f, 1.15f, 0.95f };
    public float finalDrive = 3.9f;


    [Header("Wheels")]
    public float wheelRadius = 0.33f; // meters (26" slick ≈ 0.33 m radius)
    public float drivelineInertia = 0.3f; // simplified lumped inertia


    [Header("Shift & Clutch")]
    public float shiftTime = 0.120f; // seconds torque cut
    public float clutchEngageSpeed = 5f; // how fast clutch closes (rad/s)
}
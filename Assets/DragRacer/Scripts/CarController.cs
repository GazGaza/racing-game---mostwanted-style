using UnityEngine;

public class CarController : MonoBehaviour
{
    [Header("Wheels - Colliders")]
    public WheelCollider frontLeftCollider;
    public WheelCollider frontRightCollider;
    public WheelCollider rearLeftCollider;
    public WheelCollider rearRightCollider;

    [Header("Wheels - Meshes")]
    public Transform frontLeftMesh;
    public Transform frontRightMesh;
    public Transform rearLeftMesh;
    public Transform rearRightMesh;

    [Header("Car Settings")]
    public float maxMotorTorque = 500f;   // engine force
    public float maxSteeringAngle = 30f;  // turning angle
    public float brakeForce = 1000f;      // brake strength

    [Header("Engine Simulation")]
    public float rpm;                     
    public float rpmMultiplier = 50f;     

    private void FixedUpdate()
    {
        float motor = maxMotorTorque * Input.GetAxis("Vertical");  // W/S
        float steering = maxSteeringAngle * Input.GetAxis("Horizontal"); // A/D

        // Apply steering (front-wheel steering)
        frontLeftCollider.steerAngle = steering;
        frontRightCollider.steerAngle = steering;

        // Apply motor torque (FWD)
        frontLeftCollider.motorTorque = motor;
        frontRightCollider.motorTorque = motor;

        // Braking when S is pressed
        if (Input.GetKey(KeyCode.S))
        {
            frontLeftCollider.brakeTorque = brakeForce;
            frontRightCollider.brakeTorque = brakeForce;
            rearLeftCollider.brakeTorque = brakeForce;
            rearRightCollider.brakeTorque = brakeForce;
        }
        else
        {
            frontLeftCollider.brakeTorque = 0f;
            frontRightCollider.brakeTorque = 0f;
            rearLeftCollider.brakeTorque = 0f;
            rearRightCollider.brakeTorque = 0f;
        }

        // Update meshes to follow colliders
        UpdateWheelPose(frontLeftCollider, frontLeftMesh);
        UpdateWheelPose(frontRightCollider, frontRightMesh);
        UpdateWheelPose(rearLeftCollider, rearLeftMesh);
        UpdateWheelPose(rearRightCollider, rearRightMesh);

        // Fake engine RPM
        rpm = Mathf.Abs(frontLeftCollider.rpm) * rpmMultiplier;
    }

    private void UpdateWheelPose(WheelCollider collider, Transform mesh)
    {
        if (collider == null || mesh == null) return;

        Vector3 pos;
        Quaternion rot;
        collider.GetWorldPose(out pos, out rot);

        mesh.position = pos;
        mesh.rotation = rot;
    }
}

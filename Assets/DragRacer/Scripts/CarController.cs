using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class CarController : MonoBehaviour
{
    public float acceleration = 8000f;
    public float brakeForce = 12000f;
    public float maxSpeed = 200f;

    private Rigidbody rb;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    void FixedUpdate()
    {
        float forwardInput = Input.GetKey(KeyCode.W) ? 1f : 0f;
        float brakeInput = Input.GetKey(KeyCode.S) ? 1f : 0f;

        // Forward force
        if (forwardInput > 0 && rb.linearVelocity.magnitude < maxSpeed)
        {
            rb.AddForce(transform.forward * acceleration * Time.fixedDeltaTime);
        }

        // Brake force
        if (brakeInput > 0)
        {
            rb.AddForce(-transform.forward * brakeForce * Time.fixedDeltaTime);
        }
    }
}

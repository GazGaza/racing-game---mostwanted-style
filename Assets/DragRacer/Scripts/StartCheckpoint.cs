using UnityEngine;

[RequireComponent(typeof(Collider))]
public class StartCheckpoint : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        var car = other.GetComponentInParent<HybridSplineCarController>();
        if (car != null)
        {
            car.StartRace();
            Debug.Log("✅ StartCheckpoint triggered!");
        }
    }
}
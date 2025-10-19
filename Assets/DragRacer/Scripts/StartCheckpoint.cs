using UnityEngine;

public class StartCheckpoint : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        HybridSplineCarController car = other.GetComponent<HybridSplineCarController>();
        if (car != null)
        {
            car.StartRace();
            Debug.Log("StartCheckpoint triggered!");
        }
    }
}

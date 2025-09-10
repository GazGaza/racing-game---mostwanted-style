using UnityEngine;

public class LaneManager : MonoBehaviour
{
    public Transform car;
    public float laneOffset = 4f;
    public float laneChangeSpeed = 10f;

    private int currentLane = 0; // 0 = middle, -1 = left, +1 = right
    private Vector3 targetPos;

    void Start()
    {
        targetPos = car.position;
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.A)) // move left
        {
            currentLane = Mathf.Max(currentLane - 1, -1);
        }
        if (Input.GetKeyDown(KeyCode.D)) // move right
        {
            currentLane = Mathf.Min(currentLane + 1, 1);
        }

        targetPos = new Vector3(currentLane * laneOffset, car.position.y, car.position.z);

        car.position = Vector3.Lerp(car.position, targetPos, Time.deltaTime * laneChangeSpeed);
    }
}

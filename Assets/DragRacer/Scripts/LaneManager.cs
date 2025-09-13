using UnityEngine;

public class LaneManager : MonoBehaviour
{
    public Transform car;
    public float laneOffset = 4f;
    public float laneChangeSpeed = 10f;

    private int currentLane = 0; // 0 = middle, -1 = left, +1 = right
    private Vector3 targetPos;
    private float startX; // baseline starting X

    void Start()
    {
        startX = car.position.x;
        // Don’t round, just use actual starting position
        targetPos = car.position;
    }

  void Update()
    {
        if (Input.GetKeyDown(KeyCode.A)) // move left
            currentLane--;

        if (Input.GetKeyDown(KeyCode.D)) // move right
            currentLane++;

        // Calculate the target position
        targetPos = new Vector3(startX + currentLane * laneOffset, car.position.y, car.position.z);

        // Move smoothly towards the target
        car.position = Vector3.Lerp(car.position, targetPos, Time.deltaTime * laneChangeSpeed);
    }
}

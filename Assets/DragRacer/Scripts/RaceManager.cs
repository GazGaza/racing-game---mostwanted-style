using UnityEngine;


public enum RaceState { Idle, Staging, Countdown, Running, Finished }


public class RaceManager : MonoBehaviour
{
    public CarController player;
    public Transform startLine;
    public Transform finishLine; // place at 402.336 m (1/4 mile) from start


    public float countdownSeconds = 3f;
    public RaceState state = RaceState.Idle;


    private float timer;
    private float startTime;
    private float reactionTime;


    private bool leftBeamBroken; // pre-stage/stage if you add sensors


    void Start()
    {
        state = RaceState.Staging;
        timer = countdownSeconds;
    }


    void FixedUpdate()
    {
        float t = Time.time;
        switch (state)
        {
            case RaceState.Staging:
                // Instantly move to countdown for now. Hook to beams later.
                state = RaceState.Countdown;
                timer = countdownSeconds;
                break;
            case RaceState.Countdown:
                timer -= Time.fixedDeltaTime;
                if (timer <= 0f)
                {
                    state = RaceState.Running;
                    startTime = Time.time;
                    reactionTime = 0f; // if you read throttle before green, add false start
                }
                break;
            case RaceState.Running:
                float distFromStart = Vector3.Dot(player.transform.position - startLine.position, startLine.forward);
                if (distFromStart >= 402.336f)
                {
                    state = RaceState.Finished;
                    float et = Time.time - startTime;
                    float trap = Vector3.Dot(player.GetComponent<Rigidbody>().linearVelocity, player.transform.forward) * 3.6f; // km/h
                    Debug.Log($"ET: {et:F3} s | Trap: {trap:F1} km/h | RT: {reactionTime:F3} s");
                }
                break;
        }
    }
}
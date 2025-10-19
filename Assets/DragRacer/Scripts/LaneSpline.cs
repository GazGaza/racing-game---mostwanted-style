using UnityEngine;

public class LaneSpline : MonoBehaviour
{
    public Transform[] controlPoints;

    // Get a smoothed position between 0–1 along the spline
    public Vector3 GetPoint(float t)
    {
        if (controlPoints.Length < 4)
            return transform.position;

        int numSections = controlPoints.Length - 3;
        int currPt = Mathf.Min(Mathf.FloorToInt(t * numSections), numSections - 1);
        float u = t * numSections - currPt;

        Vector3 a = controlPoints[currPt].position;
        Vector3 b = controlPoints[currPt + 1].position;
        Vector3 c = controlPoints[currPt + 2].position;
        Vector3 d = controlPoints[currPt + 3].position;

        // Catmull–Rom spline equation
        return 0.5f * (
            (-a + 3f * b - 3f * c + d) * (u * u * u) +
            (2f * a - 5f * b + 4f * c - d) * (u * u) +
            (-a + c) * u +
            2f * b
        );
    }

    // Direction vector at position t
    public Vector3 GetDirection(float t)
    {
        float delta = 0.001f;
        Vector3 p1 = GetPoint(t);
        Vector3 p2 = GetPoint(Mathf.Min(1f, t + delta));

        Vector3 dir = (p2 - p1).normalized;

        // draw direction ray for debug
        Debug.DrawRay(p1, dir * 10f, Color.red, 5f);

        return dir;
    }

    // Draw curve in Scene View
    private void OnDrawGizmos()
    {
        if (controlPoints == null || controlPoints.Length < 4) return;

        Gizmos.color = Color.cyan;
        Vector3 prev = GetPoint(0f);
        for (int i = 1; i <= 100; i++)
        {
            float t = i / 100f;
            Vector3 pos = GetPoint(t);
            Gizmos.DrawLine(prev, pos);
            prev = pos;
        }
    }

    // Finds the nearest point on the spline to a world position
    public float FindNearestPoint(Vector3 worldPos, int resolution = 50)
    {
        float nearestT = 0f;
        float minDist = float.MaxValue;

        // Sample spline along its length
        for (int i = 0; i <= resolution; i++)
        {
            float t = i / (float)resolution;
            Vector3 point = GetPoint(t);
            float dist = Vector3.SqrMagnitude(worldPos - point);
            if (dist < minDist)
            {
                minDist = dist;
                nearestT = t;
            }
        }

        return nearestT;
    }
}


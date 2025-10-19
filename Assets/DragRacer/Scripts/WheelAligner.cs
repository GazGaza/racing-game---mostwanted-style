#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

[ExecuteInEditMode]
public class WheelAligner : MonoBehaviour
{
    public Transform[] wheelMeshes;
    public WheelCollider[] wheelColliders;

    [ContextMenu("Align Colliders To Meshes")]
    void Align()
    {
        for (int i = 0; i < wheelMeshes.Length; i++)
        {
            if (wheelMeshes[i] && wheelColliders[i])
            {
                wheelColliders[i].transform.position = wheelMeshes[i].position;
                wheelColliders[i].radius = wheelMeshes[i].localScale.y * 0.5f;
            }
        }
        Debug.Log("Wheel colliders aligned to wheel meshes.");
    }
}
#endif
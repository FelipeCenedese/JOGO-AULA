using UnityEngine;

// Lugar onde o player aparece quando chega por uma porta.
// O "Id" tem que ser igual ao "Id Chegada" da porta que leva até aqui.
public class PontoChegada : MonoBehaviour
{
    public string id = "entrada";

    void OnDrawGizmos()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireCube(transform.position + Vector3.up * 0.5f, new Vector3(0.5f, 1f, 0f));
    }
}

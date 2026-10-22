using UnityEngine;

// Câmera que segue o player suavemente
public class CameraSegue : MonoBehaviour
{
    public Transform alvo;                          // arraste o Player aqui
    public float suavidade = 5f;
    public Vector3 deslocamento = new Vector3(0, 1, -10);

    void LateUpdate()
    {
        if (alvo == null) return;
        Vector3 destino = alvo.position + deslocamento;
        transform.position = Vector3.Lerp(transform.position, destino, suavidade * Time.deltaTime);
    }
}

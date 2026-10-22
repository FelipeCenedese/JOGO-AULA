using UnityEngine;

// Câmera que segue o player com suavidade, mais perto dele e olhando um pouco à frente
public class CameraSegue : MonoBehaviour
{
    public Transform alvo;                                  // arraste o Player aqui
    public Vector3 deslocamento = new Vector3(0, 1, -10);   // y = quanto a câmera fica acima do player

    [Header("Zoom")]
    public float tamanho = 4f;              // menor = câmera mais perto do player (antes era 5)

    [Header("Movimento")]
    public float tempoSuavizacao = 0.15f;   // menor = câmera gruda mais rápido no player
    public float olharAFrente = 0f;         // 0 = player sempre no centro. Ex.: 1.5 adianta a câmera para o lado que ele olha

    Camera cam;
    Vector3 velocidade;
    float frenteAtual;

    void Start()
    {
        cam = GetComponent<Camera>();
        if (cam != null) cam.orthographicSize = tamanho;

        // Começa já em cima do player, sem "viajar" até ele
        if (alvo != null) transform.position = alvo.position + deslocamento;
    }

    void LateUpdate()
    {
        if (alvo == null) return;
        if (cam != null) cam.orthographicSize = tamanho; // deixa mudar o zoom com o jogo rodando

        // O Player vira trocando a escala X: positiva = direita, negativa = esquerda
        float lado = Mathf.Sign(alvo.lossyScale.x);
        frenteAtual = Mathf.MoveTowards(frenteAtual, lado * olharAFrente, 4f * Time.deltaTime);

        Vector3 destino = alvo.position + deslocamento + new Vector3(frenteAtual, 0, 0);
        transform.position = Vector3.SmoothDamp(transform.position, destino, ref velocidade, tempoSuavizacao);
    }
}

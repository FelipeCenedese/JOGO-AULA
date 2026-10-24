using UnityEngine;
using UnityEngine.Rendering.Universal;

// Checkpoint (pilar com fogo azul).
// Quando o player chega perto, o fogo acende em cima do pilar e o checkpoint é salvo.
// Ao morrer, o player volta para o último checkpoint aceso, com a vida cheia,
// e todos os inimigos voltam (a fase é recarregada).
public class Checkpoint : MonoBehaviour
{
    [Header("Ativação")]
    public float distanciaAtivar = 1.2f;    // quão perto o player precisa chegar

    [Header("Fogo")]
    public Sprite[] quadrosFogo;            // arraste aqui os quadros do fogo azul, em ordem
    public float velocidadeFogo = 10f;      // quadros por segundo
    public Vector2 ajusteFogo = Vector2.zero;   // ajuste fino da posição do fogo em cima do pilar
    public float tamanhoFogo = 1f;          // escala do fogo
    public Light2D luzDoFogo;               // opcional: uma luz que acende junto com o fogo

    Transform player;
    SpriteRenderer fogo;
    bool aceso;
    float tempoAnim;

    // Identifica este checkpoint pela posição (para lembrar dele depois que a fase recarrega)
    string Id => $"{transform.position.x:F2}_{transform.position.y:F2}";

    void Awake()
    {
        var p = FindFirstObjectByType<Player>();
        if (p != null) player = p.transform;

        CriarFogo();

        // Se este foi o último checkpoint aceso, já começa aceso
        if (GerenciadorCheckpoint.temCheckpoint && GerenciadorCheckpoint.idAtual == Id)
            Acender(false);
        else
            Apagar();
    }

    void Update()
    {
        if (!aceso && player != null &&
            Vector2.Distance(player.position, transform.position) <= distanciaAtivar)
        {
            Acender(true);
        }

        // Anima o fogo
        if (aceso && quadrosFogo != null && quadrosFogo.Length > 0)
        {
            tempoAnim += Time.deltaTime * velocidadeFogo;
            fogo.sprite = quadrosFogo[(int)tempoAnim % quadrosFogo.Length];
        }
    }

    void Acender(bool salvar)
    {
        aceso = true;
        fogo.enabled = true;
        if (luzDoFogo != null) luzDoFogo.enabled = true;

        if (salvar)
        {
            // Apaga o checkpoint anterior (só o último fica aceso)
            foreach (var outro in FindObjectsByType<Checkpoint>(FindObjectsSortMode.None))
                if (outro != this) outro.Apagar();

            // Salva a posição do player neste momento (ele está no chão, perto do pilar)
            GerenciadorCheckpoint.Salvar(Id, player.position);
            Debug.Log("Checkpoint salvo");
        }
    }

    void Apagar()
    {
        aceso = false;
        if (fogo != null) fogo.enabled = false;
        if (luzDoFogo != null) luzDoFogo.enabled = false;
    }

    void CriarFogo()
    {
        var obj = new GameObject("FogoCheckpoint");
        obj.transform.SetParent(transform, false);
        fogo = obj.AddComponent<SpriteRenderer>();

        // Mesma camada do pilar, desenhado na frente dele
        var pilar = GetComponent<SpriteRenderer>();
        float topo = 0.5f;
        if (pilar != null)
        {
            fogo.sortingLayerID = pilar.sortingLayerID;
            fogo.sortingOrder = pilar.sortingOrder + 1;
            fogo.sharedMaterial = pilar.sharedMaterial;
            topo = (pilar.bounds.max.y - transform.position.y) / Mathf.Max(0.0001f, transform.lossyScale.y);
        }

        obj.transform.localPosition = new Vector3(ajusteFogo.x, topo + ajusteFogo.y, 0);
        obj.transform.localScale = Vector3.one * tamanhoFogo;
        if (quadrosFogo != null && quadrosFogo.Length > 0) fogo.sprite = quadrosFogo[0];
    }

    // Mostra o raio de ativação na cena (com o pilar selecionado)
    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.3f, 0.6f, 1f);
        Gizmos.DrawWireSphere(transform.position, distanciaAtivar);
    }
}

// Guarda o último checkpoint mesmo quando a fase é recarregada
public static class GerenciadorCheckpoint
{
    public static bool temCheckpoint;
    public static string idAtual;
    public static Vector3 posicao;

    public static void Salvar(string id, Vector3 pos)
    {
        temCheckpoint = true;
        idAtual = id;
        posicao = pos;
    }

    // Limpa tudo quando o jogo começa de novo (botão Play)
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Limpar()
    {
        temCheckpoint = false;
        idAtual = null;
        posicao = Vector3.zero;
    }
}

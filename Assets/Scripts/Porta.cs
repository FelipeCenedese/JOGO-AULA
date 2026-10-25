using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Porta que leva para outra cena (outra área do mapa).
// O player chega perto e aperte W, seta para cima ou E (ou clica na porta com o mouse).
// A tela escurece, a outra cena carrega e o player aparece no PontoChegada com o mesmo "Id Chegada".
public class Porta : MonoBehaviour
{
    [Header("Destino")]
    public string cenaDestino = "Area2";    // nome EXATO da cena de destino
    public string idChegada = "entrada";    // nome do PontoChegada na outra cena

    [Header("Interação")]
    public float distanciaUsar = 1.2f;      // quão perto o player precisa estar
    public float tempoFade = 0.5f;          // segundos para escurecer a tela

    Transform player;
    bool usando;

    void Awake()
    {
        var p = FindFirstObjectByType<Player>();
        if (p != null) player = p.transform;
    }

    void Update()
    {
        if (usando || player == null) return;
        if (Vector2.Distance(player.position, transform.position) > distanciaUsar) return;

        var tecla = Keyboard.current;
        bool apertou = tecla != null && (tecla.wKey.wasPressedThisFrame || tecla.upArrowKey.wasPressedThisFrame || tecla.eKey.wasPressedThisFrame);

        // Clique do mouse em cima da porta
        var mouse = Mouse.current;
        if (!apertou && mouse != null && mouse.leftButton.wasPressedThisFrame && Camera.main != null)
        {
            Vector2 ponto = Camera.main.ScreenToWorldPoint(mouse.position.ReadValue());
            var sr = GetComponent<SpriteRenderer>();
            if (sr != null && sr.bounds.Contains(new Vector3(ponto.x, ponto.y, sr.bounds.center.z))) apertou = true;
        }

        if (apertou) StartCoroutine(Entrar());
    }

    IEnumerator Entrar()
    {
        usando = true;
        var p = player.GetComponent<Player>();
        if (p != null) p.enabled = false;   // trava o player durante a troca

        yield return Fade.Escurecer(tempoFade);

        GerenciadorPortas.idChegada = idChegada;
        SceneManager.LoadScene(cenaDestino);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.8f, 0.2f);
        Gizmos.DrawWireSphere(transform.position, distanciaUsar);
    }
}

// Guarda por qual porta o player entrou, para a próxima cena saber onde colocá-lo
public static class GerenciadorPortas
{
    public static string idChegada;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Limpar() { idChegada = null; }
}

// Tela preta que escurece e clareia entre as cenas
public static class Fade
{
    static Image tela;

    static Image Tela()
    {
        if (tela != null) return tela;
        var obj = new GameObject("Fade");
        Object.DontDestroyOnLoad(obj);
        var canvas = obj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1000;
        var img = new GameObject("Tela").AddComponent<Image>();
        img.transform.SetParent(obj.transform, false);
        img.color = new Color(0, 0, 0, 0);
        img.raycastTarget = false;
        var rt = img.rectTransform;
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        tela = img;
        return tela;
    }

    public static IEnumerator Escurecer(float tempo) { yield return Mudar(0f, 1f, tempo); }
    public static IEnumerator Clarear(float tempo) { yield return Mudar(1f, 0f, tempo); }

    static IEnumerator Mudar(float de, float para, float tempo)
    {
        var img = Tela();
        float t = 0;
        while (t < tempo)
        {
            t += Time.unscaledDeltaTime;
            img.color = new Color(0, 0, 0, Mathf.Lerp(de, para, t / tempo));
            yield return null;
        }
        img.color = new Color(0, 0, 0, para);
    }

    // Quando uma cena nova carrega com a tela preta, clareia sozinha
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Registrar()
    {
        SceneManager.sceneLoaded += (cena, modo) =>
        {
            if (tela != null && tela.color.a > 0f)
            {
                if (!tela.TryGetComponent(out FadeAjudante ajudante))
                    ajudante = tela.gameObject.AddComponent<FadeAjudante>();
                ajudante.StartCoroutine(Clarear(0.5f));
            }
        };
    }
}


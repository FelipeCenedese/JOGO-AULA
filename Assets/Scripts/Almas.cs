using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Almas: a moeda do jogo.
// - Almas.Adicionar(10)  -> ganha almas (os inimigos chamam isso quando morrem)
// - Almas.Gastar(50)     -> tenta gastar; devolve false se não tiver o bastante (para a loja/upgrades no futuro)
// - Almas.total          -> quanto o player tem agora
// O total continua ao trocar de cena e ao morrer.
// O contador no canto superior direito da tela se cria sozinho em qualquer cena que tenha o Player:
// não precisa colocar nada na cena.
public static class Almas
{
    public static int total { get; private set; }

    public static void Adicionar(int quantidade)
    {
        if (quantidade <= 0) return;
        total += quantidade;
        ContadorAlmas.Ganhou(quantidade);
    }

    public static bool Gastar(int quantidade)
    {
        if (quantidade > total) return false;
        total -= quantidade;
        return true;
    }
}

public class ContadorAlmas : MonoBehaviour
{
    // Tamanho e posição na tela (tela de referência 1920 x 1080)
    static readonly Vector2 Tamanho = new Vector2(260, 84);
    static readonly Vector2 Margem = new Vector2(40, 40);   // distância da direita e do topo
    static readonly Color CorFundo = new Color(0.06f, 0.05f, 0.08f, 0.82f);
    static readonly Color CorBorda = new Color(0.55f, 0.75f, 0.95f, 1f);    // azul-claro, como o fogo do checkpoint
    static readonly Color CorTexto = new Color(0.92f, 0.95f, 1f, 1f);

    static ContadorAlmas instancia;
    static Sprite pixel;

    Text texto;
    RectTransform caixa;
    float mostrado;
    float pulo;      // efeito de "pulsar" quando ganha almas

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Iniciar()
    {
        Criar();
        SceneManager.sceneLoaded += (c, m) => Criar();
    }

    public static void Ganhou(int quantidade)
    {
        if (instancia != null) instancia.pulo = 1f;
    }

    static void Criar()
    {
        if (instancia != null) return;
        if (Object.FindFirstObjectByType<PlayerVida>() == null) return;   // cena sem player (menu, por ex.)

        if (pixel == null)
        {
            var tex = new Texture2D(1, 1) { filterMode = FilterMode.Point };
            tex.SetPixel(0, 0, Color.white);
            tex.Apply();
            pixel = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
        }

        var raiz = new GameObject("HUD Almas");
        DontDestroyOnLoad(raiz);
        var canvas = raiz.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        var escalador = raiz.AddComponent<CanvasScaler>();
        escalador.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        escalador.referenceResolution = new Vector2(1920, 1080);
        escalador.matchWidthOrHeight = 1f;

        // Caixa no canto superior direito: borda + fundo escuro por dentro
        var caixa = Retangulo("Caixa Almas", raiz.transform, CorBorda);
        caixa.anchorMin = caixa.anchorMax = caixa.pivot = new Vector2(1, 1);
        caixa.anchoredPosition = new Vector2(-Margem.x, -Margem.y);
        caixa.sizeDelta = Tamanho;

        var fundo = Retangulo("Fundo", caixa, CorFundo);
        fundo.anchorMin = Vector2.zero; fundo.anchorMax = Vector2.one;
        fundo.offsetMin = new Vector2(4, 4); fundo.offsetMax = new Vector2(-4, -4);

        // Ícone da alma: um losango azul brilhando
        var brilho = Retangulo("Icone Brilho", fundo, new Color(0.55f, 0.8f, 1f, 0.35f));
        PosicionarIcone(brilho, 36);
        var icone = Retangulo("Icone", fundo, new Color(0.75f, 0.9f, 1f, 1f));
        PosicionarIcone(icone, 24);

        // Número
        var rtTexto = new GameObject("Texto", typeof(RectTransform)).GetComponent<RectTransform>();
        rtTexto.SetParent(fundo, false);
        rtTexto.anchorMin = Vector2.zero; rtTexto.anchorMax = Vector2.one;
        rtTexto.offsetMin = new Vector2(74, 0); rtTexto.offsetMax = new Vector2(-18, 0);
        var texto = rtTexto.gameObject.AddComponent<Text>();
        texto.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        texto.fontSize = 44;
        texto.fontStyle = FontStyle.Bold;
        texto.alignment = TextAnchor.MiddleRight;
        texto.color = CorTexto;
        texto.raycastTarget = false;
        var sombra = rtTexto.gameObject.AddComponent<Shadow>();
        sombra.effectColor = new Color(0, 0, 0, 0.8f);
        sombra.effectDistance = new Vector2(3, -3);

        instancia = raiz.AddComponent<ContadorAlmas>();
        instancia.texto = texto;
        instancia.caixa = caixa;
        instancia.mostrado = Almas.total;
        texto.text = Almas.total.ToString();
    }

    static RectTransform Retangulo(string nome, Transform pai, Color cor)
    {
        var rt = new GameObject(nome, typeof(RectTransform)).GetComponent<RectTransform>();
        rt.SetParent(pai, false);
        var img = rt.gameObject.AddComponent<Image>();
        img.sprite = pixel;
        img.color = cor;
        img.raycastTarget = false;
        return rt;
    }

    static void PosicionarIcone(RectTransform rt, float tamanho)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(40, 0);
        rt.sizeDelta = new Vector2(tamanho, tamanho);
        rt.localRotation = Quaternion.Euler(0, 0, 45);
    }

    void Update()
    {
        // O número sobe aos poucos até o total (mais rápido quando a diferença é grande)
        float alvo = Almas.total;
        float velocidade = Mathf.Max(30f, Mathf.Abs(alvo - mostrado) * 4f);
        mostrado = Mathf.MoveTowards(mostrado, alvo, velocidade * Time.deltaTime);
        texto.text = Mathf.RoundToInt(mostrado).ToString();

        // Pulsa um pouco quando ganha almas
        pulo = Mathf.MoveTowards(pulo, 0f, Time.deltaTime * 4f);
        caixa.localScale = Vector3.one * (1f + 0.12f * pulo);
    }
}

using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Barra de vida do player (o tubo com o coração), no canto da tela.
// Ela se cria sozinha em qualquer cena que tenha o Player: não precisa colocar nada na cena.
// O líquido mostra a PORCENTAGEM da vida (vida / vida máxima), então continua certo
// quando a vida máxima aumentar no futuro.
public class HUDVida : MonoBehaviour
{
    // Tamanho e posição na tela
    const float Escala = 1.8f;                       // tamanho na tela: aumente para a barra ficar maior
    static readonly Vector2 Margem = new Vector2(24, 24);

    // Medidas do desenho (em pixels), recortado em 38 x 127
    const float LarguraMoldura = 38, AlturaMoldura = 127;
    static readonly Rect AreaLiquido = new Rect(14, 69, 11, 51);   // x, y (a partir do topo), largura, altura

    static HUDVida instancia;

    Image liquido;
    PlayerVida player;
    float mostrado = 1f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Iniciar()
    {
        Criar();
        SceneManager.sceneLoaded += (c, m) => Criar();
    }

    static void Criar()
    {
        if (instancia != null) return;
        if (FindFirstObjectByType<PlayerVida>() == null) return;   // cena sem player (menu, por ex.)

        var fundo = Resources.Load<Sprite>("HUD/HUD_Fundo");
        var frente = Resources.Load<Sprite>("HUD/HUD_Frente");
        var liq = Resources.Load<Sprite>("HUD/HUD_Liquido");
        if (fundo == null || frente == null || liq == null)
        {
            Debug.LogWarning("HUDVida: sprites não encontrados em Assets/Resources/HUD");
            return;
        }

        var raiz = new GameObject("HUD Vida");
        DontDestroyOnLoad(raiz);
        var canvas = raiz.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        canvas.pixelPerfect = true;
        var escalador = raiz.AddComponent<CanvasScaler>();
        escalador.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        escalador.referenceResolution = new Vector2(1920, 1080);
        escalador.matchWidthOrHeight = 1f;

        // Moldura: fica no canto superior esquerdo
        var moldura = new GameObject("Moldura", typeof(RectTransform)).GetComponent<RectTransform>();
        moldura.SetParent(raiz.transform, false);
        moldura.anchorMin = moldura.anchorMax = moldura.pivot = new Vector2(0, 1);
        moldura.anchoredPosition = new Vector2(Margem.x, -Margem.y);
        moldura.sizeDelta = new Vector2(LarguraMoldura, AlturaMoldura) * Escala;

        CriarImagem("Tubo", fundo, moldura, new Rect(0, 0, LarguraMoldura, AlturaMoldura));
        var imgLiquido = CriarImagem("Liquido", liq, moldura, AreaLiquido);
        imgLiquido.type = Image.Type.Filled;
        imgLiquido.fillMethod = Image.FillMethod.Vertical;
        imgLiquido.fillOrigin = (int)Image.OriginVertical.Bottom;
        CriarImagem("Mao e Coracao", frente, moldura, new Rect(0, 0, LarguraMoldura, AlturaMoldura));

        instancia = raiz.AddComponent<HUDVida>();
        instancia.liquido = imgLiquido;
    }

    // Cria uma imagem dentro da moldura, posicionada em pixels do desenho
    static Image CriarImagem(string nome, Sprite sprite, RectTransform pai, Rect area)
    {
        var rt = new GameObject(nome, typeof(RectTransform)).GetComponent<RectTransform>();
        rt.SetParent(pai, false);
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0, 1);
        rt.anchoredPosition = new Vector2(area.x, -area.y) * Escala;
        rt.sizeDelta = new Vector2(area.width, area.height) * Escala;
        var img = rt.gameObject.AddComponent<Image>();
        img.sprite = sprite;
        img.raycastTarget = false;
        return img;
    }

    void Update()
    {
        // Em cada cena o Player é outro objeto: procura de novo quando precisar
        if (player == null)
        {
            player = FindFirstObjectByType<PlayerVida>();
            if (player != null) mostrado = Porcentagem();
        }
        if (player == null) return;

        // Desce (ou sobe) suavemente até a porcentagem atual
        mostrado = Mathf.MoveTowards(mostrado, Porcentagem(), Time.deltaTime * 1.5f);
        liquido.fillAmount = mostrado;
    }

    float Porcentagem()
    {
        if (player.vidaMaxima <= 0) return 0f;
        return Mathf.Clamp01((float)player.vida / player.vidaMaxima);
    }
}

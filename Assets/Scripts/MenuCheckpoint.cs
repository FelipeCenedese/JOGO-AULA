using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Menu de melhorias do checkpoint.
// Perto de um checkpoint aceso, aperte E: o jogo pausa, a vida enche e dá para comprar
//   - Vida máxima +1
//   - Dano +1 (em todos os ataques)
// pagando com almas. Cada compra deixa a próxima mais cara.
// Controles: W/S (ou setas) escolhe, Espaço/Enter compra, E ou Esc fecha. Também dá para clicar com o mouse.
// O menu se cria sozinho: não precisa colocar nada na cena.
public class MenuCheckpoint : MonoBehaviour
{
    // Custos: o preço sobe a cada nível comprado (nível 0 -> custo base, nível 1 -> base x 2, ...)
    public const int CustoBaseVida = 30;
    public const int CustoBaseDano = 40;
    public const int NivelMaximo = 5;

    static MenuCheckpoint instancia;
    static Sprite pixel;
    static int ultimoFramePerto = -10;

    public static bool Aberto => instancia != null && instancia.painel.activeSelf;

    GameObject painel;
    GameObject dica;
    Text textoAlmas, textoAviso;
    Text[] textosOpcao = new Text[2];
    Image[] fundosOpcao = new Image[2];
    RectTransform[] rectsOpcao = new RectTransform[2];
    int escolhido;
    int frameAbriu;
    float tempoAviso;
    Player player;
    PlayerVida playerVida;

    // ================= Chamados pelo Checkpoint =================

    public static void PlayerPerto()
    {
        ultimoFramePerto = Time.frameCount;
        Garantir();
    }

    public static void Abrir()
    {
        Garantir();
        instancia.AbrirMenu();
    }

    static void Garantir()
    {
        if (instancia == null) Criar();
    }

    // ================= Abrir / fechar =================

    void AbrirMenu()
    {
        player = FindFirstObjectByType<Player>();
        playerVida = FindFirstObjectByType<PlayerVida>();
        if (playerVida != null) playerVida.CurarTudo();   // descansar no fogo enche a vida
        if (player != null) player.enabled = false;       // não ataca nem anda com o menu aberto

        painel.SetActive(true);
        Time.timeScale = 0f;
        frameAbriu = Time.frameCount;
        escolhido = 0;
        tempoAviso = 0;
        Atualizar();
    }

    void Fechar()
    {
        painel.SetActive(false);
        Time.timeScale = 1f;
        if (player != null) player.enabled = true;
    }

    void OnDestroy()
    {
        if (instancia == this && Time.timeScale == 0f) Time.timeScale = 1f;
    }

    // ================= Loop =================

    void Update()
    {
        // Dica "E - Descansar" quando o player está perto de um checkpoint aceso
        dica.SetActive(!painel.activeSelf && Time.frameCount - ultimoFramePerto <= 1);

        if (!painel.activeSelf) return;

        // Se a cena trocou (morreu, porta) com o menu aberto, fecha
        if (player == null) { Fechar(); return; }

        var tecla = Keyboard.current;
        var mouse = Mouse.current;
        if (tecla != null && Time.frameCount != frameAbriu)
        {
            if (tecla.eKey.wasPressedThisFrame || tecla.escapeKey.wasPressedThisFrame) { Fechar(); return; }
            if (tecla.wKey.wasPressedThisFrame || tecla.upArrowKey.wasPressedThisFrame) escolhido = 0;
            if (tecla.sKey.wasPressedThisFrame || tecla.downArrowKey.wasPressedThisFrame) escolhido = 1;
            if (tecla.spaceKey.wasPressedThisFrame || tecla.enterKey.wasPressedThisFrame) Comprar(escolhido);
        }

        // Mouse: passar por cima escolhe, clicar compra
        if (mouse != null)
        {
            Vector2 pos = mouse.position.ReadValue();
            for (int i = 0; i < 2; i++)
            {
                if (RectTransformUtility.RectangleContainsScreenPoint(rectsOpcao[i], pos, null))
                {
                    escolhido = i;
                    if (mouse.leftButton.wasPressedThisFrame && Time.frameCount != frameAbriu) Comprar(i);
                }
            }
        }

        if (tempoAviso > 0) tempoAviso -= Time.unscaledDeltaTime;
        Atualizar();
    }

    // ================= Compras =================

    static int Custo(int nivel, int baseCusto) => baseCusto * (nivel + 1);

    void Comprar(int opcao)
    {
        int nivel = opcao == 0 ? Melhorias.vidaExtra : Melhorias.danoExtra;
        int custo = Custo(nivel, opcao == 0 ? CustoBaseVida : CustoBaseDano);

        if (nivel >= NivelMaximo) { Avisar("Já está no máximo"); return; }
        if (!Almas.Gastar(custo)) { Avisar("Almas insuficientes"); return; }

        if (opcao == 0)
        {
            Melhorias.vidaExtra++;
            if (playerVida != null) playerVida.AumentarVidaMaxima(1);
            Avisar("Vida máxima aumentada!");
        }
        else
        {
            Melhorias.danoExtra++;
            Avisar("Dano aumentado!");
        }
    }

    void Avisar(string msg)
    {
        textoAviso.text = msg;
        tempoAviso = 1.5f;
    }

    void Atualizar()
    {
        textoAlmas.text = "Almas: " + Almas.total;

        int vidaAtual = playerVida != null ? playerVida.vidaMaxima : 0;
        textosOpcao[0].text = Linha("Vida máxima", vidaAtual.ToString(), (vidaAtual + 1).ToString(),
                                    Melhorias.vidaExtra, CustoBaseVida);
        textosOpcao[1].text = Linha("Dano", "+" + Melhorias.danoExtra, "+" + (Melhorias.danoExtra + 1),
                                    Melhorias.danoExtra, CustoBaseDano);

        for (int i = 0; i < 2; i++)
            fundosOpcao[i].color = i == escolhido ? new Color(0.35f, 0.55f, 0.85f, 0.45f) : new Color(1f, 1f, 1f, 0.06f);

        textoAviso.color = new Color(1f, 0.85f, 0.5f, Mathf.Clamp01(tempoAviso));
    }

    string Linha(string nome, string atual, string proximo, int nivel, int baseCusto)
    {
        if (nivel >= NivelMaximo) return $"{nome}:  {atual}   (máximo)";
        int custo = Custo(nivel, baseCusto);
        string cor = Almas.total >= custo ? "#E8F0FF" : "#C06060";
        return $"{nome}:  {atual}  →  {proximo}      <color={cor}>{custo} almas</color>";
    }

    // ================= Montar a tela =================

    static void Criar()
    {
        if (pixel == null)
        {
            var tex = new Texture2D(1, 1) { filterMode = FilterMode.Point };
            tex.SetPixel(0, 0, Color.white);
            tex.Apply();
            pixel = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
        }

        var raiz = new GameObject("Menu Checkpoint");
        DontDestroyOnLoad(raiz);
        var canvas = raiz.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 120;
        var escalador = raiz.AddComponent<CanvasScaler>();
        escalador.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        escalador.referenceResolution = new Vector2(1920, 1080);
        escalador.matchWidthOrHeight = 1f;

        var m = raiz.AddComponent<MenuCheckpoint>();
        instancia = m;

        // Dica embaixo da tela
        var rtDica = Retangulo("Dica", raiz.transform, new Color(0, 0, 0, 0.55f));
        rtDica.anchorMin = rtDica.anchorMax = rtDica.pivot = new Vector2(0.5f, 0f);
        rtDica.anchoredPosition = new Vector2(0, 150);
        rtDica.sizeDelta = new Vector2(430, 60);
        Texto(rtDica, "E  -  Descansar e melhorar", 32, TextAnchor.MiddleCenter, new Color(0.85f, 0.92f, 1f));
        m.dica = rtDica.gameObject;
        m.dica.SetActive(false);

        // Painel: escurece a tela + caixa no meio
        var escuro = Retangulo("Painel", raiz.transform, new Color(0, 0, 0, 0.6f));
        escuro.anchorMin = Vector2.zero; escuro.anchorMax = Vector2.one;
        escuro.offsetMin = escuro.offsetMax = Vector2.zero;
        m.painel = escuro.gameObject;

        var borda = Retangulo("Borda", escuro, new Color(0.45f, 0.65f, 0.95f, 1f));
        borda.anchorMin = borda.anchorMax = borda.pivot = new Vector2(0.5f, 0.5f);
        borda.sizeDelta = new Vector2(900, 480);
        var caixa = Retangulo("Caixa", borda, new Color(0.06f, 0.06f, 0.1f, 0.97f));
        caixa.anchorMin = Vector2.zero; caixa.anchorMax = Vector2.one;
        caixa.offsetMin = new Vector2(4, 4); caixa.offsetMax = new Vector2(-4, -4);

        var titulo = Faixa(caixa, "Titulo", -30, 70);
        Texto(titulo, "Fogo do Checkpoint", 50, TextAnchor.MiddleCenter, new Color(0.7f, 0.85f, 1f));

        var almas = Faixa(caixa, "Almas", -105, 50);
        m.textoAlmas = Texto(almas, "", 36, TextAnchor.MiddleCenter, new Color(0.92f, 0.95f, 1f));

        for (int i = 0; i < 2; i++)
        {
            var op = Faixa(caixa, "Opcao" + i, -175 - i * 95, 80);
            op.offsetMin = new Vector2(60, op.offsetMin.y); op.offsetMax = new Vector2(-60, op.offsetMax.y);
            var img = op.gameObject.AddComponent<Image>();
            img.sprite = pixel;
            img.raycastTarget = false;
            m.fundosOpcao[i] = img;
            m.rectsOpcao[i] = op;
            var t = Texto(op, "", 34, TextAnchor.MiddleLeft, new Color(0.92f, 0.95f, 1f));
            t.rectTransform.offsetMin = new Vector2(30, 0);
            m.textosOpcao[i] = t;
        }

        var aviso = Faixa(caixa, "Aviso", -370, 40);
        m.textoAviso = Texto(aviso, "", 30, TextAnchor.MiddleCenter, new Color(1f, 0.85f, 0.5f, 0f));

        var ajuda = Faixa(caixa, "Ajuda", -420, 36);
        Texto(ajuda, "W/S escolher    Espaço comprar    E sair", 26, TextAnchor.MiddleCenter, new Color(0.6f, 0.65f, 0.75f));

        m.painel.SetActive(false);
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

    // Faixa horizontal dentro da caixa, a "y" pixels do topo
    static RectTransform Faixa(Transform pai, string nome, float y, float altura)
    {
        var rt = new GameObject(nome, typeof(RectTransform)).GetComponent<RectTransform>();
        rt.SetParent(pai, false);
        rt.anchorMin = new Vector2(0, 1); rt.anchorMax = new Vector2(1, 1);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(0, y);
        rt.sizeDelta = new Vector2(0, altura);
        return rt;
    }

    static Text Texto(Transform pai, string conteudo, int tamanho, TextAnchor alinhamento, Color cor)
    {
        var rt = new GameObject("Texto", typeof(RectTransform)).GetComponent<RectTransform>();
        rt.SetParent(pai, false);
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        var t = rt.gameObject.AddComponent<Text>();
        t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        t.fontSize = tamanho;
        t.alignment = alinhamento;
        t.color = cor;
        t.supportRichText = true;
        t.raycastTarget = false;
        t.text = conteudo;
        return t;
    }
}

// Melhorias compradas (continuam ao morrer e ao trocar de cena)
public static class Melhorias
{
    public static int vidaExtra;
    public static int danoExtra;

    // Zera quando o jogo começa de novo (botão Play)
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Limpar()
    {
        vidaExtra = 0;
        danoExtra = 0;
    }
}

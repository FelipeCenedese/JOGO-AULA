using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// Chefe: Cavaleiro em Brasa.
// - Fica parado na arena até o player entrar nela (área amarela na cena, com ele selecionado)
// - Ao acordar: faz a pose de fúria e aparece a barra de vida grande embaixo da tela
// - Persegue o player e escolhe um golpe conforme a distância:
//     Corte (perto), Golpe Pesado (perto, mais dano), Giro (acerta dos dois lados),
//     Estocada (de longe) e, na 2ª fase, a Arte das Brasas (sequência de 6 golpes)
// - Às vezes rola na direção do player (fica invencível durante a rolagem)
// - Não se assusta a cada golpe: só cambaleia depois de levar bastante dano seguido
// - Com metade da vida entra na 2ª fase: fica mais rápido, ataca mais e ganha a Arte das Brasas
// - Ao morrer: animação de morte, almas para o player e a mensagem na tela
public class Chefe : MonoBehaviour
{
    [System.Serializable]
    public class Golpe
    {
        public string animacao = "Chefe_Corte";
        public float duracao = 1f;            // tempo total (igual à animação)
        public float[] momentos = { 0.3f };   // segundos depois do início em que o golpe acerta (pode ter vários)
        public float alcance = 1f;            // distância do centro do corpo até o centro da área do golpe
        public float raio = 1.5f;             // tamanho da área do golpe
        public int dano = 2;
        public float distanciaMinima = 0f;    // usa este golpe se o player estiver entre a mínima...
        public float distanciaMaxima = 2.4f;  // ...e a máxima
        public float avanco = 0f;             // anda para frente durante o golpe (velocidade)
        public bool soNaSegundaFase = false;
    }

    [Header("Vida")]
    public string nome = "Cavaleiro em Brasa";
    public int vidaMaxima = 40;
    public int almasAoMorrer = 1000;
    public int danoParaCambalear = 8;        // dano seguido que faz ele cambalear

    [Header("Arena (quadrado amarelo na cena)")]
    public Vector2 centroArena;              // o chefe acorda quando o player entra nessa área
    public Vector2 tamanhoArena = new Vector2(34, 16);

    [Header("Movimento")]
    public float velocidade = 2.4f;
    public float distanciaParar = 1.6f;      // chega até essa distância do player
    public float velocidadeRolar = 7f;
    public float duracaoRolar = 0.85f;
    public float chanceRolar = 0.35f;        // chance de rolar até o player quando ele está longe

    [Header("Ataques")]
    public float recargaAtaque = 1.1f;       // pausa entre golpes
    public Golpe[] golpes = new Golpe[]
    {
        new Golpe { animacao = "Chefe_Corte",    duracao = 0.9f,  momentos = new[] { 0.3f },  alcance = 1.0f, raio = 1.5f, dano = 2, distanciaMinima = 0f,   distanciaMaxima = 2.4f },
        new Golpe { animacao = "Chefe_Pesado",   duracao = 1.58f, momentos = new[] { 0.75f }, alcance = 0.5f, raio = 1.7f, dano = 3, distanciaMinima = 0f,   distanciaMaxima = 2.2f },
        new Golpe { animacao = "Chefe_Giro",     duracao = 1.08f, momentos = new[] { 0.42f }, alcance = 0f,   raio = 2.4f, dano = 2, distanciaMinima = 0f,   distanciaMaxima = 2.0f },
        new Golpe { animacao = "Chefe_Estocada", duracao = 1.17f, momentos = new[] { 0.42f }, alcance = 2.7f, raio = 1.3f, dano = 2, distanciaMinima = 2.2f, distanciaMaxima = 4.2f, avanco = 2f },
        new Golpe { animacao = "Chefe_Arte",     duracao = 2.92f, momentos = new[] { 0.25f, 0.83f, 1.25f, 1.92f, 2.25f, 2.45f }, alcance = 0.4f, raio = 1.9f, dano = 2, distanciaMinima = 0f, distanciaMaxima = 3f, soNaSegundaFase = true },
    };

    [Header("Segunda fase (metade da vida)")]
    public float multiplicadorVelocidade = 1.3f;
    public float recargaSegundaFase = 0.6f;
    public Color corSegundaFase = new Color(1f, 0.8f, 0.65f);

    [Header("Outros")]
    public LayerMask camadaChao;             // marque a Layer "Chao"
    public Color corDano = new Color(1f, 0.5f, 0.5f);

    enum Estado { Dormindo, Acordando, Livre, Atacando, Rolando, Cambaleando, MudandoFase, Morto }
    Estado estado = Estado.Dormindo;

    Rigidbody2D rb;
    Collider2D col;
    SpriteRenderer sr;
    Animator anim;
    Transform player;
    Collider2D colPlayer;

    int vida;
    float lado = -1f;
    float proximoAtaque;
    float danoAcumulado;
    float ultimoDano;
    bool segundaFase;
    bool invencivel;
    Color corBase = Color.white;
    string animAtual;
    Golpe ultimoGolpe;
    Coroutine piscar;

    // Barra de vida na tela
    GameObject barraRaiz;
    Image barraVermelha, barraAtraso;
    CanvasGroup barraGrupo;
    float mostrado = 1f, atraso = 1f;
    static Sprite pixel;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        col = GetComponent<Collider2D>();
        sr = GetComponent<SpriteRenderer>();
        anim = GetComponent<Animator>();
        if (rb.sharedMaterial == null)
            rb.sharedMaterial = new PhysicsMaterial2D("ChefeSemAtrito") { friction = 0f, bounciness = 0f };
        vida = vidaMaxima;
        if (sr != null) corBase = sr.color;

        var p = FindFirstObjectByType<Player>();
        if (p != null) { player = p.transform; colPlayer = p.GetComponent<Collider2D>(); }

        // Se ninguém marcou a arena, usa a área em volta de onde ele começa
        if (centroArena == Vector2.zero) centroArena = transform.position;

        Virar();
        Tocar("Chefe_Parado");
    }

    void FixedUpdate()
    {
        if (player == null) return;

        switch (estado)
        {
            case Estado.Dormindo:
                Parar();
                if (PlayerNaArena()) StartCoroutine(Acordar());
                break;

            case Estado.Livre:
                Decidir();
                break;

            case Estado.Atacando:
            case Estado.Cambaleando:
            case Estado.MudandoFase:
            case Estado.Acordando:
            case Estado.Morto:
                if (estado != Estado.Atacando) Parar();
                break;
        }
    }

    void Update()
    {
        AtualizarBarra();
    }

    // ================= Decidir o que fazer =================

    void Decidir()
    {
        float difX = player.position.x - transform.position.x;
        float dist = Mathf.Abs(difX);
        OlharParaPlayer();

        if (Time.time >= proximoAtaque)
        {
            Golpe g = EscolherGolpe(dist);
            if (g != null) { StartCoroutine(Atacar(g)); return; }

            // Longe demais para qualquer golpe: às vezes rola até o player
            if (dist > 5f && Random.value < chanceRolar)
            {
                proximoAtaque = Time.time + 0.4f;
                StartCoroutine(Rolar());
                return;
            }
        }

        // Anda até o player (ou espera perto dele)
        if (dist > distanciaParar && !TemParedeNaFrente())
        {
            float v = velocidade * (segundaFase ? multiplicadorVelocidade : 1f);
            rb.linearVelocity = new Vector2(lado * v, rb.linearVelocity.y);
            Tocar("Chefe_Correr");
        }
        else
        {
            Parar();
            Tocar("Chefe_Parado");
        }
    }

    Golpe EscolherGolpe(float dist)
    {
        int possiveis = 0;
        foreach (var g in golpes) if (Pode(g, dist)) possiveis++;
        if (possiveis == 0) return null;

        int sorteio = Random.Range(0, possiveis);
        foreach (var g in golpes)
        {
            if (!Pode(g, dist)) continue;
            if (sorteio == 0) return g;
            sorteio--;
        }
        return null;
    }

    bool Pode(Golpe g, float dist)
    {
        if (g.soNaSegundaFase && !segundaFase) return false;
        if (dist < g.distanciaMinima || dist > g.distanciaMaxima) return false;
        // Evita repetir o mesmo golpe duas vezes seguidas (se tiver outro para usar)
        if (g == ultimoGolpe && ContarOpcoes(dist) > 1) return false;
        return true;
    }

    int ContarOpcoes(float dist)
    {
        int n = 0;
        foreach (var g in golpes)
            if (!(g.soNaSegundaFase && !segundaFase) && dist >= g.distanciaMinima && dist <= g.distanciaMaxima) n++;
        return n;
    }

    // ================= Acordar =================

    bool PlayerNaArena()
    {
        Vector2 d = (Vector2)player.position - centroArena;
        return Mathf.Abs(d.x) <= tamanhoArena.x / 2f && Mathf.Abs(d.y) <= tamanhoArena.y / 2f;
    }

    IEnumerator Acordar()
    {
        estado = Estado.Acordando;
        OlharParaPlayer();
        CriarBarra();
        Tocar("Chefe_Buff", true);
        yield return new WaitForSeconds(1.2f);
        proximoAtaque = Time.time + 0.5f;
        estado = Estado.Livre;
    }

    // ================= Golpes =================

    IEnumerator Atacar(Golpe g)
    {
        estado = Estado.Atacando;
        ultimoGolpe = g;
        OlharParaPlayer();
        Tocar(g.animacao, true);

        float passado = 0f;
        foreach (float momento in g.momentos)
        {
            // Até o momento do acerto: se o golpe avança, anda para frente
            while (passado < momento)
            {
                passado += Time.deltaTime;
                rb.linearVelocity = new Vector2(lado * g.avanco, rb.linearVelocity.y);
                yield return null;
            }
            Acertar(g);
        }
        Parar();
        if (g.duracao > passado) yield return new WaitForSeconds(g.duracao - passado);

        proximoAtaque = Time.time + (segundaFase ? recargaSegundaFase : recargaAtaque);
        estado = Estado.Livre;
        Tocar("Chefe_Parado");
    }

    void Acertar(Golpe g)
    {
        Vector2 centro = (Vector2)col.bounds.center + new Vector2(lado * g.alcance, 0);
        foreach (var alvo in Physics2D.OverlapCircleAll(centro, g.raio))
        {
            var vidaPlayer = alvo.GetComponent<PlayerVida>();
            if (vidaPlayer != null) vidaPlayer.LevarDano(g.dano, transform.position);
        }
    }

    IEnumerator Rolar()
    {
        estado = Estado.Rolando;
        invencivel = true;
        OlharParaPlayer();
        Tocar("Chefe_Rolar", true);
        float fim = Time.time + duracaoRolar;
        while (Time.time < fim)
        {
            // Para antes de passar por cima do player ou bater na parede
            float dist = Mathf.Abs(player.position.x - transform.position.x);
            if (dist < 1.2f || TemParedeNaFrente()) break;
            rb.linearVelocity = new Vector2(lado * velocidadeRolar, rb.linearVelocity.y);
            yield return null;
        }
        Parar();
        invencivel = false;
        estado = Estado.Livre;
    }

    // ================= Levar dano (chamado pelo ataque do Player) =================

    public void ReceberDano(int dano)
    {
        if (estado == Estado.Morto || invencivel) return;
        if (estado == Estado.Dormindo) StartCoroutine(Acordar());

        vida -= dano;
        if (Time.time - ultimoDano > 2f) danoAcumulado = 0;
        ultimoDano = Time.time;
        if (piscar != null) StopCoroutine(piscar);
        piscar = StartCoroutine(Piscar());

        if (vida <= 0) { vida = 0; Morrer(); return; }

        // Metade da vida: segunda fase
        if (!segundaFase && vida <= vidaMaxima / 2 && estado != Estado.Acordando)
        {
            StopAllCoroutines();
            if (sr != null) sr.color = corBase;
            StartCoroutine(MudarFase());
            return;
        }

        // Muito dano seguido: cambaleia (interrompe o golpe)
        danoAcumulado += dano;
        if (danoAcumulado >= danoParaCambalear && (estado == Estado.Livre || estado == Estado.Atacando))
        {
            danoAcumulado = 0;
            StopAllCoroutines();
            if (sr != null) sr.color = corBase;
            StartCoroutine(Cambalear());
        }
    }

    IEnumerator Piscar()
    {
        if (sr == null) yield break;
        sr.color = corDano;
        yield return new WaitForSeconds(0.1f);
        sr.color = corBase;
    }

    IEnumerator Cambalear()
    {
        estado = Estado.Cambaleando;
        invencivel = false;
        Parar();
        Tocar("Chefe_Dano", true);
        yield return new WaitForSeconds(0.7f);
        proximoAtaque = Time.time + 0.3f;
        estado = Estado.Livre;
    }

    IEnumerator MudarFase()
    {
        estado = Estado.MudandoFase;
        invencivel = true;
        Parar();
        Tocar("Chefe_Buff", true);
        yield return new WaitForSeconds(1.2f);
        segundaFase = true;
        corBase = corSegundaFase;
        if (sr != null) sr.color = corBase;
        invencivel = false;
        proximoAtaque = Time.time + 0.3f;
        estado = Estado.Livre;
    }

    void Morrer()
    {
        StopAllCoroutines();
        StartCoroutine(Morte());
    }

    IEnumerator Morte()
    {
        estado = Estado.Morto;
        if (sr != null) sr.color = corBase;
        Parar();
        rb.linearVelocity = Vector2.zero;
        rb.bodyType = RigidbodyType2D.Kinematic;
        col.enabled = false;
        Tocar("Chefe_Morte", true);
        Almas.Adicionar(almasAoMorrer);
        StartCoroutine(MensagemVitoria());

        yield return new WaitForSeconds(4f);
        // Some aos poucos
        float t = 0;
        while (t < 1.5f)
        {
            t += Time.deltaTime;
            if (sr != null) { Color c = corBase; c.a = 1f - t / 1.5f; sr.color = c; }
            yield return null;
        }
        Destroy(gameObject);
    }

    // ================= Barra de vida grande (embaixo da tela) =================

    void CriarBarra()
    {
        if (barraRaiz != null) return;
        if (pixel == null)
        {
            var tex = new Texture2D(1, 1) { filterMode = FilterMode.Point };
            tex.SetPixel(0, 0, Color.white);
            tex.Apply();
            pixel = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
        }

        barraRaiz = new GameObject("HUD Chefe");
        var canvas = barraRaiz.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 90;
        var escalador = barraRaiz.AddComponent<CanvasScaler>();
        escalador.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        escalador.referenceResolution = new Vector2(1920, 1080);
        escalador.matchWidthOrHeight = 1f;
        barraGrupo = barraRaiz.AddComponent<CanvasGroup>();
        barraGrupo.alpha = 0f;

        // Moldura embaixo, no meio da tela
        var moldura = Retangulo("Moldura", barraRaiz.transform, new Color(0.55f, 0.42f, 0.25f, 1f));
        moldura.anchorMin = moldura.anchorMax = moldura.pivot = new Vector2(0.5f, 0f);
        moldura.anchoredPosition = new Vector2(0, 70);
        moldura.sizeDelta = new Vector2(1000, 30);

        var fundo = Retangulo("Fundo", moldura, new Color(0.08f, 0.05f, 0.05f, 0.95f));
        Esticar(fundo, 4);

        barraAtraso = Retangulo("Atraso", fundo, new Color(0.95f, 0.75f, 0.35f, 1f)).GetComponent<Image>();
        Preencher(barraAtraso);
        barraVermelha = Retangulo("Vida", fundo, new Color(0.75f, 0.1f, 0.08f, 1f)).GetComponent<Image>();
        Preencher(barraVermelha);

        // Nome em cima da barra
        var rtNome = new GameObject("Nome", typeof(RectTransform)).GetComponent<RectTransform>();
        rtNome.SetParent(moldura, false);
        rtNome.anchorMin = new Vector2(0, 1); rtNome.anchorMax = new Vector2(1, 1);
        rtNome.pivot = new Vector2(0, 0);
        rtNome.anchoredPosition = new Vector2(4, 6);
        rtNome.sizeDelta = new Vector2(0, 44);
        var texto = rtNome.gameObject.AddComponent<Text>();
        texto.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        texto.fontSize = 34;
        texto.alignment = TextAnchor.LowerLeft;
        texto.color = new Color(0.95f, 0.9f, 0.82f);
        texto.text = nome;
        texto.raycastTarget = false;
        var sombra = rtNome.gameObject.AddComponent<Shadow>();
        sombra.effectColor = new Color(0, 0, 0, 0.9f);
        sombra.effectDistance = new Vector2(2, -2);
    }

    void AtualizarBarra()
    {
        if (barraRaiz == null) return;
        float alvo = Mathf.Clamp01((float)vida / vidaMaxima);
        mostrado = Mathf.MoveTowards(mostrado, alvo, Time.deltaTime * 2f);
        // A parte clara desce devagar, mostrando quanto dano acabou de levar
        atraso = Mathf.MoveTowards(atraso, mostrado, Time.deltaTime * (Time.time - ultimoDano > 0.6f ? 0.6f : 0f));
        if (atraso < mostrado) atraso = mostrado;
        barraVermelha.fillAmount = mostrado;
        barraAtraso.fillAmount = atraso;

        // Aparece ao acordar e some depois que ele morre
        float alfaAlvo = estado == Estado.Morto ? 0f : 1f;
        barraGrupo.alpha = Mathf.MoveTowards(barraGrupo.alpha, alfaAlvo, Time.deltaTime * (estado == Estado.Morto ? 0.5f : 2f));
    }

    IEnumerator MensagemVitoria()
    {
        var raiz = new GameObject("Mensagem Vitoria");
        var canvas = raiz.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 95;
        var escalador = raiz.AddComponent<CanvasScaler>();
        escalador.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        escalador.referenceResolution = new Vector2(1920, 1080);
        escalador.matchWidthOrHeight = 1f;
        var grupo = raiz.AddComponent<CanvasGroup>();
        grupo.alpha = 0f;

        if (pixel != null)
        {
            var faixa = Retangulo("Faixa", raiz.transform, new Color(0, 0, 0, 0.6f));
            faixa.anchorMin = new Vector2(0, 0.5f); faixa.anchorMax = new Vector2(1, 0.5f);
            faixa.sizeDelta = new Vector2(0, 170);
        }
        var rt = new GameObject("Texto", typeof(RectTransform)).GetComponent<RectTransform>();
        rt.SetParent(raiz.transform, false);
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        var texto = rt.gameObject.AddComponent<Text>();
        texto.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        texto.fontSize = 84;
        texto.alignment = TextAnchor.MiddleCenter;
        texto.color = new Color(1f, 0.8f, 0.4f);
        texto.text = "INIMIGO DERROTADO";
        texto.raycastTarget = false;

        yield return new WaitForSeconds(1.2f);
        for (float t = 0; t < 1f; t += Time.deltaTime) { grupo.alpha = t; yield return null; }
        grupo.alpha = 1f;
        yield return new WaitForSeconds(2.5f);
        for (float t = 1; t > 0f; t -= Time.deltaTime) { grupo.alpha = t; yield return null; }
        Destroy(raiz);
        if (barraRaiz != null) Destroy(barraRaiz);
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

    static void Esticar(RectTransform rt, float borda)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(borda, borda); rt.offsetMax = new Vector2(-borda, -borda);
    }

    static void Preencher(Image img)
    {
        Esticar(img.rectTransform, 0);
        img.type = Image.Type.Filled;
        img.fillMethod = Image.FillMethod.Horizontal;
        img.fillOrigin = (int)Image.OriginHorizontal.Left;
        img.fillAmount = 1f;
    }

    // ================= Utilidades =================

    void OlharParaPlayer()
    {
        float difX = player.position.x - transform.position.x;
        if (Mathf.Abs(difX) < 0.1f) return;
        float novo = Mathf.Sign(difX);
        if (novo != lado) { lado = novo; Virar(); }
    }

    bool TemParedeNaFrente()
    {
        Bounds b = col.bounds;
        return Physics2D.Raycast(b.center, new Vector2(lado, 0), b.extents.x + 0.15f, camadaChao);
    }

    void Parar() { rb.linearVelocity = new Vector2(0, rb.linearVelocity.y); }

    void Virar()
    {
        Vector3 e = transform.localScale;
        e.x = Mathf.Abs(e.x) * (lado > 0 ? 1 : -1);
        transform.localScale = e;
    }

    void Tocar(string nome, bool reiniciar = false)
    {
        if (!reiniciar && animAtual == nome) return;
        if (anim == null || anim.runtimeAnimatorController == null) return;
        int id = Animator.StringToHash(nome);
        if (!anim.HasState(0, id)) return;
        anim.Play(id, 0, 0f);
        animAtual = nome;
    }

    // Desenhos de ajuda na cena (com o chefe selecionado)
    void OnDrawGizmosSelected()
    {
        // Amarelo: arena (ele acorda quando o player entra)
        Gizmos.color = Color.yellow;
        Vector2 c = centroArena == Vector2.zero ? (Vector2)transform.position : centroArena;
        Gizmos.DrawWireCube(c, tamanhoArena);

        // Vermelho: área de cada golpe
        var cc = GetComponent<Collider2D>();
        if (cc == null || golpes == null) return;
        float l = Application.isPlaying ? lado : -1f;
        Gizmos.color = new Color(1f, 0.3f, 0.2f, 0.6f);
        foreach (var g in golpes)
            Gizmos.DrawWireSphere(cc.bounds.center + new Vector3(l * g.alcance, 0), g.raio);
    }
}

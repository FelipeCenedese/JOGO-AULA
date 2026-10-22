using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

// Player completo: movimento, animações, ataques rápidos (combo 1/2), ataque pesado, dash e dash + ataque
// Substitui o PlayerMovimento.cs
public class Player : MonoBehaviour
{
    [Header("Movimento")]
    public float velocidadeAndar = 4f;
    public float velocidadeCorrer = 7f;
    public float tempoParaCorrer = 0.4f;   // segurando o direcional por esse tempo, ele começa a correr
    public float forcaPulo = 14f;

    [Header("Chão")]
    public LayerMask camadaChao;

    [Header("Dash")]
    public float velocidadeDash = 18f;
    public float duracaoDash = 0.18f;
    public float recargaDash = 0.5f;
    public float janelaDashAtaque = 0.25f; // tempo depois do dash em que ainda dá pra bater
    public bool alternarDashAtaques = true; // ligado: alterna DashAtaque1 e DashAtaque2. Desligado: usa só o DashAtaque1

    [Header("Combate")]
    public Transform pontoAtaque;          // objeto vazio na frente do player
    public LayerMask camadaInimigo;
    public float raioAtaqueRapido = 0.6f;
    public float raioAtaquePesado = 0.9f;
    public int danoRapido = 1;
    public int danoPesado = 3;
    public int danoDashAtaque = 2;
    public float janelaCombo = 0.5f;       // depois do ataque, por quanto tempo o próximo vira o Ataque2
    public float momentoAcertoRapido = 0.1f;   // segundos depois de começar a animação em que o golpe acerta
    public float momentoAcertoPesado = 0.25f;
    public float momentoAcertoDash = 0.1f;
    public float freioNoAtaque = 40f;      // quão rápido ele para de deslizar ao atacar

    enum Estado { Livre, Atacando, Dash, Atordoado }
    Estado estado = Estado.Livre;

    Rigidbody2D rb;
    Animator anim;
    ContactFilter2D filtroParede;
    readonly RaycastHit2D[] acertos = new RaycastHit2D[4];
    readonly Dictionary<string, float> duracoes = new Dictionary<string, float>();

    float direcao;
    bool noChao;
    bool olhandoDireita = true;
    float tempoAndando;
    bool correndo;
    float gravidadeOriginal;
    float proximoDash;
    float fimDoUltimoDash = -10f;
    float fimDoUltimoAtaque = -10f;
    int proximoRapido = 1;
    int proximoDashAtaque = 1;
    bool ataqueNaFila;
    string animAtual;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        anim = GetComponent<Animator>();
        gravidadeOriginal = rb.gravityScale;

        filtroParede.SetLayerMask(camadaChao);
        filtroParede.useTriggers = false;

        // Guarda a duração de cada animação para os ataques durarem o tempo certo
        foreach (var clip in anim.runtimeAnimatorController.animationClips)
            duracoes[clip.name] = clip.length;
    }

    void Update()
    {
        var tecla = Keyboard.current;
        var mouse = Mouse.current;
        if (tecla == null) return;

        // ---------- Leitura dos controles ----------
        direcao = 0;
        if (tecla.aKey.isPressed || tecla.leftArrowKey.isPressed) direcao = -1;
        if (tecla.dKey.isPressed || tecla.rightArrowKey.isPressed) direcao = 1;

        bool apertouRapido = tecla.jKey.wasPressedThisFrame || (mouse != null && mouse.leftButton.wasPressedThisFrame);
        bool apertouPesado = tecla.kKey.wasPressedThisFrame || (mouse != null && mouse.rightButton.wasPressedThisFrame);
        bool apertouDash = tecla.leftShiftKey.wasPressedThisFrame || tecla.lKey.wasPressedThisFrame;

        noChao = ChecarChao();

        // Levou dano: fica sem controle por um instante
        if (estado == Estado.Atordoado) return;

        // Andar -> correr depois de segurar um pouco
        if (direcao != 0) tempoAndando += Time.deltaTime;
        else tempoAndando = 0;
        correndo = tempoAndando >= tempoParaCorrer;

        // Pulo curto: soltou o espaço subindo, corta o pulo
        if (tecla.spaceKey.wasReleasedThisFrame && rb.linearVelocity.y > 0 && estado != Estado.Dash)
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, rb.linearVelocity.y * 0.5f);

        // ---------- Dash + ataque ----------
        bool logoDepoisDoDash = estado == Estado.Livre && Time.time - fimDoUltimoDash <= janelaDashAtaque;
        if (apertouRapido && (estado == Estado.Dash || logoDepoisDoDash))
        {
            DashAtaque();
            return;
        }

        // ---------- Durante um ataque ----------
        if (estado == Estado.Atacando)
        {
            if (apertouRapido) ataqueNaFila = true; // continua o combo quando o ataque atual terminar
            return;
        }

        if (estado != Estado.Livre) return;

        // ---------- Livre ----------
        if (apertouDash && Time.time >= proximoDash)
        {
            StartCoroutine(Dash());
            return;
        }

        if (apertouRapido)
        {
            AtaqueRapido();
            return;
        }

        if (apertouPesado)
        {
            StartCoroutine(Ataque("AtaquePesado", raioAtaquePesado, danoPesado, momentoAcertoPesado));
            return;
        }

        if (tecla.spaceKey.wasPressedThisFrame && noChao)
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, forcaPulo);

        if (direcao != 0) Virar(direcao);

        // Animações de movimento
        if (!noChao)
            TocarAnimacao(rb.linearVelocity.y > 0.1f ? "Pulo" : "Queda");
        else if (direcao != 0)
            TocarAnimacao(correndo ? "Correr" : "Andar");
        else
            TocarAnimacao("Parado");
    }

    void FixedUpdate()
    {
        if (estado == Estado.Dash || estado == Estado.Atordoado) return; // dash e empurrão controlam a velocidade sozinhos

        float vel = correndo ? velocidadeCorrer : velocidadeAndar;
        float velX;

        if (estado == Estado.Atacando)
        {
            // Atacando: vai freando (no ar ainda dá pra se mexer um pouco)
            float alvo = noChao ? 0 : direcao * vel;
            velX = Mathf.MoveTowards(rb.linearVelocity.x, alvo, freioNoAtaque * Time.fixedDeltaTime);
        }
        else
        {
            velX = direcao * vel;
        }

        // Não empurrar parede (evita escalar pedras)
        if (velX != 0 && EncostandoParede(Mathf.Sign(velX)))
            velX = 0;

        rb.linearVelocity = new Vector2(velX, rb.linearVelocity.y);
    }

    // ================= Combate =================

    void AtaqueRapido()
    {
        // Se demorou demais desde o último ataque, recomeça no Ataque1
        if (Time.time - fimDoUltimoAtaque > janelaCombo) proximoRapido = 1;

        string nome = proximoRapido == 1 ? "Ataque1" : "Ataque2";
        proximoRapido = proximoRapido == 1 ? 2 : 1;

        StartCoroutine(Ataque(nome, raioAtaqueRapido, danoRapido, momentoAcertoRapido));
    }

    void DashAtaque()
    {
        string nome = proximoDashAtaque == 1 ? "DashAtaque1" : "DashAtaque2";
        if (alternarDashAtaques) proximoDashAtaque = proximoDashAtaque == 1 ? 2 : 1;
        else proximoDashAtaque = 1;

        estado = Estado.Atacando;          // faz o dash parar
        fimDoUltimoDash = -10f;
        rb.gravityScale = gravidadeOriginal;
        rb.linearVelocity = new Vector2(Lado() * velocidadeDash * 0.6f, 0); // mantém um pouco do embalo

        StartCoroutine(Ataque(nome, raioAtaqueRapido * 1.2f, danoDashAtaque, momentoAcertoDash));
    }

    IEnumerator Ataque(string nome, float raio, int dano, float momentoAcerto)
    {
        estado = Estado.Atacando;
        ataqueNaFila = false;
        TocarAnimacao(nome, true);

        float duracao = Duracao(nome);
        yield return new WaitForSeconds(momentoAcerto);
        Acertar(raio, dano);
        yield return new WaitForSeconds(Mathf.Max(0f, duracao - momentoAcerto));

        estado = Estado.Livre;
        fimDoUltimoAtaque = Time.time;

        if (ataqueNaFila)
        {
            ataqueNaFila = false;
            AtaqueRapido();
        }
    }

    IEnumerator Dash()
    {
        estado = Estado.Dash;
        proximoDash = Time.time + recargaDash;

        if (direcao != 0) Virar(direcao);
        float lado = Lado();

        rb.gravityScale = 0;
        TocarAnimacao("Dash", true);

        float fim = Time.time + duracaoDash;
        while (Time.time < fim && estado == Estado.Dash)
        {
            rb.linearVelocity = new Vector2(lado * velocidadeDash, 0);
            yield return new WaitForFixedUpdate();
        }

        rb.gravityScale = gravidadeOriginal;

        // Só marca o fim se o dash terminou sozinho (não virou dash + ataque)
        if (estado == Estado.Dash)
        {
            estado = Estado.Livre;
            fimDoUltimoDash = Time.time;
        }
    }

    void Acertar(float raio, int dano)
    {
        if (pontoAtaque == null) return;

        var alvos = Physics2D.OverlapCircleAll(pontoAtaque.position, raio, camadaInimigo);
        foreach (var alvo in alvos)
        {
            // O inimigo precisa ter um método: public void ReceberDano(int dano)
            alvo.SendMessage("ReceberDano", dano, SendMessageOptions.DontRequireReceiver);
        }
    }

    // Chamado pelo PlayerVida quando leva dano: joga o player para trás
    public void Empurrao(Vector2 velocidade, float duracao)
    {
        StopAllCoroutines();               // cancela ataque ou dash que estava acontecendo
        rb.gravityScale = gravidadeOriginal;
        ataqueNaFila = false;
        estado = Estado.Atordoado;
        rb.linearVelocity = velocidade;
        StartCoroutine(FimDoEmpurrao(duracao));
    }

    IEnumerator FimDoEmpurrao(float duracao)
    {
        yield return new WaitForSeconds(duracao);
        estado = Estado.Livre;
    }

    // ================= Utilidades =================

    void TocarAnimacao(string nome, bool reiniciar = false)
    {
        if (!reiniciar && animAtual == nome) return;
        anim.Play(nome, 0, 0f);
        animAtual = nome;
    }

    float Duracao(string nome)
    {
        return duracoes.TryGetValue(nome, out float d) ? d : 0.4f;
    }

    float Lado() => olhandoDireita ? 1f : -1f;

    void Virar(float lado)
    {
        olhandoDireita = lado > 0;
        Vector3 escala = transform.localScale;
        escala.x = Mathf.Abs(escala.x) * (olhandoDireita ? 1 : -1);
        transform.localScale = escala; // vira o sprite e o ponto de ataque junto
    }

    bool EncostandoParede(float lado)
    {
        int qtd = rb.Cast(new Vector2(lado, 0), filtroParede, acertos, 0.05f);
        for (int i = 0; i < qtd; i++)
            if (Mathf.Abs(acertos[i].normal.x) > 0.7f)
                return true;
        return false;
    }

    // Está pisando em algo? Usa o próprio colisor do personagem (não precisa mais do ChecaChao)
    bool ChecarChao()
    {
        int qtd = rb.Cast(Vector2.down, filtroParede, acertos, 0.05f);
        for (int i = 0; i < qtd; i++)
            if (acertos[i].normal.y > 0.7f)
                return true;
        return false;
    }

    void OnDrawGizmosSelected()
    {
        if (pontoAtaque != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(pontoAtaque.position, raioAtaqueRapido);
            Gizmos.color = new Color(1f, 0.5f, 0f);
            Gizmos.DrawWireSphere(pontoAtaque.position, raioAtaquePesado);
        }
    }
}

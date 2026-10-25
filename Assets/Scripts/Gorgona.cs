using System.Collections;
using UnityEngine;

// Gorgona (medusa): inimigo comum mais forte que o Lanceiro.
// Patrulha, persegue rastejando rápido quando vê o player, e escolhe entre 3 ataques
// conforme a distância. Mais vida e mais dano. Animação de morte própria.
public class Gorgona : MonoBehaviour
{
    [System.Serializable]
    public class Ataque
    {
        public string animacao = "Gorgona_Ataque1";
        public float distanciaMaxima = 1.5f;  // usa este ataque se o player estiver até essa distância
        public float momentoAcerto = 0.5f;    // segundos depois do início em que o golpe acerta
        public float duracao = 1f;            // tempo total (igual à animação)
        public float alcance = 1f;            // distância do centro do corpo até o centro da área do golpe
        public float raio = 0.6f;             // tamanho da área do golpe
        public int dano = 2;
    }

    [Header("Vida")]
    public int vidaMaxima = 12;

    [Header("Patrulha")]
    public float velocidade = 1.2f;
    public float distanciaPatrulha = 3f;
    public float tempoPausa = 1.2f;
    public bool comecaIndoParaDireita = false;
    public LayerMask camadaChao;

    [Header("Perseguição")]
    public float distanciaVisao = 6f;
    public float distanciaPerder = 9f;
    public float alturaVisao = 2f;
    public float velocidadePerseguicao = 3.2f;

    [Header("Ataques")]
    public Ataque[] ataques = new Ataque[]
    {
        // Golpe de cauda para frente: alcance longo
        new Ataque { animacao = "Gorgona_Ataque2", distanciaMaxima = 2.0f, momentoAcerto = 0.33f, duracao = 0.58f, alcance = 1.05f, raio = 0.55f, dano = 2 },
        // Bote: se joga para frente
        new Ataque { animacao = "Gorgona_Ataque3", distanciaMaxima = 1.8f, momentoAcerto = 0.5f, duracao = 0.83f, alcance = 0.95f, raio = 0.6f, dano = 2 },
        // Enrola e gira a cauda: curto, mas forte
        new Ataque { animacao = "Gorgona_Ataque1", distanciaMaxima = 1.3f, momentoAcerto = 0.55f, duracao = 1.33f, alcance = 0.6f, raio = 0.8f, dano = 3 },
    };
    public float recargaAtaque = 1.4f;      // pausa entre ataques

    [Header("Dano no player ao encostar")]
    public int danoAoEncostar = 1;

    [Header("Ao levar dano")]
    public float forcaRecuo = 2.5f;         // mais pesada: é empurrada menos que o Lanceiro
    public float tempoRecuo = 0.2f;
    public Color corDano = new Color(1f, 0.45f, 0.45f);
    public float tempoParaSumir = 1.2f;

    Rigidbody2D rb;
    Collider2D col;
    SpriteRenderer sr;
    Animator anim;
    Transform player;

    int vida;
    float lado;
    float inicioX;
    float pausadoAte;
    float proximoAtaque;
    bool perseguindo, atacando, recuando, morto;
    Color corOriginal;
    string animAtual;
    Ataque ataqueAtual;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        col = GetComponent<Collider2D>();
        sr = GetComponent<SpriteRenderer>();
        anim = GetComponent<Animator>();

        if (rb.sharedMaterial == null)
            rb.sharedMaterial = new PhysicsMaterial2D("GorgonaSemAtrito") { friction = 0f, bounciness = 0f };

        vida = vidaMaxima;
        lado = comecaIndoParaDireita ? 1f : -1f;
        inicioX = transform.position.x;
        if (sr != null) corOriginal = sr.color;

        var p = FindFirstObjectByType<Player>();
        if (p != null) player = p.transform;

        Virar();
    }

    void FixedUpdate()
    {
        if (morto || recuando) return;

        if (atacando) { Parar(); return; }

        if (VendoPlayer()) Perseguir();
        else Patrulhar();
    }

    // ================= Patrulha =================

    void Patrulhar()
    {
        perseguindo = false;

        if (Time.time < pausadoAte)
        {
            Parar();
            TocarAnimacao("Gorgona_Parado");
            return;
        }

        float x = transform.position.x;
        bool passouDaPonta = (lado > 0 && x >= inicioX + distanciaPatrulha) ||
                             (lado < 0 && x <= inicioX - distanciaPatrulha);

        if (passouDaPonta || TemParedeNaFrente() || !TemChaoNaFrente())
        {
            lado = -lado;
            Virar();
            pausadoAte = Time.time + tempoPausa;
            Parar();
            TocarAnimacao("Gorgona_Parado");
            return;
        }

        rb.linearVelocity = new Vector2(lado * velocidade, rb.linearVelocity.y);
        TocarAnimacao("Gorgona_Andar");
    }

    // ================= Perseguição =================

    bool VendoPlayer()
    {
        if (player == null) return false;
        Vector2 dif = player.position - transform.position;
        if (Mathf.Abs(dif.y) > alturaVisao) return false;

        float limite = perseguindo ? distanciaPerder : distanciaVisao;
        if (Mathf.Abs(dif.x) > limite) return false;

        if (!perseguindo && Mathf.Sign(dif.x) != lado) return false;
        return true;
    }

    void Perseguir()
    {
        perseguindo = true;
        float difX = player.position.x - transform.position.x;
        float dist = Mathf.Abs(difX);

        if (dist > 0.1f)
        {
            float novoLado = Mathf.Sign(difX);
            if (novoLado != lado) { lado = novoLado; Virar(); }
        }

        // Tem algum ataque que alcança o player?
        Ataque escolhido = EscolherAtaque(dist);
        if (escolhido != null)
        {
            Parar();
            if (Time.time >= proximoAtaque) StartCoroutine(Atacar(escolhido));
            else TocarAnimacao("Gorgona_Parado");
            return;
        }

        if (TemParedeNaFrente() || !TemChaoNaFrente())
        {
            Parar();
            TocarAnimacao("Gorgona_Parado");
            return;
        }

        rb.linearVelocity = new Vector2(lado * velocidadePerseguicao, rb.linearVelocity.y);
        TocarAnimacao("Gorgona_Correr");
    }

    // Sorteia entre os ataques que alcançam o player nessa distância
    Ataque EscolherAtaque(float dist)
    {
        int possiveis = 0;
        foreach (var a in ataques) if (dist <= a.distanciaMaxima) possiveis++;
        if (possiveis == 0) return null;

        int sorteio = Random.Range(0, possiveis);
        foreach (var a in ataques)
        {
            if (dist > a.distanciaMaxima) continue;
            if (sorteio == 0) return a;
            sorteio--;
        }
        return null;
    }

    // ================= Ataque =================

    IEnumerator Atacar(Ataque a)
    {
        atacando = true;
        ataqueAtual = a;
        proximoAtaque = Time.time + a.duracao + recargaAtaque;
        TocarAnimacao(a.animacao, true);

        yield return new WaitForSeconds(a.momentoAcerto);

        Vector2 centro = (Vector2)col.bounds.center + new Vector2(lado * a.alcance, 0);
        foreach (var alvo in Physics2D.OverlapCircleAll(centro, a.raio))
        {
            var vidaPlayer = alvo.GetComponent<PlayerVida>();
            if (vidaPlayer != null) vidaPlayer.LevarDano(a.dano, transform.position);
        }

        yield return new WaitForSeconds(Mathf.Max(0f, a.duracao - a.momentoAcerto));

        atacando = false;
        ataqueAtual = null;
        TocarAnimacao("Gorgona_Parado");
    }

    // ================= Levar dano (chamado pelo ataque do Player) =================

    public void ReceberDano(int dano)
    {
        if (morto) return;

        vida -= dano;
        perseguindo = true;

        if (vida <= 0)
        {
            StopAllCoroutines();
            atacando = false; recuando = false;
            StartCoroutine(Morrer());
            return;
        }

        // Mais pesada que o Lanceiro: durante um ataque ela NÃO é interrompida, só pisca
        if (atacando)
        {
            StartCoroutine(Piscar());
            return;
        }

        StopAllCoroutines();
        recuando = false;
        if (sr != null) sr.color = corOriginal;
        StartCoroutine(Recuar());
    }

    IEnumerator Piscar()
    {
        if (sr != null) sr.color = corDano;
        yield return new WaitForSeconds(0.1f);
        if (sr != null) sr.color = corOriginal;
    }

    IEnumerator Recuar()
    {
        recuando = true;
        TocarAnimacao("Gorgona_Dano", true);
        if (sr != null) sr.color = corDano;

        float direcao = (player != null && player.position.x > transform.position.x) ? -1f : 1f;
        rb.linearVelocity = new Vector2(direcao * forcaRecuo, rb.linearVelocity.y);

        yield return new WaitForSeconds(tempoRecuo);

        if (sr != null) sr.color = corOriginal;
        recuando = false;
    }

    IEnumerator Morrer()
    {
        morto = true;
        if (sr != null) sr.color = corOriginal;
        TocarAnimacao("Gorgona_Morte", true);

        rb.linearVelocity = Vector2.zero;
        rb.bodyType = RigidbodyType2D.Kinematic;
        col.enabled = false;

        yield return new WaitForSeconds(DuracaoAnimacao("Gorgona_Morte", 0.5f) + 0.3f);

        float t = 0;
        while (t < tempoParaSumir)
        {
            t += Time.deltaTime;
            if (sr != null)
            {
                Color c = corOriginal;
                c.a = 1f - t / tempoParaSumir;
                sr.color = c;
            }
            yield return null;
        }
        Destroy(gameObject);
    }

    // ================= Encostar no player =================

    void OnCollisionStay2D(Collision2D outro)
    {
        if (morto) return;
        var vidaPlayer = outro.collider.GetComponent<PlayerVida>();
        if (vidaPlayer != null) vidaPlayer.LevarDano(danoAoEncostar, transform.position);
    }

    // ================= Sensores =================

    bool TemParedeNaFrente()
    {
        Bounds b = col.bounds;
        return Physics2D.Raycast(b.center, new Vector2(lado, 0), b.extents.x + 0.1f, camadaChao);
    }

    bool TemChaoNaFrente()
    {
        Bounds b = col.bounds;
        Vector2 origem = new Vector2(b.center.x + lado * (b.extents.x + 0.05f), b.min.y + 0.05f);
        return Physics2D.Raycast(origem, Vector2.down, 0.4f, camadaChao);
    }

    // ================= Utilidades =================

    void Parar() { rb.linearVelocity = new Vector2(0, rb.linearVelocity.y); }

    void Virar()
    {
        Vector3 escala = transform.localScale;
        escala.x = Mathf.Abs(escala.x) * (lado > 0 ? 1 : -1);
        transform.localScale = escala;
    }

    void TocarAnimacao(string nome, bool reiniciar = false)
    {
        if (!reiniciar && animAtual == nome) return;
        if (anim == null || anim.runtimeAnimatorController == null) return;
        int id = Animator.StringToHash(nome);
        if (!anim.HasState(0, id)) return;
        anim.Play(id, 0, 0f);
        animAtual = nome;
    }

    float DuracaoAnimacao(string nome, float padrao)
    {
        if (anim == null || anim.runtimeAnimatorController == null) return padrao;
        foreach (var c in anim.runtimeAnimatorController.animationClips)
            if (c.name == nome) return c.length;
        return padrao;
    }

    void OnDrawGizmosSelected()
    {
        var c = GetComponent<Collider2D>();
        if (c == null) return;
        Bounds b = c.bounds;
        float centroPatrulha = Application.isPlaying ? inicioX : transform.position.x;
        float l = Application.isPlaying ? lado : (comecaIndoParaDireita ? 1f : -1f);

        Gizmos.color = Color.green;
        Gizmos.DrawLine(new Vector3(centroPatrulha - distanciaPatrulha, b.min.y), new Vector3(centroPatrulha + distanciaPatrulha, b.min.y));

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(transform.position, new Vector3(distanciaVisao * 2, alturaVisao * 2, 0));

        // Área de cada ataque (vermelho, laranja, rosa)
        Color[] cores = { Color.red, new Color(1f, 0.5f, 0f), Color.magenta };
        for (int i = 0; i < ataques.Length; i++)
        {
            Gizmos.color = cores[i % cores.Length];
            Gizmos.DrawWireSphere(b.center + new Vector3(l * ataques[i].alcance, 0), ataques[i].raio);
        }
    }
}

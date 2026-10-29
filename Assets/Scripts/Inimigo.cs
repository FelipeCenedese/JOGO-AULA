using System.Collections;
using UnityEngine;

// Inimigo:
// - Patrulha: anda de um lado pro outro perto de onde começou, parando um pouco em cada ponta
// - Quando vê o player: corre atrás dele
// - Quando chega perto: ataca com a lança
// - Leva dano, recua, morre e some aos poucos
public class Inimigo : MonoBehaviour
{
    [Header("Vida")]
    public int vidaMaxima = 5;
    public int almasAoMorrer = 10;           // almas que o player ganha ao matar

    [Header("Patrulha")]
    public float velocidade = 1.5f;
    public float distanciaPatrulha = 3f;     // quanto ele anda para cada lado a partir de onde começou
    public float tempoPausa = 1f;            // tempo parado em cada ponta antes de virar
    public bool comecaIndoParaDireita = false;
    public LayerMask camadaChao;             // marque a Layer "Chao"

    [Header("Perseguição")]
    public float distanciaVisao = 5f;        // de quão longe ele enxerga o player
    public float distanciaPerder = 7f;       // se o player passar disso, ele desiste
    public float alturaVisao = 1.5f;         // diferença de altura máxima para enxergar
    public float velocidadePerseguicao = 3f;

    [Header("Ataque")]
    public float distanciaParaAtacar = 1.2f; // quão perto o player precisa estar
    public float alcanceAtaque = 0.9f;       // tamanho da área que a lança acerta
    public int danoAtaque = 1;
    public float momentoAcerto = 0.3f;       // segundos depois de começar o ataque em que a lança acerta
    public float duracaoAtaque = 0.9f;       // tempo total do ataque (igual à animação)
    public float recargaAtaque = 1.2f;       // espera entre um ataque e outro

    [Header("Encostar no player")]
    public int danoAoEncostar = 1;           // (não é mais usado: ele só dá dano com o golpe da lança)
    public float distanciaVirar = 1f;        // se o player chegar até essa distância por trás, ele vira e ataca

    [Header("Ao levar dano")]
    public float forcaRecuo = 4f;
    public float tempoRecuo = 0.15f;
    public Color corDano = Color.red;
    public float tempoParaSumir = 1f;

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
    bool perseguindo;
    bool atacando;
    bool recuando;
    bool morto;
    Color corOriginal;
    string animAtual;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        col = GetComponent<Collider2D>();
        sr = GetComponent<SpriteRenderer>();
        anim = GetComponent<Animator>();

        // Arredonda as quinas da caixa de colisão (mesmo tamanho por fora).
        // Sem isso, a caixa enrosca nas emendas entre os pedaços de chão e o inimigo trava.
        if (col is BoxCollider2D caixa && caixa.edgeRadius == 0f)
        {
            float raio = Mathf.Min(0.08f, caixa.size.x * 0.25f);
            caixa.size -= Vector2.one * raio * 2f;
            caixa.edgeRadius = raio;
        }

        // Sem atrito: não gruda no chão nem nas paredes
        if (rb.sharedMaterial == null)
            rb.sharedMaterial = new PhysicsMaterial2D("InimigoSemAtrito") { friction = 0f, bounciness = 0f };

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

        if (atacando)
        {
            Parar();
            return;
        }

        if (VendoPlayer())
            Perseguir();
        else
            Patrulhar();
    }

    // ================= Patrulha =================

    void Patrulhar()
    {
        perseguindo = false;

        // Parado numa das pontas
        if (Time.time < pausadoAte)
        {
            Parar();
            TocarAnimacao("Inimigo_Parado");
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
            TocarAnimacao("Inimigo_Parado");
            return;
        }

        rb.linearVelocity = new Vector2(lado * velocidade, rb.linearVelocity.y);
        TocarAnimacao("Inimigo_Andar");
    }

    // ================= Perseguição =================

    bool VendoPlayer()
    {
        if (player == null) return false;

        Vector2 dif = player.position - transform.position;
        if (Mathf.Abs(dif.y) > alturaVisao) return false;

        // Quando já está perseguindo, só desiste mais longe
        float limite = perseguindo ? distanciaPerder : distanciaVisao;
        if (Mathf.Abs(dif.x) > limite) return false;

        // Para começar a perseguir, o player precisa estar na frente dele...
        // ...a não ser que chegue muito perto por trás: aí ele percebe e vira
        bool muitoPerto = Mathf.Abs(dif.x) <= distanciaVirar;
        if (!perseguindo && !muitoPerto && Mathf.Sign(dif.x) != lado) return false;

        return true;
    }

    void Perseguir()
    {
        perseguindo = true;

        float difX = player.position.x - transform.position.x;

        // Vira para o player
        if (Mathf.Abs(difX) > 0.1f)
        {
            float novoLado = Mathf.Sign(difX);
            if (novoLado != lado)
            {
                lado = novoLado;
                Virar();
            }
        }

        // Perto o bastante: ataca
        if (Mathf.Abs(difX) <= distanciaParaAtacar)
        {
            Parar();
            if (Time.time >= proximoAtaque)
                StartCoroutine(Atacar());
            else
                TocarAnimacao("Inimigo_Parado");
            return;
        }

        // Não se joga de beirada nem empurra parede: espera ali
        if (TemParedeNaFrente() || !TemChaoNaFrente())
        {
            Parar();
            TocarAnimacao("Inimigo_Parado");
            return;
        }

        rb.linearVelocity = new Vector2(lado * velocidadePerseguicao, rb.linearVelocity.y);
        TocarAnimacao("Inimigo_Andar");
    }

    // ================= Ataque =================

    IEnumerator Atacar()
    {
        atacando = true;
        proximoAtaque = Time.time + recargaAtaque;
        TocarAnimacao("Inimigo_Ataque", true);

        yield return new WaitForSeconds(momentoAcerto);

        Vector2 centro = (Vector2)col.bounds.center + new Vector2(lado * alcanceAtaque, 0);
        foreach (var alvo in Physics2D.OverlapCircleAll(centro, alcanceAtaque))
        {
            var vidaPlayer = alvo.GetComponent<PlayerVida>();
            if (vidaPlayer != null) vidaPlayer.LevarDano(danoAtaque, transform.position);
        }

        yield return new WaitForSeconds(Mathf.Max(0f, duracaoAtaque - momentoAcerto));

        atacando = false;
        TocarAnimacao("Inimigo_Parado");
    }

    // ================= Levar dano (chamado pelo ataque do Player) =================

    public void ReceberDano(int dano)
    {
        if (morto) return;

        vida -= dano;
        BarraVidaInimigo.Atualizar(this, vida, vidaMaxima);
        StopAllCoroutines();   // interrompe ataque ou recuo que estava acontecendo
        atacando = false;
        recuando = false;
        if (sr != null) sr.color = corOriginal;

        // Apanhou: passa a perseguir mesmo se não tinha visto
        perseguindo = true;

        if (vida <= 0)
            StartCoroutine(Morrer());
        else
            StartCoroutine(Recuar());
    }

    IEnumerator Recuar()
    {
        recuando = true;
        TocarAnimacao("Inimigo_Dano", true);
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
        Almas.Adicionar(almasAoMorrer);
        TocarAnimacao("Inimigo_Dano", true);
        if (sr != null) sr.color = corDano;

        rb.linearVelocity = Vector2.zero;
        rb.bodyType = RigidbodyType2D.Kinematic;
        col.enabled = false;

        yield return new WaitForSeconds(0.15f);
        if (anim != null) anim.speed = 0;   // congela no quadro de dano

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

    // Encostar não dá mais dano: ele só percebe o player, vira e ataca com a lança
    void OnCollisionStay2D(Collision2D outro)
    {
        if (morto) return;
        if (outro.collider.GetComponent<PlayerVida>() != null)
            perseguindo = true;
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
        return Physics2D.Raycast(origem, Vector2.down, 0.3f, camadaChao);
    }

    // ================= Utilidades =================

    void Parar()
    {
        rb.linearVelocity = new Vector2(0, rb.linearVelocity.y);
    }

    void Virar()
    {
        Vector3 escala = transform.localScale;
        escala.x = Mathf.Abs(escala.x) * (lado > 0 ? 1 : -1);
        transform.localScale = escala;
    }

    // Só toca se o inimigo tiver Animator e uma animação com esse nome
    void TocarAnimacao(string nome, bool reiniciar = false)
    {
        if (!reiniciar && animAtual == nome) return;
        if (anim == null || anim.runtimeAnimatorController == null) return;
        int id = Animator.StringToHash(nome);
        if (!anim.HasState(0, id)) return;
        anim.Play(id, 0, 0f);
        animAtual = nome;
    }

    // Desenhos de ajuda na cena (com o inimigo selecionado)
    void OnDrawGizmosSelected()
    {
        var c = GetComponent<Collider2D>();
        if (c == null) return;
        Bounds b = c.bounds;
        float centroPatrulha = Application.isPlaying ? inicioX : transform.position.x;
        float l = Application.isPlaying ? lado : (comecaIndoParaDireita ? 1f : -1f);

        // Verde: até onde ele patrulha
        Gizmos.color = Color.green;
        Vector3 chao = new Vector3(0, b.min.y, 0);
        Gizmos.DrawLine(new Vector3(centroPatrulha - distanciaPatrulha, chao.y), new Vector3(centroPatrulha + distanciaPatrulha, chao.y));
        Gizmos.DrawLine(new Vector3(centroPatrulha - distanciaPatrulha, chao.y), new Vector3(centroPatrulha - distanciaPatrulha, chao.y + 0.5f));
        Gizmos.DrawLine(new Vector3(centroPatrulha + distanciaPatrulha, chao.y), new Vector3(centroPatrulha + distanciaPatrulha, chao.y + 0.5f));

        // Amarelo: área de visão
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(transform.position, new Vector3(distanciaVisao * 2, alturaVisao * 2, 0));

        // Vermelho: onde a lança acerta
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(b.center + new Vector3(l * alcanceAtaque, 0), alcanceAtaque);
    }
}

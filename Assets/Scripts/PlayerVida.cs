using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

// Vida do player: leva dano, pisca invencível por um tempo, é jogado para trás e reinicia a fase ao morrer
public class PlayerVida : MonoBehaviour
{
    public int vidaMaxima = 5;
    public float tempoInvencivel = 1f;
    public Vector2 forcaEmpurrao = new Vector2(8f, 6f);
    public float tempoSemControle = 0.25f;

    public int vida { get; private set; }

    Player player;
    SpriteRenderer sr;
    bool invencivel;
    bool morto;
    Coroutine piscando;

    void Awake()
    {
        vida = vidaMaxima;
        player = GetComponent<Player>();
        sr = GetComponent<SpriteRenderer>();

        // Onde o player nasce nesta cena:
        // 1) chegou por uma porta -> no PontoChegada com o mesmo id (ou, sem ele, na porta de volta)
        // 2) senão, se o último checkpoint é desta cena -> no checkpoint
        // 3) senão -> onde ele está posicionado na cena
        string cenaAtual = SceneManager.GetActiveScene().name;
        bool posicionado = false;
        if (!string.IsNullOrEmpty(GerenciadorPortas.idChegada))
        {
            foreach (var ponto in FindObjectsByType<PontoChegada>(FindObjectsSortMode.None))
            {
                if (ponto.id == GerenciadorPortas.idChegada)
                {
                    transform.position = ponto.transform.position;
                    posicionado = true;
                    break;
                }
            }
            // Não achou o PontoChegada? Aparece na porta desta cena que leva de volta para onde ele veio
            if (!posicionado && !string.IsNullOrEmpty(GerenciadorPortas.cenaOrigem))
            {
                foreach (var porta in FindObjectsByType<Porta>(FindObjectsSortMode.None))
                {
                    if (porta.cenaDestino == GerenciadorPortas.cenaOrigem)
                    {
                        transform.position = porta.transform.position;
                        posicionado = true;
                        break;
                    }
                }
            }
            GerenciadorPortas.idChegada = null;
            GerenciadorPortas.cenaOrigem = null;
        }
        if (!posicionado && GerenciadorCheckpoint.temCheckpoint && GerenciadorCheckpoint.cena == cenaAtual)
            transform.position = GerenciadorCheckpoint.posicao;
    }

    // Chamado pelo inimigo quando encosta no player
    public void LevarDano(int dano, Vector2 posicaoDoInimigo)
    {
        if (invencivel || morto) return;

        vida -= dano;
        Debug.Log("Player levou dano. Vida: " + vida);

        // Empurra para o lado oposto ao inimigo
        float lado = transform.position.x < posicaoDoInimigo.x ? -1f : 1f;
        if (player != null)
            player.Empurrao(new Vector2(lado * forcaEmpurrao.x, forcaEmpurrao.y), tempoSemControle);

        if (vida <= 0)
        {
            StartCoroutine(Morrer());
            return;
        }

        piscando = StartCoroutine(Piscar());
    }

    IEnumerator Piscar()
    {
        invencivel = true;
        float fim = Time.time + tempoInvencivel;
        while (Time.time < fim)
        {
            if (sr != null) sr.enabled = !sr.enabled;
            yield return new WaitForSeconds(0.08f);
        }
        if (sr != null) sr.enabled = true;
        invencivel = false;
    }

    IEnumerator Morrer()
    {
        morto = true;
        if (piscando != null) StopCoroutine(piscando);
        if (sr != null) sr.enabled = true;
        if (player != null) player.enabled = false;

        // Toca a animação de morte e espera ela terminar
        float espera = 1.5f;
        Animator anim = GetComponent<Animator>();
        if (anim != null)
        {
            anim.Play("Morte", 0, 0f);
            foreach (var clip in anim.runtimeAnimatorController.animationClips)
                if (clip.name == "Morte") espera = clip.length + 0.8f;
        }

        yield return new WaitForSeconds(espera);

        // Volta para a cena do último checkpoint (pode ser outra área); sem checkpoint, recomeça esta
        if (GerenciadorCheckpoint.temCheckpoint && !string.IsNullOrEmpty(GerenciadorCheckpoint.cena))
            SceneManager.LoadScene(GerenciadorCheckpoint.cena);
        else
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
}

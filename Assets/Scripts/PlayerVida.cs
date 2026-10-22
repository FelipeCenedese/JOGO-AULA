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

    void Awake()
    {
        vida = vidaMaxima;
        player = GetComponent<Player>();
        sr = GetComponent<SpriteRenderer>();
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

        StartCoroutine(Piscar());
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
        if (player != null) player.enabled = false;
        if (sr != null) sr.color = Color.red;
        yield return new WaitForSeconds(1.5f);
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex); // recomeça a fase
    }
}

using UnityEngine;

// Barrinha vermelha em cima do inimigo.
// Só aparece depois que ele toma o primeiro dano, e diminui conforme a porcentagem de vida.
// Os inimigos chamam BarraVidaInimigo.Atualizar(...) quando levam dano.
public class BarraVidaInimigo : MonoBehaviour
{
    const float Largura = 0.7f;      // tamanho da barra no mundo
    const float Altura = 0.07f;
    const float AcimaDaCabeca = 0.25f;

    static Sprite pixel;

    Transform dono;
    Collider2D colDono;
    Transform preenchimento;
    float alvo = 1f, mostrado = 1f;

    public static void Atualizar(Component inimigo, int vida, int vidaMaxima)
    {
        var barra = inimigo.GetComponentInChildren<BarraVidaInimigo>();
        if (vida <= 0)
        {
            if (barra != null) Destroy(barra.gameObject);   // morreu: tira a barra
            return;
        }
        if (barra == null) barra = Criar(inimigo);
        barra.alvo = Mathf.Clamp01((float)vida / vidaMaxima);
    }

    static BarraVidaInimigo Criar(Component inimigo)
    {
        if (pixel == null)
        {
            var tex = new Texture2D(1, 1) { filterMode = FilterMode.Point };
            tex.SetPixel(0, 0, Color.white);
            tex.Apply();
            pixel = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0f, 0.5f), 1f);
        }

        var obj = new GameObject("BarraVida");
        obj.transform.SetParent(inimigo.transform, false);
        var barra = obj.AddComponent<BarraVidaInimigo>();
        barra.dono = inimigo.transform;
        barra.colDono = inimigo.GetComponent<Collider2D>();

        var srDono = inimigo.GetComponent<SpriteRenderer>();
        int camada = srDono != null ? srDono.sortingLayerID : 0;
        int ordem = srDono != null ? srDono.sortingOrder + 10 : 10;

        // Fundo escuro e parte vermelha, as duas começando na esquerda
        Criarpedaco(obj.transform, "Fundo", new Color(0.12f, 0.05f, 0.06f, 0.85f), camada, ordem);
        barra.preenchimento = Criarpedaco(obj.transform, "Vermelho", new Color(0.85f, 0.12f, 0.15f), camada, ordem + 1);
        return barra;
    }

    static Transform Criarpedaco(Transform pai, string nome, Color cor, int camada, int ordem)
    {
        var t = new GameObject(nome).transform;
        t.SetParent(pai, false);
        t.localPosition = new Vector3(-Largura / 2f, 0, 0);
        t.localScale = new Vector3(Largura, Altura, 1);
        var sr = t.gameObject.AddComponent<SpriteRenderer>();
        sr.sprite = pixel;
        sr.color = cor;
        sr.sortingLayerID = camada;
        sr.sortingOrder = ordem;
        return t;
    }

    void LateUpdate()
    {
        if (dono == null) { Destroy(gameObject); return; }

        // Fica acima da cabeça e não espelha quando o inimigo vira
        float topo = colDono != null && colDono.enabled ? colDono.bounds.max.y : dono.position.y + 1f;
        transform.position = new Vector3(dono.position.x, topo + AcimaDaCabeca, dono.position.z);
        Vector3 e = dono.lossyScale;
        transform.localScale = new Vector3(1f / (e.x == 0 ? 1 : e.x), 1f / (e.y == 0 ? 1 : e.y), 1f);

        mostrado = Mathf.MoveTowards(mostrado, alvo, Time.deltaTime * 2f);
        preenchimento.localScale = new Vector3(Largura * mostrado, Altura, 1);
    }
}

using System.Collections;
using UnityEngine;
using UnityEngine.Rendering.Universal;

// Área escura (ex.: túnel). Quando o player entra, as luzes escolhidas vão escurecendo;
// quando sai, voltam ao normal.
// Como usar: crie um objeto vazio, adicione um Box Collider 2D com "Is Trigger" marcado
// cobrindo o túnel, coloque este script e arraste as luzes (Global Light 2D e a luz da lua) no campo Luzes.
[RequireComponent(typeof(Collider2D))]
public class ZonaEscura : MonoBehaviour
{
    public Light2D[] luzes;                 // luzes que escurecem dentro da zona
    [Range(0f, 1f)]
    public float fatorDentro = 0.2f;        // 0.2 = as luzes ficam com 20% da força dentro do túnel
    public float tempoTransicao = 1f;       // segundos para escurecer ou clarear

    float[] intensidadeOriginal;
    Coroutine transicao;

    void Awake()
    {
        GetComponent<Collider2D>().isTrigger = true;
        intensidadeOriginal = new float[luzes.Length];
        for (int i = 0; i < luzes.Length; i++)
            if (luzes[i] != null) intensidadeOriginal[i] = luzes[i].intensity;
    }

    void OnTriggerEnter2D(Collider2D outro)
    {
        if (outro.GetComponent<Player>() != null) Mudar(fatorDentro);
    }

    void OnTriggerExit2D(Collider2D outro)
    {
        if (outro.GetComponent<Player>() != null) Mudar(1f);
    }

    void Mudar(float fator)
    {
        if (transicao != null) StopCoroutine(transicao);
        transicao = StartCoroutine(Transicao(fator));
    }

    IEnumerator Transicao(float fator)
    {
        float[] inicio = new float[luzes.Length];
        for (int i = 0; i < luzes.Length; i++)
            if (luzes[i] != null) inicio[i] = luzes[i].intensity;

        float t = 0;
        while (t < tempoTransicao)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / tempoTransicao);
            for (int i = 0; i < luzes.Length; i++)
                if (luzes[i] != null)
                    luzes[i].intensity = Mathf.Lerp(inicio[i], intensidadeOriginal[i] * fator, k);
            yield return null;
        }
    }

    // Mostra a área da zona na cena
    void OnDrawGizmos()
    {
        var c = GetComponent<Collider2D>();
        if (c == null) return;
        Gizmos.color = new Color(0.2f, 0.2f, 0.6f, 0.25f);
        Gizmos.DrawCube(c.bounds.center, c.bounds.size);
    }
}

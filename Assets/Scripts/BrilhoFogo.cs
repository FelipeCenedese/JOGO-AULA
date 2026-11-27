using UnityEngine;
using UnityEngine.Rendering.Universal;

// Faz a espada de fogo do chefe brilhar:
// - uma cópia só das chamas, desenhada sem sombra (sempre acesa, mesmo no escuro)
// - uma luz alaranjada que acompanha onde o fogo está em cada quadro e tremula
// Tudo já vem configurado no prefab do CavaleiroBrasa.
public class BrilhoFogo : MonoBehaviour
{
    [Header("Partes (já ligadas no prefab)")]
    public SpriteRenderer brilho;          // filho "Brilho": mostra só as chamas
    public Light2D luz;                    // filho "LuzEspada"

    [Header("Quadros (gerados junto com os sprites)")]
    public Sprite[] quadrosBrilho;         // mesmo número e ordem dos quadros do chefe
    public Vector2[] centroFogo;           // onde está o fogo em cada quadro
    public float[] forcaFogo;              // quanto fogo tem em cada quadro

    [Header("Ajustes")]
    public float intensidade = 1.3f;       // força da luz
    public float raio = 2.6f;              // tamanho da luz
    public float tremor = 0.25f;           // quanto a luz tremula (0 = parada)
    [Range(0, 1)] public float forcaBrilho = 0.9f;   // quão forte as chamas brilham

    SpriteRenderer corpo;
    Sprite ultimo;
    int quadro = -1;
    float semente;

    void Awake()
    {
        corpo = GetComponent<SpriteRenderer>();
        semente = Random.value * 100f;

        // Material sem iluminação: as chamas ficam sempre claras
        if (brilho != null)
        {
            var shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            if (shader != null) brilho.material = new Material(shader);
        }
    }

    void LateUpdate()
    {
        if (corpo == null) return;

        // Descobre o número do quadro pelo nome do sprite (CavaleiroBrasa_37 -> 37)
        if (corpo.sprite != ultimo)
        {
            ultimo = corpo.sprite;
            quadro = -1;
            if (ultimo != null)
            {
                string n = ultimo.name;
                int i = n.LastIndexOf('_');
                if (i >= 0 && int.TryParse(n.Substring(i + 1), out int q)) quadro = q;
            }
        }

        bool temQuadro = quadrosBrilho != null && quadro >= 0 && quadro < quadrosBrilho.Length;
        float alfa = corpo.color.a;   // some junto quando o chefe desaparece

        if (brilho != null)
        {
            brilho.enabled = corpo.enabled && temQuadro;
            if (temQuadro) brilho.sprite = quadrosBrilho[quadro];
            brilho.flipX = corpo.flipX;
            float pulso = 0.85f + 0.15f * Mathf.PerlinNoise(semente, Time.time * 6f);
            brilho.color = new Color(1f, 1f, 1f, alfa * forcaBrilho * pulso);
        }

        if (luz != null)
        {
            float forca = temQuadro && forcaFogo != null && quadro < forcaFogo.Length ? forcaFogo[quadro] : 0.5f;
            Vector2 c = temQuadro && centroFogo != null && quadro < centroFogo.Length ? centroFogo[quadro] : new Vector2(0.6f, 0.6f);
            luz.transform.localPosition = new Vector3(c.x, c.y, 0);

            float t = 1f + tremor * (Mathf.PerlinNoise(semente + 7f, Time.time * 9f) - 0.5f) * 2f;
            luz.intensity = intensidade * forca * t * alfa;
            luz.pointLightOuterRadius = raio * Mathf.Lerp(0.7f, 1.4f, Mathf.Clamp01(forca - 0.3f)) * (0.95f + 0.05f * t);
        }
    }
}

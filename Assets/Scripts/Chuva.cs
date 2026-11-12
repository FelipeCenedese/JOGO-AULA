using UnityEngine;
using UnityEngine.SceneManagement;

// Chuva fraca e simples, que segue a câmera.
// Ela se cria sozinha nas cenas listadas em CenasComChuva: não precisa colocar nada na cena.
// Para ligar a chuva em outra cena, é só colocar o nome da cena na lista abaixo.
public class Chuva : MonoBehaviour
{
    static readonly string[] CenasComChuva = { "SampleScene" };   // a floresta (primeiro nível)

    // Ajustes da chuva
    const float GotasPorSegundo = 90f;      // quantidade: aumente para chover mais forte
    const float VelocidadeQueda = 11f;      // velocidade das gotas
    const float Vento = -1.2f;              // inclinação: negativo = cai para a esquerda, 0 = reto, positivo = direita
    const float Transparencia = 0.3f;       // 0 = invisível, 1 = gota bem branca
    const float Grossura = 0.025f;          // largura da gota
    const float Comprimento = 0.35f;        // tamanho do risco da gota

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Iniciar()
    {
        Criar(SceneManager.GetActiveScene());
        SceneManager.sceneLoaded += (cena, modo) => Criar(cena);
    }

    static void Criar(Scene cena)
    {
        if (System.Array.IndexOf(CenasComChuva, cena.name) < 0) return;
        var cam = Camera.main;
        if (cam == null) { Debug.LogWarning("Chuva: não achei a Main Camera"); return; }
        if (cam.GetComponentInChildren<Chuva>() != null) return;

        // Fica presa na câmera, um pouco acima da parte de cima da tela
        var obj = new GameObject("Chuva");
        obj.transform.SetParent(cam.transform, false);
        float altura = cam.orthographicSize;
        float largura = altura * cam.aspect;
        obj.transform.localPosition = new Vector3(0, altura + 1f, 10f);   // z 10: na frente da câmera
        obj.AddComponent<Chuva>();

        var ps = obj.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.loop = true;
        main.playOnAwake = true;
        main.simulationSpace = ParticleSystemSimulationSpace.World;   // as gotas não "andam" junto quando a câmera mexe
        main.startSpeed = 0f;
        main.startSize = Grossura;
        main.startLifetime = (altura * 2f + 2f) / VelocidadeQueda;     // tempo para atravessar a tela inteira
        main.startColor = new Color(0.75f, 0.85f, 1f, Transparencia);
        main.maxParticles = 1000;
        main.gravityModifier = 0f;

        var emissao = ps.emission;
        emissao.rateOverTime = GotasPorSegundo;

        // Nasce numa faixa larga acima da tela (mais larga que a tela, por causa do vento e do movimento)
        var forma = ps.shape;
        forma.shapeType = ParticleSystemShapeType.Box;
        forma.scale = new Vector3(largura * 2f + 6f, 0.1f, 0.1f);

        var vel = ps.velocityOverLifetime;
        vel.enabled = true;
        vel.space = ParticleSystemSimulationSpace.World;
        vel.x = new ParticleSystem.MinMaxCurve(Vento * 0.9f, Vento * 1.1f);
        vel.y = new ParticleSystem.MinMaxCurve(-VelocidadeQueda * 1.1f, -VelocidadeQueda * 0.9f);
        vel.z = new ParticleSystem.MinMaxCurve(0f, 0f);

        // Desenha cada gota como um risco esticado na direção em que cai
        var rend = obj.GetComponent<ParticleSystemRenderer>();
        rend.renderMode = ParticleSystemRenderMode.Stretch;
        rend.lengthScale = 1f;
        rend.velocityScale = Comprimento / VelocidadeQueda;
        rend.material = CriarMaterial();
        rend.sortingOrder = 50;          // na frente do cenário

        // Já começa chovendo na tela toda, sem esperar as primeiras gotas caírem
        ps.Play();
        ps.Simulate(main.startLifetime.constant, true, false, true);
        ps.Play();
    }

    static Material CriarMaterial()
    {
        var shader = Shader.Find("Sprites/Default");
        if (shader == null) shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
        var mat = new Material(shader);
        var tex = new Texture2D(1, 1);
        tex.SetPixel(0, 0, Color.white);
        tex.Apply();
        mat.mainTexture = tex;
        return mat;
    }
}

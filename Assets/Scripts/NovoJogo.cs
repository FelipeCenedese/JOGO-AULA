using UnityEngine;

// Começar um jogo novo (apertar Play no Unity ou abrir o jogo):
// zera almas, melhorias compradas, checkpoint e portas.
// Sem isso, com o "Enter Play Mode Options" do projeto ligado, essas coisas
// continuavam da partida anterior.
public static class NovoJogo
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void AoIniciar() => Zerar();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void AntesDaPrimeiraCena() => Zerar();

    public static void Zerar()
    {
        Almas.Zerar();
        Melhorias.Limpar();
        GerenciadorCheckpoint.Limpar();
        GerenciadorPortas.Limpar();
        Time.timeScale = 1f;
    }
}

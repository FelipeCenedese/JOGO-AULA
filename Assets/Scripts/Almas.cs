using UnityEngine;

// Almas: a moeda do jogo.
// - Almas.Adicionar(10)  -> ganha almas (os inimigos chamam isso quando morrem)
// - Almas.Gastar(50)     -> tenta gastar; devolve false se não tiver o bastante (para a loja/upgrades no futuro)
// - Almas.total          -> quanto o player tem agora
// O total continua ao trocar de cena e ao morrer.
// O contador na tela fica em ContadorAlmas.cs.
public static class Almas
{
    public static int total { get; private set; }

    public static void Adicionar(int quantidade)
    {
        if (quantidade <= 0) return;
        total += quantidade;
        ContadorAlmas.Ganhou(quantidade);
    }

    public static bool Gastar(int quantidade)
    {
        if (quantidade > total) return false;
        total -= quantidade;
        return true;
    }
}

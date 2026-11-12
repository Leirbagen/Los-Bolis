using UnityEngine;
using System;

public class SkeletonHealth : MonoBehaviour
{
    public int vidas = 3;
    public int maxVidas = 5;

    // Evento para avisar a la UI que la vida cambió (ya sea daño o curación)
    public static event Action OnCualquierVidaCambiada;

    private void OnEnable()
    {
        GameManager.OnMuerteGlobal += Morir;
    }

    private void OnDisable()
    {
        GameManager.OnMuerteGlobal -= Morir;
    }

    private void Start()
    {
        // Nos aseguramos de que siempre inicien con 3 vidas al dar Play
        vidas = 3;
        OnCualquierVidaCambiada?.Invoke(); // Actualiza los textos al iniciar
    }

    public void RecibirDano(int cantidad)
    {
        vidas -= cantidad;
        Debug.Log("[-] " + gameObject.name + " recibió daño. Vidas restantes: " + vidas);

        OnCualquierVidaCambiada?.Invoke(); // Avisa a la pantalla que actualice los números

        if (vidas <= 0)
        {
            GameManager.Instancia.DispararMuerteGlobal();
        }
    }

    // NUEVO: Método para curarse con la fruta
    public void Curar(int cantidad)
    {
        if (vidas < maxVidas)
        {
            vidas += cantidad;

            // Si por alguna razón se pasa de 5, lo limitamos a 5
            if (vidas > maxVidas) vidas = maxVidas;

            Debug.Log("[+] " + gameObject.name + " comió una fruta. Se incrementó la vida. Vidas actuales: " + vidas);

            OnCualquierVidaCambiada?.Invoke(); // Avisa a la pantalla
        }
        else
        {
            Debug.Log(gameObject.name + " intentó comer una fruta, pero ya tiene la vida al máximo (" + maxVidas + ").");
        }
    }

    private void Morir()
    {
        gameObject.SetActive(false);
    }
}
using UnityEngine;
using System;

public class SkeletonHealth : MonoBehaviour
{
    [Header("Configuración de Salud")]
    public int maxVidas = 3;
    public int vidas;

    public static event Action OnCualquierVidaCambiada;

    private float proximoDanoPermitido = 0f;

    private void OnEnable()
    {
        GameManager.OnMuerteGlobal += Morir;
    }

    private void OnDisable()
    {
        GameManager.OnMuerteGlobal -= Morir;
    }

    private void Awake()
    {
        vidas = maxVidas;
    }

    private void Start()
    {
        OnCualquierVidaCambiada?.Invoke();
    }

    // Unity llama a este método automáticamente cuando el CharacterController choca físicamente con un objeto sólido
    private void OnControllerColliderHit(ControllerColliderHit hit)
    {
        if (Time.time < proximoDanoPermitido) return;

        // Buscamos si el objeto sólido contra el que chocamos tiene el script EnemyDano
        EnemyDano peligro = hit.gameObject.GetComponentInParent<EnemyDano>();

        if (peligro != null)
        {
            RecibirDano(peligro.dano);
            proximoDanoPermitido = Time.time + peligro.tiempoEntreDanos;
        }
    }

    public void RecibirDano(int cantidad)
    {
        if (vidas <= 0) return;

        vidas = Mathf.Clamp(vidas - cantidad, 0, maxVidas);
        Debug.Log("[-] " + gameObject.name + " recibió daño. Vidas: " + vidas);
        OnCualquierVidaCambiada?.Invoke();

        if (vidas <= 0)
        {
            GameManager.Instancia.DispararMuerteGlobal();
        }
    }

    public void Curar(int cantidad)
    {
        if (vidas >= maxVidas) return;

        vidas = Mathf.Clamp(vidas + cantidad, 0, maxVidas);
        OnCualquierVidaCambiada?.Invoke();
    }

    private void Morir()
    {
        gameObject.SetActive(false);
    }
}
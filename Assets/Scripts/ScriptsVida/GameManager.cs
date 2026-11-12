using UnityEngine;
using System;

public class GameManager : MonoBehaviour
{
    public static GameManager Instancia { get; private set; }
    public static event Action OnMuerteGlobal;

    [Header("Sistema de Cambio de Personaje")]
    public GameObject[] armaduras;

    [Header("Sistema de Cámaras")]
    public GameObject[] camarasPrincipales;
    public GameObject[] camarasVirtuales;

    private int indiceActual = 0;

    private void Awake()
    {
        if (Instancia == null) { Instancia = this; }
        else { Destroy(gameObject); }

        // LA MAGIA: Apagamos los vikingos 2 y 3 ANTES de que Unity inicie los teclados.
        // Empezamos el ciclo desde el 1, dejando al Elemento 0 intacto.


        for (int i = 0; i < armaduras.Length; i++)
        {
            CambiarEstado(i, false);
        }
    }

    private void Start()
    {
        // Nos aseguramos de que el Elemento 0 esté 100% encendido al iniciar
        CambiarEstado(0, true);
        Debug.Log("Controlando al personaje 1");
    }

    private void Update()
    {
        // Detecta el cambio de personaje
        if (Input.GetKeyDown(KeyCode.M))
        {
            // 1. Apagamos el vikingo actual soltando el teclado
            CambiarEstado(indiceActual, false);

            // 2. Pasamos al siguiente
            indiceActual++;
            if (indiceActual >= armaduras.Length)
            {
                indiceActual = 0;
            }

            // 3. Encendemos el nuevo vikingo, quien ahora tomará el teclado limpio
            CambiarEstado(indiceActual, true);

            Debug.Log("Controlando al personaje " + (indiceActual + 1));
        }
    }

    // Método universal para encender o apagar todo lo de un vikingo
    private void CambiarEstado(int indice, bool estado)
    {
        if (armaduras[indice] != null)
        {
            MonoBehaviour tpc = armaduras[indice].GetComponent("ThirdPersonController") as MonoBehaviour;
            MonoBehaviour playerInput = armaduras[indice].GetComponent("PlayerInput") as MonoBehaviour;
            MonoBehaviour starterInputs = armaduras[indice].GetComponent("StarterAssetsInputs") as MonoBehaviour;

            if (tpc != null) tpc.enabled = estado;
            if (playerInput != null) playerInput.enabled = estado;
            if (starterInputs != null) starterInputs.enabled = estado;
        }

        if (camarasPrincipales.Length > indice && camarasPrincipales[indice] != null)
        {
            camarasPrincipales[indice].SetActive(estado);
        }

        if (camarasVirtuales.Length > indice && camarasVirtuales[indice] != null)
        {
            camarasVirtuales[indice].SetActive(estado);
        }
    }

    public void DispararMuerteGlobal()
    {
        Debug.Log("¡Un esqueleto ha caído! Fin del juego para todos.");
        OnMuerteGlobal?.Invoke();
    }
}
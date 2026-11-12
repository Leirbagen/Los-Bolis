using UnityEngine;
using System;

public class GameManager : MonoBehaviour
{
    public static GameManager Instancia { get; private set; }
    public static event Action OnMuerteGlobal;

    [Header("Sistema de Cambio de Personaje")]
    public GameObject[] armaduras;

    [Header("Sistema de Cámaras (SOLO VIRTUALES)")]
    // ELIMINADO: camarasPrincipales. ¡Solo debe haber 1 Main Camera en la escena!
    public GameObject[] camarasVirtuales;

    private int indiceActual = 0;

    private void Awake()
    {
        if (Instancia == null) { Instancia = this; }
        else { Destroy(gameObject); }

        for (int i = 0; i < armaduras.Length; i++)
        {
            CambiarEstado(i, false);
        }
    }

    private void Start()
    {
        CambiarEstado(0, true);
        Debug.Log("Controlando al personaje 1");
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.M))
        {
            CambiarEstado(indiceActual, false);

            indiceActual++;
            if (indiceActual >= armaduras.Length)
            {
                indiceActual = 0;
            }

            CambiarEstado(indiceActual, true);
            Debug.Log("Controlando al personaje " + (indiceActual + 1));
        }
    }

    private void CambiarEstado(int indice, bool estado)
    {
        if (armaduras[indice] != null)
        {
            // 1. Apagamos/Encendemos el sistema de movimiento base
            MonoBehaviour tpc = armaduras[indice].GetComponent("ThirdPersonController") as MonoBehaviour;
            MonoBehaviour playerInput = armaduras[indice].GetComponent("PlayerInput") as MonoBehaviour;
            MonoBehaviour starterInputs = armaduras[indice].GetComponent("StarterAssetsInputs") as MonoBehaviour;

            // 2. Apagamos/Encendemos la LÓGICA PERSONALIZADA (Tu Rudo y el de tus compañeros)
            MonoBehaviour rudoCtrl = armaduras[indice].GetComponent("RudoController") as MonoBehaviour;
            // Dile a tus compañeros que agreguen sus scripts aquí de la misma forma:
            // MonoBehaviour vikingo2Ctrl = armaduras[indice].GetComponent("BaleogController") as MonoBehaviour;
            // MonoBehaviour vikingo3Ctrl = armaduras[indice].GetComponent("ErikController") as MonoBehaviour;

            if (tpc != null) tpc.enabled = estado;
            if (playerInput != null) playerInput.enabled = estado;
            if (starterInputs != null) starterInputs.enabled = estado;

            if (rudoCtrl != null) rudoCtrl.enabled = estado;
            // Si agregaron los de arriba, enciéndanlos aquí:
            // if (vikingo2Ctrl != null) vikingo2Ctrl.enabled = estado;
            // if (vikingo3Ctrl != null) vikingo3Ctrl.enabled = estado;
        }

        // 3. Manejo de cámaras: Cinemachine hace la transición sola al activar la cámara virtual.
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
using UnityEngine;
using System;

public class GameManager : MonoBehaviour
{
    public static GameManager Instancia { get; private set; }
    public static event Action OnMuerteGlobal;
    public static event Action<int> OnPersonajeCambiado; // avisa a la UI qué índice está activo

    [Header("Sistema de Cambio de Personaje")]
    public GameObject[] armaduras;

    [Header("Sistema de Cámaras (SOLO VIRTUALES)")]
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
        OnPersonajeCambiado?.Invoke(0);
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
            OnPersonajeCambiado?.Invoke(indiceActual);
        }
    }

    private void CambiarEstado(int indice, bool estado)
    {
        if (armaduras[indice] != null)
        {
            MonoBehaviour tpc = armaduras[indice].GetComponent("ThirdPersonController") as MonoBehaviour;
            MonoBehaviour playerInput = armaduras[indice].GetComponent("PlayerInput") as MonoBehaviour;
            MonoBehaviour starterInputs = armaduras[indice].GetComponent("StarterAssetsInputs") as MonoBehaviour;
            MonoBehaviour rudoCtrl = armaduras[indice].GetComponent("RudoController") as MonoBehaviour;

            if (tpc != null) tpc.enabled = estado;
            if (playerInput != null) playerInput.enabled = estado;
            if (starterInputs != null) starterInputs.enabled = estado;
            if (rudoCtrl != null) rudoCtrl.enabled = estado;
        }

        if (camarasVirtuales.Length > indice && camarasVirtuales[indice] != null)
        {
            camarasVirtuales[indice].SetActive(estado);
        }
    }

    public void DispararMuerteGlobal()
    {
        OnMuerteGlobal?.Invoke();
    }
}
using UnityEngine;

public class UIManager : MonoBehaviour
{
    [System.Serializable]
    public struct HUDPersonaje
    {
        public string nombrePersonaje;
        public GameObject panelHUD;            // El objeto contenedor en el Canvas para este personaje
        public SkeletonHealth componenteSalud; // El script SkeletonHealth de este personaje
        public GameObject[] iconosVidas;       // Arrastra aquí los 3 objetos (imágenes) de vida en orden (0, 1, 2)
    }

    [Header("Configuración de HUD por Personaje (Mismo orden que GameManager)")]
    public HUDPersonaje[] huds;

    private void OnEnable()
    {
        SkeletonHealth.OnCualquierVidaCambiada += ActualizarIconosVida;
        GameManager.OnPersonajeCambiado += CambiarHUDActivo;
    }

    private void OnDisable()
    {
        SkeletonHealth.OnCualquierVidaCambiada -= ActualizarIconosVida;
        GameManager.OnPersonajeCambiado -= CambiarHUDActivo;
    }

    private void Start()
    {
        ActualizarIconosVida();
        CambiarHUDActivo(0);
    }

    private void ActualizarIconosVida()
    {
        for (int i = 0; i < huds.Length; i++)
        {
            if (huds[i].componenteSalud == null || huds[i].iconosVidas == null) continue;

            int vidasActuales = huds[i].componenteSalud.vidas;

            // Recorremos los 3 íconos: si su índice es menor a las vidas actuales, se enciende; si no, se apaga
            for (int j = 0; j < huds[i].iconosVidas.Length; j++)
            {
                if (huds[i].iconosVidas[j] != null)
                {
                    huds[i].iconosVidas[j].SetActive(j < vidasActuales);
                }
            }
        }
    }

    private void CambiarHUDActivo(int indiceActivo)
    {
        for (int i = 0; i < huds.Length; i++)
        {
            if (huds[i].panelHUD != null)
            {
                huds[i].panelHUD.SetActive(i == indiceActivo);
            }
        }
    }
}
using UnityEngine;
using TMPro; // Librería obligatoria para usar TextMeshPro

public class UIManager : MonoBehaviour
{
    [Header("Scripts de Salud de los Vikingos")]
    public SkeletonHealth saludVikingo1;
    public SkeletonHealth saludVikingo2;
    public SkeletonHealth saludVikingo3;

    [Header("Textos de la Interfaz")]
    // Cambiamos 'Text' por 'TextMeshProUGUI'
    public TextMeshProUGUI textoVikingo1;
    public TextMeshProUGUI textoVikingo2;
    public TextMeshProUGUI textoVikingo3;

    private void OnEnable()
    {
        SkeletonHealth.OnCualquierVidaCambiada += ActualizarTextos;
    }

    private void OnDisable()
    {
        SkeletonHealth.OnCualquierVidaCambiada -= ActualizarTextos;
    }

    private void Start()
    {
        ActualizarTextos();
    }

    private void ActualizarTextos()
    {
        if (saludVikingo1 != null && textoVikingo1 != null)
            textoVikingo1.text = "Vikingo 1: " + saludVikingo1.vidas + " vidas";

        if (saludVikingo2 != null && textoVikingo2 != null)
            textoVikingo2.text = "Vikingo 2: " + saludVikingo2.vidas + " vidas";

        if (saludVikingo3 != null && textoVikingo3 != null)
            textoVikingo3.text = "Vikingo 3: " + saludVikingo3.vidas + " vidas";
    }
}
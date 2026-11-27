using UnityEngine;

public class PuertaDeslizante : MonoBehaviour
{
    [Header("Hoja móvil")]
    [SerializeField] private Transform hojaMovil;
    [SerializeField] private Vector3 desplazamientoLocal = new Vector3(0f, 3f, 0f);
    [SerializeField, Min(0.01f)] private float velocidad = 3f;

    private Vector3 posicionLocalCerrada;
    private Vector3 posicionLocalAbierta;
    private bool inicializada;
    private bool abriendo;
    private bool abierta;

    public bool Abierta => abierta;
    public bool EnApertura => abriendo;

    private void Awake()
    {
        if (hojaMovil == null)
        {
            Debug.LogWarning($"[PuertaDeslizante] '{name}' no tiene asignada 'hojaMovil'.", this);
            return;
        }

        AsegurarInicializacion();
    }

    private void OnValidate()
    {
        if (velocidad < 0.01f)
        {
            velocidad = 0.01f;
        }
    }

    public void Abrir()
    {
        if (abriendo || abierta || hojaMovil == null)
        {
            return;
        }

        AsegurarInicializacion();

        if (desplazamientoLocal.sqrMagnitude <= 0.000001f)
        {
            hojaMovil.localPosition = posicionLocalAbierta;
            abierta = true;
            return;
        }

        abriendo = true;
    }

    private void Update()
    {
        if (!abriendo || abierta || hojaMovil == null)
        {
            return;
        }

        hojaMovil.localPosition = Vector3.MoveTowards(
            hojaMovil.localPosition,
            posicionLocalAbierta,
            velocidad * Time.deltaTime
        );

        if ((hojaMovil.localPosition - posicionLocalAbierta).sqrMagnitude <= 0.000001f)
        {
            hojaMovil.localPosition = posicionLocalAbierta;
            abriendo = false;
            abierta = true;
        }
    }

    private void AsegurarInicializacion()
    {
        if (inicializada || hojaMovil == null)
        {
            return;
        }

        posicionLocalCerrada = hojaMovil.localPosition;
        posicionLocalAbierta = posicionLocalCerrada + desplazamientoLocal;
        inicializada = true;
    }
}
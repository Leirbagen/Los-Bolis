using StarterAssets;
using UnityEngine;
using UnityEngine.Events;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[RequireComponent(typeof(Collider))]
public class SwitchInteractivo : MonoBehaviour
{
    public enum TipoSwitch
    {
        Informacion,      // Solo de cerca con E (ignora proyectiles)
        Boton_O_Palanca   // De cerca con E y también con proyectiles
    }

    [Header("1. Configuración Principal")]
    public TipoSwitch tipo = TipoSwitch.Boton_O_Palanca;
    [Tooltip("Distancia máxima para poder activarlo con la tecla E")]
    public float radioInteraccion = 2.0f;
    [Tooltip("Si está desmarcado, una vez activado no se puede volver a apagar")]
    public bool sePuedeDesactivar = true;
    public bool estaActivado = false;
    public bool jugadorCerca = false;

    [Header("2. Activación por Proyectil (Solo Botón o Palanca)")]
    [Tooltip("Escribe el Tag exacto que use el proyectil de tu compañero")]
    public string tagProyectil = "Proyectil";
    [Tooltip("Marca esto si el proyectil debe desaparecer al golpear el switch")]
    public bool destruirProyectilAlImpactar = false;

    [Header("3. Solo para Switch de Información")]
    [Tooltip("Arrastra aquí el panel/texto de UI que quieres mostrar (opcional)")]
    public GameObject panelInformacion;
    [Tooltip("Cierra automáticamente la información cuando el jugador se aleja")]
    public bool cerrarInfoAlAlejarse = true;

    [Header("4. Feedback Visual (Color y Palanca)")]
    [Tooltip("Opcional: objeto 3D que cambia de color al activarse")]
    public Renderer indicadorVisual;
    public Color colorApagado = Color.red;
    public Color colorEncendido = Color.green;

    [Tooltip("Opcional: pivote de la palanca que rota al activarse")]
    public Transform pivotePalanca;
    public Vector3 rotacionPalancaActiva = new Vector3(0f, 0f, -60f);

    [Header("5. Qué activa o desactiva este Switch")]
    public UnityEvent alActivar;
    public UnityEvent alDesactivar;

    private Quaternion rotacionInicialPalanca;
    private float tiempoUltimaActivacion = -1f;
    private const float cooldownActivacion = 0.25f;

    private void Start()
    {
        if (pivotePalanca != null)
        {
            rotacionInicialPalanca = pivotePalanca.localRotation;
        }

        if (panelInformacion != null)
        {
            panelInformacion.SetActive(estaActivado);
        }

        ActualizarVisual();
    }

    private void Update()
    {
        jugadorCerca = HayJugadorActivoCerca();

        // Si es un botón de información y el jugador se aleja, cerramos el panel automáticamente
        if (tipo == TipoSwitch.Informacion && estaActivado && cerrarInfoAlAlejarse && !jugadorCerca)
        {
            CambiarEstado(false);
            return;
        }

        // Activación de cerca con la tecla E
        if (jugadorCerca && PresionoTeclaE())
        {
            IntentarAlternar();
        }
    }

    // Detecta proyectiles si usan Collider con Is Trigger activado
    private void OnTriggerEnter(Collider other)
    {
        EvaluarImpactoProyectil(other.gameObject);
    }

    // Detecta proyectiles si usan Collider sólido (física normal)
    private void OnCollisionEnter(Collision collision)
    {
        EvaluarImpactoProyectil(collision.gameObject);
    }

    private void EvaluarImpactoProyectil(GameObject objetoImpacto)
    {
        // El switch de información NUNCA se activa con proyectiles
        if (tipo != TipoSwitch.Boton_O_Palanca) return;

        // Comparamos el string del tag directamente para evitar errores rojos si aún no crean el Tag
        if (!string.IsNullOrEmpty(tagProyectil) && objetoImpacto.tag == tagProyectil)
        {
            IntentarAlternar();

            if (destruirProyectilAlImpactar)
            {
                Destroy(objetoImpacto);
            }
        }
    }

    public void IntentarAlternar()
    {
        // Evita doble activación en el mismo instante
        if (Time.time - tiempoUltimaActivacion < cooldownActivacion) return;

        if (!estaActivado)
        {
            CambiarEstado(true);
        }
        else if (sePuedeDesactivar)
        {
            CambiarEstado(false);
        }
    }

    private void CambiarEstado(bool nuevoEstado)
    {
        tiempoUltimaActivacion = Time.time;
        estaActivado = nuevoEstado;

        if (panelInformacion != null)
        {
            panelInformacion.SetActive(estaActivado);
        }

        ActualizarVisual();

        if (estaActivado)
        {
            Debug.Log(gameObject.name + " -> ACTIVADO");
            alActivar?.Invoke();
        }
        else
        {
            Debug.Log(gameObject.name + " -> DESACTIVADO");
            alDesactivar?.Invoke();
        }
    }

    private bool HayJugadorActivoCerca()
    {
        Collider[] cercanos = Physics.OverlapSphere(transform.position, radioInteraccion);
        foreach (Collider col in cercanos)
        {
            if (col.tag == "Player")
            {
                ThirdPersonController tpc = col.GetComponent<ThirdPersonController>();
                // Solo permite activar al vikingo actualmente controlado
                if (tpc != null && tpc.enabled && tpc.canMove)
                {
                    return true;
                }
            }
        }
        return false;
    }

    private bool PresionoTeclaE()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame;
#else
        return Input.GetKeyDown(KeyCode.E);
#endif
    }

    private void ActualizarVisual()
    {
        if (indicadorVisual != null)
        {
            indicadorVisual.material.color = estaActivado ? colorEncendido : colorApagado;
        }

        if (pivotePalanca != null)
        {
            pivotePalanca.localRotation = estaActivado
                ? rotacionInicialPalanca * Quaternion.Euler(rotacionPalancaActiva)
                : rotacionInicialPalanca;
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, radioInteraccion);
    }
}
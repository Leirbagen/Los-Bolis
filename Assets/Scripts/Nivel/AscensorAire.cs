using System.Collections.Generic;
using System.Reflection;
using StarterAssets;
using UnityEngine;

[RequireComponent(typeof(BoxCollider))]
public class AscensorAire : MonoBehaviour
{
    [Header("1. Estado del Ascensor")]
    [Tooltip("Define si el chorro de aire empieza encendido o apagado al iniciar el nivel")]
    public bool estaEncendido = false;

    [Header("2. Física del Aire y Flotación")]
    [Tooltip("Velocidad a la que sube el personaje por la columna de aire")]
    public float velocidadSubida = 6.0f;
    [Tooltip("Velocidad extra de movimiento horizontal mientras flota en el aire")]
    public float velocidadHorizontalAire = 1.5f;
    [Tooltip("Distancia debajo del techo del collider donde el personaje queda flotando")]
    public float margenSuperior = 0.6f;
    [Tooltip("Qué tanto sube y baja suavemente al llegar a la cima")]
    public float amplitudFlote = 0.12f;
    public float frecuenciaFlote = 3.0f;

    [Header("3. Feedback Visual y Audio (Opcional)")]
    [Tooltip("Arrastra aquí partículas de viento o un cilindro semitransparente")]
    public GameObject efectoAire;
    [Tooltip("Opcional: sonido de viento/ventilador en bucle")]
    public AudioSource sonidoViento;

    private class DatosJugador
    {
        public CharacterController cc;
        public ThirdPersonController tpc;
        public StarterAssetsInputs inputs;
        public float gravedadOriginal;
        public bool flotando;
    }

    private BoxCollider colAire;
    private readonly Dictionary<GameObject, DatosJugador> jugadoresEnZona = new Dictionary<GameObject, DatosJugador>();
    private static FieldInfo campoVelocidadVertical;

    private void Awake()
    {
        colAire = GetComponent<BoxCollider>();
        colAire.isTrigger = true;

        // Obtenemos acceso interno a _verticalVelocity para cancelar caídas sin editar ThirdPersonController
        if (campoVelocidadVertical == null)
        {
            campoVelocidadVertical = typeof(ThirdPersonController).GetField(
                "_verticalVelocity",
                BindingFlags.Instance | BindingFlags.NonPublic
            );
        }
    }

    private void Start()
    {
        AplicarVisualYAudio();
    }

    private void Update()
    {
        if (jugadoresEnZona.Count == 0) return;

        float alturaTope = colAire.bounds.max.y - margenSuperior;
        List<GameObject> paraLimpiar = null;

        foreach (var par in jugadoresEnZona)
        {
            GameObject obj = par.Key;
            DatosJugador datos = par.Value;

            if (obj == null || !obj.activeInHierarchy || datos.cc == null)
            {
                if (paraLimpiar == null) paraLimpiar = new List<GameObject>();
                paraLimpiar.Add(obj);
                continue;
            }

            if (estaEncendido)
            {
                ActivarFlotacion(datos);
                ElevarJugador(obj.transform, datos, alturaTope);
            }
            else
            {
                RestaurarGravedad(datos);
            }
        }

        if (paraLimpiar != null)
        {
            foreach (GameObject obj in paraLimpiar) jugadoresEnZona.Remove(obj);
        }
    }

    private void ElevarJugador(Transform trJugador, DatosJugador datos, float alturaTope)
    {
        if (!datos.cc.enabled) return;

        // Reiniciamos la velocidad vertical acumulada para que la gravedad no pelee contra el aire
        if (datos.tpc != null && campoVelocidadVertical != null)
        {
            campoVelocidadVertical.SetValue(datos.tpc, 0f);
        }

        float diferenciaAltura = alturaTope - trJugador.position.y;
        float velocidadY;

        if (diferenciaAltura > 0.1f)
        {
            // Sube constante y desacelera suavemente el último metro antes de llegar arriba
            float factorFrenado = Mathf.Clamp01(diferenciaAltura / 1.0f);
            velocidadY = Mathf.Lerp(1.5f, velocidadSubida, factorFrenado);
        }
        else
        {
            // Mantiene al vikingo flotando en la cima para que pueda salir caminando con WASD
            float objetivoFlote = alturaTope + Mathf.Sin(Time.time * frecuenciaFlote) * amplitudFlote;
            velocidadY = (objetivoFlote - trJugador.position.y) * 5.0f;
        }

        // Empuje horizontal regulable relativo a la cámara (solo para el personaje activo)
        Vector3 movimientoHorizontal = Vector3.zero;
        bool esPersonajeActivo = (datos.tpc == null || datos.tpc.enabled);

        if (esPersonajeActivo && datos.inputs != null && datos.inputs.move.sqrMagnitude > 0.01f && Camera.main != null)
        {
            Transform cam = Camera.main.transform;
            Vector3 adelante = cam.forward;
            Vector3 derecha = cam.right;
            adelante.y = 0f;
            derecha.y = 0f;

            Vector3 direccion = (adelante.normalized * datos.inputs.move.y + derecha.normalized * datos.inputs.move.x).normalized;
            movimientoHorizontal = direccion * velocidadHorizontalAire;
        }

        Vector3 movimientoFinal = new Vector3(movimientoHorizontal.x, velocidadY, movimientoHorizontal.z);
        datos.cc.Move(movimientoFinal * Time.deltaTime);
    }

    private void ActivarFlotacion(DatosJugador datos)
    {
        if (datos.flotando) return;

        if (datos.tpc != null)
        {
            datos.gravedadOriginal = (Mathf.Abs(datos.tpc.Gravity) > 0.1f) ? datos.tpc.Gravity : -15.0f;
            datos.tpc.Gravity = 0f;
        }

        datos.flotando = true;
    }

    private void RestaurarGravedad(DatosJugador datos)
    {
        if (!datos.flotando) return;

        if (datos.tpc != null)
        {
            datos.tpc.Gravity = datos.gravedadOriginal;
        }

        datos.flotando = false;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        CharacterController cc = other.GetComponent<CharacterController>();
        if (cc == null) return; // Ignora sensores hijos como SensorCasco

        if (!jugadoresEnZona.ContainsKey(other.gameObject))
        {
            ThirdPersonController tpc = other.GetComponent<ThirdPersonController>();
            StarterAssetsInputs inputs = other.GetComponent<StarterAssetsInputs>();

            jugadoresEnZona.Add(other.gameObject, new DatosJugador
            {
                cc = cc,
                tpc = tpc,
                inputs = inputs,
                gravedadOriginal = (tpc != null && Mathf.Abs(tpc.Gravity) > 0.1f) ? tpc.Gravity : -15.0f,
                flotando = false
            });
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (jugadoresEnZona.TryGetValue(other.gameObject, out DatosJugador datos))
        {
            RestaurarGravedad(datos);
            jugadoresEnZona.Remove(other.gameObject);
        }
    }

    private void OnDisable()
    {
        foreach (var datos in jugadoresEnZona.Values)
        {
            RestaurarGravedad(datos);
        }
        jugadoresEnZona.Clear();
    }

    // --- FUNCIONES PÚBLICAS PARA CONECTAR CON LA PALANCA (SwitchInteractivo) ---

    public void ActivarAscensor()
    {
        estaEncendido = true;
        AplicarVisualYAudio();
    }

    public void DesactivarAscensor()
    {
        estaEncendido = false;
        AplicarVisualYAudio();
    }

    public void AlternarAscensor()
    {
        estaEncendido = !estaEncendido;
        AplicarVisualYAudio();
    }

    private void AplicarVisualYAudio()
    {
        if (efectoAire != null)
        {
            efectoAire.SetActive(estaEncendido);
        }

        if (sonidoViento != null)
        {
            if (estaEncendido && !sonidoViento.isPlaying) sonidoViento.Play();
            else if (!estaEncendido && sonidoViento.isPlaying) sonidoViento.Stop();
        }
    }

    private void OnDrawGizmos()
    {
        BoxCollider box = GetComponent<BoxCollider>();
        if (box == null) return;

        Gizmos.color = estaEncendido ? new Color(0f, 1f, 1f, 0.25f) : new Color(0.6f, 0.6f, 0.6f, 0.2f);
        Gizmos.DrawCube(box.bounds.center, box.bounds.size);

        // Línea celeste que marca la altura exacta donde flotarán los pies del personaje arriba
        float alturaTope = box.bounds.max.y - margenSuperior;
        Gizmos.color = Color.cyan;
        Vector3 centroTope = new Vector3(box.bounds.center.x, alturaTope, box.bounds.center.z);
        Gizmos.DrawWireCube(centroTope, new Vector3(box.bounds.size.x, 0.05f, box.bounds.size.z));
    }
}
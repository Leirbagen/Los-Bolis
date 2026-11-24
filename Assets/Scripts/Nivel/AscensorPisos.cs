using System.Collections.Generic;
using System.Reflection;
using StarterAssets;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[RequireComponent(typeof(Collider))]
public class AscensorPisos : MonoBehaviour
{
    [Header("1. Pisos del Ascensor (Ordenar de ABAJO hacia ARRIBA)")]
    [Tooltip("Element 0 = Piso más bajo, Element 1 = Segundo piso, Element 2 = Tercer piso...")]
    public List<Transform> pisos = new List<Transform>();
    [Tooltip("Índice del piso donde empieza el ascensor (0 es el primero de la lista)")]
    public int pisoInicial = 0;

    [Header("2. Movimiento y Comportamiento")]
    public float velocidad = 4.0f;
    [Tooltip("Si está marcado, permite invertir el sentido a mitad de camino (estilo The Lost Vikings)")]
    public bool permitirCambioEnMovimiento = true;

    [Header("3. Controles (Fáciles de cambiar luego con tu equipo)")]
#if ENABLE_INPUT_SYSTEM
    public Key teclaSubir = Key.Q;
    public Key teclaBajar = Key.LeftCtrl;
#else
    public KeyCode teclaSubir = KeyCode.Q;
    public KeyCode teclaBajar = KeyCode.LeftControl;
#endif

    [Header("4. Estado en Vivo (Solo lectura)")]
    public int pisoDestinoActual = 0;
    public bool enMovimiento = false;
    public bool jugadorActivoEncima = false;

    [Header("5. Detección de Pasajeros")]
    public LayerMask capasPasajeros = ~0;

    private Vector3[] posicionesPisos;
    private int direccionActual = 0; // 1 = subiendo, -1 = bajando, 0 = detenido
    private Collider colPlataforma;
    private readonly HashSet<CharacterController> pasajerosDetectados = new HashSet<CharacterController>();
    private static FieldInfo campoVelocidadVertical;

    private void Awake()
    {
        colPlataforma = GetComponent<Collider>();

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
        if (pisos == null || pisos.Count == 0)
        {
            Debug.LogWarning(gameObject.name + ": No hay pisos asignados en el Inspector.");
            posicionesPisos = new Vector3[] { transform.position };
            return;
        }

        // Guardamos las posiciones del mundo al iniciar para que no se muevan con la plataforma
        posicionesPisos = new Vector3[pisos.Count];
        for (int i = 0; i < pisos.Count; i++)
        {
            posicionesPisos[i] = (pisos[i] != null) ? pisos[i].position : transform.position;
        }

        pisoDestinoActual = Mathf.Clamp(pisoInicial, 0, posicionesPisos.Length - 1);
        transform.position = posicionesPisos[pisoDestinoActual];
        Physics.SyncTransforms();
    }

    private void Update()
    {
        if (posicionesPisos == null || posicionesPisos.Length < 2) return;

        // 1. Revisamos si el personaje actualmente controlado está sobre el ascensor
        jugadorActivoEncima = DetectarPasajerosYVerificarControl(0.35f);

        // 2. Leer controles Q (Subir) y Ctrl (Bajar)
        if (jugadorActivoEncima)
        {
            if (PresionoSubir())
            {
                SubirUnPiso();
            }
            else if (PresionoBajar())
            {
                BajarUnPiso();
            }
        }

        // 3. Mover el ascensor hacia el piso destino
        if (!enMovimiento) return;

        Vector3 destino = posicionesPisos[pisoDestinoActual];
        Vector3 nuevaPos = Vector3.MoveTowards(transform.position, destino, velocidad * Time.deltaTime);
        Vector3 desplazamiento = nuevaPos - transform.position;

        if (desplazamiento.sqrMagnitude > 0.0000001f)
        {
            MoverConPasajeros(nuevaPos, desplazamiento);
        }

        // 4. Detener exactamente al llegar al piso
        if (Vector3.Distance(transform.position, destino) <= 0.005f)
        {
            Vector3 ajusteFinal = destino - transform.position;
            if (ajusteFinal.sqrMagnitude > 0.0000001f)
            {
                MoverConPasajeros(destino, ajusteFinal);
            }
            enMovimiento = false;
            direccionActual = 0;
        }
    }

    private void MoverConPasajeros(Vector3 nuevaPos, Vector3 desplazamiento)
    {
        // Ampliamos un poco el margen superior al subir para que Ágil no atraviese el suelo al aterrizar de un salto
        float alturaDeteccion = desplazamiento.y > 0f ? (0.35f + desplazamiento.y) : 0.35f;
        DetectarPasajerosYVerificarControl(alturaDeteccion);

        if (desplazamiento.y > 0f)
        {
            // Al SUBIR: primero elevamos a los pasajeros y luego la plataforma
            foreach (CharacterController cc in pasajerosDetectados)
            {
                cc.Move(desplazamiento);
            }
            transform.position = nuevaPos;
            Physics.SyncTransforms();
        }
        else
        {
            // Al BAJAR: primero baja la plataforma, y solo arrastramos hacia abajo a quienes NO están saltando
            transform.position = nuevaPos;
            Physics.SyncTransforms();

            foreach (CharacterController cc in pasajerosDetectados)
            {
                if (!EstaSaltandoHaciaArriba(cc))
                {
                    cc.Move(desplazamiento);
                }
            }
        }
    }

    private bool DetectarPasajerosYVerificarControl(float alturaCaja)
    {
        pasajerosDetectados.Clear();
        bool hayControlable = false;

        Bounds b = colPlataforma.bounds;
        Vector3 centro = new Vector3(b.center.x, b.max.y + (alturaCaja * 0.5f), b.center.z);
        Vector3 mitadTamano = new Vector3(b.extents.x * 0.95f, alturaCaja * 0.5f, b.extents.z * 0.95f);

        Collider[] encontrados = Physics.OverlapBox(centro, mitadTamano, Quaternion.identity, capasPasajeros, QueryTriggerInteraction.Ignore);

        foreach (Collider col in encontrados)
        {
            CharacterController cc = col.GetComponent<CharacterController>();
            if (cc == null || !cc.enabled) continue;

            pasajerosDetectados.Add(cc);

            if (col.CompareTag("Player"))
            {
                ThirdPersonController tpc = col.GetComponent<ThirdPersonController>();
                if (tpc != null && tpc.enabled && tpc.canMove)
                {
                    hayControlable = true;
                }
            }
        }

        return hayControlable;
    }

    private bool EstaSaltandoHaciaArriba(CharacterController cc)
    {
        ThirdPersonController tpc = cc.GetComponent<ThirdPersonController>();
        if (tpc != null && campoVelocidadVertical != null)
        {
            float velY = (float)campoVelocidadVertical.GetValue(tpc);
            // Si tiene impulso de salto positivo, no lo jalamos hacia abajo con el ascensor
            return velY > 0.2f;
        }
        return false;
    }

    // --- FUNCIONES DE MOVIMIENTO (También se pueden conectar a un SwitchInteractivo) ---

    public void SubirUnPiso()
    {
        if (posicionesPisos == null || posicionesPisos.Length < 2) return;

        if (!enMovimiento)
        {
            if (pisoDestinoActual < posicionesPisos.Length - 1)
            {
                pisoDestinoActual++;
                direccionActual = 1;
                enMovimiento = true;
            }
        }
        else if (permitirCambioEnMovimiento && direccionActual == -1)
        {
            // Iba bajando: invierte la marcha y vuelve al piso anterior sin saltarse pisos
            if (pisoDestinoActual < posicionesPisos.Length - 1)
            {
                pisoDestinoActual++;
                direccionActual = 1;
            }
        }
    }

    public void BajarUnPiso()
    {
        if (posicionesPisos == null || posicionesPisos.Length < 2) return;

        if (!enMovimiento)
        {
            if (pisoDestinoActual > 0)
            {
                pisoDestinoActual--;
                direccionActual = -1;
                enMovimiento = true;
            }
        }
        else if (permitirCambioEnMovimiento && direccionActual == 1)
        {
            // Iba subiendo: invierte la marcha y regresa al piso del que salió
            if (pisoDestinoActual > 0)
            {
                pisoDestinoActual--;
                direccionActual = -1;
            }
        }
    }

    public void IrAPiso(int indice)
    {
        if (posicionesPisos == null || posicionesPisos.Length == 0) return;

        int destinoSeguro = Mathf.Clamp(indice, 0, posicionesPisos.Length - 1);
        if (destinoSeguro == pisoDestinoActual && !enMovimiento) return;

        direccionActual = (posicionesPisos[destinoSeguro].y >= transform.position.y) ? 1 : -1;
        pisoDestinoActual = destinoSeguro;
        enMovimiento = true;
    }

    private bool PresionoSubir()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null && teclaSubir != Key.None && Keyboard.current[teclaSubir].wasPressedThisFrame;
#else
        return Input.GetKeyDown(teclaSubir);
#endif
    }

    private bool PresionoBajar()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null && teclaBajar != Key.None && Keyboard.current[teclaBajar].wasPressedThisFrame;
#else
        return Input.GetKeyDown(teclaBajar);
#endif
    }

    private void OnDrawGizmos()
    {
        if (pisos == null || pisos.Count == 0) return;

        for (int i = 0; i < pisos.Count; i++)
        {
            if (pisos[i] == null) continue;

            Gizmos.color = (i == pisoInicial) ? Color.green : Color.yellow;
            Gizmos.DrawWireSphere(pisos[i].position, 0.3f);

            if (i + 1 < pisos.Count && pisos[i + 1] != null)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawLine(pisos[i].position, pisos[i + 1].position);
            }
        }
    }
}
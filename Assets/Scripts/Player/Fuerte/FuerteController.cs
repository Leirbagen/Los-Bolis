using UnityEngine;
using StarterAssets;
using System.Reflection;
using Unity.Cinemachine;

public class FuerteController : MonoBehaviour
{
    [Header("Referencias de la Plantilla")]
    [SerializeField] private ThirdPersonController controladorTerceraPersona;
    [SerializeField] private StarterAssetsInputs entradasPlantilla;

    [Header("Configuración de Escudos")]
    [SerializeField] private Collider colliderEscudoFrontal;
    [SerializeField] private Collider colliderPlataformaSuperior;

    [Header("Ajustes Físicos de Fuerte")]
    [SerializeField] private float velocidadBase = 2.0f;
    [SerializeField] private float gravedadNormal = 15.0f;
    [SerializeField] private float gravedadPlaneando = 2.0f;
    [SerializeField] private float velocidadCaidaMaximaPlaneando = -2.0f;

    [Header("Z-Targeting (Apuntado Zelda)")]
    [SerializeField] private LayerMask capaEnemigos;
    [SerializeField] private float radioDeteccion = 15f;

    [Range(10f, 180f)]
    [SerializeField] private float anguloVisionFijacion = 90f;

    [Tooltip("Tiempo en segundos que tarda en girar. (Ej: 0.15)")]
    [SerializeField] private float suavizadoGiro = 0.15f;

    [SerializeField] private CinemachineVirtualCamera camaraFijacion;

    [Header("Dinámica de Cámara")]
    [SerializeField] private float velocidadCambioHombro = 3f;
    [SerializeField] private float hombroIzquierdo = 0f;
    [SerializeField] private float hombroDerecho = 1f;

    private Transform objetivoActual;
    private bool estaBloqueandoArriba = false;
    private Cinemachine3rdPersonFollow componenteTerceraPersona;

    // --- VARIABLES DE INERCIA
    private float velocidadRotacionYaw;
    private float velocidadRotacionPitch;
    private float velocidadCuerpoYaw; 

    private FieldInfo campoVelocidadCaida;
    private FieldInfo campoTargetYaw;
    private FieldInfo campoTargetPitch;
    private FieldInfo campoRotationVelocity;

    private void Start()
    {
        if (controladorTerceraPersona == null) controladorTerceraPersona = GetComponent<ThirdPersonController>();
        if (entradasPlantilla == null) entradasPlantilla = GetComponent<StarterAssetsInputs>();

        var flags = BindingFlags.NonPublic | BindingFlags.Instance;
        campoVelocidadCaida = typeof(ThirdPersonController).GetField("_verticalVelocity", flags);
        campoTargetYaw = typeof(ThirdPersonController).GetField("_cinemachineTargetYaw", flags);
        campoTargetPitch = typeof(ThirdPersonController).GetField("_cinemachineTargetPitch", flags);
        campoRotationVelocity = typeof(ThirdPersonController).GetField("_rotationVelocity", flags);

        controladorTerceraPersona.SprintSpeed = velocidadBase;
        controladorTerceraPersona.MoveSpeed = velocidadBase;

        if (camaraFijacion != null)
        {
            camaraFijacion.Priority = 9;
            camaraFijacion.LookAt = null;
            componenteTerceraPersona = camaraFijacion.GetCinemachineComponent<Cinemachine3rdPersonFollow>();
        }

        ActualizarPosicionEscudo();
    }

    private void Update()
    {
        if (entradasPlantilla.jump) entradasPlantilla.jump = false;

        if (Input.GetKeyDown(KeyCode.R))
        {
            estaBloqueandoArriba = !estaBloqueandoArriba;
            ActualizarPosicionEscudo();
        }

        if (Input.GetKeyDown(KeyCode.Z) || Input.GetKeyDown(KeyCode.Tab))
        {
            AlternarFijacion();
        }

        if (objetivoActual != null)
        {
            entradasPlantilla.look = Vector2.zero;
            ActualizarHombroCamara();
        }

        GestionarPlaneo();
    }

    private void LateUpdate()
    {
        if (objetivoActual != null)
        {
            Vector3 direccion = objetivoActual.position - transform.position;
            float anguloDestino = Mathf.Atan2(direccion.x, direccion.z) * Mathf.Rad2Deg;

            // 1. OBTENER VALORES ACTUALES
            float yawActual = 0f;
            float pitchActual = 0f;
            float yawCuerpoActual = transform.eulerAngles.y; // Dónde mira el cuerpo AHORA

            if (campoTargetYaw != null) yawActual = (float)campoTargetYaw.GetValue(controladorTerceraPersona);
            if (campoTargetPitch != null) pitchActual = (float)campoTargetPitch.GetValue(controladorTerceraPersona);

            // 2. SUAVIZAR TODO POR SEPARADO (Cámara vs Cuerpo)
            float yawSuavizado = Mathf.SmoothDampAngle(yawActual, anguloDestino, ref velocidadRotacionYaw, suavizadoGiro);
            float pitchSuavizado = Mathf.SmoothDampAngle(pitchActual, 15f, ref velocidadRotacionPitch, suavizadoGiro);
            float yawCuerpoSuavizado = Mathf.SmoothDampAngle(yawCuerpoActual, anguloDestino, ref velocidadCuerpoYaw, suavizadoGiro);

            // 3. APLICAR VALORES SUAVES
            if (campoTargetYaw != null) campoTargetYaw.SetValue(controladorTerceraPersona, yawSuavizado);
            if (campoTargetPitch != null) campoTargetPitch.SetValue(controladorTerceraPersona, pitchSuavizado);

            transform.rotation = Quaternion.Euler(0, yawCuerpoSuavizado, 0); // El cuerpo gira fluido, sin tirones

            // 4. MATAR LA INERCIA DEL MOUSE
            if (campoRotationVelocity != null) campoRotationVelocity.SetValue(controladorTerceraPersona, 0f);
        }
    }

    private void ActualizarHombroCamara()
    {
        if (componenteTerceraPersona == null) return;

        float movimientoLateral = entradasPlantilla.move.x;
        float ladoDestino;

        if (movimientoLateral > 0.1f) ladoDestino = hombroIzquierdo;
        else if (movimientoLateral < -0.1f) ladoDestino = hombroDerecho;
        else ladoDestino = componenteTerceraPersona.CameraSide;

        componenteTerceraPersona.CameraSide = Mathf.Lerp(
            componenteTerceraPersona.CameraSide,
            ladoDestino,
            Time.deltaTime * velocidadCambioHombro
        );
    }

    private void AlternarFijacion()
    {
        if (objetivoActual != null)
        {
            objetivoActual = null;
            if (camaraFijacion != null)
            {
                camaraFijacion.Priority = 9;
            }
        }
        else
        {
            Collider[] enemigosCercanos = Physics.OverlapSphere(transform.position, radioDeteccion, capaEnemigos);

            if (enemigosCercanos.Length > 0)
            {
                Transform enemigoMasValido = null;
                float distanciaMinima = Mathf.Infinity;

                foreach (Collider enemigo in enemigosCercanos)
                {
                    Vector3 direccionAlEnemigo = (enemigo.transform.position - transform.position).normalized;
                    float anguloAlEnemigo = Vector3.Angle(transform.forward, direccionAlEnemigo);

                    if (anguloAlEnemigo <= anguloVisionFijacion)
                    {
                        float distancia = Vector3.Distance(transform.position, enemigo.transform.position);
                        if (distancia < distanciaMinima)
                        {
                            distanciaMinima = distancia;
                            enemigoMasValido = enemigo.transform;
                        }
                    }
                }

                if (enemigoMasValido != null)
                {
                    objetivoActual = enemigoMasValido;

                    if (componenteTerceraPersona != null) componenteTerceraPersona.CameraSide = 0.5f;

                    if (camaraFijacion != null)
                    {
                        camaraFijacion.Priority = 11;
                    }
                }
            }
        }
    }

    private void ActualizarPosicionEscudo()
    {
        if (estaBloqueandoArriba)
        {
            colliderEscudoFrontal.gameObject.SetActive(false);
            colliderPlataformaSuperior.gameObject.SetActive(true);
        }
        else
        {
            colliderEscudoFrontal.gameObject.SetActive(true);
            colliderPlataformaSuperior.gameObject.SetActive(false);
        }
    }

    private void GestionarPlaneo()
    {
        bool estaEnElAire = !controladorTerceraPersona.Grounded;

        if (estaBloqueandoArriba && estaEnElAire)
        {
            controladorTerceraPersona.Gravity = gravedadPlaneando;

            if (campoVelocidadCaida != null)
            {
                float inerciaActual = (float)campoVelocidadCaida.GetValue(controladorTerceraPersona);
                if (inerciaActual < velocidadCaidaMaximaPlaneando)
                {
                    campoVelocidadCaida.SetValue(controladorTerceraPersona, velocidadCaidaMaximaPlaneando);
                }
            }
        }
        else
        {
            controladorTerceraPersona.Gravity = gravedadNormal;
        }
    }
}
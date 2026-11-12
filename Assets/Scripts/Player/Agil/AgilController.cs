using StarterAssets;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[RequireComponent(typeof(ThirdPersonController))]
public class AgilController : MonoBehaviour
{
    public enum EstadoAgil
    {
        En_Reposo,
        Corriendo,
        Saltando,
        Embistiendo,
        Aturdido,
        Muerto
    }

    [Header("1. Estado del Personaje")]
    public int Salud_Actual = 3;
    public bool Es_Activo = true;
    public EstadoAgil Estado_Actual = EstadoAgil.En_Reposo;
    public bool En_Suelo = true;
    public float Tiempo_Aturdido = 0.0f;

    [Header("2. Control Pesado en Embestida (Cámara y Giro)")]
    [Tooltip("Grados máximos que puede girar por segundo al embestir (menor = más difícil esquivar)")]
    public float gradosGiroPorSegundo = 25.0f;
    [Tooltip("Multiplica la velocidad de la cámara al embestir (0.15 = cámara 85% más lenta)")]
    [Range(0.02f, 0.5f)]
    public float sensibilidadCamaraEmbestida = 0.12f;
    [Tooltip("Límite máximo de giro de cámara por fotograma durante la embestida")]
    public float limiteVelocidadCamara = 0.35f;
    [Tooltip("Altura de salto durante la embestida")]
    public float saltoEnEmbestida = 2.5f;

    [Header("3. Choques, Retroceso y Penalizaciones")]
    public float penalizacionMuroSolido = 5.0f;
    public float penalizacionMuroRompible = 3.0f;
    [Tooltip("Fuerza del empuje hacia atrás al chocar")]
    public float fuerzaRebote = 8.0f;
    [Tooltip("Duración del empujón hacia atrás en segundos")]
    public float duracionRebote = 0.35f;
    public SensorCascoAgil sensorCasco;

    private ThirdPersonController controladorBase;
    private CharacterController characterController;
    private StarterAssetsInputs inputs;
    private Animator animator;
    private Transform camaraPrincipal;

    private float giroNormalOriginal;
    private float saltoNormalOriginal;
    private float timerRebote = 0f;
    private Vector3 direccionRebote;
    private bool embestidaActiva = false;
    private float anguloCarreraActual = 0f;

    private void Start()
    {
        controladorBase = GetComponent<ThirdPersonController>();
        characterController = GetComponent<CharacterController>();
        inputs = GetComponent<StarterAssetsInputs>();
        animator = GetComponent<Animator>();
        if (Camera.main != null) camaraPrincipal = Camera.main.transform;

        giroNormalOriginal = controladorBase.RotationSmoothTime;
        saltoNormalOriginal = controladorBase.JumpHeight;

        if (sensorCasco == null) sensorCasco = GetComponentInChildren<SensorCascoAgil>();
        if (sensorCasco != null)
        {
            sensorCasco.Inicializar(this);
            sensorCasco.ActivarSensor(false);
        }
    }

    private void Update()
    {
        if (Estado_Actual == EstadoAgil.Muerto) return;

        // Estado Aturdido: aplica el retroceso físico y congela por completo al personaje y la cámara
        if (Estado_Actual == EstadoAgil.Aturdido)
        {
            inputs.move = Vector2.zero;
            inputs.look = Vector2.zero;
            inputs.jump = false;
            inputs.sprint = false;

            // Aplicamos el retroceso hacia atrás (desacelerando suave) + gravedad para no flotar
            Vector3 movimientoRebote = Vector3.zero;
            if (timerRebote > 0f)
            {
                timerRebote -= Time.deltaTime;
                float proporcion = Mathf.Clamp01(timerRebote / duracionRebote);
                movimientoRebote = direccionRebote * proporcion;
            }
            movimientoRebote.y = -15.0f; // Gravedad constante hacia el suelo
            characterController.Move(movimientoRebote * Time.deltaTime);

            Tiempo_Aturdido -= Time.deltaTime;
            if (Tiempo_Aturdido <= 0f)
            {
                Tiempo_Aturdido = 0f;
                Estado_Actual = EstadoAgil.En_Reposo;
                if (Es_Activo) DesbloquearPersonajeYCamara();
            }
            return;
        }

        En_Suelo = controladorBase.Grounded;

        if (!Es_Activo) return;

        // Activar / Desactivar embestida con un toque de Shift
        if (PresionoToggleEmbestida())
        {
            if (embestidaActiva) DetenerEmbestida();
            else if (En_Suelo) IniciarEmbestida();
        }

        inputs.sprint = embestidaActiva;

        if (embestidaActiva)
        {
            Estado_Actual = En_Suelo ? EstadoAgil.Embistiendo : EstadoAgil.Saltando;
            ProcesarGiroPesadoEmbestida();
            return;
        }

        if (!En_Suelo)
        {
            Estado_Actual = EstadoAgil.Saltando;
            return;
        }

        Estado_Actual = (inputs.move != Vector2.zero) ? EstadoAgil.Corriendo : EstadoAgil.En_Reposo;
    }

    private void ProcesarGiroPesadoEmbestida()
    {
        // 1. Frenar drásticamente la velocidad de giro de la cámara
        inputs.look = Vector2.ClampMagnitude(inputs.look * sensibilidadCamaraEmbestida, limiteVelocidadCamara);

        if (camaraPrincipal == null && Camera.main != null) camaraPrincipal = Camera.main.transform;
        float anguloCamara = (camaraPrincipal != null) ? camaraPrincipal.eulerAngles.y : transform.eulerAngles.y;

        // 2. Calcular hacia dónde intenta doblar el jugador con A/D o con la cámara
        float intentoGiroTeclado = inputs.move.x * 45.0f;
        float anguloObjetivo = anguloCamara + intentoGiroTeclado;

        // 3. Limitar el giro real del personaje a unos pocos grados por segundo (difícil esquivar)
        anguloCarreraActual = Mathf.MoveTowardsAngle(anguloCarreraActual, anguloObjetivo, gradosGiroPorSegundo * Time.deltaTime);
        transform.rotation = Quaternion.Euler(0f, anguloCarreraActual, 0f);

        // 4. Convertir el ángulo bloqueado al vector que lee ThirdPersonController
        float rad = (anguloCarreraActual - anguloCamara) * Mathf.Deg2Rad;
        inputs.move = new Vector2(Mathf.Sin(rad), Mathf.Cos(rad));
    }

    private bool PresionoToggleEmbestida()
    {
        bool toque = false;
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null && Keyboard.current.leftShiftKey.wasPressedThisFrame) toque = true;
#else
        if (Input.GetKeyDown(KeyCode.LeftShift)) toque = true;
#endif
        if (inputs.attack)
        {
            inputs.attack = false;
            toque = true;
        }
        return toque;
    }

    private void IniciarEmbestida()
    {
        embestidaActiva = true;
        anguloCarreraActual = transform.eulerAngles.y;

        // Subimos el tiempo de suavizado para que no rote brusco
        controladorBase.RotationSmoothTime = 0.8f;
        if (saltoEnEmbestida > 0f) controladorBase.JumpHeight = saltoEnEmbestida;

        if (sensorCasco != null) sensorCasco.ActivarSensor(true);
        if (animator != null) animator.SetBool("Embistiendo", true);
    }

    private void DetenerEmbestida()
    {
        embestidaActiva = false;
        inputs.sprint = false;
        inputs.attack = false;

        controladorBase.RotationSmoothTime = giroNormalOriginal;
        controladorBase.JumpHeight = saltoNormalOriginal;

        if (sensorCasco != null) sensorCasco.ActivarSensor(false);
        if (animator != null) animator.SetBool("Embistiendo", false);
    }

    public void EvaluarImpactoEmbestida(Collider otro, Vector3 puntoImpacto, Vector3 normalImpacto)
    {
        if (!embestidaActiva || otro.transform.root == transform.root) return;

        // 1. Romper bloque ("Rompible") o golpear Enemigo ("Enemigo") -> 3s congelado + retroceso
        if (otro.CompareTag("Rompible") || otro.CompareTag("Enemigo"))
        {
            if (otro.CompareTag("Rompible")) Destroy(otro.gameObject);
            AplicarPenalizacionAturdido(penalizacionMuroRompible, fuerzaRebote * 0.85f);
            return;
        }

        // 2. Chocar contra pared sólida (< 45 grados) -> 5s congelado + retroceso fuerte
        if (otro.CompareTag("Muro_Solido") || !otro.isTrigger)
        {
            // Lanzamos un rayo corto al frente para obtener el ángulo exacto de la pared
            if (Physics.Raycast(transform.position + Vector3.up * 0.8f, transform.forward, out RaycastHit hitInfo, 1.5f))
            {
                if (hitInfo.collider == otro) normalImpacto = hitInfo.normal;
            }

            Vector3 normalPlana = new Vector3(normalImpacto.x, 0f, normalImpacto.z).normalized;
            if (Mathf.Abs(normalImpacto.y) > 0.5f || normalPlana == Vector3.zero) return;

            if (Vector3.Angle(transform.forward, -normalPlana) < 45f)
            {
                AplicarPenalizacionAturdido(penalizacionMuroSolido, fuerzaRebote);
            }
        }
    }

    private void AplicarPenalizacionAturdido(float segundos, float rebote)
    {
        // Guardamos la dirección hacia atrás antes de detener la embestida
        direccionRebote = -transform.forward * rebote;
        timerRebote = duracionRebote;
        Tiempo_Aturdido = segundos;

        DetenerEmbestida();

        // Apagamos temporalmente el controlador base para que no anule el retroceso ni mueva la cámara
        controladorBase.canMove = false;
        controladorBase.canJump = false;
        controladorBase.LockCameraPosition = true;
        controladorBase.enabled = false;

        if (animator != null)
        {
            animator.SetFloat("Speed", 0f);
            animator.SetFloat("MotionSpeed", 0f);
            animator.SetTrigger("Aturdido");
        }

        Estado_Actual = EstadoAgil.Aturdido;
    }

    private void DesbloquearPersonajeYCamara()
    {
        controladorBase.enabled = true;
        controladorBase.canMove = true;
        controladorBase.canJump = true;
        controladorBase.LockCameraPosition = false;
    }

    private void OnControllerColliderHit(ControllerColliderHit hit)
    {
        if (embestidaActiva && hit.moveDirection.y >= -0.3f)
        {
            EvaluarImpactoEmbestida(hit.collider, hit.point, hit.normal);
        }
    }

    public void RecibirDaño(int daño = 1)
    {
        if (Estado_Actual == EstadoAgil.Muerto) return;
        Salud_Actual = Mathf.Max(0, Salud_Actual - daño);

        if (Salud_Actual == 0)
        {
            DetenerEmbestida();
            controladorBase.canMove = false;
            controladorBase.canJump = false;
            controladorBase.LockCameraPosition = true;
            controladorBase.enabled = false;
            Estado_Actual = EstadoAgil.Muerto;
            if (animator != null) animator.SetTrigger("Muerto");
        }
    }

    public void SetPersonajeActivo(bool activo)
    {
        Es_Activo = activo;
        if (!Es_Activo)
        {
            DetenerEmbestida();
            inputs.move = Vector2.zero;
            inputs.look = Vector2.zero;
            inputs.jump = false;
            controladorBase.canMove = false;
            controladorBase.canJump = false;
            controladorBase.LockCameraPosition = true;

            if (Estado_Actual != EstadoAgil.Aturdido && Estado_Actual != EstadoAgil.Muerto)
                Estado_Actual = EstadoAgil.En_Reposo;
        }
        else if (Estado_Actual != EstadoAgil.Aturdido && Estado_Actual != EstadoAgil.Muerto)
        {
            DesbloquearPersonajeYCamara();
        }
    }
}
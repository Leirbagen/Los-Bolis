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
    public bool Sobre_Escudo_Olaf = false;
    public float Tiempo_Embestida_Restante = 0.0f;
    public float Tiempo_Aturdido = 0.0f;

    [Header("2. Duración, Frenado y Control en Embestida")]
    public float duracionMaximaEmbestida = 2.5f;
    public float duracionDerrapeFrenado = 0.35f;
    public float gradosGiroPorSegundo = 25.0f;
    [Range(0.02f, 0.5f)]
    public float sensibilidadCamaraEmbestida = 0.12f;
    public float limiteVelocidadCamara = 0.35f;
    public float saltoEnEmbestida = 2.5f;

    [Header("3. Choques, Retroceso y Penalizaciones")]
    public float penalizacionMuroSolido = 5.0f;
    public float penalizacionMuroRompible = 3.0f;
    public float fuerzaRebote = 8.0f;
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
    private float timerDerrape = 0f;
    private Vector3 velocidadDerrape;
    private bool embestidaActiva = false;
    private float anguloCarreraActual = 0f;

    // Variables para heredar el movimiento del escudo (Regla 4)
    private Transform escudoActual;
    private Vector3 ultimaPosicionEscudo;

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

        En_Suelo = controladorBase.Grounded;

        // Regla 4: Mantener a Ágil sobre el escudo aunque esté en reposo o hayas cambiado de vikingo
        ActualizarPlataformaEscudo();

        if (Estado_Actual == EstadoAgil.Aturdido)
        {
            inputs.move = Vector2.zero;
            inputs.look = Vector2.zero;
            inputs.jump = false;
            inputs.sprint = false;

            Vector3 movimientoRebote = Vector3.zero;
            if (timerRebote > 0f)
            {
                timerRebote -= Time.deltaTime;
                float proporcion = Mathf.Clamp01(timerRebote / duracionRebote);
                movimientoRebote = direccionRebote * proporcion;
            }
            movimientoRebote.y = -15.0f;
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

        if (!Es_Activo) return;

        if (timerDerrape > 0f)
        {
            timerDerrape -= Time.deltaTime;
            if (LeerMovimientoReal() != Vector2.zero)
            {
                timerDerrape = 0f;
            }
            else
            {
                float factorFrenado = Mathf.Clamp01(timerDerrape / Mathf.Max(0.01f, duracionDerrapeFrenado));
                characterController.Move(velocidadDerrape * factorFrenado * Time.deltaTime);
            }
        }

        if (PresionoTeclaShift())
        {
            if (embestidaActiva) DetenerEmbestida(true);
            else if (En_Suelo) IniciarEmbestida();
        }

        if (embestidaActiva)
        {
            Tiempo_Embestida_Restante -= Time.deltaTime;

            if (Tiempo_Embestida_Restante <= 0f && En_Suelo)
            {
                Tiempo_Embestida_Restante = 0f;
                DetenerEmbestida(true);
            }
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

    // --- REGLA 4: PLATAFORMA COOPERATIVA SOBRE EL ESCUDO ---
    private void ActualizarPlataformaEscudo()
    {
        Transform escudoDetectado = null;

        if (En_Suelo)
        {
            // Revisamos con una esfera en los pies si el suelo que pisa tiene el Tag "Escudo_Olaf"
            Vector3 posicionPies = new Vector3(transform.position.x, transform.position.y - controladorBase.GroundedOffset, transform.position.z);
            Collider[] pisando = Physics.OverlapSphere(posicionPies, controladorBase.GroundedRadius + 0.05f, controladorBase.GroundLayers, QueryTriggerInteraction.Ignore);

            foreach (Collider col in pisando)
            {
                if (col.CompareTag("Escudo_Fuerte"))
                {
                    escudoDetectado = col.transform;
                    break;
                }
            }
        }

        if (escudoDetectado != null)
        {
            if (escudoActual == escudoDetectado)
            {
                // Movemos a Ágil exactamente lo mismo que se haya movido el escudo en este frame
                Vector3 desplazamiento = escudoActual.position - ultimaPosicionEscudo;
                if (desplazamiento.sqrMagnitude > 0.000001f)
                {
                    characterController.Move(desplazamiento);
                }
            }
            escudoActual = escudoDetectado;
            ultimaPosicionEscudo = escudoActual.position;
            Sobre_Escudo_Olaf = true;
        }
        else
        {
            escudoActual = null;
            Sobre_Escudo_Olaf = false;
        }
    }

    private void ProcesarGiroPesadoEmbestida()
    {
        inputs.look = Vector2.ClampMagnitude(inputs.look * sensibilidadCamaraEmbestida, limiteVelocidadCamara);

        if (camaraPrincipal == null && Camera.main != null) camaraPrincipal = Camera.main.transform;
        float anguloCamara = (camaraPrincipal != null) ? camaraPrincipal.eulerAngles.y : transform.eulerAngles.y;

        Vector2 tecladoReal = LeerMovimientoReal();
        float intentoGiroTeclado = tecladoReal.x * 45.0f;
        float anguloObjetivo = anguloCamara + intentoGiroTeclado;

        anguloCarreraActual = Mathf.MoveTowardsAngle(anguloCarreraActual, anguloObjetivo, gradosGiroPorSegundo * Time.deltaTime);
        transform.rotation = Quaternion.Euler(0f, anguloCarreraActual, 0f);

        float rad = (anguloCarreraActual - anguloCamara) * Mathf.Deg2Rad;
        inputs.move = new Vector2(Mathf.Sin(rad), Mathf.Cos(rad));
    }

    private Vector2 LeerMovimientoReal()
    {
        Vector2 dir = Vector2.zero;
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null)
        {
            if (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed) dir.y += 1f;
            if (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed) dir.y -= 1f;
            if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed) dir.x += 1f;
            if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed) dir.x -= 1f;
        }
        if (dir == Vector2.zero && Gamepad.current != null)
        {
            dir = Gamepad.current.leftStick.ReadValue();
        }
#else
        dir = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
#endif
        return dir.normalized;
    }

    private bool PresionoTeclaShift()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null && Keyboard.current.leftShiftKey.wasPressedThisFrame;
#else
        return Input.GetKeyDown(KeyCode.LeftShift);
#endif
    }

    private void IniciarEmbestida()
    {
        embestidaActiva = true;
        timerDerrape = 0f;
        Tiempo_Embestida_Restante = duracionMaximaEmbestida;
        anguloCarreraActual = transform.eulerAngles.y;

        controladorBase.RotationSmoothTime = 0.8f;
        if (saltoEnEmbestida > 0f) controladorBase.JumpHeight = saltoEnEmbestida;

        if (sensorCasco != null) sensorCasco.ActivarSensor(true);
        if (animator != null) animator.SetBool("Embistiendo", true);
    }

    private void DetenerEmbestida(bool aplicarDerrape = false)
    {
        embestidaActiva = false;
        Tiempo_Embestida_Restante = 0f;
        inputs.sprint = false;

        Vector2 movimientoActual = LeerMovimientoReal();
        inputs.move = movimientoActual;

        if (aplicarDerrape && duracionDerrapeFrenado > 0f && movimientoActual == Vector2.zero)
        {
            timerDerrape = duracionDerrapeFrenado;
            velocidadDerrape = transform.forward * (controladorBase.SprintSpeed * 0.6f);
        }

        controladorBase.RotationSmoothTime = giroNormalOriginal;
        controladorBase.JumpHeight = saltoNormalOriginal;

        if (sensorCasco != null) sensorCasco.ActivarSensor(false);
        if (animator != null) animator.SetBool("Embistiendo", false);
    }

    public void EvaluarImpactoEmbestida(Collider otro, Vector3 puntoImpacto, Vector3 normalImpacto)
    {
        if (!embestidaActiva || otro.transform.root == transform.root) return;

        // Ignorar el escudo si lo pisa desde arriba
        if (otro.CompareTag("Escudo_Olaf") && normalImpacto.y > 0.5f) return;

        if (otro.CompareTag("Rompible") || otro.CompareTag("Enemigo"))
        {
            if (otro.CompareTag("Rompible")) Destroy(otro.gameObject);
            AplicarPenalizacionAturdido(penalizacionMuroRompible, fuerzaRebote * 0.85f);
            return;
        }

        if (otro.CompareTag("Muro_Solido") || !otro.isTrigger)
        {
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
        direccionRebote = -transform.forward * rebote;
        timerRebote = duracionRebote;
        timerDerrape = 0f;
        Tiempo_Aturdido = segundos;

        DetenerEmbestida(false);

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
        inputs.move = LeerMovimientoReal();
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
            DetenerEmbestida(false);
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
            DetenerEmbestida(false);
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
using StarterAssets;
using UnityEngine;

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

    [Header("1. Variables del Personaje (Tabla PDF)")]
    public int Salud_Actual = 3;
    public bool Es_Activo = true;
    public EstadoAgil Estado_Actual = EstadoAgil.En_Reposo;
    public bool En_Suelo = true;
    public float Vel_Giro_Actual = 360f;
    public float Tiempo_Aturdido = 0.0f;

    [Header("2. Habilidad Principal: Embestida y Choques")]
    [Tooltip("Velocidad normal al moverse con WASD")]
    public float velocidadBase = 4.0f;
    [Tooltip("Velocidad turbo durante la embestida")]
    public float velocidadEmbestida = 8.5f;
    [Tooltip("Qué tanto le cuesta girar al embestir (mayor valor = giro más duro)")]
    public float giroDuroEmbestida = 1.0f;
    public float duracionAturdido = 1.5f;
    public float fuerzaRebote = 6.0f;
    public SensorCascoAgil sensorCasco;

    // Referencias internas (mismo estilo que RudoController)
    private ThirdPersonController controladorBase;
    private CharacterController characterController;
    private StarterAssetsInputs inputs;
    private Animator animator;

    private float giroNormalBase;
    private float timerRebote = 0f;
    private Vector3 direccionRebote;
    private bool enSaltoLargo = false;

    private void Start()
    {
        controladorBase = GetComponent<ThirdPersonController>();
        characterController = GetComponent<CharacterController>();
        inputs = GetComponent<StarterAssetsInputs>();
        animator = GetComponent<Animator>();

        // Anulamos el Sprint de Shift por defecto para que solo corra más rápido al Embestir
        controladorBase.MoveSpeed = velocidadBase;
        controladorBase.SprintSpeed = velocidadBase;
        giroNormalBase = controladorBase.RotationSmoothTime;

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

        // Si no es el vikingo activo, se queda quieto en reposo
        if (!Es_Activo) return;

        En_Suelo = controladorBase.Grounded;

        // Empuje físico hacia atrás cuando choca contra un muro o bloque
        if (timerRebote > 0f)
        {
            timerRebote -= Time.deltaTime;
            characterController.Move(direccionRebote * Time.deltaTime);
        }

        // Estado Aturdido: bloqueado por 1.5 segundos tras chocar de frente
        if (Estado_Actual == EstadoAgil.Aturdido)
        {
            Tiempo_Aturdido -= Time.deltaTime;
            if (Tiempo_Aturdido <= 0f)
            {
                Tiempo_Aturdido = 0f;
                DetenerEmbestida();
                Estado_Actual = EstadoAgil.En_Reposo;
            }
            return;
        }

        ActualizarMovimientoYEmbestida();
    }

    private void ActualizarMovimientoYEmbestida()
    {
        // Se puede embestir manteniendo Shift (inputs.sprint) o con el botón de ataque (Clic Izquierdo)
        bool quiereEmbestir = inputs.sprint || inputs.attack;

        // 1. Lógica en el aire (Salto normal o Salto Largo si venía embistiendo)
        if (!En_Suelo)
        {
            if (Estado_Actual == EstadoAgil.Embistiendo)
            {
                enSaltoLargo = true; // Conserva la velocidad turbo en el aire
                if (sensorCasco != null) sensorCasco.ActivarSensor(false);
            }
            Estado_Actual = EstadoAgil.Saltando;
            return;
        }

        // Al aterrizar de un salto largo, si ya no presiona embestir, vuelve a velocidad normal
        if (enSaltoLargo && !quiereEmbestir)
        {
            enSaltoLargo = false;
            DetenerEmbestida();
        }

        // 2. Lógica de Embestida en el suelo
        if (quiereEmbestir)
        {
            if (Estado_Actual != EstadoAgil.Embistiendo)
            {
                IniciarEmbestida();
            }

            // Hace que Erik corra hacia adelante automáticamente aunque no presione W
            if (inputs.move == Vector2.zero)
            {
                inputs.move = new Vector2(0f, 1f);
            }
            return;
        }
        else if (Estado_Actual == EstadoAgil.Embistiendo)
        {
            DetenerEmbestida();
        }

        // 3. Estados básicos: Corriendo (WASD) o En Reposo
        Estado_Actual = (inputs.move != Vector2.zero) ? EstadoAgil.Corriendo : EstadoAgil.En_Reposo;
    }

    private void IniciarEmbestida()
    {
        Estado_Actual = EstadoAgil.Embistiendo;
        controladorBase.MoveSpeed = velocidadEmbestida;
        controladorBase.SprintSpeed = velocidadEmbestida;
        controladorBase.RotationSmoothTime = giroDuroEmbestida;
        Vel_Giro_Actual = 30f;

        if (sensorCasco != null) sensorCasco.ActivarSensor(true);
        if (animator != null) animator.SetBool("Embistiendo", true);
    }

    private void DetenerEmbestida()
    {
        inputs.attack = false;
        controladorBase.canMove = true;
        controladorBase.canJump = true;
        controladorBase.MoveSpeed = velocidadBase;
        controladorBase.SprintSpeed = velocidadBase;
        controladorBase.RotationSmoothTime = giroNormalBase;
        Vel_Giro_Actual = 360f;

        if (sensorCasco != null) sensorCasco.ActivarSensor(false);
        if (animator != null) animator.SetBool("Embistiendo", false);
    }

    // --- LÓGICA DE CHOQUES (Romper bloques, dañar enemigos o aturdirse en muros) ---
    public void EvaluarImpactoEmbestida(Collider otro, Vector3 puntoImpacto, Vector3 normalImpacto)
    {
        if (Estado_Actual != EstadoAgil.Embistiendo) return;
        if (otro.transform.root == transform.root) return;

        // Regla 2: Romper bloque ("Rompible") o chocar Enemigo ("Enemigo")
        if (otro.CompareTag("Rompible") || otro.CompareTag("Enemigo"))
        {
            if (otro.CompareTag("Rompible"))
            {
                Destroy(otro.gameObject);
            }

            AplicarRebote(fuerzaRebote * 0.7f);
            DetenerEmbestida();
            Estado_Actual = EstadoAgil.En_Reposo;
            return;
        }

        // Regla 3: Choque contra pared sólida (Calcula si el ángulo es menor a 45 grados)
        if (otro.CompareTag("Muro_Solido") || !otro.isTrigger)
        {
            Vector3 normalPlana = new Vector3(normalImpacto.x, 0f, normalImpacto.z).normalized;
            if (Mathf.Abs(normalImpacto.y) > 0.5f || normalPlana == Vector3.zero) return;

            float anguloChoque = Vector3.Angle(transform.forward, -normalPlana);

            if (anguloChoque < 45f)
            {
                // Choque frontal: rebota y se aturde 1.5s
                DetenerEmbestida();
                controladorBase.canMove = false;
                controladorBase.canJump = false;
                Tiempo_Aturdido = duracionAturdido;
                AplicarRebote(fuerzaRebote);

                Estado_Actual = EstadoAgil.Aturdido;
                if (animator != null) animator.SetTrigger("Aturdido");
            }
            // Si es mayor a 45°, no entra al if y simplemente se desliza por la pared
        }
    }

    private void OnControllerColliderHit(ControllerColliderHit hit)
    {
        if (Estado_Actual == EstadoAgil.Embistiendo && hit.moveDirection.y >= -0.3f)
        {
            EvaluarImpactoEmbestida(hit.collider, hit.point, hit.normal);
        }
    }

    private void AplicarRebote(float fuerza)
    {
        direccionRebote = -transform.forward * fuerza;
        timerRebote = 0.25f;
    }

    // --- LÓGICA DE SALUD Y MUERTE ---
    public void RecibirDaño(int daño = 1)
    {
        if (Estado_Actual == EstadoAgil.Muerto) return;
        Salud_Actual = Mathf.Max(0, Salud_Actual - daño);

        if (Salud_Actual == 0)
        {
            DetenerEmbestida();
            controladorBase.canMove = false;
            controladorBase.canJump = false;
            Estado_Actual = EstadoAgil.Muerto;
            if (animator != null) animator.SetTrigger("Muerto");
        }
    }

    // =========================================================================
    // ESPACIO PARA COMPAÑEROS (Cambio de personaje / Interacción futura)
    // =========================================================================

    // El compañero que programe el cambio de Vikingo solo debe llamar a esta función:
    public void SetPersonajeActivo(bool activo)
    {
        Es_Activo = activo;
        if (!Es_Activo)
        {
            DetenerEmbestida();
            inputs.move = Vector2.zero;
            inputs.jump = false;
            controladorBase.canMove = false;
            controladorBase.canJump = false;
            Estado_Actual = EstadoAgil.En_Reposo;
        }
        else
        {
            controladorBase.canMove = true;
            controladorBase.canJump = true;
        }
    }
}
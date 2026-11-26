using System.Reflection;
using StarterAssets;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[RequireComponent(typeof(BoxCollider))]
public class Escalera : MonoBehaviour
{
    [Header("1. Puntos de la Escalera")]
    [Tooltip("Empty al pie de la escalera. Su flecha AZUL (Z) debe apuntar HACIA la pared/escalera")]
    public Transform puntoInferior;
    [Tooltip("Empty en el tope superior de la escalera")]
    public Transform puntoSuperior;
    [Tooltip("Empty un paso adelante sobre el piso superior donde aterriza al terminar de subir")]
    public Transform puntoSalidaArriba;

    [Header("2. Movimiento")]
    public float velocidadSubida = 4.0f;
    [Tooltip("Desmarcado (recomendado en 3ra persona): W siempre sube, S siempre baja. Marcado: si miras abajo con la cámara, W baja")]
    public bool usarDireccionCamara = false;

    [Header("3. Soltarse con Espacio")]
    [Tooltip("Empuje horizontal hacia atrás al soltarse con Espacio")]
    public float fuerzaEmpujeEspacio = 4.5f;
    [Tooltip("Impulso vertical al saltar de la escalera (solo se aplica a personajes con canJump = true, como Ágil)")]
    public float impulsoSaltoEspacio = 6.5f;
    [Tooltip("Segundos antes de poder volver a engancharse tras soltarse")]
    public float cooldownReenganche = 0.5f;

    [Header("4. Estado en Vivo (Solo lectura)")]
    public bool alguienEscalando = false;

    private GameObject jugadorEscalando;
    private ThirdPersonController tpcEscalando;
    private CharacterController ccEscalando;
    private StarterAssetsInputs inputsEscalando;
    private Animator animEscalando;

    private float gravedadOriginal = -15f;
    private bool canJumpOriginal = true;
    private bool tieneParametroEscalando = false;
    private float tiempoFinCooldown = 0f;

    // Empuje corto al presionar Espacio para separar al personaje de la escalera
    private CharacterController ccEmpuje;
    private Vector3 velocidadEmpuje;
    private float timerEmpuje = 0f;

    private static FieldInfo campoVelocidadVertical;

    private void Awake()
    {
        BoxCollider box = GetComponent<BoxCollider>();
        box.isTrigger = true;

        if (campoVelocidadVertical == null)
        {
            campoVelocidadVertical = typeof(ThirdPersonController).GetField(
                "_verticalVelocity",
                BindingFlags.Instance | BindingFlags.NonPublic
            );
        }
    }

    private void Update()
    {
        // Aplicamos el pequeño empuje hacia atrás después de soltarse con Espacio
        if (timerEmpuje > 0f && ccEmpuje != null && ccEmpuje.enabled)
        {
            timerEmpuje -= Time.deltaTime;
            ccEmpuje.Move(velocidadEmpuje * Time.deltaTime);
        }

        if (jugadorEscalando == null) return;

        // Si el jugador cambió a otro vikingo (cámara bloqueada), se queda sujeto en pausa sin moverse
        if (tpcEscalando.LockCameraPosition)
        {
            AnularGravedad();
            return;
        }

        // Mantener bloqueado el movimiento normal mientras la cámara sigue libre
        tpcEscalando.canMove = false;
        tpcEscalando.canJump = false;
        AnularGravedad();

        if (inputsEscalando != null)
        {
            inputsEscalando.move = Vector2.zero;
            inputsEscalando.jump = false;
            inputsEscalando.sprint = false;
        }

        if (animEscalando != null)
        {
            animEscalando.SetFloat("Speed", 0f);
            animEscalando.SetBool("Grounded", true);
            animEscalando.SetBool("FreeFall", false);
        }

        // 1. Soltarse al instante con Espacio
        if (PresionoEspacio())
        {
            SoltarEscalera(false, true);
            return;
        }

        // 2. Calcular dirección vertical con W / S
        float inputVertical = LeerEjeVertical();
        if (usarDireccionCamara && Camera.main != null && Mathf.Abs(inputVertical) > 0.01f)
        {
            float anguloX = Camera.main.transform.eulerAngles.x;
            if (anguloX > 180f) anguloX -= 360f;
            if (anguloX > 25f) inputVertical = -inputVertical;
        }

        if (Mathf.Abs(inputVertical) > 0.01f)
        {
            Vector3 movimientoY = new Vector3(0f, inputVertical * velocidadSubida * Time.deltaTime, 0f);
            ccEscalando.Move(movimientoY);
        }

        // 3. Salida automática al llegar al tope superior
        if (puntoSuperior != null && inputVertical > 0.1f && jugadorEscalando.transform.position.y >= puntoSuperior.position.y)
        {
            SoltarEscalera(true, false);
            return;
        }

        // 4. Salida automática al llegar al suelo bajando
        if (puntoInferior != null && inputVertical < -0.1f && jugadorEscalando.transform.position.y <= puntoInferior.position.y + 0.08f)
        {
            SoltarEscalera(false, false);
        }
    }

    private void OnTriggerStay(Collider other)
    {
        if (jugadorEscalando != null) return;
        if (Time.time < tiempoFinCooldown) return;
        if (!other.CompareTag("Player")) return;

        CharacterController cc = other.GetComponent<CharacterController>();
        ThirdPersonController tpc = other.GetComponent<ThirdPersonController>();
        if (cc == null || tpc == null) return;

        // Solo se engancha el personaje activo que puede moverse (ignora aturdidos/muertos)
        if (!tpc.enabled || !tpc.canMove || tpc.LockCameraPosition) return;

        // No enganchar a Ágil si viene en plena embestida con Shift
        AgilController agil = other.GetComponent<AgilController>();
        if (agil != null && agil.Estado_Actual == AgilController.EstadoAgil.Embistiendo) return;

        if (DebeEngancharse(other.transform, tpc))
        {
            EngancharJugador(other.gameObject, tpc, cc);
        }
    }

    private bool DebeEngancharse(Transform trJugador, ThirdPersonController tpc)
    {
        // Si está en el aire (ej. saltó hacia la escalera), se atrapa al instante
        if (!tpc.Grounded) return true;

        Vector2 teclado = LeerMovimientoReal();
        if (teclado.sqrMagnitude < 0.01f) return false;

        Transform cam = (Camera.main != null) ? Camera.main.transform : trJugador;
        Vector3 camAdelante = Vector3.ProjectOnPlane(cam.forward, Vector3.up).normalized;
        Vector3 camDerecha = Vector3.ProjectOnPlane(cam.right, Vector3.up).normalized;
        Vector3 dirMovimientoMundo = (camAdelante * teclado.y + camDerecha * teclado.x).normalized;

        Vector3 frenteEscalera = ObtenerFrenteEscalera();

        // Si está parado en el piso superior, entra si camina hacia el borde de la escalera
        if (puntoSuperior != null && trJugador.position.y >= puntoSuperior.position.y - 0.6f)
        {
            Vector3 haciaEscalera = Vector3.ProjectOnPlane(puntoSuperior.position - trJugador.position, Vector3.up).normalized;
            return Vector3.Dot(dirMovimientoMundo, haciaEscalera) > 0.3f;
        }

        // Si está en el suelo abajo, solo se engancha si camina HACIA la escalera
        return Vector3.Dot(dirMovimientoMundo, frenteEscalera) > 0.35f;
    }

    private void EngancharJugador(GameObject jugador, ThirdPersonController tpc, CharacterController cc)
    {
        jugadorEscalando = jugador;
        tpcEscalando = tpc;
        ccEscalando = cc;
        inputsEscalando = jugador.GetComponent<StarterAssetsInputs>();
        animEscalando = jugador.GetComponent<Animator>();
        alguienEscalando = true;

        gravedadOriginal = (Mathf.Abs(tpc.Gravity) > 0.1f) ? tpc.Gravity : -15.0f;
        canJumpOriginal = tpc.canJump;

        tpcEscalando.Gravity = 0f;
        tpcEscalando.canMove = false;
        tpcEscalando.canJump = false;
        AnularGravedad();

        if (inputsEscalando != null)
        {
            inputsEscalando.move = Vector2.zero;
            inputsEscalando.jump = false;
            inputsEscalando.sprint = false;
        }

        tieneParametroEscalando = TieneParametroBool(animEscalando, "Escalando");
        if (tieneParametroEscalando) animEscalando.SetBool("Escalando", true);

        if (puntoInferior != null && puntoSuperior != null)
        {
            float yClamped = Mathf.Clamp(jugador.transform.position.y, puntoInferior.position.y, puntoSuperior.position.y - 0.2f);
            Vector3 posAlineada = new Vector3(puntoInferior.position.x, yClamped, puntoInferior.position.z);

            ccEscalando.enabled = false;
            jugador.transform.position = posAlineada;
            jugador.transform.rotation = Quaternion.LookRotation(ObtenerFrenteEscalera(), Vector3.up);
            ccEscalando.enabled = true;
        }
    }

    private void SoltarEscalera(bool salioPorArriba, bool porSaltoEspacio)
    {
        if (jugadorEscalando == null) return;

        if (salioPorArriba && puntoSalidaArriba != null)
        {
            ccEscalando.enabled = false;
            jugadorEscalando.transform.position = puntoSalidaArriba.position;
            ccEscalando.enabled = true;
        }

        tpcEscalando.Gravity = gravedadOriginal;
        tpcEscalando.canMove = true;
        tpcEscalando.canJump = canJumpOriginal;

        if (tieneParametroEscalando && animEscalando != null)
        {
            animEscalando.SetBool("Escalando", false);
        }

        if (porSaltoEspacio)
        {
            ccEmpuje = ccEscalando;
            velocidadEmpuje = -ObtenerFrenteEscalera() * fuerzaEmpujeEspacio;
            timerEmpuje = 0.2f;

            if (campoVelocidadVertical != null)
            {
                float velSalto = canJumpOriginal ? impulsoSaltoEspacio : 0f;
                campoVelocidadVertical.SetValue(tpcEscalando, velSalto);
            }
        }
        else if (campoVelocidadVertical != null)
        {
            campoVelocidadVertical.SetValue(tpcEscalando, -2f);
        }

        if (inputsEscalando != null)
        {
            inputsEscalando.jump = false;
            inputsEscalando.move = LeerMovimientoReal();
        }

        tiempoFinCooldown = Time.time + cooldownReenganche;

        jugadorEscalando = null;
        tpcEscalando = null;
        ccEscalando = null;
        inputsEscalando = null;
        animEscalando = null;
        alguienEscalando = false;
    }

    private void AnularGravedad()
    {
        if (tpcEscalando != null && campoVelocidadVertical != null)
        {
            campoVelocidadVertical.SetValue(tpcEscalando, 0f);
        }
    }

    private Vector3 ObtenerFrenteEscalera()
    {
        Vector3 dir = (puntoInferior != null) ? puntoInferior.forward : transform.forward;
        dir.y = 0f;
        return (dir.sqrMagnitude > 0.001f) ? dir.normalized : Vector3.forward;
    }

    private bool TieneParametroBool(Animator anim, string nombre)
    {
        if (anim == null) return false;
        foreach (AnimatorControllerParameter p in anim.parameters)
        {
            if (p.type == AnimatorControllerParameterType.Bool && p.name == nombre) return true;
        }
        return false;
    }

    private float LeerEjeVertical()
    {
        float v = 0f;
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null)
        {
            if (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed) v += 1f;
            if (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed) v -= 1f;
        }
#else
        v = Input.GetAxisRaw("Vertical");
#endif
        return v;
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
#else
        dir = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
#endif
        return dir.normalized;
    }

    private bool PresionoEspacio()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame;
#else
        return Input.GetKeyDown(KeyCode.Space);
#endif
    }

    private void OnDrawGizmos()
    {
        if (puntoInferior != null && puntoSuperior != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(puntoInferior.position, puntoSuperior.position);
            Gizmos.DrawWireSphere(puntoInferior.position, 0.2f);
            Gizmos.DrawWireSphere(puntoSuperior.position, 0.2f);

            // Flecha azul: indica hacia dónde mira el personaje al subir (debe apuntar a la pared)
            Gizmos.color = Color.blue;
            Gizmos.DrawRay(puntoInferior.position + Vector3.up * 0.5f, ObtenerFrenteEscalera() * 1.0f);
        }

        if (puntoSalidaArriba != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(puntoSalidaArriba.position, 0.25f);
        }
    }
}
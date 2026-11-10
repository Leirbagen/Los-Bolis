using StarterAssets;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[RequireComponent(typeof(ThirdPersonController))]
public class RudoController : MonoBehaviour
{
    [Header("Inventario de Armas")]
    public Transform puntoAgarre;
    public GameObject prefabArma;
    public GameObject prefabHonda;

    [Header("Cámara de Apuntado")]
    public GameObject camaraApuntado;
    public GameObject miraUI;

    private ArmaMelee scriptArma;
    private ArmaDistancia scriptHonda;

    private int armaActual = 1;
    private bool estaAtacando = false;

    private ThirdPersonController controladorBase;
    public Animator animator;
    public StarterAssetsInputs inputs;
    private Transform camaraPrincipal;

    private void Start()
    {
        controladorBase = GetComponent<ThirdPersonController>();
        inputs = GetComponent<StarterAssetsInputs>();
        animator = GetComponent<Animator>();
        camaraPrincipal = Camera.main.transform;

        controladorBase.canJump = false;

        if (prefabArma != null && puntoAgarre != null)
        {
            GameObject nuevaKinsa = Instantiate(prefabArma, puntoAgarre.position, puntoAgarre.rotation, puntoAgarre);
            scriptArma = nuevaKinsa.GetComponent<ArmaMelee>();
        }

        if (prefabHonda != null && puntoAgarre != null)
        {
            GameObject nuevaHonda = Instantiate(prefabHonda, puntoAgarre.position, puntoAgarre.rotation, puntoAgarre);
            scriptHonda = nuevaHonda.GetComponent<ArmaDistancia>();
        }

        if (camaraApuntado != null)
        {
            camaraApuntado.SetActive(false);
        }

        CambiarArma(1);
    }

    private void Update()
    {
        if (estaAtacando) return;

        bool estaApuntando = Mouse.current != null && Mouse.current.rightButton.isPressed;

        if (!estaApuntando && Keyboard.current != null)
        {
            if (Keyboard.current.digit1Key.wasPressedThisFrame) CambiarArma(1);
            if (Keyboard.current.digit2Key.wasPressedThisFrame) CambiarArma(2);
        }

        if (armaActual == 1)
        {
            if (inputs.attack && controladorBase.Grounded)
            {
                inputs.attack = false;
                EjecutarAtaque();
            }
        }
        else if (armaActual == 2 && scriptHonda != null)
        {
            if (estaApuntando && controladorBase.Grounded)
            {
                controladorBase.canMove = true;
                controladorBase.isStrafing = true;
                inputs.move.y = 0f;
                inputs.sprint = false;
                // Encendemos la cámara de hombro,  Cinemachine hace el zoom solo
                if (camaraApuntado != null && !camaraApuntado.activeSelf)
                {
                    camaraApuntado.SetActive(true);
                }
                if (miraUI != null && !miraUI.activeSelf) 
                {
                    miraUI.SetActive(true);
                }

                RotarConCamara();
                scriptHonda.MostrarTrayectoria(true);

                if (inputs.attack)
                {
                    inputs.attack = false;
                    scriptHonda.Disparar();
                }
            }
            else
            {
                controladorBase.isStrafing = false;
                // Apagamos la cámara de hombro para regresar a PlayerFollowCamera
                if (camaraApuntado != null && camaraApuntado.activeSelf)
                {
                    camaraApuntado.SetActive(false);
                }
                if (miraUI != null && miraUI.activeSelf)
                {
                    miraUI.SetActive(false);
                }

                scriptHonda.MostrarTrayectoria(false);
                controladorBase.canMove = true;
                inputs.attack = false;
            }
        }
    }

    private void CambiarArma(int nuevaArma)
    {
        armaActual = nuevaArma;

        if (scriptArma != null) scriptArma.gameObject.SetActive(armaActual == 1);
        if (scriptHonda != null) scriptHonda.gameObject.SetActive(armaActual == 2);
        if (camaraApuntado != null) camaraApuntado.SetActive(false);
        if (miraUI != null) miraUI.SetActive(false);

        controladorBase.canMove = true;
        controladorBase.isStrafing = false;
    }

    private void RotarConCamara()
    {
        Vector3 direccionVista = camaraPrincipal.forward;
        direccionVista.y = 0f;

        if (direccionVista.sqrMagnitude > 0.01f)
        {
            Quaternion rotacionObjetivo = Quaternion.LookRotation(direccionVista);
            transform.rotation = Quaternion.Slerp(transform.rotation, rotacionObjetivo, Time.deltaTime * 15f);
        }
    }

    private void EjecutarAtaque()
    {
        estaAtacando = true;
        controladorBase.canMove = false;

        if (animator != null)
        {
            animator.SetTrigger("Ataque");
        }
    }

    public void ActivarDaño()
    {
        if (scriptArma != null) scriptArma.ActivarHitbox(true);
    }

    public void DesactivarDaño()
    {
        if (scriptArma != null) scriptArma.ActivarHitbox(false);
    }

    public void TerminarAtaque()
    {
        controladorBase.canMove = true;
        estaAtacando = false;
    }
}
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
                controladorBase.canMove = false;
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
                scriptHonda.MostrarTrayectoria(false);
                controladorBase.canMove = true;
                inputs.attack = false;
            }
        }
    }

    private void CambiarArma(int nuevaArma)
    {
        armaActual = nuevaArma;

        scriptArma.gameObject.SetActive(armaActual == 1);
        scriptHonda.gameObject.SetActive(armaActual == 2);

        controladorBase.canMove = true;
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
using StarterAssets;
using UnityEngine;

[RequireComponent(typeof(ThirdPersonController))]
public class RudoController : MonoBehaviour
{
    [Header("Ataque Melee")]
    public GameObject prefabArma;
    public Transform puntoAgarre;

    private ArmaMelee scriptArma;
    private bool estaAtacando = false;

    private ThirdPersonController controladorBase;
    public Animator animator;
    public StarterAssetsInputs inputs;

    private void Start()
    {
        controladorBase = GetComponent<ThirdPersonController>();
        inputs = GetComponent<StarterAssetsInputs>();
        animator = GetComponent<Animator>();

        controladorBase.canJump = false;

        if (prefabArma != null && puntoAgarre != null)
        {
            GameObject nuevaArma = Instantiate(prefabArma, puntoAgarre.position, puntoAgarre.rotation, puntoAgarre);
            scriptArma = nuevaArma.GetComponent<ArmaMelee>();
        }
    }

    private void Update()
    {
        if (inputs.attack && !estaAtacando && controladorBase.Grounded)
        {
            inputs.attack = false;
            EjecutarAtaque();
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
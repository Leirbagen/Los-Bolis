using UnityEngine;
using StarterAssets; 

public class FuerteController : MonoBehaviour
{
    [Header("Referencias de la Plantilla")]
    [SerializeField] private ThirdPersonController controladorTerceraPersona;

    [SerializeField] private StarterAssetsInputs entradasPlantilla;

    [Header("Configuración de Escudos y Colliders")]
    [SerializeField] private Collider colliderEscudoFrontal;

    [Tooltip("Collider plano sobre la cabeza para que sirva de plataforma")]
    [SerializeField] private Collider colliderPlataformaSuperior;

    [Header("Ajustes de Movimiento Pesado (Fuerte)")]
    [SerializeField] private float velocidadCaminataConEscudo = 2.0f;
    [SerializeField] private float velocidadCaminataNormal = 4.0f;

    [Header("Ajustes de Planeo (Paravela)")]
    [SerializeField] private float gravedadNormal = 15.0f;
    [SerializeField] private float gravedadPlaneando = 2.0f;
    [SerializeField] private float velocidadCaidaMaximaPlaneando = -2.0f;

    private bool estaBloqueandoFrontal = false;
    private bool estaBloqueandoArriba = false;

    private void Start()
    {
        if (controladorTerceraPersona == null) controladorTerceraPersona = GetComponent<ThirdPersonController>();
        if (entradasPlantilla == null) entradasPlantilla = GetComponent<StarterAssetsInputs>();

        if (controladorTerceraPersona == null || entradasPlantilla == null)
        {
            Debug.LogError("FuerteController: ¡Faltan referencias a la plantilla de Starter Assets!");
            enabled = false;
            return;
        }

        controladorTerceraPersona.SprintSpeed = velocidadCaminataNormal;
        controladorTerceraPersona.MoveSpeed = velocidadCaminataNormal;

        DesactivarEscudos();
    }

    private void Update()
    {
        if (entradasPlantilla.jump)
        {
            entradasPlantilla.jump = false;
        }

       
        bool inputBloqueoFrontal = Input.GetMouseButton(1); // Clic derecho
        bool inputBloqueoArriba = Input.GetKey(KeyCode.R);   // Tecla R

      
        if (inputBloqueoArriba)
        {
            ActivarBloqueoSuperior();
        }
        else if (inputBloqueoFrontal)
        {
            ActivarBloqueoFrontal();
        }
        else
        {
            DesactivarEscudos();
        }

      
        GestionarPlaneo();
    }

    private void ActivarBloqueoFrontal()
    {
        estaBloqueandoFrontal = true;
        estaBloqueandoArriba = false;

        if (colliderEscudoFrontal != null) colliderEscudoFrontal.enabled = true;
        if (colliderPlataformaSuperior != null) colliderPlataformaSuperior.enabled = false;

        
        controladorTerceraPersona.MoveSpeed = velocidadCaminataConEscudo;
    }

    private void ActivarBloqueoSuperior()
    {
        estaBloqueandoFrontal = false;
        estaBloqueandoArriba = true;

        if (colliderEscudoFrontal != null) colliderEscudoFrontal.enabled = false;
        if (colliderPlataformaSuperior != null) colliderPlataformaSuperior.enabled = true;

       
        controladorTerceraPersona.MoveSpeed = velocidadCaminataNormal;
    }

    private void DesactivarEscudos()
    {
        estaBloqueandoFrontal = false;
        estaBloqueandoArriba = false;

        if (colliderEscudoFrontal != null) colliderEscudoFrontal.enabled = false;
        if (colliderPlataformaSuperior != null) colliderPlataformaSuperior.enabled = false;

     
        controladorTerceraPersona.MoveSpeed = velocidadCaminataNormal;

       
        controladorTerceraPersona.Gravity = gravedadNormal;
    }

    private void GestionarPlaneo()
    {
      
        bool estaEnElAire = !controladorTerceraPersona.Grounded;

        if (estaBloqueandoArriba && estaEnElAire)
        {
            
            controladorTerceraPersona.Gravity = gravedadPlaneando;
        }
        else
        {
            
            controladorTerceraPersona.Gravity = gravedadNormal;
        }
    }
}
using UnityEngine;
using StarterAssets;
using System.Reflection; // <-- NUEVO: Herramienta para leer variables privadas de otros scripts

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
    [SerializeField] private float velocidadCaidaMaximaPlaneando = -2.0f; // El freno de mano

    // Memoria interna 
    private bool estaBloqueandoArriba = false;

    // Nuestro "acceso secreto" a la variable privada de la plantilla
    private FieldInfo campoVelocidadPrivada;

    private void Start()
    {
        if (controladorTerceraPersona == null) controladorTerceraPersona = GetComponent<ThirdPersonController>();
        if (entradasPlantilla == null) entradasPlantilla = GetComponent<StarterAssetsInputs>();

        if (controladorTerceraPersona == null || entradasPlantilla == null || colliderEscudoFrontal == null || colliderPlataformaSuperior == null)
        {
            Debug.LogError("FuerteController: Faltan componentes.");
            enabled = false;
            return;
        }

        // --- MAGIA DE REFLECTION ---
        // Le decimos a C# que busque a la fuerza la variable "_verticalVelocity" dentro del ThirdPersonController
        campoVelocidadPrivada = typeof(ThirdPersonController).GetField("_verticalVelocity", BindingFlags.NonPublic | BindingFlags.Instance);

        if (campoVelocidadPrivada == null)
        {
            Debug.LogError("FuerteController: No se pudo encontrar _verticalVelocity mediante Reflection.");
        }
        // ---------------------------

        controladorTerceraPersona.SprintSpeed = velocidadBase;
        controladorTerceraPersona.MoveSpeed = velocidadBase;
        estaBloqueandoArriba = false;
        ActualizarPosicionEscudo();
    }

    private void Update()
    {
        // 1. ANULAR SALTO
        if (entradasPlantilla.jump) entradasPlantilla.jump = false;

        // 2. LECTURA DE ENTRADA (Mecánica de Toggle / R)
        if (Input.GetKeyDown(KeyCode.R))
        {
            estaBloqueandoArriba = !estaBloqueandoArriba;
            ActualizarPosicionEscudo();
        }

        // 3. LÓGICA DE PLANEO
        GestionarPlaneo();
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
            // 1. Cambiamos la fuerza de gravedad constante
            controladorTerceraPersona.Gravity = gravedadPlaneando;

            // 2. USAMOS REFLECTION PARA FRENAR LA INERCIA ACUMULADA
            if (campoVelocidadPrivada != null)
            {
                // Extraemos el valor actual que tiene la plantilla a escondidas
                float inerciaActual = (float)campoVelocidadPrivada.GetValue(controladorTerceraPersona);

                // Si viene cayendo a -15, lo frenamos en seco a -2 (velocidadCaidaMaximaPlaneando)
                if (inerciaActual < velocidadCaidaMaximaPlaneando)
                {
                    // Sobrescribimos el valor privado sin que la plantilla original se entere
                    campoVelocidadPrivada.SetValue(controladorTerceraPersona, velocidadCaidaMaximaPlaneando);
                }
            }
        }
        else
        {
            controladorTerceraPersona.Gravity = gravedadNormal;
        }
    }
}
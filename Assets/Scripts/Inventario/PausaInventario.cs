using System.Collections.Generic;
using StarterAssets;
using UnityEngine;
using UnityEngine.EventSystems;

public class PausaInventario : MonoBehaviour
{
    private struct EstadoComponente
    {
        public MonoBehaviour componente;
        public bool estabaHabilitado;
    }

    [Header("Componentes de juego a suspender")]
    [SerializeField] private List<MonoBehaviour> componentesASuspender = new List<MonoBehaviour>();

    [Tooltip("Si está activo, incluye automáticamente GameManager y los controladores de GameManager.armaduras sin duplicarlos.")]
    [SerializeField] private bool incluirAutomaticamenteControlesPrincipales = true;

    [Tooltip("Incluye los switches, escaleras y ascensores de pisos de las escenas cargadas, para reutilizar el prefab sin enlazarlos uno a uno.")]
    [SerializeField] private bool incluirAutomaticamenteMecanismos = true;

    [Header("Entradas de Starter Assets a limpiar")]
    [SerializeField] private List<StarterAssetsInputs> entradasStarterAssets = new List<StarterAssetsInputs>();

    [Header("Estado de apuntado de Rudo")]
    [SerializeField] private RudoController rudoController;
    [SerializeField] private ThirdPersonController thirdPersonControllerRudo;
    [SerializeField] private GameObject camaraApuntadoRudo;
    [SerializeField] private GameObject miraUIRudo;

    private readonly List<EstadoComponente> estadosGuardados = new List<EstadoComponente>();
    private readonly HashSet<MonoBehaviour> componentesUnicos = new HashSet<MonoBehaviour>();
    private readonly HashSet<StarterAssetsInputs> entradasUnicas = new HashSet<StarterAssetsInputs>();

    private bool estaPausado;
    private float timeScaleAnterior = 1f;

    private bool debeRestaurarEstadoRudo;
    private bool camaraApuntadoEstabaActiva;
    private bool miraUIEstabaActiva;
    private bool rudoEstabaStrafing;
    private ThirdPersonController tpcRudoActivoEnPausa;

    public bool EstaPausado => estaPausado;

    public void Pausar()
    {
        if (estaPausado)
        {
            return;
        }

        RecopilarComponentesUnicos();

        CapturarEstadoRudoAntesDeSuspender();

        timeScaleAnterior = Time.timeScale;
        Time.timeScale = 0f;

        estadosGuardados.Clear();
        foreach (MonoBehaviour componente in componentesUnicos)
        {
            estadosGuardados.Add(new EstadoComponente
            {
                componente = componente,
                estabaHabilitado = componente.enabled
            });
        }

        // Capturamos todos los estados antes de ejecutar cualquier OnDisable.
        estaPausado = true;
        foreach (EstadoComponente estado in estadosGuardados)
        {
            if (estado.estabaHabilitado && estado.componente != null)
            {
                estado.componente.enabled = false;
            }
        }

        LimpiarEntradasStarterAssets();
        estaPausado = true;
    }

    public void Reanudar()
    {
        if (!estaPausado)
        {
            return;
        }

        estaPausado = false;

        LimpiarEntradasStarterAssets();

        for (int i = 0; i < estadosGuardados.Count; i++)
        {
            EstadoComponente estado = estadosGuardados[i];
            if (estado.componente != null)
            {
                estado.componente.enabled = estado.estabaHabilitado;
            }
        }

        estadosGuardados.Clear();

        RestaurarEstadoRudoDespuesDeReanudar();

        Time.timeScale = timeScaleAnterior;
    }

    private void OnDisable()
    {
        Reanudar();
    }

    private void OnDestroy()
    {
        Reanudar();
    }

    private void RecopilarComponentesUnicos()
    {
        componentesUnicos.Clear();

        for (int i = 0; i < componentesASuspender.Count; i++)
        {
            AgregarSiPermitido(componentesASuspender[i]);
        }

        if (incluirAutomaticamenteMecanismos)
        {
            foreach (SwitchInteractivo mecanismo in FindObjectsByType<SwitchInteractivo>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                AgregarSiPermitido(mecanismo);
            foreach (Escalera mecanismo in FindObjectsByType<Escalera>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                AgregarSiPermitido(mecanismo);
            foreach (AscensorPisos mecanismo in FindObjectsByType<AscensorPisos>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                AgregarSiPermitido(mecanismo);
        }

        if (!incluirAutomaticamenteControlesPrincipales)
        {
            return;
        }

        GameManager manager = GameManager.Instancia;
        if (manager == null)
        {
            return;
        }

        AgregarSiPermitido(manager);

        GameObject[] armaduras = manager.armaduras;
        if (armaduras == null)
        {
            return;
        }

        for (int i = 0; i < armaduras.Length; i++)
        {
            GameObject armadura = armaduras[i];
            if (armadura == null)
            {
                continue;
            }

            AgregarSiPermitido(armadura.GetComponent<ThirdPersonController>());
            AgregarSiPermitido(armadura.GetComponent<AgilController>());
            AgregarSiPermitido(armadura.GetComponent<RudoController>());
            AgregarSiPermitido(armadura.GetComponent<FuerteController>());
        }
    }

    private void AgregarSiPermitido(MonoBehaviour componente)
    {
        if (EsComponenteProtegido(componente))
        {
            return;
        }

        componentesUnicos.Add(componente);
    }

    private static bool EsComponenteProtegido(MonoBehaviour componente)
    {
        return componente == null
            || componente is SkeletonHealth
            || componente is InventarioPersonaje
            || componente is PausaInventario
            || componente is MenuInventarioUI
            || componente is RanuraInventarioUI
            || componente is EventSystem;
    }

    private void CapturarEstadoRudoAntesDeSuspender()
    {
        debeRestaurarEstadoRudo = false;
        tpcRudoActivoEnPausa = null;

        RudoController rudo = ResolverRudoController();
        if (rudo == null || !rudo.enabled || !componentesUnicos.Contains(rudo))
        {
            return;
        }

        // Estas referencias se resuelven en la escena, no en el asset del prefab.
        if (camaraApuntadoRudo == null) camaraApuntadoRudo = rudo.camaraApuntado;
        if (miraUIRudo == null) miraUIRudo = rudo.miraUI;

        tpcRudoActivoEnPausa = thirdPersonControllerRudo != null
            ? thirdPersonControllerRudo
            : rudo.GetComponent<ThirdPersonController>();

        camaraApuntadoEstabaActiva = camaraApuntadoRudo != null && camaraApuntadoRudo.activeSelf;
        miraUIEstabaActiva = miraUIRudo != null && miraUIRudo.activeSelf;
        rudoEstabaStrafing = tpcRudoActivoEnPausa != null && tpcRudoActivoEnPausa.isStrafing;
        debeRestaurarEstadoRudo = true;
    }

    private void RestaurarEstadoRudoDespuesDeReanudar()
    {
        if (!debeRestaurarEstadoRudo)
        {
            return;
        }

        if (camaraApuntadoRudo != null)
        {
            camaraApuntadoRudo.SetActive(camaraApuntadoEstabaActiva);
        }

        if (miraUIRudo != null)
        {
            miraUIRudo.SetActive(miraUIEstabaActiva);
        }

        if (tpcRudoActivoEnPausa != null)
        {
            tpcRudoActivoEnPausa.isStrafing = rudoEstabaStrafing;
        }

        debeRestaurarEstadoRudo = false;
        tpcRudoActivoEnPausa = null;
    }

    private RudoController ResolverRudoController()
    {
        if (rudoController != null)
        {
            return rudoController;
        }

        foreach (MonoBehaviour componente in componentesUnicos)
        {
            if (componente is RudoController encontrado)
            {
                return encontrado;
            }
        }

        GameManager manager = GameManager.Instancia;
        if (manager != null && manager.armaduras != null)
        {
            for (int i = 0; i < manager.armaduras.Length; i++)
            {
                GameObject armadura = manager.armaduras[i];
                if (armadura == null)
                {
                    continue;
                }

                RudoController encontrado = armadura.GetComponent<RudoController>();
                if (encontrado != null)
                {
                    return encontrado;
                }
            }
        }

        return null;
    }

    private void LimpiarEntradasStarterAssets()
    {
        entradasUnicas.Clear();

        for (int i = 0; i < entradasStarterAssets.Count; i++)
        {
            if (entradasStarterAssets[i] != null)
            {
                entradasUnicas.Add(entradasStarterAssets[i]);
            }
        }

        GameManager manager = GameManager.Instancia;
        if (manager != null && manager.armaduras != null)
        {
            for (int i = 0; i < manager.armaduras.Length; i++)
            {
                GameObject armadura = manager.armaduras[i];
                if (armadura == null)
                {
                    continue;
                }

                StarterAssetsInputs entradas = armadura.GetComponentInChildren<StarterAssetsInputs>(true);
                if (entradas != null)
                {
                    entradasUnicas.Add(entradas);
                }
            }
        }

        foreach (StarterAssetsInputs entradas in entradasUnicas)
        {
            entradas.move = Vector2.zero;
            entradas.look = Vector2.zero;
            entradas.jump = false;
            entradas.sprint = false;
            entradas.attack = false;
            entradas.aim = false;
        }
    }
}

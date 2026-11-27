using UnityEngine;

public class IntercambioInventario : MonoBehaviour
{
    [SerializeField, Min(0f)] private float distanciaMaximaIntercambio = 3f;

    private bool transferenciaActiva;
    private int indicePersonajeOrigen = -1;
    private int indiceRanuraOrigen = -1;
    private GameObject personajeOrigen;
    private InventarioPersonaje inventarioOrigen;
    private SkeletonHealth saludOrigen;
    private ItemDatos itemPendiente;

    public float DistanciaMaximaIntercambio => distanciaMaximaIntercambio;
    public bool TransferenciaActiva => transferenciaActiva;
    public int IndicePersonajeOrigen => indicePersonajeOrigen;
    public int IndiceRanuraOrigen => indiceRanuraOrigen;
    public ItemDatos ItemPendiente => itemPendiente;

    private void OnValidate()
    {
        if (distanciaMaximaIntercambio < 0f)
        {
            distanciaMaximaIntercambio = 0f;
        }
    }

    private void OnDisable()
    {
        CancelarTransferencia();
    }

    private void OnDestroy()
    {
        CancelarTransferencia();
    }

    public bool IntentarIniciarTransferencia(
        int indicePersonaje,
        GameObject objetoOrigen,
        InventarioPersonaje inventario,
        SkeletonHealth salud,
        int indiceRanura
    )
    {
        if (transferenciaActiva)
        {
            return false;
        }

        if (objetoOrigen == null || !objetoOrigen.activeInHierarchy)
        {
            return false;
        }

        if (inventario == null || !inventario.isActiveAndEnabled)
        {
            return false;
        }

        if (!EstaVivo(salud))
        {
            return false;
        }

        if (indiceRanura < 0 || indiceRanura >= InventarioPersonaje.Capacidad)
        {
            return false;
        }

        ItemDatos item = inventario.ObtenerItem(indiceRanura);
        if (item == null)
        {
            return false;
        }

        transferenciaActiva = true;
        indicePersonajeOrigen = indicePersonaje;
        indiceRanuraOrigen = indiceRanura;
        personajeOrigen = objetoOrigen;
        inventarioOrigen = inventario;
        saludOrigen = salud;
        itemPendiente = item;
        return true;
    }

    public bool IntentarConfirmarTransferencia(
        int indicePersonajeDestino,
        GameObject objetoDestino,
        InventarioPersonaje inventarioDestino,
        SkeletonHealth saludDestino,
        int indiceRanuraDestino
    )
    {
        if (!transferenciaActiva)
        {
            return false;
        }

        if (!EsOrigenAunValido())
        {
            CancelarTransferencia();
            return false;
        }

        if (indicePersonajeDestino == indicePersonajeOrigen)
        {
            return false;
        }

        if (objetoDestino == null || objetoDestino == personajeOrigen || !objetoDestino.activeInHierarchy)
        {
            return false;
        }

        if (inventarioDestino == null || inventarioDestino == inventarioOrigen || !inventarioDestino.isActiveAndEnabled)
        {
            return false;
        }

        if (!EstaVivo(saludDestino))
        {
            return false;
        }

        if (indiceRanuraDestino < 0 || indiceRanuraDestino >= InventarioPersonaje.Capacidad)
        {
            return false;
        }

        float distancia = Vector3.Distance(personajeOrigen.transform.position, objetoDestino.transform.position);
        if (distancia > distanciaMaximaIntercambio)
        {
            return false;
        }

        if (inventarioDestino.ObtenerItem(indiceRanuraDestino) != null)
        {
            return false;
        }

        bool transferido = inventarioOrigen.IntentarTransferir(
            indiceRanuraOrigen,
            inventarioDestino,
            indiceRanuraDestino
        );

        if (!transferido)
        {
            return false;
        }

        CancelarTransferencia();
        return true;
    }

    public void CancelarTransferencia()
    {
        transferenciaActiva = false;
        indicePersonajeOrigen = -1;
        indiceRanuraOrigen = -1;
        personajeOrigen = null;
        inventarioOrigen = null;
        saludOrigen = null;
        itemPendiente = null;
    }

    public bool EsOrigenAunValido()
    {
        if (!transferenciaActiva)
        {
            return false;
        }

        if (personajeOrigen == null || !personajeOrigen.activeInHierarchy)
        {
            return false;
        }

        if (inventarioOrigen == null || !inventarioOrigen.isActiveAndEnabled)
        {
            return false;
        }

        if (!EstaVivo(saludOrigen))
        {
            return false;
        }

        if (itemPendiente == null)
        {
            return false;
        }

        return inventarioOrigen.ObtenerItem(indiceRanuraOrigen) == itemPendiente;
    }

    private static bool EstaVivo(SkeletonHealth salud)
    {
        return salud != null && salud.vidas > 0;
    }
}
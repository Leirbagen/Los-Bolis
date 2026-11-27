using UnityEngine;
using UnityEngine.Events;

public class PuertasLlaves : MonoBehaviour
{
    [Header("Configuración de cerradura")]
    [SerializeField] private ItemDatos llaveRequerida;
    [SerializeField] private UnityEvent alAbrir;

    private bool abierto;
    private bool procesandoApertura;

    public ItemDatos LlaveRequerida => llaveRequerida;
    public bool Abierto => abierto;

    private void Awake()
    {
        if (llaveRequerida == null)
        {
            Debug.LogWarning($"[PuertasLlaves] '{name}' no tiene asignada una llaveRequerida.", this);
        }
        else if (llaveRequerida.Tipo != TipoItem.Llave)
        {
            Debug.LogWarning($"[PuertasLlaves] '{name}' tiene asignado '{llaveRequerida.name}', que no es de tipo TipoItem.Llave.", this);
        }
    }

    public bool IntentarAbrir(InventarioPersonaje inventario, int indiceRanura)
    {
        if (abierto || procesandoApertura || !isActiveAndEnabled)
        {
            return false;
        }

        if (llaveRequerida == null || llaveRequerida.Tipo != TipoItem.Llave)
        {
            return false;
        }

        if (inventario == null || !inventario.isActiveAndEnabled)
        {
            return false;
        }

        ItemDatos itemSeleccionado = inventario.ObtenerItem(indiceRanura);
        if (itemSeleccionado == null || itemSeleccionado.Tipo != TipoItem.Llave)
        {
            return false;
        }

        if (itemSeleccionado != llaveRequerida)
        {
            return false;
        }

        procesandoApertura = true;
        bool llaveConsumida;
        try
        {
            llaveConsumida = inventario.IntentarQuitar(indiceRanura);
        }
        finally
        {
            procesandoApertura = false;
        }

        if (!llaveConsumida)
        {
            return false;
        }

        abierto = true;
        alAbrir?.Invoke();
        return true;
    }
}
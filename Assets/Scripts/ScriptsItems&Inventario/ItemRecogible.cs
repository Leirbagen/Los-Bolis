using UnityEngine;

public class ItemRecogible : MonoBehaviour
{
    [SerializeField] private ItemDatos itemDatos;

    private bool recogido;
    private bool procesandoRecogida;

    public ItemDatos ItemDatos => itemDatos;

    private void Awake()
    {
        if (itemDatos == null)
        {
            Debug.LogWarning($"[ItemRecogible] '{name}' no tiene asignado un ItemDatos.", this);
        }

        if (!TieneColliderTriggerEnRaiz())
        {
            Debug.LogWarning($"[ItemRecogible] '{name}' requiere un Collider 3D en la raíz marcado como Is Trigger.", this);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        IntentarRecogerDesdeCollider(other);
    }

    private void OnTriggerStay(Collider other)
    {
        IntentarRecogerDesdeCollider(other);
    }

    private void IntentarRecogerDesdeCollider(Collider other)
    {
        if (recogido || procesandoRecogida || !isActiveAndEnabled || itemDatos == null || other == null)
        {
            return;
        }

        InventarioPersonaje inventario = other.GetComponentInParent<InventarioPersonaje>();
        if (inventario == null || !inventario.gameObject.activeInHierarchy)
        {
            return;
        }

        if (!EstaVivo(inventario))
        {
            return;
        }

        procesandoRecogida = true;
        bool agregado;
        try
        {
            agregado = inventario.IntentarAgregar(itemDatos);
        }
        finally
        {
            procesandoRecogida = false;
        }

        if (!agregado)
        {
            return;
        }

        recogido = true;
        DesactivarColliders();
        Destroy(gameObject);
    }

    private static bool EstaVivo(InventarioPersonaje inventario)
    {
        SkeletonHealth salud = inventario.GetComponent<SkeletonHealth>();
        if (salud == null)
        {
            salud = inventario.GetComponentInParent<SkeletonHealth>();
        }
        if (salud == null)
        {
            salud = inventario.GetComponentInChildren<SkeletonHealth>();
        }

        return salud != null && salud.vidas > 0;
    }

    private bool TieneColliderTriggerEnRaiz()
    {
        Collider[] colliders = GetComponents<Collider>();
        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i].isTrigger)
            {
                return true;
            }
        }

        return false;
    }

    private void DesactivarColliders()
    {
        Collider[] colliders = GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            colliders[i].enabled = false;
        }
    }
}
using System.Collections.Generic;
using UnityEngine;

public class UsoLlaves : MonoBehaviour
{
    [Header("Detección de cerraduras")]
    [SerializeField, Min(0.01f)] private float alcance = 2f;
    [SerializeField] private LayerMask capasCerraduras = ~0;

    public float Alcance => alcance;

    private void OnValidate()
    {
        if (alcance < 0.01f)
        {
            alcance = 0.01f;
        }
    }

    public bool IntentarUsar(
        GameObject personaje,
        InventarioPersonaje inventario,
        int indiceRanura
    )
    {
        if (personaje == null || !personaje.activeInHierarchy)
        {
            return false;
        }

        if (inventario == null || !inventario.isActiveAndEnabled)
        {
            return false;
        }

        if (!EstaVivo(personaje))
        {
            return false;
        }

        ItemDatos item = inventario.ObtenerItem(indiceRanura);
        if (item == null || item.Tipo != TipoItem.Llave)
        {
            return false;
        }

        Vector3 origenBusqueda = ObtenerOrigenBusqueda(personaje);
        PuertasLlaves cerraduraMasCercana = BuscarCerraduraCerradaMasCercana(origenBusqueda);
        if (cerraduraMasCercana == null)
        {
            return false;
        }

        return cerraduraMasCercana.IntentarAbrir(inventario, indiceRanura);
    }

    private PuertasLlaves BuscarCerraduraCerradaMasCercana(Vector3 origenBusqueda)
    {
        Collider[] colliders = Physics.OverlapSphere(
            origenBusqueda,
            alcance,
            capasCerraduras,
            QueryTriggerInteraction.Collide
        );

        HashSet<PuertasLlaves> cerradurasEvaluadas = new HashSet<PuertasLlaves>();
        PuertasLlaves masCercana = null;
        float menorDistanciaCuadrada = float.PositiveInfinity;

        for (int i = 0; i < colliders.Length; i++)
        {
            Collider col = colliders[i];
            if (col == null)
            {
                continue;
            }

            PuertasLlaves cerradura = col.GetComponentInParent<PuertasLlaves>();
            if (cerradura == null || !cerradura.isActiveAndEnabled)
            {
                continue;
            }

            if (!cerradurasEvaluadas.Add(cerradura))
            {
                continue;
            }

            if (cerradura.Abierto)
            {
                continue;
            }

            float distanciaCuadrada = (cerradura.transform.position - origenBusqueda).sqrMagnitude;
            if (distanciaCuadrada < menorDistanciaCuadrada)
            {
                menorDistanciaCuadrada = distanciaCuadrada;
                masCercana = cerradura;
            }
        }

        return masCercana;
    }

    private static Vector3 ObtenerOrigenBusqueda(GameObject personaje)
    {
        CharacterController cc = personaje.GetComponent<CharacterController>();
        if (cc != null)
        {
            return personaje.transform.TransformPoint(cc.center);
        }

        return personaje.transform.position;
    }

    private static bool EstaVivo(GameObject personaje)
    {
        SkeletonHealth salud = personaje.GetComponent<SkeletonHealth>();
        if (salud == null)
        {
            salud = personaje.GetComponentInParent<SkeletonHealth>();
        }
        if (salud == null)
        {
            salud = personaje.GetComponentInChildren<SkeletonHealth>();
        }

        return salud != null && salud.vidas > 0;
    }
}
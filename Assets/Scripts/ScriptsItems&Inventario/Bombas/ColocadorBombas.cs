using UnityEngine;

public class ColocadorBombas : MonoBehaviour
{
    [Header("Prefab")]
    [SerializeField] private BombaColocada prefabBombaColocada;

    [Header("Colocación y comprobación")]
    [SerializeField, Min(0f)] private float desplazamientoFrontal = 0.7f;
    [SerializeField, Min(0f)] private float desplazamientoVertical = 0.35f;
    [SerializeField, Min(0.05f)] private float radioComprobacion = 0.22f;
    [SerializeField] private LayerMask capasObstaculos = ~0;

    private void OnValidate()
    {
        if (desplazamientoFrontal < 0f)
        {
            desplazamientoFrontal = 0f;
        }

        if (desplazamientoVertical < 0f)
        {
            desplazamientoVertical = 0f;
        }

        if (radioComprobacion < 0.05f)
        {
            radioComprobacion = 0.05f;
        }
    }

    public bool IntentarColocar(
        GameObject personaje,
        InventarioPersonaje inventario,
        int indiceRanura
    )
    {
        if (prefabBombaColocada == null)
        {
            Debug.LogWarning("[ColocadorBombas] No hay ningún prefab de BombaColocada asignado.", this);
            return false;
        }

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
        if (item == null || item.Tipo != TipoItem.Bomba)
        {
            return false;
        }

        CalcularPuntosColocacion(personaje, out Vector3 origenHorizontal, out Vector3 posicionColocacion);

        if (!EsUbicacionValida(personaje, origenHorizontal, posicionColocacion))
        {
            return false;
        }

        BombaColocada bomba = Instantiate(prefabBombaColocada, posicionColocacion, Quaternion.identity);
        if (bomba == null)
        {
            return false;
        }

        if (!inventario.IntentarQuitar(indiceRanura))
        {
            bomba.gameObject.SetActive(false);
            Destroy(bomba.gameObject);
            return false;
        }

        bomba.Armar(personaje);
        return true;
    }

    private void CalcularPuntosColocacion(
        GameObject personaje,
        out Vector3 origenHorizontal,
        out Vector3 posicionColocacion
    )
    {
        Transform t = personaje.transform;
        Vector3 basePies = t.position;

        CharacterController cc = personaje.GetComponent<CharacterController>();
        if (cc != null)
        {
            Vector3 centroMundo = t.TransformPoint(cc.center);
            float mitadAltura = Mathf.Max(cc.height * 0.5f, cc.radius) * Mathf.Abs(t.lossyScale.y);
            basePies = new Vector3(centroMundo.x, centroMundo.y - mitadAltura, centroMundo.z);
        }

        Vector3 frente = t.forward;
        frente.y = 0f;
        if (frente.sqrMagnitude > 0.0001f)
        {
            frente.Normalize();
        }
        else
        {
            frente = Vector3.forward;
        }

        origenHorizontal = basePies + Vector3.up * desplazamientoVertical;
        posicionColocacion = origenHorizontal + frente * desplazamientoFrontal;
    }

    private bool EsUbicacionValida(
        GameObject personaje,
        Vector3 origenHorizontal,
        Vector3 posicionColocacion
    )
    {
        Transform raizPersonaje = personaje.transform;

        Vector3 desplazamiento = posicionColocacion - origenHorizontal;
        float distancia = desplazamiento.magnitude;
        if (distancia > 0.0001f)
        {
            RaycastHit[] impactos = Physics.RaycastAll(
                origenHorizontal,
                desplazamiento / distancia,
                distancia,
                capasObstaculos,
                QueryTriggerInteraction.Ignore
            );

            for (int i = 0; i < impactos.Length; i++)
            {
                Collider col = impactos[i].collider;
                if (col != null && !col.transform.IsChildOf(raizPersonaje))
                {
                    return false;
                }
            }
        }

        Collider[] obstaculos = Physics.OverlapSphere(
            posicionColocacion,
            radioComprobacion,
            capasObstaculos,
            QueryTriggerInteraction.Ignore
        );

        for (int i = 0; i < obstaculos.Length; i++)
        {
            Collider col = obstaculos[i];
            if (col != null && !col.transform.IsChildOf(raizPersonaje))
            {
                return false;
            }
        }

        return true;
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
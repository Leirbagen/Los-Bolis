using System.Collections.Generic;
using UnityEngine;

public class BombaColocada : MonoBehaviour
{
    [Header("Configuración de explosión")]
    [SerializeField, Min(0f)] private float tiempoMecha = 2.5f;
    [SerializeField, Min(0.01f)] private float radioExplosion = 2.5f;
    [SerializeField, Min(0)] private int dano = 1;
    [SerializeField] private bool afectaJugadores = true;
    [SerializeField] private LayerMask capasAfectadas = ~0;

    [Header("Efecto visual opcional")]
    [SerializeField] private GameObject prefabEfectoVisual;

    private bool armada;
    private bool explotada;
    private float tiempoRestante;

    public bool Armada => armada;
    public bool Explotada => explotada;
    public float TiempoRestante => tiempoRestante;

    private void OnValidate()
    {
        if (tiempoMecha < 0f)
        {
            tiempoMecha = 0f;
        }

        if (radioExplosion < 0.01f)
        {
            radioExplosion = 0.01f;
        }

        if (dano < 0)
        {
            dano = 0;
        }
    }

    public void Armar(GameObject dueno)
    {
        if (armada || explotada)
        {
            return;
        }

        IgnorarColisionesConDueno(dueno);

        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null && !rb.isKinematic)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        tiempoRestante = tiempoMecha;
        armada = true;
    }

    private void Update()
    {
        if (!armada || explotada)
        {
            return;
        }

        tiempoRestante -= Time.deltaTime;
        if (tiempoRestante <= 0f)
        {
            Explotar();
        }
    }

    private void Explotar()
    {
        if (explotada)
        {
            return;
        }

        explotada = true;
        armada = false;

        Vector3 origenExplosion = transform.position;

        if (prefabEfectoVisual != null)
        {
            Instantiate(prefabEfectoVisual, origenExplosion, Quaternion.identity);
        }

        if (dano > 0)
        {
            Collider[] collidersDetectados = Physics.OverlapSphere(
                origenExplosion,
                radioExplosion,
                capasAfectadas,
                QueryTriggerInteraction.Ignore
            );

            HashSet<SkeletonHealth> jugadoresAfectados = new HashSet<SkeletonHealth>();
            HashSet<DestructiblePorBomba> destructiblesAfectados = new HashSet<DestructiblePorBomba>();

            for (int i = 0; i < collidersDetectados.Length; i++)
            {
                Collider col = collidersDetectados[i];
                if (col == null || col.transform.IsChildOf(transform))
                {
                    continue;
                }

                if (afectaJugadores)
                {
                    SkeletonHealth salud = col.GetComponentInParent<SkeletonHealth>();
                    if (salud != null && salud.vidas > 0 && jugadoresAfectados.Add(salud))
                    {
                        salud.RecibirDano(dano);
                    }
                }

                DestructiblePorBomba destructible = col.GetComponentInParent<DestructiblePorBomba>();
                if (destructible != null && !destructible.EstaDestruido && destructiblesAfectados.Add(destructible))
                {
                    destructible.RecibirExplosion(dano);
                }
            }
        }

        Destroy(gameObject);
    }

    private void IgnorarColisionesConDueno(GameObject dueno)
    {
        if (dueno == null)
        {
            return;
        }

        Collider[] collidersBomba = GetComponentsInChildren<Collider>(true);
        Collider[] collidersDueno = dueno.GetComponentsInChildren<Collider>(true);

        for (int i = 0; i < collidersBomba.Length; i++)
        {
            Collider colBomba = collidersBomba[i];
            if (colBomba == null || !colBomba.enabled || !colBomba.gameObject.activeInHierarchy)
            {
                continue;
            }

            for (int j = 0; j < collidersDueno.Length; j++)
            {
                Collider colDueno = collidersDueno[j];
                if (colDueno == null || !colDueno.enabled || !colDueno.gameObject.activeInHierarchy)
                {
                    continue;
                }

                Physics.IgnoreCollision(colBomba, colDueno, true);
            }
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.35f, 0f, 0.85f);
        Gizmos.DrawWireSphere(transform.position, radioExplosion);
    }
}
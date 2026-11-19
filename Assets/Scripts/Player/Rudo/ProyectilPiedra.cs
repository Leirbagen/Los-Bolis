using UnityEngine;

public class ProyectilPiedra : MonoBehaviour
{
    [Header("Detección y Efecto Post-Impacto")]
    public float radioColision = 0.08f;
    public bool caerConFisicaAlChocar = true;

    private Vector3 velocidadActual;
    private float distanciaRecorrida = 0f;
    private float distanciaSinGravedad;
    private float gravedad;
    private Transform dueñoDisparo;
    private bool inicializado = false;
    private bool haImpactado = false;

    public void Inicializar(Vector3 direccion, float velocidad, float distRecta, float grav, Transform dueño)
    {
        velocidadActual = direccion * velocidad;
        distanciaSinGravedad = distRecta;
        gravedad = grav;
        dueñoDisparo = dueño;
        inicializado = true;

        Destroy(gameObject, 6f);
    }

    private void Update()
    {
        if (!inicializado || haImpactado) return;

        Vector3 posicionActual = transform.position;
        float delta = Time.deltaTime;

        // La piedra viaja 100% en línea recta hasta alcanzar la distancia exacta donde está tu mira.
        // Recién cuando pasa ese punto , empieza a curvarse y caer.
        if (distanciaRecorrida >= distanciaSinGravedad)
        {
            velocidadActual += Vector3.down * (gravedad * delta);
        }

        Vector3 desplazamiento = velocidadActual * delta;
        float distanciaPaso = desplazamiento.magnitude;

        if (distanciaPaso > 0.0001f)
        {
            // Usamos RaycastAll/SphereCastAll para asegurarnos de que jamás choque con el cuerpo de Rudo al salir
            RaycastHit[] hits = Physics.SphereCastAll(posicionActual, radioColision, desplazamiento.normalized, distanciaPaso, ~0, QueryTriggerInteraction.Ignore);

            RaycastHit impactoValido = new RaycastHit();
            float menorDist = Mathf.Infinity;
            bool choco = false;

            foreach (RaycastHit hit in hits)
            {
                if (dueñoDisparo != null && hit.transform.IsChildOf(dueñoDisparo)) continue;
                if (hit.collider.CompareTag("Player")) continue;

                if (hit.distance < menorDist)
                {
                    menorDist = hit.distance;
                    impactoValido = hit;
                    choco = true;
                }
            }

            if (choco)
            {
                ProcesarImpacto(impactoValido);
                return;
            }
        }

        transform.position = posicionActual + desplazamiento;
        if (velocidadActual.sqrMagnitude > 0.01f)
        {
            transform.rotation = Quaternion.LookRotation(velocidadActual.normalized);
        }
        distanciaRecorrida += distanciaPaso;
    }

    private void ProcesarImpacto(RaycastHit hit)
    {
        haImpactado = true;
        transform.position = hit.point + (hit.normal * radioColision);

        if (hit.collider.CompareTag("Enemigo"))
        {
            Debug.Log("¡Impacto clavado en la mira contra: " + hit.collider.name + "!");
        }
        else if (hit.collider.CompareTag("Switch"))
        {
            Debug.Log("¡Interruptor activado!");
        }

        if (caerConFisicaAlChocar)
        {
            SphereCollider col = gameObject.AddComponent<SphereCollider>();
            col.radius = radioColision;

            Rigidbody rb = gameObject.AddComponent<Rigidbody>();
            rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
            rb.linearVelocity = Vector3.Reflect(velocidadActual.normalized, hit.normal) * 2.5f;

            Destroy(gameObject, 2f);
        }
        else
        {
            Destroy(gameObject);
        }
    }
}
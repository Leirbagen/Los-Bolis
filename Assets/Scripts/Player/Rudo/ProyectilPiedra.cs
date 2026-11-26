using UnityEngine;

public class ProyectilPiedra : MonoBehaviour
{
    [Header("Detección de Impacto")]
    public float radioColision = 0.08f;

    public GameObject prefabEfectoImpacto;

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

        if (distanciaRecorrida >= distanciaSinGravedad)
        {
            velocidadActual += Vector3.down * (gravedad * delta);
        }

        Vector3 desplazamiento = velocidadActual * delta;
        float distanciaPaso = desplazamiento.magnitude;

        if (distanciaPaso > 0.0001f)
        {
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

        if (prefabEfectoImpacto != null)
        {
            Quaternion rotacionImpacto = Quaternion.LookRotation(hit.normal);
            Instantiate(prefabEfectoImpacto, hit.point, rotacionImpacto);
        }

        SwitchInteractivo switchGolpeado = hit.collider.GetComponentInParent<SwitchInteractivo>();
        if (switchGolpeado != null && switchGolpeado.tipo == SwitchInteractivo.TipoSwitch.Boton_O_Palanca)
        {
            switchGolpeado.IntentarAlternar();
        }

        if (hit.collider.CompareTag("Enemigo"))
        {
            Debug.Log("Impacto : " + hit.collider.name);
        }

        Destroy(gameObject);
    }
}
using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
public class ArmaDistancia : MonoBehaviour
{
    [Header("Configuración de Disparo")]
    public GameObject prefabProyectil;
    public float fuerzaDisparo = 18f;
    public float inclinacionArriba = 0.25f;

    [Header("Configuración de Línea")]
    public int puntosLinea = 30;
    public float separacionTiempo = 0.05f;

    private LineRenderer lineaTrayectoria;
    private Transform camaraPrincipal;

    private void Awake()
    {
        lineaTrayectoria = GetComponent<LineRenderer>();
        camaraPrincipal = Camera.main.transform;
        lineaTrayectoria.enabled = false;
    }

    private void OnDisable()
    {
        if (lineaTrayectoria != null) lineaTrayectoria.enabled = false;
    }

    public Vector3 CalcularVelocidadInicial()
    {
        Ray rayoCentro = new Ray(camaraPrincipal.position, camaraPrincipal.forward);
        Vector3 puntoObjetivo;

        if (Physics.Raycast(rayoCentro, out RaycastHit hit, 50f))
        {
            puntoObjetivo = hit.point;
        }
        else
        {
            puntoObjetivo = camaraPrincipal.position + camaraPrincipal.forward * 30f;
        }

        Vector3 direccionHaciaMira = (puntoObjetivo - transform.position).normalized;

        Vector3 direccionFinal = (direccionHaciaMira + Vector3.up * inclinacionArriba).normalized;
        return direccionFinal * fuerzaDisparo;
    }

    public void MostrarTrayectoria(bool estado)
    {
        if (lineaTrayectoria == null) return;

        lineaTrayectoria.enabled = estado;
        if (!estado) return;

        lineaTrayectoria.positionCount = puntosLinea;
        Vector3 velocidadInicial = CalcularVelocidadInicial();
        Vector3 posicionOrigen = transform.position; // Sale directo desde la honda en la mano

        for (int i = 0; i < puntosLinea; i++)
        {
            float t = i * separacionTiempo;
            Vector3 puntoEnCurva = posicionOrigen + (velocidadInicial * t) + (0.5f * Physics.gravity * (t * t));
            lineaTrayectoria.SetPosition(i, puntoEnCurva);
        }
    }

    public void Disparar()
    {
        if (prefabProyectil == null) return;

        GameObject proyectil = Instantiate(prefabProyectil, transform.position, Quaternion.identity);
        Collider colProyectil = proyectil.GetComponent<Collider>();
        CharacterController colRudo = GetComponentInParent<CharacterController>();
        if (colProyectil != null && colRudo != null)
        {
            Physics.IgnoreCollision(colProyectil, colRudo);
        }

        Rigidbody rb = proyectil.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.linearVelocity = CalcularVelocidadInicial();
        }
    }
}
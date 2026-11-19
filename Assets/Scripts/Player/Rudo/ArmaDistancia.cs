using UnityEngine;

public class ArmaDistancia : MonoBehaviour
{
    [Header("Configuración de Disparo (Precisión Total a la Mira)")]
    public GameObject prefabProyectil;
    public float velocidadDisparo = 45f;
    public float distanciaRecta = 35f;
    public float gravedadCaida = 20f;

    [Header("Cadencia de Tiro (Cooldown)")]
    public float tiempoEntreDisparos = 1.5f;  // Segundos de espera entre piedra y piedra
    private float proximoDisparoPermitido = 0f;

    private Camera camaraComp;
    private Transform raizJugador;

    private void Awake()
    {
        camaraComp = Camera.main;
        raizJugador = transform.root;
    }

    public void Disparar()
    {
         if (Time.time < proximoDisparoPermitido) return;

        if (prefabProyectil == null || camaraComp == null) return;

        // Registramos el momento exacto en el que podrá volver a disparar
        proximoDisparoPermitido = Time.time + tiempoEntreDisparos;

        //Rayo matemático exacto desde el centro (0.5, 0.5) de la pantalla
        Ray rayoCentroPantalla = camaraComp.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));

        Vector3 puntoObjetivo = rayoCentroPantalla.GetPoint(distanciaRecta);
        float distanciaViajeRecto = distanciaRecta;

        // Buscamos qué hay bajo la mira ignorando al propio Rudo
        RaycastHit[] impactos = Physics.RaycastAll(rayoCentroPantalla, distanciaRecta, ~0, QueryTriggerInteraction.Ignore);
        float menorDistancia = Mathf.Infinity;

        foreach (RaycastHit hit in impactos)
        {
            if (raizJugador != null && hit.transform.IsChildOf(raizJugador)) continue;
            if (hit.distance < 1.5f) continue;

            if (hit.distance < menorDistancia)
            {
                menorDistancia = hit.distance;
                puntoObjetivo = hit.point;
            }
        }

        // Calculamos el vector directo desde la mano hasta el punto bajo la mira
        Vector3 vectorHaciaMira = puntoObjetivo - transform.position;
        Vector3 direccionDisparo = vectorHaciaMira.normalized;
        distanciaViajeRecto = vectorHaciaMira.magnitude;

        // Instanciamos y lanzamos la piedra
        GameObject proyectilObj = Instantiate(prefabProyectil, transform.position, Quaternion.LookRotation(direccionDisparo));
        ProyectilPiedra scriptPiedra = proyectilObj.GetComponent<ProyectilPiedra>();

        if (scriptPiedra != null)
        {
            scriptPiedra.Inicializar(direccionDisparo, velocidadDisparo, distanciaViajeRecto, gravedadCaida, raizJugador);
        }
    }
}
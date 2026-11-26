using UnityEngine;

[RequireComponent(typeof(Collider))]
public class ParedElectrica : MonoBehaviour
{
    [Header("1. Configuración de la Pared")]
    [Tooltip("Define si la pared inicia encendida o apagada al empezar el nivel")]
    public bool estaEncendida = true;
    [Tooltip("Daño masivo para asegurar muerte instantánea sin importar cuánta vida tenga el personaje")]
    public int danoLetal = 9999;

    [Header("2. Referencias Visuales y de Audio (Opcional)")]
    [Tooltip("Arrastra aquí el objeto hijo con los rayos/malla eléctrica para ocultarlo al desactivar")]
    public GameObject efectoElectricidad;
    [Tooltip("Opcional: sonido eléctrico en bucle")]
    public AudioSource sonidoElectricidad;

    private Collider colliderLetal;
    private Renderer rendererPropio;

    private void Awake()
    {
        colliderLetal = GetComponent<Collider>();
        rendererPropio = GetComponent<Renderer>();

        // Aseguramos que sea Trigger para que mate al contacto y Ágil no rebote como si fuera muro sólido
        colliderLetal.isTrigger = true;
    }

    private void Start()
    {
        AplicarEstado(estaEncendida);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!estaEncendida) return;

        // Solo afecta a los personajes (ignora proyectiles o el SensorCasco)
        if (!other.CompareTag("Player")) return;

        MatarPersonaje(other.gameObject);
    }

    private void MatarPersonaje(GameObject jugador)
    {
        // 1. Si es Ágil, llama directamente a su función de daño letal
        AgilController agil = jugador.GetComponent<AgilController>();
        if (agil != null)
        {
            agil.RecibirDaño(danoLetal);
            Debug.Log(jugador.name + " murió electrocutado por " + gameObject.name);
            return;
        }

        // 2. Para Rudo o Fuerte: busca cualquier script en el personaje que tenga el método RecibirDaño(int)
        jugador.SendMessage("RecibirDaño", danoLetal, SendMessageOptions.DontRequireReceiver);
        Debug.Log(jugador.name + " tocó la pared eléctrica " + gameObject.name);
    }

    // --- FUNCIONES PÚBLICAS PARA CONECTAR CON EL SWITCH ---

    public void DesactivarPared()
    {
        AplicarEstado(false);
    }

    public void ActivarPared()
    {
        AplicarEstado(true);
    }

    public void AlternarPared()
    {
        AplicarEstado(!estaEncendida);
    }

    private void AplicarEstado(bool encender)
    {
        estaEncendida = encender;

        if (colliderLetal != null)
        {
            colliderLetal.enabled = estaEncendida;
        }

        // Si pusiste un objeto hijo con los rayos, apaga ese hijo; si no, apaga el cubo principal
        if (efectoElectricidad != null)
        {
            efectoElectricidad.SetActive(estaEncendida);
        }
        else if (rendererPropio != null)
        {
            rendererPropio.enabled = estaEncendida;
        }

        if (sonidoElectricidad != null)
        {
            if (estaEncendida && !sonidoElectricidad.isPlaying) sonidoElectricidad.Play();
            else if (!estaEncendida && sonidoElectricidad.isPlaying) sonidoElectricidad.Stop();
        }
    }
}
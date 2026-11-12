using UnityEngine;

public class ItemVida : MonoBehaviour
{
    public int curacion = 1;

    private void OnTriggerEnter(Collider other)
    {
        // Buscamos el script de salud en el jugador que chocó con la fruta
        SkeletonHealth salud = other.GetComponentInParent<SkeletonHealth>();

        if (salud != null)
        {
            // Solo desaparece la fruta si el jugador realmente necesita curarse (tiene menos de 5)
            if (salud.vidas < salud.maxVidas)
            {
                salud.Curar(curacion);

                // Desactiva la fruta del escenario
                gameObject.SetActive(false);
            }
        }
    }
}
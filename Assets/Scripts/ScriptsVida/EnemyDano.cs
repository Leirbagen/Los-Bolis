using UnityEngine;

public class EnemyDano : MonoBehaviour
{
    public int dano = 1;

    // Cambiamos OnCollisionEnter por OnTriggerEnter para que funcione con Character Controllers
    private void OnTriggerEnter(Collider other)
    {
        // Busca el script SkeletonHealth en el objeto que entró, o en sus objetos superiores (padres)
        SkeletonHealth salud = other.GetComponentInParent<SkeletonHealth>();

        if (salud != null)
        {
            salud.RecibirDano(dano);
        }
    }
}
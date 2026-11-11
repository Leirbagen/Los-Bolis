using UnityEngine;

public class ProyectilPiedra : MonoBehaviour
{
    void Start()
    {
        Destroy(gameObject, 10f);
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("Player")) 
        {
            return;
        }
        if (collision.gameObject.CompareTag("Enemigo")) 
        {
            Debug.Log("Atacando a distancia a: " + collision.gameObject.name);
        }

        Destroy(gameObject);
    }


}

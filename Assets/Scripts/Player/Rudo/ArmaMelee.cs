using UnityEngine;

public class ArmaMelee : MonoBehaviour
{
    private BoxCollider hitbox;
    private bool puedeHacerDaño = false;

    private void Start()
    {
        hitbox = GetComponentInChildren<BoxCollider>();
        hitbox.isTrigger = true;
        hitbox.enabled = false;
    }

    public void ActivarHitbox(bool estado) 
    {
        puedeHacerDaño = estado;
        hitbox.enabled = estado;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Enemigo")) 
        {
            Debug.Log("haciendo daño a " + other.gameObject.name);
        }
    }
}

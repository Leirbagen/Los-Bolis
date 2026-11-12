using UnityEngine;
using UnityEngine.UIElements;

public class asensor : MonoBehaviour
{
    public Transform positionX;
    public float velocidadAscensor = 1;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {
        Subir();
    }
    void Subir()
    {
        transform.position = Vector3.MoveTowards(transform.position, positionX.position, velocidadAscensor * Time.deltaTime);
    }
}

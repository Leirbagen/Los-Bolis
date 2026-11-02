using StarterAssets;
using System.Collections;
using UnityEngine;

public class RudoController : ThirdPersonController
{
    [Header("Ataque Melee")]
    public Transform puntoGolpe;
    public Transform arma;
    public float radioGolpe = 1.5f;
    public LayerMask capasInteractuables; // tanto para los switches como para los enemigos
    public float duracionGolpe = 0.15f;
    public Vector3 anguloAtaque = new Vector3(90f, 0f, 0f);

    private bool estaAtacando = false;
    private Vector3 rotacionArma;

    protected override void Start()
    {
        base.Start();
        if (arma != null)
        {
            rotacionArma = arma.localEulerAngles;
        }
    }

    protected override void Update()
    {
        base.Update();

        if (Input.GetMouseButtonDown(0) && !estaAtacando && Grounded)
        {
            Debug.Log("¡Se presionó la tecla de ataque!");
            StartCoroutine(AtaqueMelee());
        }
    }

    private IEnumerator AtaqueMelee()
    {
        estaAtacando = true;

        Quaternion rotacionInicial = Quaternion.Euler(rotacionArma);
        Quaternion rotacionFinal = rotacionInicial * Quaternion.Euler(anguloAtaque);
        float tiempo = 0f;

        // la rotacion del arma simulando el ataque 
        while (tiempo < duracionGolpe)
        {
            tiempo += Time.deltaTime;
            if (arma != null) arma.localRotation = Quaternion.Slerp(rotacionInicial, rotacionFinal, tiempo / duracionGolpe);
            yield return null; // Espera al siguiente frame
        }
        arma.localRotation = rotacionFinal; // Asegura que llegue exactamente a la posición final

        Collider[] hits = Physics.OverlapSphere(puntoGolpe.position, radioGolpe, capasInteractuables);
        foreach (var hit in hits)
        {
            // aca podria estar la logica de implementar para los switches, si detecta que el hit tiene quizas una interfaz de daño para los enemigos
            // o para los switches, por ahora no. 
        }

        yield return new WaitForSeconds(0.1f); // Pequeña pausa con el arma 

        tiempo = 0f;
        // Subiendo el arma a su posición original
        while (tiempo < duracionGolpe)
        {
            tiempo += Time.deltaTime;
            if (arma != null) arma.localRotation = Quaternion.Slerp(rotacionFinal, rotacionInicial, tiempo / duracionGolpe);
            yield return null;
        }
        if (arma != null) arma.localRotation = rotacionInicial;

        yield return new WaitForSeconds(0.5f);
        estaAtacando = false;
    }

    private void OnDrawGizmosSelected()
    {
        // Dibuja una esfera roja en el editor para visualizar el área de daño
        if (puntoGolpe != null)
        {
            Gizmos.color = new Color(1, 0, 0, 0.5f);
            Gizmos.DrawSphere(puntoGolpe.position, radioGolpe);
        }
    }
}
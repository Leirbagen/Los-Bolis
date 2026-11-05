using UnityEngine;

[RequireComponent(typeof(BoxCollider))]
public class SensorCascoAgil : MonoBehaviour
{
    private BoxCollider hitboxCasco;
    private AgilController controladorAgil;

    public void Inicializar(AgilController agil)
    {
        controladorAgil = agil;
        hitboxCasco = GetComponent<BoxCollider>();
        hitboxCasco.isTrigger = true;
        hitboxCasco.enabled = false;
    }

    public void ActivarSensor(bool estado)
    {
        if (hitboxCasco == null) hitboxCasco = GetComponent<BoxCollider>();
        if (hitboxCasco != null) hitboxCasco.enabled = estado;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (controladorAgil == null) return;
        if (other.transform.root == controladorAgil.transform.root) return;

        // Calculamos la dirección del impacto hacia el objeto chocado
        Vector3 puntoMasCercano = other.ClosestPoint(transform.position);
        Vector3 normalAproximada = (transform.position - puntoMasCercano).normalized;
        if (normalAproximada == Vector3.zero) normalAproximada = -controladorAgil.transform.forward;

        controladorAgil.EvaluarImpactoEmbestida(other, puntoMasCercano, normalAproximada);
    }
}
using UnityEngine;
using UnityEngine.Events;

public class DestructiblePorBomba : MonoBehaviour
{
    [SerializeField, Min(1)] private int resistencia = 1;
    [SerializeField] private UnityEvent alDestruirse;

    private bool destruido;

    public int ResistenciaActual => resistencia;
    public bool EstaDestruido => destruido;

    private void OnValidate()
    {
        if (resistencia < 1)
        {
            resistencia = 1;
        }
    }

    public void RecibirExplosion(int dano)
    {
        if (destruido || dano <= 0)
        {
            return;
        }

        resistencia -= dano;
        if (resistencia > 0)
        {
            return;
        }

        resistencia = 0;
        destruido = true;
        alDestruirse?.Invoke();
        Destroy(gameObject);
    }
}
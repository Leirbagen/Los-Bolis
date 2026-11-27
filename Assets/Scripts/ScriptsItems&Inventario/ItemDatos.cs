using UnityEngine;

public enum TipoItem
{
    Llave,
    Comida,
    Bomba
}

[CreateAssetMenu(fileName = "NuevoItem", menuName = "Los Bolis/Inventario/Item")]
public class ItemDatos : ScriptableObject
{
    [SerializeField] private string id;
    [SerializeField] private string nombreVisible;
    [SerializeField] private Sprite icono;
    [SerializeField] private TipoItem tipo;
    [SerializeField, Min(0)] private int cantidadCuracion;

    public string Id => id;
    public string NombreVisible => nombreVisible;
    public Sprite Icono => icono;
    public TipoItem Tipo => tipo;
    public int CantidadCuracion => tipo == TipoItem.Comida ? cantidadCuracion : 0;

    private void OnValidate()
    {
        if (cantidadCuracion < 0)
        {
            cantidadCuracion = 0;
        }
    }
}
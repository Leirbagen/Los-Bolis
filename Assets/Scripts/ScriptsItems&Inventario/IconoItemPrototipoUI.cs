using UnityEngine;
using UnityEngine.UI;

// Iconos temporales hechos con rectangulos de UI. Un Sprite de ItemDatos los sustituye.
[AddComponentMenu("Los Bolis/UI/Icono de item de prototipo")]
[RequireComponent(typeof(CanvasRenderer))]
public class IconoItemPrototipoUI : MaskableGraphic
{
    [SerializeField] private TipoItem tipo;

    public void Mostrar(TipoItem nuevoTipo)
    {
        if (tipo == nuevoTipo) return;
        tipo = nuevoTipo;
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        Color oscuro = new Color32(49, 28, 22, 255);
        if (tipo == TipoItem.Comida)
        {
            Cuadro(vh, 7, 2, 11, 2, oscuro);
            Cuadro(vh, 4, 4, 16, 3, oscuro);
            Cuadro(vh, 2, 7, 20, 9, oscuro);
            Cuadro(vh, 4, 16, 16, 4, oscuro);
            Cuadro(vh, 6, 4, 12, 14, new Color32(210, 43, 26, 255));
            Cuadro(vh, 4, 7, 16, 9, new Color32(226, 55, 30, 255));
            Cuadro(vh, 6, 15, 5, 3, new Color32(239, 76, 37, 255));
            Cuadro(vh, 6, 11, 2, 4, new Color32(255, 164, 74, 255));
            Cuadro(vh, 11, 18, 2, 5, new Color32(112, 67, 31, 255));
            Cuadro(vh, 13, 21, 6, 2, new Color32(75, 156, 55, 255));
        }
        else if (tipo == TipoItem.Llave)
        {
            Color rojo = new Color32(227, 60, 48, 255);
            Color sombra = new Color32(114, 28, 22, 255);
            Cuadro(vh, 7, 13, 10, 2, sombra);
            Cuadro(vh, 7, 21, 10, 2, sombra);
            Cuadro(vh, 5, 15, 2, 6, sombra);
            Cuadro(vh, 17, 15, 2, 6, sombra);
            Cuadro(vh, 7, 15, 10, 2, rojo);
            Cuadro(vh, 7, 19, 10, 2, rojo);
            Cuadro(vh, 7, 17, 3, 2, rojo);
            Cuadro(vh, 14, 17, 3, 2, rojo);
            Cuadro(vh, 10, 2, 4, 13, sombra);
            Cuadro(vh, 11, 3, 2, 12, rojo);
            Cuadro(vh, 13, 3, 5, 3, rojo);
            Cuadro(vh, 13, 8, 4, 3, rojo);
        }
        else
        {
            Cuadro(vh, 6, 2, 12, 2, Color.black);
            Cuadro(vh, 3, 5, 18, 12, Color.black);
            Cuadro(vh, 6, 3, 12, 15, new Color32(61, 63, 65, 255));
            Cuadro(vh, 5, 7, 14, 8, new Color32(74, 77, 80, 255));
            Cuadro(vh, 7, 12, 3, 3, new Color32(149, 153, 155, 255));
            Cuadro(vh, 11, 18, 3, 3, new Color32(139, 94, 39, 255));
            Cuadro(vh, 14, 20, 5, 2, new Color32(226, 165, 58, 255));
        }
    }

    private void Cuadro(VertexHelper vh, float x, float y, float ancho, float alto, Color tono)
    {
        Rect rect = GetPixelAdjustedRect();
        float unidad = Mathf.Min(rect.width, rect.height) / 24f;
        Vector2 origen = rect.center - Vector2.one * (12f * unidad);
        int inicio = vh.currentVertCount;
        Color tinte = tono * color;
        vh.AddVert(origen + new Vector2(x, y) * unidad, tinte, Vector2.zero);
        vh.AddVert(origen + new Vector2(x, y + alto) * unidad, tinte, Vector2.zero);
        vh.AddVert(origen + new Vector2(x + ancho, y + alto) * unidad, tinte, Vector2.zero);
        vh.AddVert(origen + new Vector2(x + ancho, y) * unidad, tinte, Vector2.zero);
        vh.AddTriangle(inicio, inicio + 1, inicio + 2);
        vh.AddTriangle(inicio, inicio + 2, inicio + 3);
    }
}

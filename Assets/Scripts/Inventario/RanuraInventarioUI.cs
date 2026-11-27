using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class RanuraInventarioUI : MonoBehaviour
{
    [SerializeField] private Image imagenIcono;
    [SerializeField] private TMP_Text textoNombre;
    [SerializeField] private GameObject indicadorSeleccion;
    [SerializeField] private IconoItemPrototipoUI iconoPrototipo;
    [SerializeField] private bool mostrarNombre = false;

    [Tooltip("Opcional: marco o resaltado adicional que solo se muestra cuando el menú de gestión pausado está abierto en el personaje activo.")]
    [SerializeField] private GameObject indicadorModoGestion;

    [SerializeField] private string textoVacio = "";

    public void Configurar(ItemDatos item, bool estaSeleccionada, bool resaltarModoGestion = false)
    {
        if (indicadorSeleccion != null)
        {
            indicadorSeleccion.SetActive(estaSeleccionada);
        }

        if (indicadorModoGestion != null)
        {
            indicadorModoGestion.SetActive(false);
        }

        bool usarIconoPrototipo = item != null && item.Icono == null && iconoPrototipo != null;
        if (iconoPrototipo != null)
        {
            iconoPrototipo.gameObject.SetActive(usarIconoPrototipo);
            if (usarIconoPrototipo) iconoPrototipo.Mostrar(item.Tipo);
        }
        if (textoNombre != null) textoNombre.gameObject.SetActive(mostrarNombre);

        if (item == null)
        {
            if (imagenIcono != null)
            {
                imagenIcono.sprite = null;
                imagenIcono.enabled = false;
            }

            if (textoNombre != null)
            {
                textoNombre.text = textoVacio;
            }

            return;
        }

        if (imagenIcono != null)
        {
            bool tieneIcono = item.Icono != null;
            imagenIcono.sprite = item.Icono;
            imagenIcono.enabled = tieneIcono;
        }

        if (textoNombre != null)
        {
            textoNombre.text = string.IsNullOrWhiteSpace(item.NombreVisible)
                ? item.name
                : item.NombreVisible;
        }
    }
}

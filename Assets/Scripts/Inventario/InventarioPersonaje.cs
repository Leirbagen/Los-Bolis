using System;
using UnityEngine;

public class InventarioPersonaje : MonoBehaviour
{
    public const int Capacidad = 4;

    [SerializeField] private ItemDatos[] espacios = new ItemDatos[Capacidad];
    [SerializeField, Range(0, Capacidad - 1)] private int indiceSeleccionado;

    private bool inicializado;

    public int IndiceSeleccionado
    {
        get
        {
            AsegurarInicializacion();
            return indiceSeleccionado;
        }
    }

    public ItemDatos ItemSeleccionado => ObtenerItem(IndiceSeleccionado);

    public event Action AlCambiar;

    private void Awake()
    {
        AsegurarInicializacion();
    }

    private void OnValidate()
    {
        if (espacios == null || espacios.Length != Capacidad)
        {
            Array.Resize(ref espacios, Capacidad);
        }

        indiceSeleccionado = Mathf.Clamp(indiceSeleccionado, 0, Capacidad - 1);
    }

    public ItemDatos ObtenerItem(int indice)
    {
        AsegurarInicializacion();

        if (!EsIndiceValido(indice))
        {
            return null;
        }

        return espacios[indice];
    }

    public bool IntentarAgregar(ItemDatos item)
    {
        AsegurarInicializacion();

        if (item == null)
        {
            return false;
        }

        for (int i = 0; i < Capacidad; i++)
        {
            if (espacios[i] == null)
            {
                espacios[i] = item;
                AlCambiar?.Invoke();
                return true;
            }
        }

        return false;
    }

    public bool IntentarSeleccionar(int indice)
    {
        AsegurarInicializacion();

        if (!EsIndiceValido(indice))
        {
            return false;
        }

        if (indiceSeleccionado == indice)
        {
            return true;
        }

        indiceSeleccionado = indice;
        AlCambiar?.Invoke();
        return true;
    }

    public bool IntentarQuitar(int indice)
    {
        AsegurarInicializacion();

        if (!EsIndiceValido(indice))
        {
            return false;
        }

        if (espacios[indice] == null)
        {
            return false;
        }

        espacios[indice] = null;
        AlCambiar?.Invoke();
        return true;
    }

    public bool IntentarTransferir(
        int indiceOrigen,
        InventarioPersonaje destino,
        int indiceDestino
    )
    {
        AsegurarInicializacion();

        if (destino == null || destino == this)
        {
            return false;
        }

        destino.AsegurarInicializacion();

        if (!EsIndiceValido(indiceOrigen) || !EsIndiceValido(indiceDestino))
        {
            return false;
        }

        ItemDatos item = espacios[indiceOrigen];
        if (item == null)
        {
            return false;
        }

        if (destino.espacios[indiceDestino] != null)
        {
            return false;
        }

        espacios[indiceOrigen] = null;
        destino.espacios[indiceDestino] = item;

        AlCambiar?.Invoke();
        destino.AlCambiar?.Invoke();
        return true;
    }

    private void AsegurarInicializacion()
    {
        if (inicializado)
        {
            return;
        }

        espacios = new ItemDatos[Capacidad];
        indiceSeleccionado = 0;
        inicializado = true;
    }

    private static bool EsIndiceValido(int indice)
    {
        return indice >= 0 && indice < Capacidad;
    }
}
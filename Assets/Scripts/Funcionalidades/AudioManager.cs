using UnityEngine;
using System.Collections.Generic;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instancia { get; private set; }

    [System.Serializable]
    public struct Sonido
    {
        public string nombre;
        public AudioClip clip;
        [Range(0f, 1f)] public float volumen;
    }

    [Header("Fuentes de Audio")]
    public AudioSource fuenteMusica;
    public AudioSource fuenteSFX;

    [Header("Listas de Sonidos")]
    public Sonido[] listaSFX;
    public Sonido[] listaMusica;

    private Dictionary<string, Sonido> diccionarioSFX = new Dictionary<string, Sonido>();
    private Dictionary<string, Sonido> diccionarioMusica = new Dictionary<string, Sonido>();

    private void Awake()
    {
        //singleton
        if (Instancia == null)
        {
            Instancia = this;
            DontDestroyOnLoad(gameObject); // Opcional: mantiene la música al cambiar de escena
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        // Si se olvida añadir los AudioSource en el Inspector, el script los crea automáticamente
        if (fuenteMusica == null)
        {
            fuenteMusica = gameObject.AddComponent<AudioSource>();
            fuenteMusica.loop = true;
            fuenteMusica.playOnAwake = false;
        }
        // Si se olvida añadir los AudioSource en el Inspector, el script los crea automáticamente
        if (fuenteSFX == null)
        {
            fuenteSFX = gameObject.AddComponent<AudioSource>();
            fuenteSFX.playOnAwake = false;
        }

        foreach (Sonido s in listaSFX)
        {
            if (!string.IsNullOrEmpty(s.nombre) && !diccionarioSFX.ContainsKey(s.nombre))
            {
                diccionarioSFX.Add(s.nombre, s);
            }
        }

        foreach (Sonido m in listaMusica)
        {
            if (!string.IsNullOrEmpty(m.nombre) && !diccionarioMusica.ContainsKey(m.nombre))
            {
                diccionarioMusica.Add(m.nombre, m);
            }
        }
    }

    // reproduce cualquier sonido que este ya dentro de la lista de los audios de aca.
    public void ReproducirSFX(string nombre)
    {
        if (diccionarioSFX.TryGetValue(nombre, out Sonido sonido) && sonido.clip != null)
        {
            float vol = sonido.volumen > 0f ? sonido.volumen : 1f;
            fuenteSFX.PlayOneShot(sonido.clip, vol);
        }
        else
        {
            Debug.Log("AudioManager: No se encontró el SFX llamado '" + nombre + "'");
        }
    }


    //reproduce un sonido cualquieraa que este dentro de un objeto. por ejemplo, en el script de una puerta se llamaria a este metodo
    // y se reproduciria el sonido que este asignado a esa puerta. (en su propio inspector). Para este el audio no es necesario que este en la lista
    //solo reproduce el clip que tenga asignado el objeto
    public void ReproducirSFX(AudioClip clip, float volumen = 1f)
    {
        fuenteSFX.PlayOneShot(clip, volumen);
    }

    // para la musica de fondo
    public void ReproducirMusica(string nombre)
    {
        if (diccionarioMusica.TryGetValue(nombre, out Sonido musica))
        {
            if (fuenteMusica.clip == musica.clip && fuenteMusica.isPlaying) return; // que no reproduzca nada por que ya esta sonando una. 
            fuenteMusica.clip = musica.clip;
            fuenteMusica.volume = musica.volumen > 0f ? musica.volumen : 1f;
            fuenteMusica.Play();
        }
        else
        {
            Debug.Log("AudioManager: No se encontró la música llamada '" + nombre + "'");
        }
    }

    public void DetenerMusica()
    {
        fuenteMusica.Stop();
    }
}
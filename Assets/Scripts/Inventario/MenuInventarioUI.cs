using System;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.UI;

public enum ModoSeleccionInventario
{
    ClasicoPausado,
    CiclicoRapido
}

[DefaultExecutionOrder(-100)]
public class MenuInventarioUI : MonoBehaviour
{
    [Serializable]
    private class FilaInventarioUI
    {
        [SerializeField] private string etiquetaPersonaje;
        [SerializeField] private TMP_Text textoNombrePersonaje;
        [SerializeField] private GameObject indicadorPersonajeActivo;
        [SerializeField] private RanuraInventarioUI[] ranuras = new RanuraInventarioUI[InventarioPersonaje.Capacidad];

        public void Actualizar(string nombreFallback, InventarioPersonaje inventario, bool esPersonajeActivo, bool enModoGestion)
        {
            if (indicadorPersonajeActivo != null)
            {
                indicadorPersonajeActivo.SetActive(esPersonajeActivo);
            }

            if (textoNombrePersonaje != null)
            {
                textoNombrePersonaje.text = string.IsNullOrWhiteSpace(etiquetaPersonaje)
                    ? nombreFallback
                    : etiquetaPersonaje;
            }

            if (ranuras == null)
            {
                return;
            }

            int limite = Mathf.Min(ranuras.Length, InventarioPersonaje.Capacidad);
            for (int i = 0; i < limite; i++)
            {
                RanuraInventarioUI ranura = ranuras[i];
                if (ranura == null)
                {
                    continue;
                }

                ItemDatos item = inventario != null ? inventario.ObtenerItem(i) : null;
                bool seleccionada = inventario != null && inventario.IndiceSeleccionado == i;
                bool resaltarGestion = esPersonajeActivo && enModoGestion;
                ranura.Configurar(item, seleccionada, resaltarGestion);
            }
        }

        public RanuraInventarioUI ObtenerRanura(int indiceRanura)
        {
            if (ranuras == null || indiceRanura < 0 || indiceRanura >= ranuras.Length)
            {
                return null;
            }

            return ranuras[indiceRanura];
        }
    }

    private const int CantidadFilasEsperadas = 3;
    private const int ColumnasPorPersonaje = 2;
    private const int FilasVisualesTransferencia = 2;

    [Header("Modo de selección")]
    [SerializeField] private ModoSeleccionInventario modoSeleccion = ModoSeleccionInventario.ClasicoPausado;
    [SerializeField] private bool saltarEspaciosVacios = true;

    [Header("Controles (Input System)")]
    [SerializeField] private Key abrirInventario = Key.I;
    [SerializeField] private Key siguienteObjeto = Key.C;
    [SerializeField] private Key usarObjeto = Key.F;
    [SerializeField] private Key cerrarInventario = Key.Escape;
    [SerializeField] private Key confirmarTransferencia = Key.Enter;

    [Header("Visualización y paneles")]
    [Tooltip("Contenedor de los tres inventarios. Puede mantenerse visible durante el juego.")]
    [SerializeField] private GameObject panelBarraInventario;
    [SerializeField] private bool mantenerBarraVisibleEnJuego = true;

    [Tooltip("Elementos visuales exclusivos del modo de gestión pausado.")]
    [SerializeField] private GameObject panelModoGestion;
    [SerializeField] private TMP_Text textoEstadoModo;
    [SerializeField] private string textoModoGestionAbierto = "GESTIÓN DE INVENTARIO (PAUSA)";
    [SerializeField] private string textoModoJuego = "";

    [Header("Intercambio de objetos")]
    [SerializeField] private IntercambioInventario intercambioInventario;

    [Tooltip("Opcional: único recuadro amarillo sin relleno que señala el destino provisional. Si se deja vacío, se crea automáticamente.")]
    [SerializeField] private RectTransform recuadroDestinoTransferencia;
    [SerializeField] private Color colorRecuadroTransferencia = Color.yellow;
    [SerializeField, Min(1f)] private float grosorBordeTransferencia = 4f;

    [Header("Uso de bombas")]
    [SerializeField] private ColocadorBombas colocadorBombas;

    [Header("Referencias del menú")]
    [SerializeField] private PausaInventario pausaInventario;
    [SerializeField] private FilaInventarioUI[] filas = new FilaInventarioUI[CantidadFilasEsperadas];

    private readonly Vector3[] esquinasDestinoBuffer = new Vector3[4];

    private GameObject[] armaduras = Array.Empty<GameObject>();
    private InventarioPersonaje[] inventarios = Array.Empty<InventarioPersonaje>();
    private SkeletonHealth[] saludes = Array.Empty<SkeletonHealth>();

    private int indicePersonajeActivo;
    private int cursorPersonajeDestino;
    private int cursorRanuraDestino;

    private bool menuAbierto;
    private bool referenciasEnlazadas;
    private bool suscritoAInventarios;

    public ModoSeleccionInventario ModoSeleccion => modoSeleccion;
    public bool MenuAbierto => menuAbierto;
    public bool TransferenciaPendiente => intercambioInventario != null && intercambioInventario.TransferenciaActiva;

    private void Awake()
    {
        if (pausaInventario == null)
        {
            pausaInventario = GetComponent<PausaInventario>();
        }

        if (intercambioInventario == null)
        {
            intercambioInventario = GetComponent<IntercambioInventario>();
        }

        if (colocadorBombas == null)
        {
            colocadorBombas = GetComponent<ColocadorBombas>();
        }

        if (panelBarraInventario == gameObject)
        {
            Debug.LogWarning("[MenuInventarioUI] 'panelBarraInventario' debe ser un GameObject hijo separado del controlador para seguir recibiendo teclado al ocultarse.", this);
        }

        if (panelModoGestion == gameObject)
        {
            Debug.LogWarning("[MenuInventarioUI] 'panelModoGestion' debe ser un GameObject hijo separado del controlador.", this);
        }

        if (recuadroDestinoTransferencia != null && recuadroDestinoTransferencia.gameObject == gameObject)
        {
            Debug.LogWarning("[MenuInventarioUI] 'recuadroDestinoTransferencia' debe ser un objeto UI separado del controlador.", this);
            recuadroDestinoTransferencia = null;
        }

        PrepararRecuadroDestinoExistente();

        menuAbierto = false;
        ActualizarVisibilidadPaneles();
    }

    private void OnEnable()
    {
        GameManager.OnPersonajeCambiado += ManejarPersonajeCambiado;

        if (referenciasEnlazadas)
        {
            SuscribirAInventarios();
        }
    }

    private void Start()
    {
        AsegurarReferenciasPersonajes();
        ActualizarVistaCompleta();
    }

    private void Update()
    {
        Keyboard teclado = Keyboard.current;
        if (teclado == null)
        {
            return;
        }

        if (menuAbierto)
        {
            if (TransferenciaPendiente)
            {
                if (FuePulsada(teclado, abrirInventario))
                {
                    CerrarMenu();
                    return;
                }

                if (FuePulsada(teclado, cerrarInventario))
                {
                    CancelarTransferenciaActiva();
                    return;
                }

                if (FuePulsadaConfirmar(teclado))
                {
                    IntentarConfirmarTransferenciaDesdeCursor();
                    return;
                }

                if (teclado.leftArrowKey.wasPressedThisFrame)
                {
                    MoverCursorTransferencia(-1, 0);
                }
                else if (teclado.rightArrowKey.wasPressedThisFrame)
                {
                    MoverCursorTransferencia(1, 0);
                }
                else if (teclado.upArrowKey.wasPressedThisFrame)
                {
                    MoverCursorTransferencia(0, -1);
                }
                else if (teclado.downArrowKey.wasPressedThisFrame)
                {
                    MoverCursorTransferencia(0, 1);
                }

                return;
            }

            if (FuePulsada(teclado, abrirInventario) || FuePulsada(teclado, cerrarInventario))
            {
                CerrarMenu();
                return;
            }

            if (FuePulsadaConfirmar(teclado))
            {
                IntentarIniciarTransferenciaDesdeSeleccion();
                return;
            }

            if (teclado.leftArrowKey.wasPressedThisFrame)
            {
                MoverSeleccionActiva(-1);
            }
            else if (teclado.rightArrowKey.wasPressedThisFrame)
            {
                MoverSeleccionActiva(1);
            }

            return;
        }

        if (FuePulsada(teclado, abrirInventario))
        {
            AbrirMenu();
            return;
        }

        if (modoSeleccion == ModoSeleccionInventario.CiclicoRapido
            && FuePulsada(teclado, siguienteObjeto))
        {
            AvanzarSeleccionRapida();
            return;
        }

        if (FuePulsada(teclado, usarObjeto))
        {
            IntentarUsarObjetoSeleccionado();
        }
    }

    private void OnDisable()
    {
        GameManager.OnPersonajeCambiado -= ManejarPersonajeCambiado;
        DesuscribirDeInventarios();

        if (intercambioInventario != null)
        {
            intercambioInventario.CancelarTransferencia();
        }

        OcultarRecuadroDestino();

        if (menuAbierto)
        {
            CerrarMenu(false);
        }
    }

    private void OnDestroy()
    {
        GameManager.OnPersonajeCambiado -= ManejarPersonajeCambiado;
        DesuscribirDeInventarios();

        if (intercambioInventario != null)
        {
            intercambioInventario.CancelarTransferencia();
        }

        OcultarRecuadroDestino();

        if (menuAbierto)
        {
            CerrarMenu(false);
        }
    }

    private void AbrirMenu()
    {
        if (menuAbierto)
        {
            return;
        }

        AsegurarReferenciasPersonajes();

        if (intercambioInventario != null)
        {
            intercambioInventario.CancelarTransferencia();
        }

        if (pausaInventario != null)
        {
            pausaInventario.Pausar();
        }

        menuAbierto = true;
        ActualizarVisibilidadPaneles();
        ActualizarVistaCompleta();
    }

    private void CerrarMenu(bool actualizarVista = true)
    {
        if (intercambioInventario != null)
        {
            intercambioInventario.CancelarTransferencia();
        }

        OcultarRecuadroDestino();

        if (!menuAbierto)
        {
            return;
        }

        menuAbierto = false;
        ActualizarVisibilidadPaneles();

        if (actualizarVista)
        {
            ActualizarVistaCompleta();
        }

        if (pausaInventario != null)
        {
            pausaInventario.Reanudar();
        }
    }

    private void CancelarTransferenciaActiva()
    {
        if (intercambioInventario != null)
        {
            intercambioInventario.CancelarTransferencia();
        }

        ActualizarVistaCompleta();
    }

    private void IntentarIniciarTransferenciaDesdeSeleccion()
    {
        if (!menuAbierto || intercambioInventario == null || !AsegurarReferenciasPersonajes())
        {
            return;
        }

        if (!EsIndicePersonajeValido(indicePersonajeActivo))
        {
            return;
        }

        InventarioPersonaje inventarioActivo = inventarios[indicePersonajeActivo];
        if (inventarioActivo == null)
        {
            return;
        }

        int ranuraOrigen = inventarioActivo.IndiceSeleccionado;
        bool iniciada = intercambioInventario.IntentarIniciarTransferencia(
            indicePersonajeActivo,
            armaduras[indicePersonajeActivo],
            inventarioActivo,
            saludes[indicePersonajeActivo],
            ranuraOrigen
        );

        if (!iniciada)
        {
            return;
        }

        cursorPersonajeDestino = indicePersonajeActivo;
        cursorRanuraDestino = ranuraOrigen;
        ActualizarVistaCompleta();
    }

    private void IntentarConfirmarTransferenciaDesdeCursor()
    {
        if (!menuAbierto || !TransferenciaPendiente || !AsegurarReferenciasPersonajes())
        {
            return;
        }

        if (!EsIndicePersonajeValido(cursorPersonajeDestino))
        {
            return;
        }

        bool confirmada = intercambioInventario.IntentarConfirmarTransferencia(
            cursorPersonajeDestino,
            armaduras[cursorPersonajeDestino],
            inventarios[cursorPersonajeDestino],
            saludes[cursorPersonajeDestino],
            cursorRanuraDestino
        );

        if (confirmada || !TransferenciaPendiente)
        {
            ActualizarVistaCompleta();
        }
    }

    private void MoverCursorTransferencia(int deltaColumna, int deltaFila)
    {
        if (!TransferenciaPendiente || !AsegurarReferenciasPersonajes())
        {
            return;
        }

        int cantidadPersonajes = ObtenerCantidadPersonajesNavegables();
        if (cantidadPersonajes <= 0)
        {
            return;
        }

        int totalColumnas = cantidadPersonajes * ColumnasPorPersonaje;
        int columnaLocalActual = cursorRanuraDestino % ColumnasPorPersonaje;
        int filaActual = cursorRanuraDestino / ColumnasPorPersonaje;
        int columnaGlobalActual = cursorPersonajeDestino * ColumnasPorPersonaje + columnaLocalActual;

        int nuevaColumnaGlobal = (columnaGlobalActual + deltaColumna + totalColumnas) % totalColumnas;
        int nuevaFila = (filaActual + deltaFila + FilasVisualesTransferencia) % FilasVisualesTransferencia;

        cursorPersonajeDestino = nuevaColumnaGlobal / ColumnasPorPersonaje;
        int nuevaColumnaLocal = nuevaColumnaGlobal % ColumnasPorPersonaje;
        cursorRanuraDestino = nuevaFila * ColumnasPorPersonaje + nuevaColumnaLocal;

        ActualizarRecuadroDestinoTransferencia();
    }

    private void ActualizarVisibilidadPaneles()
    {
        if (panelBarraInventario != null && panelBarraInventario != gameObject)
        {
            panelBarraInventario.SetActive(mantenerBarraVisibleEnJuego || menuAbierto);
        }

        if (panelModoGestion != null && panelModoGestion != gameObject)
        {
            panelModoGestion.SetActive(menuAbierto);
        }

        if (textoEstadoModo != null)
        {
            textoEstadoModo.text = menuAbierto ? textoModoGestionAbierto : textoModoJuego;
        }

        if (!menuAbierto)
        {
            OcultarRecuadroDestino();
        }
    }

    private void ManejarPersonajeCambiado(int nuevoIndice)
    {
        AsegurarReferenciasPersonajes();

        if (intercambioInventario != null && intercambioInventario.TransferenciaActiva)
        {
            intercambioInventario.CancelarTransferencia();
        }

        if (armaduras.Length == 0)
        {
            indicePersonajeActivo = Mathf.Max(0, nuevoIndice);
            return;
        }

        if (nuevoIndice < 0 || nuevoIndice >= armaduras.Length)
        {
            return;
        }

        indicePersonajeActivo = nuevoIndice;
        ActualizarVistaCompleta();
    }

    private void ManejarInventarioCambiado()
    {
        ActualizarVistaCompleta();
    }

    private void MoverSeleccionActiva(int desplazamiento)
    {
        InventarioPersonaje inventarioActivo = ObtenerInventarioActivo();
        if (inventarioActivo == null)
        {
            return;
        }

        int nuevoIndice = (inventarioActivo.IndiceSeleccionado + desplazamiento + InventarioPersonaje.Capacidad)
            % InventarioPersonaje.Capacidad;

        inventarioActivo.IntentarSeleccionar(nuevoIndice);
    }

    private void AvanzarSeleccionRapida()
    {
        InventarioPersonaje inventarioActivo = ObtenerInventarioActivo();
        if (inventarioActivo == null)
        {
            return;
        }

        int indiceActual = inventarioActivo.IndiceSeleccionado;

        if (!saltarEspaciosVacios)
        {
            int siguiente = (indiceActual + 1) % InventarioPersonaje.Capacidad;
            inventarioActivo.IntentarSeleccionar(siguiente);
            return;
        }

        for (int paso = 1; paso <= InventarioPersonaje.Capacidad; paso++)
        {
            int candidato = (indiceActual + paso) % InventarioPersonaje.Capacidad;
            if (inventarioActivo.ObtenerItem(candidato) != null)
            {
                inventarioActivo.IntentarSeleccionar(candidato);
                return;
            }
        }
    }

    private void IntentarUsarObjetoSeleccionado()
    {
        if (menuAbierto || TransferenciaPendiente || !AsegurarReferenciasPersonajes())
        {
            return;
        }

        if (!EsIndicePersonajeValido(indicePersonajeActivo))
        {
            return;
        }

        GameObject armadura = armaduras[indicePersonajeActivo];
        InventarioPersonaje inventario = inventarios[indicePersonajeActivo];
        SkeletonHealth salud = saludes[indicePersonajeActivo];

        if (armadura == null || !armadura.activeInHierarchy)
        {
            return;
        }

        if (inventario == null || !inventario.isActiveAndEnabled)
        {
            return;
        }

        if (salud == null || salud.vidas <= 0)
        {
            return;
        }

        int indiceSeleccionado = inventario.IndiceSeleccionado;
        ItemDatos item = inventario.ObtenerItem(indiceSeleccionado);
        if (item == null)
        {
            return;
        }

        switch (item.Tipo)
        {
            case TipoItem.Comida:
                IntentarUsarComida(inventario, salud, indiceSeleccionado, item);
                break;

            case TipoItem.Bomba:
                if (colocadorBombas != null)
                {
                    colocadorBombas.IntentarColocar(armadura, inventario, indiceSeleccionado);
                }
                break;
        }
    }

    private static void IntentarUsarComida(
        InventarioPersonaje inventario,
        SkeletonHealth salud,
        int indiceSeleccionado,
        ItemDatos item
    )
    {
        if (item.CantidadCuracion <= 0 || salud.vidas >= salud.maxVidas)
        {
            return;
        }

        int vidasAntes = salud.vidas;
        salud.Curar(item.CantidadCuracion);

        if (salud.vidas > vidasAntes)
        {
            inventario.IntentarQuitar(indiceSeleccionado);
        }
    }

    private InventarioPersonaje ObtenerInventarioActivo()
    {
        if (!AsegurarReferenciasPersonajes())
        {
            return null;
        }

        if (!EsIndicePersonajeValido(indicePersonajeActivo))
        {
            return null;
        }

        return inventarios[indicePersonajeActivo];
    }

    private bool AsegurarReferenciasPersonajes()
    {
        if (referenciasEnlazadas)
        {
            return armaduras.Length > 0;
        }

        GameManager manager = GameManager.Instancia;
        if (manager == null || manager.armaduras == null || manager.armaduras.Length == 0)
        {
            return false;
        }

        int cantidad = manager.armaduras.Length;
        armaduras = new GameObject[cantidad];
        inventarios = new InventarioPersonaje[cantidad];
        saludes = new SkeletonHealth[cantidad];

        for (int i = 0; i < cantidad; i++)
        {
            GameObject armadura = manager.armaduras[i];
            armaduras[i] = armadura;

            if (armadura == null)
            {
                continue;
            }

            InventarioPersonaje inventario = armadura.GetComponent<InventarioPersonaje>();
            if (inventario == null)
            {
                inventario = armadura.GetComponentInChildren<InventarioPersonaje>(true);
            }
            inventarios[i] = inventario;

            SkeletonHealth salud = armadura.GetComponent<SkeletonHealth>();
            if (salud == null)
            {
                salud = armadura.GetComponentInChildren<SkeletonHealth>(true);
            }
            saludes[i] = salud;
        }

        if (indicePersonajeActivo < 0 || indicePersonajeActivo >= cantidad)
        {
            indicePersonajeActivo = 0;
        }

        referenciasEnlazadas = true;
        SuscribirAInventarios();
        return true;
    }

    private void SuscribirAInventarios()
    {
        if (suscritoAInventarios || inventarios == null)
        {
            return;
        }

        for (int i = 0; i < inventarios.Length; i++)
        {
            if (inventarios[i] != null)
            {
                inventarios[i].AlCambiar += ManejarInventarioCambiado;
            }
        }

        suscritoAInventarios = true;
    }

    private void DesuscribirDeInventarios()
    {
        if (!suscritoAInventarios || inventarios == null)
        {
            return;
        }

        for (int i = 0; i < inventarios.Length; i++)
        {
            if (inventarios[i] != null)
            {
                inventarios[i].AlCambiar -= ManejarInventarioCambiado;
            }
        }

        suscritoAInventarios = false;
    }

    private void ActualizarVistaCompleta()
    {
        if (filas != null)
        {
            for (int i = 0; i < filas.Length; i++)
            {
                FilaInventarioUI fila = filas[i];
                if (fila == null)
                {
                    continue;
                }

                InventarioPersonaje inventario = i < inventarios.Length ? inventarios[i] : null;
                string nombreFallback = i < armaduras.Length && armaduras[i] != null
                    ? armaduras[i].name
                    : $"Personaje {i + 1}";

                bool esActivo = i == indicePersonajeActivo;
                fila.Actualizar(nombreFallback, inventario, esActivo, menuAbierto);
            }
        }

        ActualizarRecuadroDestinoTransferencia();
    }

    private void ActualizarRecuadroDestinoTransferencia()
    {
        if (!menuAbierto || !TransferenciaPendiente)
        {
            OcultarRecuadroDestino();
            return;
        }

        RanuraInventarioUI ranuraDestino = ObtenerRanuraUI(cursorPersonajeDestino, cursorRanuraDestino);
        if (ranuraDestino == null)
        {
            OcultarRecuadroDestino();
            return;
        }

        AsegurarRecuadroDestino();
        if (recuadroDestinoTransferencia == null)
        {
            return;
        }

        RectTransform destinoRect = ranuraDestino.transform as RectTransform;
        RectTransform padreRecuadro = recuadroDestinoTransferencia.parent as RectTransform;
        if (destinoRect == null || padreRecuadro == null)
        {
            OcultarRecuadroDestino();
            return;
        }

        recuadroDestinoTransferencia.gameObject.SetActive(true);
        Canvas.ForceUpdateCanvases();

        destinoRect.GetWorldCorners(esquinasDestinoBuffer);

        Vector3 primeraEsquina = padreRecuadro.InverseTransformPoint(esquinasDestinoBuffer[0]);
        float minX = primeraEsquina.x;
        float maxX = primeraEsquina.x;
        float minY = primeraEsquina.y;
        float maxY = primeraEsquina.y;

        for (int i = 1; i < esquinasDestinoBuffer.Length; i++)
        {
            Vector3 esquinaLocal = padreRecuadro.InverseTransformPoint(esquinasDestinoBuffer[i]);
            if (esquinaLocal.x < minX) minX = esquinaLocal.x;
            if (esquinaLocal.x > maxX) maxX = esquinaLocal.x;
            if (esquinaLocal.y < minY) minY = esquinaLocal.y;
            if (esquinaLocal.y > maxY) maxY = esquinaLocal.y;
        }

        float ancho = maxX - minX;
        float alto = maxY - minY;
        Vector3 centroLocal = new Vector3((minX + maxX) * 0.5f, (minY + maxY) * 0.5f, 0f);

        recuadroDestinoTransferencia.anchorMin = new Vector2(0.5f, 0.5f);
        recuadroDestinoTransferencia.anchorMax = new Vector2(0.5f, 0.5f);
        recuadroDestinoTransferencia.pivot = new Vector2(0.5f, 0.5f);
        recuadroDestinoTransferencia.localRotation = Quaternion.identity;
        recuadroDestinoTransferencia.localScale = Vector3.one;
        recuadroDestinoTransferencia.localPosition = centroLocal;
        recuadroDestinoTransferencia.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, ancho);
        recuadroDestinoTransferencia.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, alto);
        recuadroDestinoTransferencia.SetAsLastSibling();
    }

    private void PrepararRecuadroDestinoExistente()
    {
        if (recuadroDestinoTransferencia == null)
        {
            return;
        }

        LayoutElement layoutElement = recuadroDestinoTransferencia.GetComponent<LayoutElement>();
        if (layoutElement == null)
        {
            layoutElement = recuadroDestinoTransferencia.gameObject.AddComponent<LayoutElement>();
        }

        layoutElement.ignoreLayout = true;
        recuadroDestinoTransferencia.gameObject.SetActive(false);
    }

    private void AsegurarRecuadroDestino()
    {
        if (recuadroDestinoTransferencia != null)
        {
            return;
        }

        Transform padre = (panelBarraInventario != null && panelBarraInventario != gameObject)
            ? panelBarraInventario.transform
            : transform;

        GameObject raiz = new GameObject("RecuadroDestinoTransferencia", typeof(RectTransform), typeof(LayoutElement));
        RectTransform rect = raiz.GetComponent<RectTransform>();
        rect.SetParent(padre, false);

        LayoutElement layoutElement = raiz.GetComponent<LayoutElement>();
        layoutElement.ignoreLayout = true;

        CrearBordeAmarillo(rect, "BordeSuperior", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, grosorBordeTransferencia));
        CrearBordeAmarillo(rect, "BordeInferior", new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, grosorBordeTransferencia));
        CrearBordeAmarillo(rect, "BordeIzquierdo", new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(grosorBordeTransferencia, 0f));
        CrearBordeAmarillo(rect, "BordeDerecho", new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(1f, 0.5f), new Vector2(grosorBordeTransferencia, 0f));

        raiz.SetActive(false);
        recuadroDestinoTransferencia = rect;
    }

    private void CrearBordeAmarillo(
        RectTransform padre,
        string nombre,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 pivot,
        Vector2 sizeDelta
    )
    {
        GameObject borde = new GameObject(nombre, typeof(RectTransform), typeof(Image));
        RectTransform rect = borde.GetComponent<RectTransform>();
        rect.SetParent(padre, false);
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = sizeDelta;

        Image imagen = borde.GetComponent<Image>();
        imagen.color = colorRecuadroTransferencia;
        imagen.raycastTarget = false;
    }

    private void OcultarRecuadroDestino()
    {
        if (recuadroDestinoTransferencia != null && recuadroDestinoTransferencia.gameObject != gameObject)
        {
            recuadroDestinoTransferencia.gameObject.SetActive(false);
        }
    }

    private RanuraInventarioUI ObtenerRanuraUI(int indicePersonaje, int indiceRanura)
    {
        if (filas == null || indicePersonaje < 0 || indicePersonaje >= filas.Length)
        {
            return null;
        }

        FilaInventarioUI fila = filas[indicePersonaje];
        return fila != null ? fila.ObtenerRanura(indiceRanura) : null;
    }

    private int ObtenerCantidadPersonajesNavegables()
    {
        int cantidadFilas = filas != null ? filas.Length : 0;
        return Mathf.Min(armaduras.Length, cantidadFilas);
    }

    private bool EsIndicePersonajeValido(int indice)
    {
        return indice >= 0
            && indice < armaduras.Length
            && indice < inventarios.Length
            && indice < saludes.Length;
    }

    private bool FuePulsadaConfirmar(Keyboard teclado)
    {
        if (FuePulsada(teclado, confirmarTransferencia))
        {
            return true;
        }

        return confirmarTransferencia == Key.Enter && FuePulsada(teclado, Key.NumpadEnter);
    }

    private static bool FuePulsada(Keyboard teclado, Key tecla)
    {
        if (teclado == null || tecla == Key.None)
        {
            return false;
        }

        KeyControl control = teclado[tecla];
        return control != null && control.wasPressedThisFrame;
    }
}
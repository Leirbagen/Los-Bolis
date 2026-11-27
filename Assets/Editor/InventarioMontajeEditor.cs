#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using StarterAssets;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

// Herramienta de montaje. No participa en una compilacion del juego.
[InitializeOnLoad]
public static class InventarioMontajeEditor
{
    private const string Carpeta = "Assets/Prefabs/Inventario";
    private const string EstadoPrueba = "LosBolis.Inventario.PruebaPendiente";
    private static readonly string Trabajo = Path.GetFullPath("Library/InventarioMontaje");
    private static double siguienteRevision;
    private static int fasePrueba = -1;
    private static int siguienteFrame;
    private static double inicioPrueba;
    private static int personajeCambiado;
    private static int pasoVertical;
    private static readonly List<string> pruebas = new List<string>();

    [Serializable] private class Solicitud { public string accion = ""; public string escena = ""; }
    [Serializable] private class Resultado
    {
        public bool correcto;
        public string accion;
        public string escena;
        public string detalle;
        public string[] comprobaciones;
    }

    static InventarioMontajeEditor()
    {
        EditorApplication.update += RevisarSolicitud;
        EditorApplication.playModeStateChanged += AlCambiarPlay;
    }

    [MenuItem("Tools/Los Bolis/Inventario/Montar interfaz en escena")]
    public static void Montar()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Sal del modo Play antes de montar la interfaz.");

        Scene escena = SceneManager.GetActiveScene();
        GameManager manager = Buscar<GameManager>(escena).Single();
        Canvas canvas = Buscar<Canvas>(escena).Single(c => c.name == "HUD");
        if (manager.armaduras == null || manager.armaduras.Length != 3)
            throw new InvalidOperationException("GameManager debe tener los tres personajes asignados.");
        foreach (GameObject personaje in manager.armaduras)
            if (personaje == null || personaje.GetComponent<InventarioPersonaje>() == null
                || personaje.GetComponent<SkeletonHealth>() == null)
                throw new InvalidOperationException("Cada personaje necesita InventarioPersonaje y SkeletonHealth.");

        // Primero guardamos y respaldamos tambien el trabajo que estaba sin guardar.
        if (string.IsNullOrEmpty(escena.path)) throw new InvalidOperationException("Guarda primero la escena.");
        EditorSceneManager.SaveScene(escena);
        Directory.CreateDirectory(Trabajo);
        File.Copy(escena.path, Path.Combine(Trabajo, "EscenaAntes-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".unity"));
        Directory.CreateDirectory(Carpeta);
        AssetDatabase.Refresh();

        Undo.IncrementCurrentGroup();
        int grupo = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Montar inventario de Los Bolis");
        RectTransform sistema = ObtenerRect(canvas.transform, "SistemaInventario");
        Undo.RegisterFullObjectHierarchyUndo(sistema.gameObject, "Configurar interfaz de inventario");
        Estirar(sistema);
        Image fondoAnterior = sistema.GetComponent<Image>();
        if (fondoAnterior != null) { fondoAnterior.color = Color.clear; fondoAnterior.raycastTarget = false; }
        MenuInventarioUI menu = Obtener<MenuInventarioUI>(sistema.gameObject);
        PausaInventario pausa = Obtener<PausaInventario>(sistema.gameObject);

        RectTransform gestion = ObtenerRect(sistema, "PanelModoGestion");
        Estirar(gestion);
        Imagen(gestion, new Color(0, 0, 0, 0.12f));
        TextMeshProUGUI estado = Texto(gestion, "TextoEstado", "", 16);
        estado.gameObject.SetActive(false);

        RectTransform barra = ObtenerRect(sistema, "BarraInventario");
        Posicionar(barra, Vector2.one, Vector2.one, new Vector2(-16, -16), new Vector2(360, 144));
        Image fondoBarra = Imagen(barra, new Color(0.075f, 0.075f, 0.075f, 0.9f));
        fondoBarra.sprite = null;
        fondoBarra.type = Image.Type.Simple;
        VerticalLayoutGroup verticalAnterior = barra.GetComponent<VerticalLayoutGroup>();
        if (verticalAnterior != null) Undo.DestroyObjectImmediate(verticalAnterior);
        HorizontalLayoutGroup grupos = Obtener<HorizontalLayoutGroup>(barra.gameObject);
        grupos.padding = new RectOffset(8, 8, 8, 8);
        grupos.spacing = 4;
        grupos.childControlWidth = grupos.childControlHeight = true;
        grupos.childForceExpandWidth = grupos.childForceExpandHeight = false;
        grupos.childAlignment = TextAnchor.UpperLeft;

        GameObject casillaPrefab = CrearPrefabCasilla();
        var datosMenu = new SerializedObject(menu);
        Asignar(datosMenu, "panelBarraInventario", barra.gameObject);
        Asignar(datosMenu, "panelModoGestion", gestion.gameObject);
        Asignar(datosMenu, "textoEstadoModo", estado);
        Asignar(datosMenu, "pausaInventario", pausa);
        datosMenu.FindProperty("mantenerBarraVisibleEnJuego").boolValue = true;
        datosMenu.FindProperty("saltarEspaciosVacios").boolValue = true;
        datosMenu.FindProperty("textoModoGestionAbierto").stringValue = "";
        SerializedProperty filas = datosMenu.FindProperty("filas");
        filas.arraySize = 3;

        for (int i = 0; i < 3; i++)
        {
            GameObject personaje = manager.armaduras[i];
            string nombre = personaje.GetComponent<RudoController>() != null ? "Rudo"
                : personaje.GetComponent<FuerteController>() != null ? "Fuerte" : "Agil";
            string etiqueta = nombre == "Agil" ? "Ágil" : nombre;
            RectTransform fila = ObtenerRect(barra, "Fila" + nombre);
            fila.SetSiblingIndex(i);
            Imagen(fila, Color.clear);
            LayoutElement alto = Obtener<LayoutElement>(fila.gameObject);
            alto.preferredWidth = alto.minWidth = 112;
            alto.preferredHeight = alto.minHeight = 128;
            alto.flexibleWidth = 0;
            alto.flexibleHeight = 0;
            HorizontalLayoutGroup horizontalAnterior = fila.GetComponent<HorizontalLayoutGroup>();
            if (horizontalAnterior != null) Undo.DestroyObjectImmediate(horizontalAnterior);

            RectTransform activo = ObtenerRect(fila, "Activo");
            activo.SetAsFirstSibling();
            Obtener<LayoutElement>(activo.gameObject).ignoreLayout = true;
            Posicionar(activo, new Vector2(0, 1), new Vector2(0, 1), new Vector2(3, -8), new Vector2(3, 8));
            Imagen(activo, new Color(0.85f, 0.85f, 0.85f, 1));
            activo.gameObject.SetActive(i == 0);
            TextMeshProUGUI nombreTexto = Texto(fila, "NombrePersonaje", etiqueta, 16);
            nombreTexto.alignment = TextAlignmentOptions.Center;
            Posicionar(nombreTexto.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), Vector2.zero, new Vector2(100, 20));
            LayoutElement anchoNombre = Obtener<LayoutElement>(nombreTexto.gameObject);
            anchoNombre.preferredWidth = anchoNombre.minWidth = 100;
            anchoNombre.preferredHeight = 20;
            anchoNombre.flexibleWidth = 0;
            RectTransform ranuras = ObtenerRect(fila, "Ranuras");
            LayoutElement tamanoRanuras = Obtener<LayoutElement>(ranuras.gameObject);
            tamanoRanuras.preferredWidth = tamanoRanuras.minWidth = 100;
            tamanoRanuras.preferredHeight = 100;
            tamanoRanuras.flexibleWidth = 0;
            Posicionar(ranuras, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -24), new Vector2(100, 100));
            GridLayoutGroup grid = Obtener<GridLayoutGroup>(ranuras.gameObject);
            grid.cellSize = new Vector2(48, 48);
            grid.spacing = new Vector2(4, 4);
            grid.padding = new RectOffset();
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 2;

            SerializedProperty datosFila = filas.GetArrayElementAtIndex(i);
            datosFila.FindPropertyRelative("etiquetaPersonaje").stringValue = etiqueta;
            datosFila.FindPropertyRelative("textoNombrePersonaje").objectReferenceValue = nombreTexto;
            datosFila.FindPropertyRelative("indicadorPersonajeActivo").objectReferenceValue = activo.gameObject;
            SerializedProperty referencias = datosFila.FindPropertyRelative("ranuras");
            referencias.arraySize = 4;
            for (int j = 0; j < 4; j++)
            {
                Transform existente = ranuras.Find("Ranura" + j);
                GameObject casilla = existente != null ? existente.gameObject
                    : (GameObject)PrefabUtility.InstantiatePrefab(casillaPrefab, ranuras);
                if (existente == null) Undo.RegisterCreatedObjectUndo(casilla, "Crear casilla");
                casilla.name = "Ranura" + j;
                casilla.transform.SetSiblingIndex(j);
                // Aplicar el estilo tambien a instancias con overrides del montaje anterior.
                Image fondoCasilla = casilla.GetComponent<Image>();
                fondoCasilla.color = new Color(0.2f, 0.2f, 0.2f, 1);
                PrefabUtility.RecordPrefabInstancePropertyModifications(fondoCasilla);
                Image fondoSeleccionCasilla = casilla.transform.Find("Seleccion").GetComponent<Image>();
                fondoSeleccionCasilla.color = Color.clear;
                PrefabUtility.RecordPrefabInstancePropertyModifications(fondoSeleccionCasilla);
                referencias.GetArrayElementAtIndex(j).objectReferenceValue = casilla.GetComponent<RanuraInventarioUI>();
            }
        }
        Transform ayuda = barra.Find("AyudaControles");
        if (ayuda != null) ayuda.gameObject.SetActive(false);
        datosMenu.ApplyModifiedProperties();

        var datosPausa = new SerializedObject(pausa);
        datosPausa.FindProperty("incluirAutomaticamenteControlesPrincipales").boolValue = true;
        datosPausa.FindProperty("incluirAutomaticamenteMecanismos").boolValue = true;
        RudoController rudo = manager.armaduras.Select(p => p.GetComponent<RudoController>()).FirstOrDefault(r => r != null);
        if (rudo != null)
        {
            Asignar(datosPausa, "rudoController", rudo);
            Asignar(datosPausa, "thirdPersonControllerRudo", rudo.GetComponent<ThirdPersonController>());
            Asignar(datosPausa, "camaraApuntadoRudo", rudo.camaraApuntado);
            Asignar(datosPausa, "miraUIRudo", rudo.miraUI);
        }
        datosPausa.ApplyModifiedProperties();
        gestion.gameObject.SetActive(false);
        sistema.gameObject.SetActive(true);
        barra.gameObject.SetActive(true);
        sistema.SetAsLastSibling();
        Canvas.ForceUpdateCanvases();
        GuardarPrefabSistema(sistema.gameObject);
        EditorSceneManager.MarkSceneDirty(escena);
        EditorSceneManager.SaveScene(escena);
        AssetDatabase.SaveAssets();
        Undo.CollapseUndoOperations(grupo);
        Selection.activeGameObject = sistema.gameObject;
        Validar();
        Debug.Log("[InventarioMontaje] Interfaz conectada y guardada en " + escena.path);
    }

    private static GameObject CrearPrefabCasilla()
    {
        string ruta = Carpeta + "/RanuraInventarioUI.prefab";
        GameObject existente = AssetDatabase.LoadAssetAtPath<GameObject>(ruta);
        RectTransform raiz = existente != null
            ? PrefabUtility.LoadPrefabContents(ruta).GetComponent<RectTransform>()
            : ObtenerRect(null, "RanuraInventarioUI");
        raiz.sizeDelta = new Vector2(48, 48);
        Imagen(raiz, new Color(0.2f, 0.2f, 0.2f, 1));
        RectTransform seleccion = ObtenerRect(raiz, "Seleccion");
        Estirar(seleccion);
        Image fondoSeleccion = Imagen(seleccion, Color.clear);
        fondoSeleccion.sprite = null;
        fondoSeleccion.type = Image.Type.Simple;
        CrearBorde(seleccion, "Arriba", new Vector2(0, 1), Vector2.one, new Vector2(0.5f, 1), new Vector2(0, 2));
        CrearBorde(seleccion, "Abajo", Vector2.zero, new Vector2(1, 0), new Vector2(0.5f, 0), new Vector2(0, 2));
        CrearBorde(seleccion, "Izquierda", Vector2.zero, new Vector2(0, 1), new Vector2(0, 0.5f), new Vector2(2, 0));
        CrearBorde(seleccion, "Derecha", new Vector2(1, 0), Vector2.one, new Vector2(1, 0.5f), new Vector2(2, 0));
        seleccion.gameObject.SetActive(false);
        RectTransform gestion = ObtenerRect(raiz, "ModoGestion");
        gestion.gameObject.SetActive(false);
        RectTransform icono = ObtenerRect(raiz, "Icono");
        Posicionar(icono, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(36, 36));
        Image imagenIcono = Imagen(icono, Color.white);
        imagenIcono.preserveAspect = true;
        imagenIcono.enabled = false;
        RectTransform provisional = ObtenerRect(raiz, "IconoPrototipo");
        Posicionar(provisional, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(36, 36));
        Obtener<CanvasRenderer>(provisional.gameObject);
        IconoItemPrototipoUI dibujo = Obtener<IconoItemPrototipoUI>(provisional.gameObject);
        dibujo.raycastTarget = false;
        provisional.gameObject.SetActive(false);
        TextMeshProUGUI nombre = Texto(raiz, "Nombre", "", 12);
        nombre.gameObject.SetActive(false);
        RanuraInventarioUI componente = Obtener<RanuraInventarioUI>(raiz.gameObject);
        var datos = new SerializedObject(componente);
        Asignar(datos, "imagenIcono", imagenIcono);
        Asignar(datos, "textoNombre", nombre);
        Asignar(datos, "indicadorSeleccion", seleccion.gameObject);
        Asignar(datos, "indicadorModoGestion", null);
        Asignar(datos, "iconoPrototipo", dibujo);
        datos.FindProperty("mostrarNombre").boolValue = false;
        datos.FindProperty("textoVacio").stringValue = "";
        datos.ApplyModifiedPropertiesWithoutUndo();
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(raiz.gameObject, ruta);
        if (existente != null) PrefabUtility.UnloadPrefabContents(raiz.gameObject);
        else Object.DestroyImmediate(raiz.gameObject);
        return prefab;
    }

    private static void CrearBorde(RectTransform padre, string nombre, Vector2 min, Vector2 max, Vector2 pivote, Vector2 tamano)
    {
        RectTransform borde = ObtenerRect(padre, nombre);
        borde.anchorMin = min;
        borde.anchorMax = max;
        borde.pivot = pivote;
        borde.anchoredPosition = Vector2.zero;
        borde.sizeDelta = tamano;
        Imagen(borde, new Color32(255, 220, 0, 255));
    }

    private static void GuardarPrefabSistema(GameObject sistema)
    {
        string ruta = Carpeta + "/SistemaInventario.prefab";
        GameObject copia = Object.Instantiate(sistema);
        copia.name = "SistemaInventario";
        var datos = new SerializedObject(copia.GetComponent<PausaInventario>());
        foreach (string campo in new[] { "rudoController", "thirdPersonControllerRudo", "camaraApuntadoRudo", "miraUIRudo" })
            Asignar(datos, campo, null);
        datos.FindProperty("componentesASuspender").arraySize = 0;
        datos.FindProperty("entradasStarterAssets").arraySize = 0;
        datos.ApplyModifiedPropertiesWithoutUndo();
        PrefabUtility.SaveAsPrefabAsset(copia, ruta);
        Object.DestroyImmediate(copia);
    }

    [MenuItem("Tools/Los Bolis/Inventario/Validar montaje")]
    public static void Validar()
    {
        Scene escena = SceneManager.GetActiveScene();
        MenuInventarioUI menu = Buscar<MenuInventarioUI>(escena).Single();
        var datos = new SerializedObject(menu);
        foreach (string campo in new[] { "panelBarraInventario", "panelModoGestion", "textoEstadoModo", "pausaInventario" })
            Exigir(datos.FindProperty(campo).objectReferenceValue != null, "Referencia conectada: " + campo);
        SerializedProperty filas = datos.FindProperty("filas");
        Exigir(filas.arraySize == 3, "Tres filas de inventario");
        var unicas = new HashSet<Object>();
        for (int i = 0; i < filas.arraySize; i++)
        {
            SerializedProperty fila = filas.GetArrayElementAtIndex(i);
            SerializedProperty ranuras = fila.FindPropertyRelative("ranuras");
            Exigir(ranuras.arraySize == 4, "Cuatro casillas en fila " + i);
            for (int j = 0; j < ranuras.arraySize; j++)
            {
                Object ranura = ranuras.GetArrayElementAtIndex(j).objectReferenceValue;
                Exigir(ranura != null && unicas.Add(ranura), "Casilla independiente " + i + ":" + j);
                var datosRanura = new SerializedObject(ranura);
                foreach (string campo in new[] { "imagenIcono", "textoNombre", "indicadorSeleccion" })
                    Exigir(datosRanura.FindProperty(campo).objectReferenceValue != null, "Casilla conectada " + i + ":" + j + " " + campo);
                GameObject seleccion = (GameObject)datosRanura.FindProperty("indicadorSeleccion").objectReferenceValue;
                Exigir(seleccion.GetComponent<Image>().color.a == 0, "Seleccion sin relleno " + i + ":" + j);
            }
        }
        EscribirResultado(true, "validar", "Referencias correctas; 3 filas y 12 casillas.");
    }

    private static RectTransform ObtenerRect(Transform padre, string nombre)
    {
        Transform existente = padre != null ? padre.Find(nombre) : null;
        if (existente != null) return (RectTransform)existente;
        var go = new GameObject(nombre, typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(go, "Crear " + nombre);
        go.layer = LayerMask.NameToLayer("UI");
        go.transform.SetParent(padre, false);
        return go.GetComponent<RectTransform>();
    }
    private static T Obtener<T>(GameObject go) where T : Component
        => go.GetComponent<T>() ?? Undo.AddComponent<T>(go);
    private static Image Imagen(RectTransform rect, Color color)
    {
        Image imagen = Obtener<Image>(rect.gameObject);
        imagen.color = color;
        imagen.raycastTarget = false;
        return imagen;
    }
    private static TextMeshProUGUI Texto(Transform padre, string nombre, string contenido, float tamano)
    {
        TextMeshProUGUI texto = Obtener<TextMeshProUGUI>(ObtenerRect(padre, nombre).gameObject);
        texto.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
        texto.text = contenido;
        texto.fontSize = tamano;
        texto.color = Color.white;
        texto.alignment = TextAlignmentOptions.Center;
        texto.raycastTarget = false;
        return texto;
    }
    private static void Estirar(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        rect.localScale = Vector3.one;
    }
    private static void Posicionar(RectTransform rect, Vector2 ancla, Vector2 pivote, Vector2 posicion, Vector2 tamano)
    {
        rect.anchorMin = rect.anchorMax = ancla; rect.pivot = pivote;
        rect.anchoredPosition = posicion; rect.sizeDelta = tamano;
        rect.localScale = Vector3.one;
    }
    private static void Asignar(SerializedObject datos, string campo, Object valor)
        => datos.FindProperty(campo).objectReferenceValue = valor;
    private static IEnumerable<T> Buscar<T>(Scene escena) where T : Component
        => escena.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<T>(true));
    private static void Exigir(bool condicion, string mensaje)
    {
        if (!condicion) throw new InvalidOperationException(mensaje);
        pruebas.Add(mensaje);
    }

    private static void RevisarSolicitud()
    {
        if (EditorApplication.isPlaying)
        {
            if (fasePrueba >= 0) EjecutarPasoPrueba();
            return;
        }
        if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode
            || EditorApplication.timeSinceStartup < siguienteRevision) return;
        siguienteRevision = EditorApplication.timeSinceStartup + 0.5;
        string archivo = Path.Combine(Trabajo, "solicitud.json");
        if (!File.Exists(archivo)) return;
        Solicitud solicitud = JsonUtility.FromJson<Solicitud>(File.ReadAllText(archivo));
        File.Delete(archivo);
        pruebas.Clear();
        try
        {
            if (SceneManager.GetActiveScene().path != solicitud.escena)
                throw new InvalidOperationException("La escena abierta no coincide con la solicitada.");
            if (solicitud.accion == "montar") Montar();
            else if (solicitud.accion == "validar") Validar();
            else if (solicitud.accion == "probar")
            {
                SessionState.SetBool(EstadoPrueba, true);
                EditorApplication.isPlaying = true;
            }
            else throw new InvalidOperationException("Accion desconocida.");
        }
        catch (Exception error)
        {
            EscribirResultado(false, solicitud.accion, error.ToString());
            Debug.LogException(error);
        }
    }

    private static void AlCambiarPlay(PlayModeStateChange cambio)
    {
        if (cambio == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(EstadoPrueba, false))
        {
            fasePrueba = 0;
            siguienteFrame = Time.frameCount + 10;
            inicioPrueba = EditorApplication.timeSinceStartup;
            personajeCambiado = 0;
            pasoVertical = 0;
            pruebas.Clear();
            GameManager.OnPersonajeCambiado += RegistrarCambio;
        }
        if (cambio == PlayModeStateChange.ExitingPlayMode)
        {
            fasePrueba = -1;
            GameManager.OnPersonajeCambiado -= RegistrarCambio;
            SessionState.SetBool(EstadoPrueba, false);
        }
    }
    private static void RegistrarCambio(int indice) => personajeCambiado = indice;
    private static void Tecla(params Key[] teclas)
        => InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState(teclas));

    private static void EjecutarPasoPrueba()
    {
        if (Time.frameCount < siguienteFrame && EditorApplication.timeSinceStartup - inicioPrueba < 50) return;
        try
        {
            if (EditorApplication.timeSinceStartup - inicioPrueba > 50) throw new TimeoutException("Prueba de Play excedio 50 segundos.");
            GameManager manager = GameManager.Instancia;
            MenuInventarioUI menu = Object.FindFirstObjectByType<MenuInventarioUI>();
            InventarioPersonaje inventario = manager.armaduras[0].GetComponent<InventarioPersonaje>();
            SkeletonHealth salud = manager.armaduras[0].GetComponent<SkeletonHealth>();
            switch (fasePrueba)
            {
                case 0:
                    Exigir(menu != null && Keyboard.current != null, "Menu y teclado disponibles en Play");
                    for (int i = 0; i < 4; i++) inventario.IntentarQuitar(i);
                    inventario.IntentarAgregar(AssetDatabase.LoadAssetAtPath<ItemDatos>("Assets/Datos/Items/LlaveRoja.asset"));
                    inventario.IntentarAgregar(AssetDatabase.LoadAssetAtPath<ItemDatos>("Assets/Datos/Items/Comida.asset"));
                    inventario.IntentarAgregar(AssetDatabase.LoadAssetAtPath<ItemDatos>("Assets/Datos/Items/Comida.asset"));
                    inventario.IntentarSeleccionar(0);
                    salud.vidas = salud.maxVidas - 1;
                    Tecla(Key.I);
                    break;
                case 1:
                    Exigir(menu.MenuAbierto && Time.timeScale == 0 && !manager.enabled, "I abre y pausa tambien el cambio de personaje");
                    Exigir(manager.armaduras.All(p => !p.GetComponent<ThirdPersonController>().enabled), "Los tres controladores estan suspendidos");
                    Tecla(Key.RightArrow);
                    break;
                case 2:
                    if (pasoVertical == 0)
                    {
                        Exigir(inventario.IndiceSeleccionado == 1, "La flecha selecciona la comida");
                        pasoVertical = 1;
                        Tecla(Key.DownArrow);
                        siguienteFrame = Time.frameCount + 6;
                        return;
                    }
                    if (pasoVertical == 1)
                    {
                        Exigir(inventario.IndiceSeleccionado == 3, "Flecha abajo navega la cuadricula 2x2");
                        pasoVertical = 2;
                        Tecla(Key.UpArrow);
                        siguienteFrame = Time.frameCount + 6;
                        return;
                    }
                    Exigir(inventario.IndiceSeleccionado == 1, "La flecha selecciona la comida");
                    Canvas.ForceUpdateCanvases();
                    ScreenCapture.CaptureScreenshot(Path.Combine(Trabajo, "inventario-pausa.png"));
                    Tecla(Key.M);
                    break;
                case 3:
                    Exigir(personajeCambiado == 0 && menu.MenuAbierto && !manager.enabled, "GameManager permanece suspendido durante la pausa");
                    Tecla(Key.I);
                    break;
                case 4:
                    Exigir(!menu.MenuAbierto && Time.timeScale == 1 && manager.enabled, "I cierra y restaura el tiempo y GameManager");
                    Exigir(manager.armaduras[0].GetComponent<ThirdPersonController>().enabled
                        && !manager.armaduras[1].GetComponent<ThirdPersonController>().enabled, "Solo se restaura el controlador previamente activo");
                    Tecla(Key.F);
                    break;
                case 5:
                    Exigir(salud.vidas == salud.maxVidas && inventario.ObtenerItem(1) == null, "F cura y consume una sola comida");
                    Exigir(inventario.ObtenerItem(0) != null && inventario.ObtenerItem(2) != null, "La llave y la otra comida se conservan");
                    inventario.IntentarSeleccionar(2);
                    Tecla();
                    break;
                case 6: Tecla(Key.F); break;
                case 7:
                    Exigir(inventario.ObtenerItem(2) != null, "La comida no se gasta con salud completa");
                    var datosMenu = new SerializedObject(menu);
                    datosMenu.FindProperty("modoSeleccion").enumValueIndex = (int)ModoSeleccionInventario.CiclicoRapido;
                    datosMenu.ApplyModifiedPropertiesWithoutUndo();
                    inventario.IntentarSeleccionar(0);
                    Tecla(Key.C);
                    break;
                case 8:
                    Exigir(inventario.IndiceSeleccionado == 2 && !menu.MenuAbierto && Time.timeScale == 1, "C avanza y salta espacios vacios sin pausar");
                    break;
                case 9:
                    Exigir(inventario.IndiceSeleccionado == 2, "Mantener C no repite la seleccion");
                    Tecla();
                    break;
                case 10: Tecla(Key.C); break;
                case 11:
                    Exigir(inventario.IndiceSeleccionado == 0, "La seleccion rapida vuelve al primer objeto");
                    Tecla(Key.I);
                    break;
                case 12:
                    Exigir(menu.MenuAbierto && Time.timeScale == 0, "El modo rapido tambien permite gestion pausada");
                    Tecla(Key.Escape);
                    break;
                case 13:
                    Exigir(!menu.MenuAbierto && Time.timeScale == 1, "Escape cierra la gestion");
                    Tecla();
                    EscribirResultado(true, "esperandoM", "Esperando pulsacion fisica M en Game View para comprobar el input legacy de GameManager.");
                    break;
                case 14:
                    // Los eventos sinteticos del Input System no alimentan Input.GetKeyDown.
                    // Esta ultima comprobacion usa una pulsacion real en Game View.
                    if (personajeCambiado != 1) { siguienteFrame = Time.frameCount + 1; return; }
                    Exigir(personajeCambiado == 1, "M vuelve a funcionar al salir de gestion");
                    Exigir(inventario.ObtenerItem(0) != null && inventario.ObtenerItem(2) != null, "Cambiar de personaje conserva la mochila");
                    Canvas.ForceUpdateCanvases();
                    ScreenCapture.CaptureScreenshot(Path.Combine(Trabajo, "inventario-juego.png"));
                    Tecla();
                    break;
                case 15:
                    EscribirResultado(true, "probar", "Pruebas de teclado, pausa, seleccion y comida superadas en Play. Datos de prueba solo temporales.");
                    fasePrueba = -1;
                    EditorApplication.isPlaying = false;
                    return;
            }
            fasePrueba++;
            siguienteFrame = Time.frameCount + 6;
        }
        catch (Exception error)
        {
            Tecla();
            EscribirResultado(false, "probar", error.ToString());
            Debug.LogException(error);
            fasePrueba = -1;
            EditorApplication.isPlaying = false;
        }
    }

    private static void EscribirResultado(bool correcto, string accion, string detalle)
    {
        Directory.CreateDirectory(Trabajo);
        File.WriteAllText(Path.Combine(Trabajo, "resultado.json"), JsonUtility.ToJson(new Resultado
        {
            correcto = correcto,
            accion = accion,
            escena = SceneManager.GetActiveScene().path,
            detalle = detalle,
            comprobaciones = pruebas.ToArray()
        }, true));
    }
}
#endif

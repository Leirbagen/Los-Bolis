#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using StarterAssets;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

// Montaje y comprobaciones del prototipo; no se incluye en el juego compilado.
[InitializeOnLoad]
public static class PuertasMontajeEditor
{
    private const string PrefabPuerta = "Assets/Prefabs/Mecanismos/PuertaRoja.prefab";
    private const string PrefabSistema = "Assets/Prefabs/Inventario/SistemaInventario.prefab";
    private const string LlaveRoja = "Assets/Datos/Items/LlaveRoja.asset";
    private const string PruebaPendiente = "LosBolis.Puertas.PruebaPendiente";
    private const string PruebaColores = "LosBolis.Puertas.ColoresPendiente";
    private static readonly string Trabajo = Path.GetFullPath("Library/PuertasMontaje");
    private static readonly List<string> Comprobaciones = new List<string>();
    private static double siguienteRevision;
    private static int fase = -1;
    private static int siguienteFrame;
    private static double inicioPrueba;
    private static double esperarHasta;
    private static GameObject puertaPrueba;
    private static PuertaDeslizante hojaPrueba;
    private static PuertasLlaves cerraduraPrueba;
    private static Transform hoja;
    private static Vector3 posicionCerrada;
    private static Vector3 posicionPausa;
    private static Vector3 posicionCerradura;
    private static ItemDatos otraLlave;
    private static GameObject puertaIncompatible;
    private static GameObject puertaLejana;

    [Serializable] private class Solicitud
    {
        public string accion = "";
        public string escena = "";
        public Vector3 posicion = Vector3.zero;
        public float giro = 0f;
    }

    [Serializable] private class Resultado
    {
        public bool correcto;
        public string accion;
        public string escena;
        public string detalle;
        public string[] comprobaciones;
    }

    static PuertasMontajeEditor()
    {
        EditorApplication.update += Revisar;
        EditorApplication.playModeStateChanged += CambioPlay;
    }

    [MenuItem("Tools/Los Bolis/Puertas/Crear prefab y conectar inventario")]
    public static void Montar()
    {
        Exigir(!EditorApplication.isPlayingOrWillChangePlaymode, "Montaje fuera de Play");
        Scene escena = SceneManager.GetActiveScene();
        MenuInventarioUI menu = Buscar<MenuInventarioUI>(escena).Single();
        ItemDatos llave = AssetDatabase.LoadAssetAtPath<ItemDatos>(LlaveRoja);
        Exigir(llave != null && llave.Tipo == TipoItem.Llave, "Asset LlaveRoja valido");
        Exigir(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabSistema) != null, "Prefab SistemaInventario existente");
        Directory.CreateDirectory(Trabajo);
        EditorSceneManager.SaveScene(escena);
        Respaldar(escena.path);
        Respaldar(PrefabSistema);
        Respaldar(PrefabPuerta);
        Carpeta("Assets/Prefabs/Mecanismos");
        Carpeta("Assets/Materiales/PrototipoPuertas");

        CrearPuerta(llave);
        Undo.IncrementCurrentGroup();
        Undo.SetCurrentGroupName("Conectar llaves al inventario");
        Conectar(menu, true);
        GameObject sistema = PrefabUtility.LoadPrefabContents(PrefabSistema);
        try
        {
            MenuInventarioUI menuPrefab = sistema.GetComponent<MenuInventarioUI>();
            Conectar(menuPrefab, false);
            CopiarServicio<IntercambioInventario>(menu, menuPrefab, "intercambioInventario");
            CopiarServicio<ColocadorBombas>(menu, menuPrefab, "colocadorBombas");
            PrefabUtility.SaveAsPrefabAsset(sistema, PrefabSistema);
        }
        finally { PrefabUtility.UnloadPrefabContents(sistema); }
        EditorSceneManager.MarkSceneDirty(escena);
        EditorSceneManager.SaveScene(escena);
        AssetDatabase.SaveAssets();
        Validar();
        Selection.activeObject = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPuerta);
        Escribir(true, "montar", "Prefab de puerta creado; UsoLlaves conectado en escena y prefab de inventario.");
    }

    private static void CrearPuerta(ItemDatos llave)
    {
        // Si ya existe, no sustituimos un modelo o ajustes que el usuario haya cambiado.
        GameObject existente = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPuerta);
        if (existente != null) return;
        Material rojo = MaterialPrototipo("PuertaRoja", new Color(0.65f, 0.08f, 0.06f));
        Material oro = MaterialPrototipo("Cerradura", new Color(0.8f, 0.56f, 0.13f));
        Material oscuro = MaterialPrototipo("Bocallave", new Color(0.055f, 0.045f, 0.035f));
        GameObject raiz = new GameObject("PuertaRoja");
        raiz.SetActive(false);
        try
        {
            PuertaDeslizante puerta = raiz.AddComponent<PuertaDeslizante>();
            GameObject movil = Cubo("HojaPuerta", raiz.transform,
                new Vector3(0, 1.4f, 0), new Vector3(2.2f, 2.8f, 0.35f), rojo, true);
            // Las placas son solo geometria; el collider solido esta en HojaPuerta.
            for (int lado = -1; lado <= 1; lado += 2)
            {
                Cubo("PlacaCerradura" + (lado < 0 ? "Frente" : "Reverso"), movil.transform,
                    new Vector3(0.3f, 0, lado * 0.52f), new Vector3(0.16f, 0.18f, 0.06f), oro, false);
                Cubo("Bocallave" + (lado < 0 ? "Frente" : "Reverso"), movil.transform,
                    new Vector3(0.3f, 0, lado * 0.558f), new Vector3(0.035f, 0.08f, 0.025f), oscuro, false);
            }
            var datosPuerta = new SerializedObject(puerta);
            datosPuerta.FindProperty("hojaMovil").objectReferenceValue = movil.transform;
            datosPuerta.FindProperty("desplazamientoLocal").vector3Value = new Vector3(0, 3, 0);
            datosPuerta.FindProperty("velocidad").floatValue = 3;
            datosPuerta.ApplyModifiedPropertiesWithoutUndo();

            GameObject zona = new GameObject("Cerradura");
            zona.transform.SetParent(raiz.transform, false);
            zona.transform.localPosition = new Vector3(0, 1.1f, 0);
            BoxCollider trigger = zona.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.size = new Vector3(2.4f, 2.2f, 1.6f);
            PuertasLlaves cerradura = zona.AddComponent<PuertasLlaves>();
            var datos = new SerializedObject(cerradura);
            datos.FindProperty("llaveRequerida").objectReferenceValue = llave;
            datos.ApplyModifiedPropertiesWithoutUndo();
            var evento = new UnityEngine.Events.UnityEvent();
            UnityEventTools.AddPersistentListener(evento, puerta.Abrir);
            // Usamos el evento real serializado del componente.
            var campo = typeof(PuertasLlaves).GetField("alAbrir", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            campo.SetValue(cerradura, evento);
            EditorUtility.SetDirty(cerradura);
            raiz.SetActive(true);
            PrefabUtility.SaveAsPrefabAsset(raiz, PrefabPuerta);
        }
        finally { Object.DestroyImmediate(raiz); }
    }

    private static GameObject Cubo(string nombre, Transform padre, Vector3 posicion, Vector3 escala, Material material, bool solido)
    {
        GameObject cubo = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cubo.name = nombre;
        cubo.transform.SetParent(padre, false);
        cubo.transform.localPosition = posicion;
        cubo.transform.localScale = escala;
        cubo.GetComponent<MeshRenderer>().sharedMaterial = material;
        if (!solido) Object.DestroyImmediate(cubo.GetComponent<BoxCollider>());
        return cubo;
    }

    private static Material MaterialPrototipo(string nombre, Color color)
    {
        string ruta = "Assets/Materiales/PrototipoPuertas/" + nombre + ".mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(ruta);
        if (material != null) return material;
        Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        Exigir(shader != null, "Shader disponible para " + nombre);
        material = new Material(shader) { name = nombre, color = color };
        if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.15f);
        AssetDatabase.CreateAsset(material, ruta);
        return material;
    }

    private static void Conectar(MenuInventarioUI menu, bool conUndo)
    {
        Exigir(menu != null, "MenuInventarioUI presente");
        UsoLlaves uso = menu.GetComponent<UsoLlaves>();
        if (uso == null) uso = conUndo ? Undo.AddComponent<UsoLlaves>(menu.gameObject) : menu.gameObject.AddComponent<UsoLlaves>();
        if (conUndo) Undo.RecordObject(menu, "Conectar UsoLlaves");
        var datos = new SerializedObject(uso);
        datos.FindProperty("alcance").floatValue = 2;
        datos.FindProperty("capasCerraduras").intValue = ~0;
        datos.ApplyModifiedProperties();
        var datosMenu = new SerializedObject(menu);
        datosMenu.FindProperty("usoLlaves").objectReferenceValue = uso;
        datosMenu.ApplyModifiedProperties();
        PrefabUtility.RecordPrefabInstancePropertyModifications(menu);
        PrefabUtility.RecordPrefabInstancePropertyModifications(uso);
        VerificarPausa(menu);
    }

    private static void CopiarServicio<T>(MenuInventarioUI origen, MenuInventarioUI destino, string campo) where T : MonoBehaviour
    {
        T servicioOrigen = origen.GetComponent<T>();
        if (servicioOrigen == null) return;
        T servicioDestino = destino.GetComponent<T>();
        if (servicioDestino == null) servicioDestino = destino.gameObject.AddComponent<T>();
        EditorUtility.CopySerialized(servicioOrigen, servicioDestino);
        var datos = new SerializedObject(destino);
        datos.FindProperty(campo).objectReferenceValue = servicioDestino;
        datos.ApplyModifiedPropertiesWithoutUndo();
        Comprobaciones.Add(typeof(T).Name + " copiado al prefab con ajustes de la escena");
    }

    [MenuItem("Tools/Los Bolis/Puertas/Validar montaje")]
    public static void Validar()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPuerta);
        Exigir(prefab != null, "PuertaRoja.prefab existe");
        PuertaDeslizante puerta = prefab.GetComponent<PuertaDeslizante>();
        Exigir(puerta != null, "PuertaDeslizante en raiz");
        Transform movil = prefab.transform.Find("HojaPuerta");
        Transform zona = prefab.transform.Find("Cerradura");
        Exigir(movil != null && zona != null, "Hoja movil y cerradura independientes");
        Exigir(movil.GetComponent<BoxCollider>() != null && !movil.GetComponent<BoxCollider>().isTrigger, "Hoja con collider solido");
        Exigir(zona.GetComponent<BoxCollider>() != null && zona.GetComponent<BoxCollider>().isTrigger, "Cerradura con trigger");
        var datosPuerta = new SerializedObject(puerta);
        Exigir(datosPuerta.FindProperty("hojaMovil").objectReferenceValue == movil, "Referencia interna HojaMovil");
        Exigir(datosPuerta.FindProperty("desplazamientoLocal").vector3Value == new Vector3(0, 3, 0), "Desplazamiento de 3 metros");
        Exigir(Mathf.Approximately(datosPuerta.FindProperty("velocidad").floatValue, 3), "Velocidad de 3 metros por segundo");
        var cerradura = zona.GetComponent<PuertasLlaves>();
        Exigir(cerradura != null && cerradura.LlaveRequerida == AssetDatabase.LoadAssetAtPath<ItemDatos>(LlaveRoja), "Cerradura requiere asset LlaveRoja");
        var evento = (UnityEngine.Events.UnityEvent)typeof(PuertasLlaves).GetField("alAbrir", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(cerradura);
        Exigir(evento != null && evento.GetPersistentEventCount() == 1 && evento.GetPersistentTarget(0) == puerta
            && evento.GetPersistentMethodName(0) == "Abrir", "UnityEvent persistente conectado a Abrir");
        foreach (MenuInventarioUI menu in new[] { Buscar<MenuInventarioUI>(SceneManager.GetActiveScene()).Single(), AssetDatabase.LoadAssetAtPath<GameObject>(PrefabSistema).GetComponent<MenuInventarioUI>() })
        {
            var datosMenu = new SerializedObject(menu);
            UsoLlaves uso = menu.GetComponent<UsoLlaves>();
            Exigir(uso != null && datosMenu.FindProperty("usoLlaves").objectReferenceValue == uso, "UsoLlaves conectado en " + (EditorUtility.IsPersistent(menu) ? "prefab" : "escena"));
            var datosUso = new SerializedObject(uso);
            Exigir(Mathf.Approximately(uso.Alcance, 2) && datosUso.FindProperty("capasCerraduras").intValue == ~0, "Alcance 2 y capas Everything");
            if (EditorUtility.IsPersistent(menu))
            {
                Exigir(menu.GetComponent<IntercambioInventario>() != null && datosMenu.FindProperty("intercambioInventario").objectReferenceValue == menu.GetComponent<IntercambioInventario>(), "Intercambio conectado en prefab reutilizable");
                ColocadorBombas bombas = menu.GetComponent<ColocadorBombas>();
                Exigir(bombas != null && datosMenu.FindProperty("colocadorBombas").objectReferenceValue == bombas, "ColocadorBombas conectado en prefab reutilizable");
                Exigir(new SerializedObject(bombas).FindProperty("prefabBombaColocada").objectReferenceValue != null, "Prefab de bomba conservado");
            }
            VerificarPausa(menu);
        }
    }

    private static void VerificarPausa(MenuInventarioUI menu)
    {
        PausaInventario pausa = menu.GetComponent<PausaInventario>();
        Exigir(pausa != null, "PausaInventario conservado");
        SerializedProperty componentes = new SerializedObject(pausa).FindProperty("componentesASuspender");
        for (int i = 0; i < componentes.arraySize; i++)
        {
            Object componente = componentes.GetArrayElementAtIndex(i).objectReferenceValue;
            Exigir(!(componente is UsoLlaves) && !(componente is PuertasLlaves) && !(componente is PuertaDeslizante), "Mecanica de llaves fuera de componentes a suspender");
        }
    }

    private static void Inspeccionar()
    {
        Scene escena = SceneManager.GetActiveScene();
        GameManager gm = Buscar<GameManager>(escena).Single();
        foreach (GameObject personaje in gm.armaduras)
            Comprobaciones.Add("Personaje " + personaje.name + ": posicion=" + personaje.transform.position.ToString("F2") + ", adelante=" + personaje.transform.forward.ToString("F2"));
        Vector3 origen = gm.armaduras[0].transform.position;
        foreach (Collider col in Buscar<Collider>(escena).Where(c => !c.isTrigger && c.enabled && Vector3.Distance(c.bounds.ClosestPoint(origen), origen) < 12).Take(35))
            Comprobaciones.Add("Solido " + col.name + ": centro=" + col.bounds.center.ToString("F2") + ", tamano=" + col.bounds.size.ToString("F2"));
        foreach (MenuInventarioUI menu in Buscar<MenuInventarioUI>(escena))
            Comprobaciones.Add("SistemaInventario: " + string.Join(", ", menu.GetComponents<MonoBehaviour>().Where(c => c != null).Select(c => c.GetType().Name)));
        Escribir(true, "inspeccionar", "Datos de la escena antes del montaje.");
    }

    private static void Colocar(Solicitud solicitud)
    {
        Scene escena = SceneManager.GetActiveScene();
        Exigir(!Buscar<PuertaDeslizante>(escena).Any(p => p.name == "PuertaRoja_Prueba"), "Sin instancia de prueba duplicada");
        Respaldar(escena.path);
        GameObject go = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPuerta), escena);
        Undo.RegisterCreatedObjectUndo(go, "Colocar PuertaRoja de prueba");
        go.name = "PuertaRoja_Prueba";
        go.transform.SetPositionAndRotation(solicitud.posicion, Quaternion.Euler(0, solicitud.giro, 0));
        PrefabUtility.RecordPrefabInstancePropertyModifications(go.transform);
        EditorSceneManager.MarkSceneDirty(escena);
        EditorSceneManager.SaveScene(escena);
        Selection.activeGameObject = go;
        SceneView.lastActiveSceneView?.Frame(new Bounds(go.transform.position + Vector3.up * 1.5f, Vector3.one * 5), false);
        Escribir(true, "colocar", "Instancia PuertaRoja_Prueba en " + go.transform.position.ToString("F2"));
    }

    private static void Revisar()
    {
        if (EditorApplication.isPlaying) { if (fase >= 0) PasoPrueba(); return; }
        if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode
            || EditorApplication.timeSinceStartup < siguienteRevision) return;
        siguienteRevision = EditorApplication.timeSinceStartup + 0.5;
        string archivo = Path.Combine(Trabajo, "solicitud.json");
        if (!File.Exists(archivo)) return;
        Solicitud solicitud = JsonUtility.FromJson<Solicitud>(File.ReadAllText(archivo));
        File.Delete(archivo);
        Comprobaciones.Clear();
        try
        {
            Exigir(SceneManager.GetActiveScene().path == solicitud.escena, "Escena abierta coincide con solicitud");
            switch (solicitud.accion)
            {
                case "inspeccionar": Inspeccionar(); break;
                case "montar": Montar(); break;
                case "colocar": Colocar(solicitud); break;
                case "validar": Validar(); Escribir(true, "validar", "Montaje validado."); break;
                case "colores":
                    Comprobaciones.AddRange(LlavesColoresEditor.CrearEjemplos());
                    Escribir(true, "colores", "Llaves y puertas de colores creadas.");
                    break;
                case "probarColores":
                    Comprobaciones.AddRange(LlavesColoresEditor.ValidarEjemplos());
                    EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
                    SessionState.SetBool(PruebaColores, true);
                    EditorApplication.isPlaying = true;
                    break;
                case "probar":
                    Validar();
                    EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
                    SessionState.SetBool(PruebaPendiente, true);
                    EditorApplication.isPlaying = true;
                    break;
                default: throw new InvalidOperationException("Accion desconocida.");
            }
        }
        catch (Exception error) { Escribir(false, solicitud.accion, error.ToString()); Debug.LogException(error); }
    }

    private static void CambioPlay(PlayModeStateChange cambio)
    {
        if (cambio == PlayModeStateChange.EnteredPlayMode && (SessionState.GetBool(PruebaPendiente, false) || SessionState.GetBool(PruebaColores, false)))
        {
            fase = SessionState.GetBool(PruebaColores, false) ? 100 : 0;
            siguienteFrame = Time.frameCount + 10;
            inicioPrueba = EditorApplication.timeSinceStartup;
            Comprobaciones.Clear();
        }
        if (cambio == PlayModeStateChange.ExitingPlayMode)
        {
            fase = -1;
            SessionState.SetBool(PruebaPendiente, false);
            SessionState.SetBool(PruebaColores, false);
            if (Keyboard.current != null) Tecla();
        }
    }

    private static void PasoPrueba()
    {
        if (Time.frameCount < siguienteFrame || EditorApplication.timeSinceStartup < esperarHasta) return;
        try
        {
            if (EditorApplication.timeSinceStartup - inicioPrueba > 45) throw new TimeoutException("Prueba de puertas excedio 45 segundos.");
            if (fase == 100)
            {
                Comprobaciones.AddRange(LlavesColoresEditor.ComprobarEnPlay());
                Escribir(true, "probarColores", "Las nueve combinaciones de llave y puerta se comportan correctamente en Play.");
                fase = -1;
                EditorApplication.isPlaying = false;
                return;
            }
            GameObject personaje = GameManager.Instancia.armaduras[0];
            InventarioPersonaje inventario = personaje.GetComponent<InventarioPersonaje>();
            MenuInventarioUI menu = Object.FindFirstObjectByType<MenuInventarioUI>();
            ItemDatos roja = AssetDatabase.LoadAssetAtPath<ItemDatos>(LlaveRoja);
            Vector3 basePrueba = new Vector3(1000, 100, 1000);
            switch (fase)
            {
                case 0:
                    Exigir(menu != null && Keyboard.current != null, "Menu y teclado en Play");
                    Time.timeScale = 0.25f;
                    foreach (GameObject p in GameManager.Instancia.armaduras)
                    {
                        ThirdPersonController control = p.GetComponent<ThirdPersonController>();
                        if (control != null) control.enabled = false;
                    }
                    CharacterController cc = personaje.GetComponent<CharacterController>();
                    if (cc != null) cc.enabled = false;
                    personaje.transform.position = basePrueba;
                    personaje.transform.rotation = Quaternion.identity;
                    for (int i = 0; i < 4; i++) inventario.IntentarQuitar(i);
                    otraLlave = ScriptableObject.CreateInstance<ItemDatos>();
                    otraLlave.name = "LlaveIncompatibleSoloPrueba";
                    var datosLlave = new SerializedObject(otraLlave);
                    datosLlave.FindProperty("tipo").enumValueIndex = (int)TipoItem.Llave;
                    datosLlave.ApplyModifiedPropertiesWithoutUndo();
                    inventario.IntentarAgregar(otraLlave);
                    inventario.IntentarAgregar(roja);
                    inventario.IntentarAgregar(roja);
                    inventario.IntentarSeleccionar(1);
                    puertaPrueba = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPuerta), basePrueba + Vector3.forward * 30, Quaternion.identity);
                    hojaPrueba = puertaPrueba.GetComponent<PuertaDeslizante>();
                    cerraduraPrueba = puertaPrueba.GetComponentInChildren<PuertasLlaves>();
                    hoja = puertaPrueba.transform.Find("HojaPuerta");
                    posicionCerrada = hoja.localPosition;
                    Physics.SyncTransforms();
                    Tecla(Key.F);
                    break;
                case 1:
                    Exigir(!cerraduraPrueba.Abierto && inventario.ObtenerItem(1) == roja, "F fuera de alcance conserva llave y puerta cerrada");
                    Tecla();
                    puertaPrueba.transform.position = basePrueba + Vector3.forward * 1.5f;
                    posicionCerradura = cerraduraPrueba.transform.position;
                    inventario.IntentarSeleccionar(0);
                    Physics.SyncTransforms();
                    break;
                case 2: Tecla(Key.F); break;
                case 3:
                    Exigir(!cerraduraPrueba.Abierto && inventario.ObtenerItem(0) == otraLlave, "Llave incorrecta no se consume ni abre");
                    inventario.IntentarSeleccionar(1);
                    Tecla();
                    break;
                case 4: Tecla(Key.F); break;
                case 5:
                    Exigir(cerraduraPrueba.Abierto && (hojaPrueba.EnApertura || hojaPrueba.Abierta), "F con LlaveRoja ejecuta evento y abre");
                    Exigir(inventario.ObtenerItem(1) == null && inventario.ObtenerItem(2) == roja, "Se consume exactamente una llave");
                    Exigir(hoja.localPosition.y > posicionCerrada.y, "La hoja se desliza hacia arriba");
                    inventario.IntentarSeleccionar(2);
                    Tecla();
                    break;
                case 6: Tecla(Key.F); break;
                case 7:
                    Exigir(inventario.ObtenerItem(2) == roja, "Repetir F sobre puerta abierta conserva segunda llave");
                    Tecla();
                    break;
                case 8: Tecla(Key.I); break;
                case 9:
                    Exigir(menu.MenuAbierto && Mathf.Approximately(Time.timeScale, 0), "I abre inventario y pausa");
                    Exigir(hojaPrueba.EnApertura && !hojaPrueba.Abierta, "Pausa comprobada con puerta todavia en movimiento");
                    posicionPausa = hoja.localPosition;
                    Tecla();
                    esperarHasta = EditorApplication.timeSinceStartup + 0.35;
                    break;
                case 10:
                    Exigir(hoja.localPosition == posicionPausa, "Hoja inmovil durante la pausa");
                    Tecla(Key.F);
                    break;
                case 11:
                    Exigir(inventario.ObtenerItem(2) == roja, "F con inventario abierto no usa llaves");
                    Tecla();
                    break;
                case 12: Tecla(Key.I); break;
                case 13:
                    Exigir(!menu.MenuAbierto && Time.timeScale > 0, "Cerrar inventario reanuda");
                    Time.timeScale = 1;
                    Tecla();
                    esperarHasta = EditorApplication.timeSinceStartup + 1.4;
                    break;
                case 14:
                    Exigir(hojaPrueba.Abierta && !hojaPrueba.EnApertura && Vector3.Distance(hoja.localPosition, posicionCerrada + Vector3.up * 3) < 0.001f, "Hoja alcanza exactamente desplazamiento de 3 metros");
                    Exigir(cerraduraPrueba.transform.position == posicionCerradura, "Cerradura permanece fija");
                    Exigir(hoja.GetComponent<BoxCollider>().enabled && !hoja.GetComponent<BoxCollider>().isTrigger, "Collider solido viaja con la hoja");
                    puertaIncompatible = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPuerta), basePrueba + Vector3.forward, Quaternion.identity);
                    var datosCerradura = new SerializedObject(puertaIncompatible.GetComponentInChildren<PuertasLlaves>());
                    datosCerradura.FindProperty("llaveRequerida").objectReferenceValue = otraLlave;
                    datosCerradura.ApplyModifiedPropertiesWithoutUndo();
                    puertaLejana = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPuerta), basePrueba + Vector3.forward * 1.8f, Quaternion.identity);
                    Physics.SyncTransforms();
                    break;
                case 15: Tecla(Key.F); break;
                case 16:
                    Exigir(!puertaIncompatible.GetComponentInChildren<PuertasLlaves>().Abierto && !puertaLejana.GetComponentInChildren<PuertasLlaves>().Abierto && inventario.ObtenerItem(2) == roja,
                        "Se elige cerradura cercana incompatible sin abrir otra mas lejana");
                    Tecla();
                    Object.Destroy(otraLlave);
                    Escribir(true, "probar", "Comprobaciones en Play superadas. Inventario y posiciones de prueba no se guardan.");
                    fase = -1;
                    EditorApplication.isPlaying = false;
                    return;
            }
            fase++;
            siguienteFrame = Time.frameCount + 2;
        }
        catch (Exception error)
        {
            Tecla();
            Escribir(false, "probar", error.ToString());
            Debug.LogException(error);
            fase = -1;
            EditorApplication.isPlaying = false;
        }
    }

    private static void Tecla(params Key[] teclas) => InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState(teclas));
    private static IEnumerable<T> Buscar<T>(Scene escena) where T : Component
        => escena.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<T>(true));
    private static void Exigir(bool condicion, string mensaje)
    {
        if (!condicion) throw new InvalidOperationException(mensaje);
        Comprobaciones.Add(mensaje);
    }
    private static void Carpeta(string ruta)
    {
        if (AssetDatabase.IsValidFolder(ruta)) return;
        string padre = Path.GetDirectoryName(ruta).Replace('\\', '/');
        Carpeta(padre);
        AssetDatabase.CreateFolder(padre, Path.GetFileName(ruta));
    }
    private static void Respaldar(string ruta)
    {
        if (File.Exists(ruta))
            File.Copy(ruta, Path.Combine(Trabajo, DateTime.Now.ToString("yyyyMMdd-HHmmss-fff") + "-" + Path.GetFileName(ruta)), false);
    }
    private static void Escribir(bool correcto, string accion, string detalle)
    {
        Directory.CreateDirectory(Trabajo);
        var resultado = new Resultado { correcto = correcto, accion = accion, escena = SceneManager.GetActiveScene().path,
            detalle = detalle, comprobaciones = Comprobaciones.ToArray() };
        File.WriteAllText(Path.Combine(Trabajo, "resultado.json"), JsonUtility.ToJson(resultado, true));
    }
}
#endif

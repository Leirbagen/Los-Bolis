#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Events;
using Object = UnityEngine.Object;

// Crea datos y variantes. La logica del juego sigue usando ItemDatos y PuertasLlaves.
public class LlavesColoresEditor : EditorWindow
{
    private const string Datos = "Assets/Datos/Items/";
    private const string Recogibles = "Assets/Prefabs/Items/";
    private const string Puertas = "Assets/Prefabs/Mecanismos/";
    private const string Iconos = "Assets/UI/Iconos/Inventario/";
    private const string Materiales = "Assets/Materiales/PrototipoPuertas/";
    private static readonly string[] ColoresEjemplo = { "Roja", "Azul", "Amarilla" };

    [SerializeField] private string nombreVariante = "Verde";
    [SerializeField] private Color colorVariante = new Color(0.18f, 0.65f, 0.24f);
    private string resultado = "";

    [MenuItem("Tools/Los Bolis/Puertas/Crear llave de otro color")]
    public static void Mostrar()
    {
        GetWindow<LlavesColoresEditor>("Nueva llave").minSize = new Vector2(360, 240);
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Llave y puerta del mismo color", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Escribe un nombre, por ejemplo Verde. Se crean un ItemDatos, un icono y variantes de la llave recogible y de la puerta. Los assets existentes se conservan.", MessageType.Info);
        nombreVariante = EditorGUILayout.TextField("Nombre de variante", nombreVariante);
        colorVariante = EditorGUILayout.ColorField("Color", colorVariante);
        using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
        {
            if (GUILayout.Button("Crear llave y puerta"))
            {
                try
                {
                    CrearVariante(nombreVariante, colorVariante);
                    AssetDatabase.SaveAssets();
                    Selection.activeObject = AssetDatabase.LoadAssetAtPath<GameObject>(Puertas + "Puerta" + nombreVariante.Trim() + ".prefab");
                    EditorGUIUtility.PingObject(Selection.activeObject);
                    resultado = "Listo. Encontraras la llave en Prefabs/Items y la puerta en Prefabs/Mecanismos.";
                }
                catch (Exception error) { resultado = error.Message; Debug.LogException(error); }
            }
        }
        if (!string.IsNullOrEmpty(resultado)) EditorGUILayout.HelpBox(resultado, MessageType.None);
    }

    public static string[] CrearEjemplos()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Sal de Play antes de crear las variantes.");
        ItemDatos roja = AssetDatabase.LoadAssetAtPath<ItemDatos>(Datos + "LlaveRoja.asset");
        Exigir(roja != null, "Falta LlaveRoja.asset");
        var datosRoja = new SerializedObject(roja);
        if (roja.Icono == null)
        {
            string respaldo = "Library/PuertasMontaje/LlaveRojaAntesIcono-" + DateTime.Now.ToString("yyyyMMdd-HHmmss-fff") + ".asset";
            Directory.CreateDirectory("Library/PuertasMontaje");
            File.Copy(Datos + "LlaveRoja.asset", respaldo);
            datosRoja.FindProperty("icono").objectReferenceValue = CrearIcono("Roja", new Color32(227, 60, 48, 255));
            datosRoja.ApplyModifiedPropertiesWithoutUndo();
        }
        CrearVariante("Azul", new Color32(46, 112, 230, 255));
        CrearVariante("Amarilla", new Color32(245, 199, 43, 255));
        AssetDatabase.SaveAssets();
        return ValidarEjemplos();
    }

    public static void CrearVariante(string nombre, Color color)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Sal de Play antes de crear las variantes.");
        nombre = nombre.Trim();
        Exigir(!string.IsNullOrEmpty(nombre) && nombre.All(c => c >= 'A' && c <= 'Z' || c >= 'a' && c <= 'z' || c >= '0' && c <= '9'),
            "Usa un nombre sin espacios ni tildes, por ejemplo Verde o AzulTemplo.");
        Exigir(nombre != "Roja", "Roja es la variante base. Elige otro nombre.");
        GameObject baseLlave = AssetDatabase.LoadAssetAtPath<GameObject>(Recogibles + "LlaveRojaRecogible.prefab");
        GameObject basePuerta = AssetDatabase.LoadAssetAtPath<GameObject>(Puertas + "PuertaRoja.prefab");
        Exigir(baseLlave != null && basePuerta != null, "Faltan los prefabs base LlaveRojaRecogible y PuertaRoja.");
        color.a = 1;
        Sprite icono = CrearIcono(nombre, color);
        string rutaDatos = Datos + "Llave" + nombre + ".asset";
        ItemDatos llave = AssetDatabase.LoadAssetAtPath<ItemDatos>(rutaDatos);
        if (llave == null)
        {
            llave = ScriptableObject.CreateInstance<ItemDatos>();
            var datos = new SerializedObject(llave);
            datos.FindProperty("id").stringValue = "llave_" + nombre.ToLowerInvariant();
            datos.FindProperty("nombreVisible").stringValue = "Llave " + nombre;
            datos.FindProperty("tipo").enumValueIndex = (int)TipoItem.Llave;
            datos.FindProperty("icono").objectReferenceValue = icono;
            datos.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.CreateAsset(llave, rutaDatos);
        }
        Exigir(llave.Tipo == TipoItem.Llave, "Ya existe un item de otro tipo con ese nombre. Elige otro nombre.");
        string rutaMaterial = Materiales + "Puerta" + nombre + ".mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(rutaMaterial);
        if (material == null)
        {
            Material baseMaterial = basePuerta.transform.Find("HojaPuerta").GetComponent<MeshRenderer>().sharedMaterial;
            material = new Material(baseMaterial) { name = "Puerta" + nombre, color = color };
            AssetDatabase.CreateAsset(material, rutaMaterial);
        }
        string rutaRecogible = Recogibles + "Llave" + nombre + "Recogible.prefab";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(rutaRecogible) == null)
        {
            GameObject instancia = (GameObject)PrefabUtility.InstantiatePrefab(baseLlave);
            try
            {
                instancia.name = "Llave" + nombre + "Recogible";
                instancia.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                var datos = new SerializedObject(instancia.GetComponent<ItemRecogible>());
                datos.FindProperty("itemDatos").objectReferenceValue = llave;
                datos.ApplyModifiedPropertiesWithoutUndo();
                instancia.GetComponentInChildren<MeshRenderer>().sharedMaterial = material;
                PrefabUtility.RecordPrefabInstancePropertyModifications(instancia.GetComponent<ItemRecogible>());
                PrefabUtility.RecordPrefabInstancePropertyModifications(instancia.GetComponentInChildren<MeshRenderer>());
                PrefabUtility.SaveAsPrefabAsset(instancia, rutaRecogible);
            }
            finally { Object.DestroyImmediate(instancia); }
        }
        string rutaPuerta = Puertas + "Puerta" + nombre + ".prefab";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(rutaPuerta) == null)
        {
            GameObject instancia = (GameObject)PrefabUtility.InstantiatePrefab(basePuerta);
            try
            {
                instancia.name = "Puerta" + nombre;
                var datos = new SerializedObject(instancia.GetComponentInChildren<PuertasLlaves>());
                datos.FindProperty("llaveRequerida").objectReferenceValue = llave;
                datos.ApplyModifiedPropertiesWithoutUndo();
                MeshRenderer renderer = instancia.transform.Find("HojaPuerta").GetComponent<MeshRenderer>();
                renderer.sharedMaterial = material;
                PrefabUtility.RecordPrefabInstancePropertyModifications(instancia.GetComponentInChildren<PuertasLlaves>());
                PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
                PrefabUtility.SaveAsPrefabAsset(instancia, rutaPuerta);
            }
            finally { Object.DestroyImmediate(instancia); }
        }
    }

    private static Sprite CrearIcono(string nombre, Color color)
    {
        Carpeta(Iconos.TrimEnd('/'));
        string ruta = Iconos + "Llave" + nombre + ".png";
        if (!File.Exists(ruta))
        {
            var textura = new Texture2D(24, 24, TextureFormat.RGBA32, false);
            try
            {
                textura.SetPixels(new Color[24 * 24]);
                Color sombra = new Color(color.r * 0.4f, color.g * 0.4f, color.b * 0.4f, 1);
                // Misma silueta sencilla que el icono temporal del inventario.
                Rectangulo(textura, 7, 13, 10, 2, sombra);
                Rectangulo(textura, 7, 21, 10, 2, sombra);
                Rectangulo(textura, 5, 15, 2, 6, sombra);
                Rectangulo(textura, 17, 15, 2, 6, sombra);
                Rectangulo(textura, 7, 15, 10, 2, color);
                Rectangulo(textura, 7, 19, 10, 2, color);
                Rectangulo(textura, 7, 17, 3, 2, color);
                Rectangulo(textura, 14, 17, 3, 2, color);
                Rectangulo(textura, 10, 2, 4, 13, sombra);
                Rectangulo(textura, 11, 3, 2, 12, color);
                Rectangulo(textura, 13, 3, 5, 3, color);
                Rectangulo(textura, 13, 8, 4, 3, color);
                textura.Apply();
                File.WriteAllBytes(ruta, textura.EncodeToPNG());
            }
            finally { Object.DestroyImmediate(textura); }
            AssetDatabase.ImportAsset(ruta, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(ruta);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 24;
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.SaveAndReimport();
        }
        Sprite icono = AssetDatabase.LoadAssetAtPath<Sprite>(ruta);
        Exigir(icono != null, "No se pudo importar el icono " + ruta);
        return icono;
    }

    private static void Rectangulo(Texture2D textura, int x, int y, int ancho, int alto, Color color)
    {
        for (int fila = y; fila < y + alto; fila++)
            for (int columna = x; columna < x + ancho; columna++) textura.SetPixel(columna, fila, color);
    }

    public static string[] ValidarEjemplos()
    {
        var resultado = new List<string>();
        var unicas = new HashSet<ItemDatos>();
        var iconosUnicos = new HashSet<Sprite>();
        foreach (string nombre in ColoresEjemplo)
        {
            ItemDatos llave = AssetDatabase.LoadAssetAtPath<ItemDatos>(Datos + "Llave" + nombre + ".asset");
            Exigir(llave != null && llave.Tipo == TipoItem.Llave && unicas.Add(llave), "ItemDatos independiente: " + nombre);
            Exigir(llave.Icono != null && iconosUnicos.Add(llave.Icono), "Icono independiente: " + nombre);
            GameObject recogible = AssetDatabase.LoadAssetAtPath<GameObject>(Recogibles + "Llave" + nombre + "Recogible.prefab");
            GameObject puerta = AssetDatabase.LoadAssetAtPath<GameObject>(Puertas + "Puerta" + nombre + ".prefab");
            Exigir(recogible != null && recogible.GetComponent<ItemRecogible>().ItemDatos == llave, "ItemRecogible conectado: " + nombre);
            Exigir(recogible.GetComponents<Collider>().Any(c => c.isTrigger) && recogible.GetComponent<Rigidbody>().isKinematic && !recogible.GetComponent<Rigidbody>().useGravity, "Fisica del recogible: " + nombre);
            Exigir(puerta != null && puerta.GetComponentInChildren<PuertasLlaves>().LlaveRequerida == llave, "Cerradura conectada: " + nombre);
            var evento = (UnityEvent)typeof(PuertasLlaves).GetField("alAbrir", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(puerta.GetComponentInChildren<PuertasLlaves>());
            Exigir(evento.GetPersistentEventCount() == 1 && evento.GetPersistentTarget(0) == puerta.GetComponent<PuertaDeslizante>() && evento.GetPersistentMethodName(0) == "Abrir", "Evento interno conservado: " + nombre);
            if (nombre != "Roja")
            {
                Exigir(PrefabUtility.GetPrefabAssetType(recogible) == PrefabAssetType.Variant && PrefabUtility.GetPrefabAssetType(puerta) == PrefabAssetType.Variant, "Prefabs son variantes: " + nombre);
                Exigir(puerta.transform.Find("HojaPuerta").GetComponent<MeshRenderer>().sharedMaterial == recogible.GetComponentInChildren<MeshRenderer>().sharedMaterial, "Color del mundo coherente: " + nombre);
            }
            resultado.Add("Llave " + nombre + ": datos, icono, recogible, puerta y evento conectados.");
        }
        return resultado.ToArray();
    }

    public static string[] ComprobarEnPlay()
    {
        var resultado = new List<string>(ValidarEjemplos());
        foreach (string nombrePuerta in ColoresEjemplo)
            foreach (string nombreLlave in ColoresEjemplo)
            {
                GameObject objetoInventario = new GameObject("InventarioTemporalColores");
                objetoInventario.transform.position = new Vector3(1000, 100, 1000);
                InventarioPersonaje inventario = objetoInventario.AddComponent<InventarioPersonaje>();
                GameObject instancia = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Puertas + "Puerta" + nombrePuerta + ".prefab"), new Vector3(1000, 100, 1005), Quaternion.identity);
                try
                {
                    ItemDatos llave = AssetDatabase.LoadAssetAtPath<ItemDatos>(Datos + "Llave" + nombreLlave + ".asset");
                    inventario.IntentarAgregar(llave);
                    PuertasLlaves cerradura = instancia.GetComponentInChildren<PuertasLlaves>();
                    PuertaDeslizante puerta = instancia.GetComponent<PuertaDeslizante>();
                    bool correcta = nombrePuerta == nombreLlave;
                    Exigir(cerradura.IntentarAbrir(inventario, 0) == correcta && cerradura.Abierto == correcta, "Coincidencia de color incorrecta");
                    Exigir(correcta ? inventario.ObtenerItem(0) == null && puerta.EnApertura : inventario.ObtenerItem(0) == llave && !puerta.EnApertura, "Consumo o evento incorrectos");
                    resultado.Add("Puerta " + nombrePuerta + " + llave " + nombreLlave + ": " + (correcta ? "abre y consume una" : "no abre y conserva la llave"));
                }
                finally { Object.Destroy(instancia); Object.Destroy(objetoInventario); }
            }
        return resultado.ToArray();
    }

    private static void Carpeta(string ruta)
    {
        if (AssetDatabase.IsValidFolder(ruta)) return;
        Carpeta(Path.GetDirectoryName(ruta).Replace('\\', '/'));
        AssetDatabase.CreateFolder(Path.GetDirectoryName(ruta).Replace('\\', '/'), Path.GetFileName(ruta));
    }

    private static void Exigir(bool condicion, string mensaje)
    {
        if (!condicion) throw new InvalidOperationException(mensaje);
    }
}
#endif

using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class Terrain250SceneBuilder
{
    private const float MapSize = 250f;
    private const int HeightResolution = 513;
    private const int AlphaResolution = 512;
    private const int DetailResolution = 256;

    [MenuItem("Tools/Terrain/Create 250x250 Map Scene")]
    private static void CreateScene()
    {
        Terrain sourceTerrain = FindCentralTerrain();
        if (sourceTerrain == null || sourceTerrain.terrainData == null)
        {
            Debug.LogError("Could not find the center terrain tile in the open scene.");
            return;
        }

        string dataFolder = "Assets/Scenes/Generated/TerrainData";
        string scenesFolder = "Assets/Scenes/Generated";
        Directory.CreateDirectory(dataFolder);
        Directory.CreateDirectory(scenesFolder);

        TerrainData compactData = CreateCompactTerrainData(sourceTerrain.terrainData);
        string dataPath = AssetDatabase.GenerateUniqueAssetPath(dataFolder + "/Terrain_250.asset");
        AssetDatabase.CreateAsset(compactData, dataPath);
        AssetDatabase.SaveAssets();

        Scene compactScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        SceneManager.SetActiveScene(compactScene);

        GameObject terrainObject = Terrain.CreateTerrainGameObject(compactData);
        terrainObject.name = "Terrain 250x250";
        Terrain terrain = terrainObject.GetComponent<Terrain>();
        terrain.drawInstanced = true;
        terrain.detailObjectDistance = 60f;
        terrain.detailObjectDensity = 0.7f;
        terrain.treeDistance = 500f;
        terrain.treeBillboardDistance = 100f;
        terrain.heightmapPixelError = 10f;
        terrain.Flush();

        Vector3 mapCenter = new Vector3(MapSize * 0.5f, compactData.GetInterpolatedHeight(0.5f, 0.5f), MapSize * 0.5f);
        CreateLightingAndCamera(mapCenter);
        ScatterRocks(terrain, mapCenter);

        string scenePath = AssetDatabase.GenerateUniqueAssetPath(scenesFolder + "/Terrain250.unity");
        if (!EditorSceneManager.SaveScene(compactScene, scenePath))
        {
            Debug.LogError("Could not save the 250x250 terrain scene.");
            return;
        }

        Selection.activeGameObject = terrainObject;
        Debug.Log("Created a 250x250 terrain scene at " + scenePath + " using " + dataPath + ".");
    }

    private static Terrain FindCentralTerrain()
    {
        Terrain fallback = null;
        foreach (Terrain terrain in Object.FindObjectsByType<Terrain>(FindObjectsSortMode.None))
        {
            if (terrain.terrainData == null)
            {
                continue;
            }

            if (terrain.name.StartsWith("Terrain_2_2"))
            {
                return terrain;
            }

            if (fallback == null || terrain.transform.position.sqrMagnitude < fallback.transform.position.sqrMagnitude)
            {
                fallback = terrain;
            }
        }

        return fallback;
    }

    private static TerrainData CreateCompactTerrainData(TerrainData source)
    {
        TerrainData compact = Object.Instantiate(source);
        compact.name = "Terrain_250";

        float fractionX = MapSize / source.size.x;
        float fractionZ = MapSize / source.size.z;
        float startX = (1f - fractionX) * 0.5f;
        float startZ = (1f - fractionZ) * 0.5f;

        int sourceHeightResolution = source.heightmapResolution;
        int heightStartX = Mathf.RoundToInt(startX * (sourceHeightResolution - 1));
        int heightStartZ = Mathf.RoundToInt(startZ * (sourceHeightResolution - 1));
        int heightCountX = Mathf.RoundToInt(fractionX * (sourceHeightResolution - 1)) + 1;
        int heightCountZ = Mathf.RoundToInt(fractionZ * (sourceHeightResolution - 1)) + 1;
        float[,] sourceHeights = source.GetHeights(heightStartX, heightStartZ, heightCountX, heightCountZ);

        compact.heightmapResolution = HeightResolution;
        compact.SetHeights(0, 0, ResampleHeights(sourceHeights, HeightResolution, HeightResolution));
        compact.size = new Vector3(MapSize, source.size.y, MapSize);

        CopyAlphamaps(source, compact, startX, startZ, fractionX, fractionZ);
        CopyDetails(source, compact, startX, startZ, fractionX, fractionZ);
        CopyTrees(source, compact, startX, startZ, fractionX, fractionZ);

        return compact;
    }

    private static void CopyAlphamaps(TerrainData source, TerrainData target, float startX, float startZ, float fractionX, float fractionZ)
    {
        int resolution = source.alphamapResolution;
        int x = Mathf.Clamp(Mathf.RoundToInt(startX * resolution), 0, resolution - 1);
        int z = Mathf.Clamp(Mathf.RoundToInt(startZ * resolution), 0, resolution - 1);
        int width = Mathf.Clamp(Mathf.RoundToInt(fractionX * resolution), 1, resolution - x);
        int height = Mathf.Clamp(Mathf.RoundToInt(fractionZ * resolution), 1, resolution - z);
        float[,,] sourceMaps = source.GetAlphamaps(x, z, width, height);

        target.alphamapResolution = AlphaResolution;
        target.SetAlphamaps(0, 0, ResampleAlphamaps(sourceMaps, AlphaResolution, AlphaResolution));
    }

    private static void CopyDetails(TerrainData source, TerrainData target, float startX, float startZ, float fractionX, float fractionZ)
    {
        DetailPrototype[] prototypes = source.detailPrototypes;
        target.detailPrototypes = prototypes;
        target.SetDetailResolution(DetailResolution, 16);

        int resolution = source.detailResolution;
        if (resolution == 0 || prototypes.Length == 0)
        {
            return;
        }

        int x = Mathf.Clamp(Mathf.RoundToInt(startX * resolution), 0, resolution - 1);
        int z = Mathf.Clamp(Mathf.RoundToInt(startZ * resolution), 0, resolution - 1);
        int width = Mathf.Clamp(Mathf.RoundToInt(fractionX * resolution), 1, resolution - x);
        int height = Mathf.Clamp(Mathf.RoundToInt(fractionZ * resolution), 1, resolution - z);

        for (int layer = 0; layer < prototypes.Length; layer++)
        {
            int[,] sourceLayer = source.GetDetailLayer(x, z, width, height, layer);
            target.SetDetailLayer(0, 0, layer, ResampleDetails(sourceLayer, DetailResolution, DetailResolution));
        }
    }

    private static void CopyTrees(TerrainData source, TerrainData target, float startX, float startZ, float fractionX, float fractionZ)
    {
        TreeInstance[] sourceTrees = source.treeInstances;
        List<TreeInstance> compactTrees = new List<TreeInstance>(sourceTrees.Length);

        foreach (TreeInstance tree in sourceTrees)
        {
            if (tree.position.x < startX || tree.position.x > startX + fractionX ||
                tree.position.z < startZ || tree.position.z > startZ + fractionZ)
            {
                continue;
            }

            TreeInstance compactTree = tree;
            Vector3 position = compactTree.position;
            position.x = (position.x - startX) / fractionX;
            position.z = (position.z - startZ) / fractionZ;
            compactTree.position = position;
            compactTrees.Add(compactTree);
        }

        target.treeInstances = compactTrees.ToArray();
    }

    private static float[,] ResampleHeights(float[,] source, int width, int height)
    {
        float[,] result = new float[height, width];
        for (int y = 0; y < height; y++)
        {
            float sourceY = height == 1 ? 0f : y * (source.GetLength(0) - 1f) / (height - 1f);
            int y0 = Mathf.FloorToInt(sourceY);
            int y1 = Mathf.Min(y0 + 1, source.GetLength(0) - 1);
            float ty = sourceY - y0;

            for (int x = 0; x < width; x++)
            {
                float sourceX = width == 1 ? 0f : x * (source.GetLength(1) - 1f) / (width - 1f);
                int x0 = Mathf.FloorToInt(sourceX);
                int x1 = Mathf.Min(x0 + 1, source.GetLength(1) - 1);
                float tx = sourceX - x0;
                float lower = Mathf.Lerp(source[y0, x0], source[y0, x1], tx);
                float upper = Mathf.Lerp(source[y1, x0], source[y1, x1], tx);
                result[y, x] = Mathf.Lerp(lower, upper, ty);
            }
        }

        return result;
    }

    private static float[,,] ResampleAlphamaps(float[,,] source, int width, int height)
    {
        int layers = source.GetLength(2);
        float[,,] result = new float[height, width, layers];
        for (int y = 0; y < height; y++)
        {
            int sourceY = Mathf.Clamp(Mathf.RoundToInt(y * (source.GetLength(0) - 1f) / (height - 1f)), 0, source.GetLength(0) - 1);
            for (int x = 0; x < width; x++)
            {
                int sourceX = Mathf.Clamp(Mathf.RoundToInt(x * (source.GetLength(1) - 1f) / (width - 1f)), 0, source.GetLength(1) - 1);
                for (int layer = 0; layer < layers; layer++)
                {
                    result[y, x, layer] = source[sourceY, sourceX, layer];
                }
            }
        }

        return result;
    }

    private static int[,] ResampleDetails(int[,] source, int width, int height)
    {
        int[,] result = new int[height, width];
        for (int y = 0; y < height; y++)
        {
            int sourceY = Mathf.Clamp(Mathf.RoundToInt(y * (source.GetLength(0) - 1f) / (height - 1f)), 0, source.GetLength(0) - 1);
            for (int x = 0; x < width; x++)
            {
                int sourceX = Mathf.Clamp(Mathf.RoundToInt(x * (source.GetLength(1) - 1f) / (width - 1f)), 0, source.GetLength(1) - 1);
                result[y, x] = source[sourceY, sourceX];
            }
        }

        return result;
    }

    private static void CreateLightingAndCamera(Vector3 mapCenter)
    {
        RenderSettings.skybox = AssetDatabase.LoadAssetAtPath<Material>("Assets/GreenfallEnvironment/Prefabs/Skybox/Materials/Sky.mat");
        RenderSettings.fog = true;
        RenderSettings.fogColor = new Color(0.78f, 0.84f, 0.88f);
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogDensity = 0.0015f;
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Skybox;
        RenderSettings.ambientIntensity = 1f;

        GameObject sunObject = new GameObject("Sun");
        Light sun = sunObject.AddComponent<Light>();
        sun.type = LightType.Directional;
        sun.color = new Color(1f, 0.91f, 0.78f);
        sun.intensity = 3f;
        sun.shadows = LightShadows.Soft;
        sunObject.transform.rotation = Quaternion.Euler(38f, -32f, 0f);
        RenderSettings.sun = sun;

        GameObject cameraObject = new GameObject("Main Camera");
        cameraObject.tag = "MainCamera";
        cameraObject.transform.position = mapCenter + new Vector3(0f, 90f, -190f);
        cameraObject.transform.LookAt(mapCenter);
        Camera camera = cameraObject.AddComponent<Camera>();
        camera.fieldOfView = 60f;
        camera.farClipPlane = 1000f;
        cameraObject.AddComponent<AudioListener>();
    }

    private static void ScatterRocks(Terrain terrain, Vector3 mapCenter)
    {
        string[] paths =
        {
            "Assets/GreenfallEnvironment/Prefabs/Rocks/Rock_A_01.prefab",
            "Assets/GreenfallEnvironment/Prefabs/Rocks/Rock_B_01.prefab",
            "Assets/GreenfallEnvironment/Prefabs/Rocks/Rock_C_01.prefab",
            "Assets/GreenfallEnvironment/Prefabs/Rocks/Rock_Overgrown_A.prefab"
        };
        List<GameObject> prefabs = new List<GameObject>();
        foreach (string path in paths)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab != null)
            {
                prefabs.Add(prefab);
            }
        }

        if (prefabs.Count == 0)
        {
            return;
        }

        GameObject rocks = new GameObject("Rocks");
        Random.State previousRandomState = Random.state;
        Random.InitState(250);

        for (int i = 0; i < 10; i++)
        {
            float x = Random.Range(12f, MapSize - 12f);
            float z = Random.Range(12f, MapSize - 12f);
            Vector3 position = new Vector3(x, 0f, z);
            position.y = terrain.SampleHeight(position) + terrain.transform.position.y;
            GameObject rock = (GameObject)PrefabUtility.InstantiatePrefab(prefabs[i % prefabs.Count]);
            rock.transform.SetParent(rocks.transform);
            rock.transform.position = position;
            rock.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            rock.transform.localScale *= Random.Range(0.7f, 1.3f);
        }

        Random.state = previousRandomState;
    }
}

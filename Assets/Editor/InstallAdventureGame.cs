using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class InstallAdventureGame
{
    private const string ScenePath = "Assets/Scenes/Greenfall.unity";
    private const string EnemyPrefabPath = "Assets/Character/Enemy.prefab";
    private const string RelicMaterialPath = "Assets/Character/Relic_Glow.mat";

    [MenuItem("Tools/Game/Install Greenfall Adventure")]
    private static void Install()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded || scene.path != ScenePath)
        {
            Debug.LogError("Open Greenfall before installing Greenfall.");
            return;
        }

        PlayerCharacterController player = Object.FindAnyObjectByType<PlayerCharacterController>();
        GameObject enemyPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(EnemyPrefabPath);
        if (player == null || enemyPrefab == null)
        {
            Debug.LogError("The Player in the scene or Assets/Character/Enemy.prefab is missing.");
            return;
        }

        GameManager manager = Object.FindAnyObjectByType<GameManager>();
        if (manager == null)
        {
            manager = new GameObject("Game Manager").AddComponent<GameManager>();
        }

        EnemyAI[] existingEnemies = Object.FindObjectsByType<EnemyAI>();
        List<EnemyAI> enemies = new List<EnemyAI>(existingEnemies);
        Vector3[] enemyOffsets =
        {
            new Vector3(15f, 0f, 12f),
            new Vector3(-17f, 0f, 15f),
            new Vector3(4f, 0f, -23f)
        };

        for (int i = enemies.Count; i < enemyOffsets.Length; i++)
        {
            GameObject enemyObject = (GameObject)PrefabUtility.InstantiatePrefab(enemyPrefab, scene);
            enemyObject.name = "Guardian " + (i + 1);
            enemyObject.transform.position = FindGroundPosition(player.transform.position + enemyOffsets[i]);
            EnemyAI enemy = enemyObject.GetComponent<EnemyAI>();
            enemy.SetTarget(player);
            enemies.Add(enemy);
        }

        RelicPickup[] existingRelics = Object.FindObjectsByType<RelicPickup>();
        List<RelicPickup> relics = new List<RelicPickup>(existingRelics);
        Vector3[] relicOffsets =
        {
            new Vector3(28f, 0f, 4f),
            new Vector3(-29f, 0f, 20f),
            new Vector3(1f, 0f, 38f),
            new Vector3(30f, 0f, -25f),
            new Vector3(-27f, 0f, -33f)
        };

        Material relicMaterial = LoadOrCreateRelicMaterial();
        if (relicMaterial == null)
        {
            return;
        }

        GameObject relicRoot = GameObject.Find("Ancient Relics");
        if (relicRoot == null)
        {
            relicRoot = new GameObject("Ancient Relics");
            SceneManager.MoveGameObjectToScene(relicRoot, scene);
        }

        for (int i = relics.Count; i < relicOffsets.Length; i++)
        {
            GameObject relic = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            relic.name = "Ancient Relic " + (i + 1);
            SceneManager.MoveGameObjectToScene(relic, scene);
            relic.transform.SetParent(relicRoot.transform, true);
            relic.transform.position = FindGroundPosition(player.transform.position + relicOffsets[i]) + Vector3.up * 1.25f;
            relic.transform.localScale = Vector3.one * 0.8f;
            relic.GetComponent<Renderer>().sharedMaterial = relicMaterial;
            relic.GetComponent<SphereCollider>().isTrigger = true;
            relics.Add(relic.AddComponent<RelicPickup>());
        }

        manager.Configure(player, enemies.ToArray(), relics.ToArray());
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene))
        {
            Debug.LogError("Could not save Greenfall after installing Greenfall.");
            return;
        }

        AssetDatabase.SaveAssets();
        Debug.Log("Installed Greenfall: third-person controls, three guardians, five relics, HUD, pause, win and game-over screens.");
    }

    public static void InstallFromBatchMode()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded || scene.path != ScenePath)
        {
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }

        if (scene.IsValid() && scene.isLoaded)
        {
            Install();
        }
        else
        {
            Debug.LogError("Could not open Greenfall to install Greenfall.");
        }
    }

    private static Vector3 FindGroundPosition(Vector3 position)
    {
        Terrain nearestTerrain = null;
        Vector3 nearestPoint = position;
        float nearestDistance = float.PositiveInfinity;
        foreach (Terrain terrain in Terrain.activeTerrains)
        {
            Vector3 origin = terrain.transform.position;
            Vector3 size = terrain.terrainData.size;
            if (position.x >= origin.x && position.x <= origin.x + size.x &&
                position.z >= origin.z && position.z <= origin.z + size.z)
            {
                position.y = terrain.SampleHeight(position) + origin.y;
                return position;
            }

            Vector3 candidate = position;
            candidate.x = Mathf.Clamp(position.x, origin.x + 8f, origin.x + size.x - 8f);
            candidate.z = Mathf.Clamp(position.z, origin.z + 8f, origin.z + size.z - 8f);
            float distance = (candidate - position).sqrMagnitude;
            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                nearestTerrain = terrain;
                nearestPoint = candidate;
            }
        }

        if (nearestTerrain != null)
        {
            nearestPoint.y = nearestTerrain.SampleHeight(nearestPoint) + nearestTerrain.transform.position.y;
            return nearestPoint;
        }

        Debug.LogWarning("No terrain covers a Greenfall spawn position; using the original height.");
        return position;
    }

    private static Material LoadOrCreateRelicMaterial()
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(RelicMaterialPath);
        if (material != null)
        {
            return material;
        }

        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
        {
            shader = Shader.Find("Standard");
        }

        if (shader == null)
        {
            Debug.LogError("Could not find a supported shader for the relic material.");
            return null;
        }

        material = new Material(shader);
        material.name = "Relic_Glow";
        if (material.HasProperty("_BaseColor"))
        {
            material.SetColor("_BaseColor", new Color(0.95f, 0.63f, 0.18f));
        }
        else if (material.HasProperty("_Color"))
        {
            material.SetColor("_Color", new Color(0.95f, 0.63f, 0.18f));
        }

        if (material.HasProperty("_EmissionColor"))
        {
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", new Color(1f, 0.45f, 0.05f) * 1.8f);
        }

        AssetDatabase.CreateAsset(material, RelicMaterialPath);
        return material;
    }
}

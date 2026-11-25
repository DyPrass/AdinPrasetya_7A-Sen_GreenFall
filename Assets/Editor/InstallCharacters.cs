using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

public static class InstallCharacters
{
    private const string ScenePath = "Assets/Scenes/Greenfall.unity";
    private const string CharacterFolder = "Assets/Character";
    private const string PlayerPrefabPath = CharacterFolder + "/Player.prefab";
    private const string EnemyPrefabPath = CharacterFolder + "/Enemy.prefab";
    private const string PlayerModelPath = CharacterFolder + "/Aj.fbx";
    private const string EnemyModelPath = CharacterFolder + "/Big Vegas.fbx";
    private const string AnimatorControllerPath = CharacterFolder + "/Animator Controller.controller";

    [MenuItem("Tools/Character/Install Player, Enemy and Animations")]
    private static void Install()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded || scene.path != ScenePath)
        {
            Debug.LogError("Open the project's Greenfall before installing the characters.");
            return;
        }

        GameObject playerModel = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerModelPath);
        GameObject enemyModel = AssetDatabase.LoadAssetAtPath<GameObject>(EnemyModelPath);
        if (playerModel == null || enemyModel == null)
        {
            Debug.LogError("Character models are missing from Assets/Character.");
            return;
        }

        Avatar playerAvatar = GetAvatar(PlayerModelPath);
        if (playerAvatar == null || !playerAvatar.isValid || !playerAvatar.isHuman)
        {
            playerAvatar = CreateHumanoidAvatar(playerModel);
        }

        if (playerAvatar == null || !playerAvatar.isValid || !playerAvatar.isHuman)
        {
            Debug.LogError("Could not build a valid Humanoid Avatar for Aj.fbx.");
            return;
        }

        if (!ConfigureAnimationImports())
        {
            return;
        }

        RuntimeAnimatorController controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(AnimatorControllerPath);
        if (controller == null)
        {
            Debug.LogError("Animator Controller is missing from Assets/Character.");
            return;
        }

        GameObject playerPrefab = CreatePlayerPrefab(playerModel, playerAvatar, controller);
        GameObject enemyPrefab = ConfigureEnemyPrefab(enemyModel, playerAvatar, controller);
        if (playerPrefab == null || enemyPrefab == null)
        {
            return;
        }

        PlayerCharacterController player = Object.FindAnyObjectByType<PlayerCharacterController>();
        if (player == null)
        {
            playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            player = (PrefabUtility.InstantiatePrefab(playerPrefab, scene) as GameObject)?.GetComponent<PlayerCharacterController>();
            if (player == null)
            {
                Debug.LogError("Could not instantiate the Player prefab.");
                return;
            }

            Vector3 spawn = Camera.main != null
                ? new Vector3(Camera.main.transform.position.x, 0f, Camera.main.transform.position.z)
                : Vector3.zero;
            spawn.y = GetTerrainHeight(spawn);
            player.transform.position = spawn;
        }

        EnemyAI enemy = Object.FindAnyObjectByType<EnemyAI>();
        if (enemy == null)
        {
            enemyPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(EnemyPrefabPath);
            enemy = (PrefabUtility.InstantiatePrefab(enemyPrefab, scene) as GameObject)?.GetComponent<EnemyAI>();
            if (enemy == null)
            {
                Debug.LogError("Could not instantiate the Enemy prefab.");
                return;
            }

            Vector3 spawn = player.transform.position + player.transform.forward * 7f;
            spawn.y = GetTerrainHeight(spawn);
            enemy.transform.position = spawn;
        }

        enemy.SetTarget(player);
        Camera mainCamera = Camera.main;
        if (mainCamera != null)
        {
            CharacterFollowCamera followCamera = mainCamera.GetComponent<CharacterFollowCamera>();
            if (followCamera == null)
            {
                followCamera = Undo.AddComponent<CharacterFollowCamera>(mainCamera.gameObject);
            }

            followCamera.SetTarget(player.transform);
        }
        else
        {
            Debug.LogError("The active scene has no Main Camera tagged camera.");
        }

        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene))
        {
            Debug.LogError("Could not save Greenfall after installing the characters.");
            return;
        }

        Debug.Log("Installed the Aj player, Big Vegas enemy, and Animator Controller in Greenfall.");
    }

    public static void InstallFromBatchMode()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded || scene.path != ScenePath)
        {
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            if (!scene.IsValid() || !scene.isLoaded)
            {
                Debug.LogError("Could not open Greenfall for character installation.");
                return;
            }
        }

        Install();
    }

    private static GameObject CreatePlayerPrefab(GameObject model, Avatar avatar, RuntimeAnimatorController controller)
    {
        GameObject root = new GameObject("Player");
        root.AddComponent<CharacterController>().height = 1.8f;
        CharacterController characterController = root.GetComponent<CharacterController>();
        characterController.radius = 0.35f;
        characterController.center = new Vector3(0f, 0.9f, 0f);

        GameObject modelInstance = PrefabUtility.InstantiatePrefab(model) as GameObject;
        if (modelInstance == null)
        {
            Object.DestroyImmediate(root);
            Debug.LogError("Could not instantiate Aj.fbx.");
            return null;
        }

        modelInstance.name = "Aj";
        modelInstance.transform.SetParent(root.transform, false);
        Animator animator = ConfigureAnimator(modelInstance, avatar, controller);
        PlayerCharacterController playerController = root.AddComponent<PlayerCharacterController>();
        SerializedObject serializedPlayer = new SerializedObject(playerController);
        serializedPlayer.FindProperty("animator").objectReferenceValue = animator;
        serializedPlayer.ApplyModifiedPropertiesWithoutUndo();

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
        Object.DestroyImmediate(root);
        return prefab;
    }

    private static GameObject ConfigureEnemyPrefab(GameObject model, Avatar fallbackAvatar, RuntimeAnimatorController controller)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(EnemyPrefabPath);
        if (root == null)
        {
            Debug.LogError("Could not open Assets/Character/Enemy.prefab.");
            return null;
        }

        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
        {
            GameObjectUtility.RemoveMonoBehavioursWithMissingScript(child.gameObject);
        }

        root.name = "Enemy";
        root.transform.position = Vector3.zero;
        root.transform.rotation = Quaternion.identity;

        Transform enemyModel = root.transform.Find("Big Vegas");
        if (enemyModel == null)
        {
            GameObject modelInstance = PrefabUtility.InstantiatePrefab(model) as GameObject;
            if (modelInstance == null)
            {
                PrefabUtility.UnloadPrefabContents(root);
                Debug.LogError("Could not instantiate Big Vegas.fbx.");
                return null;
            }

            modelInstance.name = "Big Vegas";
            modelInstance.transform.SetParent(root.transform, false);
            enemyModel = modelInstance.transform;
        }

        Animator animator = ConfigureAnimator(enemyModel.gameObject, fallbackAvatar, controller);
        CapsuleCollider capsule = root.GetComponent<CapsuleCollider>();
        if (capsule == null)
        {
            capsule = root.AddComponent<CapsuleCollider>();
        }

        capsule.radius = 0.45f;
        capsule.height = 2f;
        capsule.center = new Vector3(0f, 1f, 0f);

        NavMeshAgent agent = root.GetComponent<NavMeshAgent>();
        if (agent == null)
        {
            agent = root.AddComponent<NavMeshAgent>();
        }

        agent.speed = 3.5f;
        agent.stoppingDistance = 1.5f;

        EnemyHealth health = root.GetComponent<EnemyHealth>() ?? root.AddComponent<EnemyHealth>();
        HealthBar healthBar = root.GetComponent<HealthBar>() ?? root.AddComponent<HealthBar>();
        EnemyCombat combat = root.GetComponent<EnemyCombat>() ?? root.AddComponent<EnemyCombat>();
        EnemyAI enemyAI = root.GetComponent<EnemyAI>() ?? root.AddComponent<EnemyAI>();

        SetObjectReference(health, "animator", animator);
        SetObjectReference(health, "healthBar", healthBar);
        SetObjectReference(combat, "animator", animator);
        SetObjectReference(enemyAI, "animator", animator);
        SetObjectReference(enemyAI, "combat", combat);

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, EnemyPrefabPath);
        PrefabUtility.UnloadPrefabContents(root);
        return prefab;
    }

    private static Animator ConfigureAnimator(GameObject model, Avatar avatar, RuntimeAnimatorController controller)
    {
        Animator animator = model.GetComponent<Animator>();
        if (animator == null)
        {
            animator = model.AddComponent<Animator>();
        }

        animator.avatar = avatar;
        animator.runtimeAnimatorController = controller;
        animator.applyRootMotion = false;
        return animator;
    }

    private static Avatar GetAvatar(string modelPath)
    {
        return AssetDatabase.LoadAllAssetsAtPath(modelPath).OfType<Avatar>().FirstOrDefault();
    }

    private static Avatar CreateHumanoidAvatar(GameObject model)
    {
        GameObject modelInstance = PrefabUtility.InstantiatePrefab(model) as GameObject;
        if (modelInstance == null)
        {
            Debug.LogError("Could not instantiate Aj.fbx to build its Humanoid Avatar.");
            return null;
        }

        string[,] boneNames =
        {
            { "Hips", "Hips" },
            { "LeftUpperLeg", "LeftUpLeg" },
            { "RightUpperLeg", "RightUpLeg" },
            { "LeftLowerLeg", "LeftLeg" },
            { "RightLowerLeg", "RightLeg" },
            { "LeftFoot", "LeftFoot" },
            { "RightFoot", "RightFoot" },
            { "Spine", "Spine" },
            { "Chest", "Spine1" },
            { "UpperChest", "Spine2" },
            { "Neck", "Neck" },
            { "Head", "Head" },
            { "LeftShoulder", "LeftShoulder" },
            { "RightShoulder", "RightShoulder" },
            { "LeftUpperArm", "LeftArm" },
            { "RightUpperArm", "RightArm" },
            { "LeftLowerArm", "LeftForeArm" },
            { "RightLowerArm", "RightForeArm" },
            { "LeftHand", "LeftHand" },
            { "RightHand", "RightHand" }
        };

        Transform[] transforms = modelInstance.GetComponentsInChildren<Transform>(true);
        HumanBone[] humanBones = new HumanBone[boneNames.GetLength(0)];
        int humanBoneCount = 0;
        for (int i = 0; i < boneNames.GetLength(0); i++)
        {
            string humanName = boneNames[i, 0];
            string sourceName = boneNames[i, 1];
            Transform bone = transforms.FirstOrDefault(transform =>
                transform.name == sourceName ||
                transform.name.EndsWith(":" + sourceName, System.StringComparison.Ordinal));
            if (bone == null)
            {
                continue;
            }

            humanBones[humanBoneCount++] = new HumanBone
            {
                humanName = humanName,
                boneName = bone.name,
                limit = new HumanLimit { useDefaultValues = true }
            };
        }

        HumanDescription description = new HumanDescription
        {
            human = humanBones.Take(humanBoneCount).ToArray(),
            skeleton = transforms.Select(transform => new SkeletonBone
            {
                name = transform.name,
                position = transform.localPosition,
                rotation = transform.localRotation,
                scale = transform.localScale
            }).ToArray(),
            upperArmTwist = 0.5f,
            lowerArmTwist = 0.5f,
            upperLegTwist = 0.5f,
            lowerLegTwist = 0.5f,
            armStretch = 0.05f,
            legStretch = 0.05f,
            feetSpacing = 0f,
            hasTranslationDoF = false
        };

        Avatar avatar = AvatarBuilder.BuildHumanAvatar(modelInstance, description);
        Object.DestroyImmediate(modelInstance);
        if (avatar == null || !avatar.isValid || !avatar.isHuman)
        {
            if (avatar != null)
            {
                Object.DestroyImmediate(avatar);
            }

            Debug.LogError("Aj.fbx does not contain enough mapped bones to build a Humanoid Avatar.");
            return null;
        }

        avatar.name = "Aj_HumanoidAvatar";
        string avatarPath = CharacterFolder + "/Aj_HumanoidAvatar.asset";
        Avatar existingAvatar = AssetDatabase.LoadAssetAtPath<Avatar>(avatarPath);
        if (existingAvatar != null)
        {
            AssetDatabase.DeleteAsset(avatarPath);
        }

        AssetDatabase.CreateAsset(avatar, avatarPath);
        AssetDatabase.SaveAssets();
        return avatar;
    }

    private static bool ConfigureAnimationImports()
    {
        string[] modelGuids = AssetDatabase.FindAssets("t:Model", new[] { CharacterFolder });
        foreach (string guid in modelGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (!path.EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase) ||
                path == PlayerModelPath ||
                path == EnemyModelPath)
            {
                continue;
            }

            ModelImporter importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null)
            {
                Debug.LogError("Could not configure animation import settings for " + path + ".");
                return false;
            }

            if (importer.animationType == ModelImporterAnimationType.Human &&
                importer.avatarSetup == ModelImporterAvatarSetup.CreateFromThisModel)
            {
                continue;
            }

            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.sourceAvatar = null;
            importer.SaveAndReimport();
        }

        return true;
    }

    private static void SetObjectReference(Object target, string propertyName, Object value)
    {
        SerializedObject serialized = new SerializedObject(target);
        SerializedProperty property = serialized.FindProperty(propertyName);
        if (property == null)
        {
            Debug.LogError("Could not find serialized field '" + propertyName + "' on " + target.GetType().Name + ".");
            return;
        }

        property.objectReferenceValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static float GetTerrainHeight(Vector3 position)
    {
        foreach (Terrain terrain in Object.FindObjectsByType<Terrain>())
        {
            Vector3 origin = terrain.transform.position;
            Vector3 size = terrain.terrainData.size;
            if (position.x >= origin.x && position.x <= origin.x + size.x &&
                position.z >= origin.z && position.z <= origin.z + size.z)
            {
                return terrain.SampleHeight(position) + origin.y;
            }
        }

        Debug.LogWarning("No terrain covers the character spawn point; using ground level.");
        return 0f;
    }
}

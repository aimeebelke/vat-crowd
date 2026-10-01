using System.Collections.Generic;
using System.IO;
using Unity.Scenes;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AimeeBelke.VATCrowd.Editor
{
    // Everything the crowd setup needs. Leave optional fields null to have them found or created.
    public class VATCrowdSetupSettings
    {
        public VATClipData clipData;
        public Mesh mesh;                 // null: "<prefix>VAT_Mesh.asset" next to the clip data
        public Material material;         // null: a Custom/VAT material using this bake, else a new one
        public Texture2D albedo;          // only used when a material is created
        public GameObject prefab;         // null: a prefab whose VATCharacterAuthoring uses this clip data, else a new one
        public SubScene targetSubScene;   // null: create a new SubScene in the active scene
        public int count = 2000;
        public float radius = 40f;
        public Vector3 center = Vector3.zero;
        public bool addEnvironment = true; // camera, light and ground, each only if the scene lacks one
    }

    // Builds a working crowd from one baked character: material, character prefab, VATRenderer,
    // and a Spawner inside a SubScene. Reuses anything that already exists for the same bake.
    public static class VATCrowdSetup
    {
        public const string ShaderName = "Custom/VAT";
        const string ClipDataSuffix = "_VATClipData";

        public static string Prefix(VATClipData clip) =>
            clip.name.EndsWith(ClipDataSuffix) ? clip.name.Substring(0, clip.name.Length - ClipDataSuffix.Length) : clip.name;

        public static string Folder(Object asset) =>
            Path.GetDirectoryName(AssetDatabase.GetAssetPath(asset))?.Replace('\\', '/');

        public static Mesh FindMesh(VATClipData clip) =>
            AssetDatabase.LoadAssetAtPath<Mesh>($"{Folder(clip)}/{Prefix(clip)}VAT_Mesh.asset");

        public static Material FindMaterial(VATClipData clip)
        {
            if (clip.positionTexture == null) return null;
            foreach (var guid in AssetDatabase.FindAssets("t:Material", new[] { "Assets" }))
            {
                var mat = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
                if (mat != null && mat.shader != null && mat.shader.name == ShaderName &&
                    mat.HasProperty("_PosTex") && mat.GetTexture("_PosTex") == clip.positionTexture)
                    return mat;
            }
            return null;
        }

        public static GameObject FindPrefab(VATClipData clip)
        {
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" }))
            {
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
                var authoring = go != null ? go.GetComponent<VATCharacterAuthoring>() : null;
                if (authoring != null && authoring.clipData == clip)
                    return go;
            }
            return null;
        }

        public static List<SubScene> SubScenesInActiveScene()
        {
            var active = SceneManager.GetActiveScene();
            var result = new List<SubScene>();
            foreach (var sub in Object.FindObjectsByType<SubScene>(FindObjectsInactive.Include))
                if (sub.gameObject.scene == active && sub.SceneAsset != null)
                    result.Add(sub);
            return result;
        }

        // Returns a summary of what was created or reused, or null if the inputs were invalid.
        public static List<string> Create(VATCrowdSetupSettings s)
        {
            if (!Validate(s, out string error))
            {
                EditorUtility.DisplayDialog("VAT Crowd Setup", error, "OK");
                return null;
            }

            var active = SceneManager.GetActiveScene();

            // A new SubScene can't be created next to an untitled scene, so save first (before any changes)
            if (s.targetSubScene == null && string.IsNullOrEmpty(active.path))
            {
                if (!EditorUtility.DisplayDialog("VAT Crowd Setup",
                        "The active scene hasn't been saved yet. Save it first so the new SubScene can be created next to it.",
                        "Save Scene…", "Cancel"))
                    return null;
                EditorSceneManager.SaveScene(active);
                if (string.IsNullOrEmpty(active.path))
                    return null; // save dialog was cancelled
            }

            var log = new List<string>();
            string prefix = Prefix(s.clipData);
            string folder = Folder(s.clipData);

            var material = ResolveMaterial(s, prefix, folder, log);
            var prefab = ResolvePrefab(s, prefix, folder, log);
            var renderer = ResolveRenderer(s, material, active, prefix, log);
            AddSpawner(s, prefab, active, prefix, folder, log);
            if (s.addEnvironment)
                AddEnvironment(s, active, log);

            SceneManager.SetActiveScene(active);
            EditorSceneManager.MarkSceneDirty(active);
            Selection.activeGameObject = renderer.gameObject;
            Debug.Log("[VAT Crowd Setup] Done:\n- " + string.Join("\n- ", log));
            return log;
        }

        static bool Validate(VATCrowdSetupSettings s, out string error)
        {
            error = null;
            if (s.clipData == null) error = "Assign the VAT Clip Data produced by the baker.";
            else if (s.clipData.positionTexture == null) error = "The clip data has no position texture. Rebake the character.";
            else if (s.mesh == null) error = $"No VAT mesh found. Assign the mesh baked alongside '{s.clipData.name}'.";
            else if (s.mesh.vertexCount != s.clipData.vertexCount)
                error = $"The mesh has {s.mesh.vertexCount} vertices but the clip data was baked from {s.clipData.vertexCount}. Use the mesh from the same bake.";
            else if (Shader.Find(ShaderName) == null) error = $"Shader '{ShaderName}' not found.";
            else if (s.count <= 0) error = "Count must be greater than zero.";
            return error == null;
        }

        static Material ResolveMaterial(VATCrowdSetupSettings s, string prefix, string folder, List<string> log)
        {
            var material = s.material != null ? s.material : FindMaterial(s.clipData);
            if (material == null)
            {
                material = new Material(Shader.Find(ShaderName)) { enableInstancing = true };
                if (s.albedo != null) material.SetTexture("_MainTex", s.albedo);
                material.SetTexture("_PosTex", s.clipData.positionTexture);
                material.SetFloat("_TexHeight", s.clipData.positionTexture.height);
                string path = AssetDatabase.GenerateUniqueAssetPath($"{folder}/{prefix}_VAT.mat");
                AssetDatabase.CreateAsset(material, path);
                log.Add($"Created material {path}");
                return material;
            }

            if (!material.enableInstancing)
            {
                material.enableInstancing = true;
                EditorUtility.SetDirty(material);
                log.Add($"Enabled GPU instancing on {material.name} (required by VATRenderer)");
            }
            log.Add($"Using material {material.name}");
            return material;
        }

        static GameObject ResolvePrefab(VATCrowdSetupSettings s, string prefix, string folder, List<string> log)
        {
            var prefab = s.prefab != null ? s.prefab : FindPrefab(s.clipData);
            if (prefab != null)
            {
                log.Add($"Using prefab {prefab.name}");
                return prefab;
            }

            var go = new GameObject($"{prefix}_Character");
            try
            {
                go.AddComponent<VATCharacterAuthoring>().clipData = s.clipData;
                go.AddComponent<AgentAuthoring>();
                string path = AssetDatabase.GenerateUniqueAssetPath($"{folder}/{prefix}_Character.prefab");
                prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
                log.Add($"Created prefab {path}");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
            return prefab;
        }

        static VATRenderer ResolveRenderer(VATCrowdSetupSettings s, Material material, Scene active, string prefix, List<string> log)
        {
            foreach (var existing in Object.FindObjectsByType<VATRenderer>(FindObjectsInactive.Include))
            {
                if (existing.gameObject.scene != active || existing.vatClipData != s.clipData) continue;
                Undo.RecordObject(existing, "VAT Crowd Setup");
                existing.vatMesh = s.mesh;
                existing.vatMaterial = material;
                log.Add($"Updated existing renderer '{existing.name}'");
                return existing;
            }

            var go = CreateInScene($"{prefix} VAT Renderer", active);
            var renderer = go.AddComponent<VATRenderer>();
            renderer.vatMesh = s.mesh;
            renderer.vatMaterial = material;
            renderer.vatClipData = s.clipData;
            log.Add($"Created renderer '{go.name}' in {active.name}");
            return renderer;
        }

        static void AddSpawner(VATCrowdSetupSettings s, GameObject prefab, Scene active, string prefix, string folder, List<string> log)
        {
            Scene subScene;
            string path;
            bool closeAfter;

            if (s.targetSubScene != null)
            {
                path = AssetDatabase.GetAssetPath(s.targetSubScene.SceneAsset);
                subScene = SceneManager.GetSceneByPath(path);
                closeAfter = !subScene.isLoaded; // leave a SubScene the user has open for editing open
                if (closeAfter)
                    subScene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            }
            else
            {
                string dir = string.IsNullOrEmpty(active.path) ? folder : Path.GetDirectoryName(active.path).Replace('\\', '/');
                string sceneName = string.IsNullOrEmpty(active.name) ? "Untitled" : active.name;
                path = AssetDatabase.GenerateUniqueAssetPath($"{dir}/{sceneName}_{prefix}_SubScene.unity");
                subScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
                closeAfter = true;
            }

            var spawnerGo = new GameObject($"{prefix} Spawner");
            SceneManager.MoveGameObjectToScene(spawnerGo, subScene);
            spawnerGo.transform.position = s.center;
            var spawner = spawnerGo.AddComponent<SpawnerAuthoring>();
            spawner.prefab = prefab;
            spawner.count = s.count;
            spawner.radius = s.radius;
            spawner.seed = (uint)Random.Range(1, int.MaxValue);

            EditorSceneManager.SaveScene(subScene, path);
            if (closeAfter)
                EditorSceneManager.CloseScene(subScene, true);
            SceneManager.SetActiveScene(active);

            if (s.targetSubScene == null)
            {
                var go = CreateInScene($"{prefix} SubScene", active);
                var component = go.AddComponent<SubScene>();
                component.SceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(path);
                component.AutoLoadScene = true;
                log.Add($"Created SubScene {path}");
            }
            log.Add($"Added spawner ({s.count} x {prefab.name}, radius {s.radius}) to {Path.GetFileNameWithoutExtension(path)}");
        }

        static void AddEnvironment(VATCrowdSetupSettings s, Scene active, List<string> log)
        {
            bool hasCamera = false, hasSun = false;
            foreach (var cam in Object.FindObjectsByType<Camera>())
                hasCamera |= cam.gameObject.scene == active;
            foreach (var light in Object.FindObjectsByType<Light>())
                hasSun |= light.gameObject.scene == active && light.type == LightType.Directional;

            if (!hasCamera)
            {
                var cam = CreateInScene("Main Camera", active);
                cam.tag = "MainCamera";
                cam.AddComponent<Camera>().farClipPlane = Mathf.Max(500f, s.radius * 10f);
                if (Object.FindAnyObjectByType<AudioListener>() == null) cam.AddComponent<AudioListener>();
                cam.transform.position = s.center + new Vector3(0f, s.radius * 0.55f, -s.radius * 1.5f);
                cam.transform.LookAt(s.center);

                // A scene with no camera is treated as empty, so give it a ground to stand on too
                var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
                ground.name = "Ground";
                SceneManager.MoveGameObjectToScene(ground, active);
                Undo.RegisterCreatedObjectUndo(ground, "VAT Crowd Setup");
                ground.transform.position = s.center;
                float size = Mathf.Max(1f, s.radius * 2.5f / 10f); // Plane is 10 x 10 units
                ground.transform.localScale = new Vector3(size, 1f, size);
                log.Add("Added camera and ground");
            }

            if (!hasSun)
            {
                var sun = CreateInScene("Directional Light", active);
                var light = sun.AddComponent<Light>();
                light.type = LightType.Directional;
                light.shadows = LightShadows.Soft;
                sun.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
                log.Add("Added directional light");
            }
        }

        static GameObject CreateInScene(string name, Scene scene)
        {
            var go = new GameObject(name);
            SceneManager.MoveGameObjectToScene(go, scene);
            Undo.RegisterCreatedObjectUndo(go, "VAT Crowd Setup");
            return go;
        }

        // Right-click a VATClipData asset → VAT → Create Crowd Setup
        [MenuItem("Assets/VAT/Create Crowd Setup", false, 2000)]
        static void CreateFromSelection() => VATCrowdSetupWindow.Open(Selection.activeObject as VATClipData);

        [MenuItem("Assets/VAT/Create Crowd Setup", true)]
        static bool CreateFromSelectionValidate() => Selection.activeObject is VATClipData;

        // Same, but skips the window and uses defaults (2,000 characters, radius 40, new SubScene)
        [MenuItem("Assets/VAT/Quick Crowd Setup (Defaults)", false, 2001)]
        static void QuickCreateFromSelection()
        {
            var clip = Selection.activeObject as VATClipData;
            Create(new VATCrowdSetupSettings { clipData = clip, mesh = FindMesh(clip) });
        }

        [MenuItem("Assets/VAT/Quick Crowd Setup (Defaults)", true)]
        static bool QuickCreateFromSelectionValidate() => Selection.activeObject is VATClipData;
    }
}

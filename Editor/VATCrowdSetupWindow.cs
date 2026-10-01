using System.Collections.Generic;
using System.Linq;
using Unity.Scenes;
using UnityEditor;
using UnityEngine;

namespace AimeeBelke.VATCrowd.Editor
{
    // Tools → VAT → Create Crowd Setup. Picks up a baked character and builds everything needed to
    // spawn and draw a crowd of it in the active scene. See VATCrowdSetup for the steps.
    public class VATCrowdSetupWindow : EditorWindow
    {
        VATClipData _clipData;
        Mesh _mesh;
        Material _material;
        Texture2D _albedo;
        GameObject _prefab;
        int _subSceneIndex; // 0 = new SubScene, otherwise index + 1 into _subScenes
        int _count = 2000;
        float _radius = 40f;
        Vector3 _center;
        bool _addEnvironment = true;

        List<SubScene> _subScenes = new();
        Vector2 _scroll;

        [MenuItem("Tools/VAT/Create Crowd Setup", false, 2)]
        public static void Open() => Open(null);

        public static void Open(VATClipData clipData)
        {
            var window = GetWindow<VATCrowdSetupWindow>("VAT Crowd Setup");
            window.minSize = new Vector2(380, 420);
            if (clipData != null) window.SetClipData(clipData);
            window.Show();
        }

        void OnEnable() => RefreshSubScenes();
        void OnHierarchyChange() { RefreshSubScenes(); Repaint(); }

        void RefreshSubScenes() => _subScenes = VATCrowdSetup.SubScenesInActiveScene();

        // Fill in whatever already exists for this bake
        void SetClipData(VATClipData clipData)
        {
            _clipData = clipData;
            if (clipData == null) return;
            _mesh = VATCrowdSetup.FindMesh(clipData);
            _material = VATCrowdSetup.FindMaterial(clipData);
            _prefab = VATCrowdSetup.FindPrefab(clipData);
        }

        void OnGUI()
        {
            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            EditorGUILayout.LabelField("Baked character", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            var clip = (VATClipData)EditorGUILayout.ObjectField("VAT Clip Data", _clipData, typeof(VATClipData), false);
            if (EditorGUI.EndChangeCheck()) SetClipData(clip);

            _mesh = (Mesh)EditorGUILayout.ObjectField("VAT Mesh", _mesh, typeof(Mesh), false);
            _material = (Material)EditorGUILayout.ObjectField(new GUIContent("Material", "Leave empty to create one using the Custom/VAT shader."), _material, typeof(Material), false);
            if (_material == null)
                _albedo = (Texture2D)EditorGUILayout.ObjectField(new GUIContent("Albedo (new material)", "Optional colour texture for the created material."), _albedo, typeof(Texture2D), false);
            _prefab = (GameObject)EditorGUILayout.ObjectField(new GUIContent("Character Prefab", "Leave empty to create one with VATCharacterAuthoring + AgentAuthoring."), _prefab, typeof(GameObject), false);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Crowd", EditorStyles.boldLabel);
            var options = new[] { "New SubScene" }.Concat(_subScenes.Select(s => $"Existing: {s.name}")).ToArray();
            _subSceneIndex = Mathf.Clamp(_subSceneIndex, 0, options.Length - 1);
            _subSceneIndex = EditorGUILayout.Popup(new GUIContent("Spawner goes in", "The spawner must live inside a SubScene so it's baked into an entity."), _subSceneIndex, options);
            _count = Mathf.Max(1, EditorGUILayout.IntField("Count", _count));
            _radius = Mathf.Max(0.1f, EditorGUILayout.FloatField("Radius", _radius));
            _center = EditorGUILayout.Vector3Field("Center", _center);
            _addEnvironment = EditorGUILayout.Toggle(new GUIContent("Add camera, light & ground", "Each is only added if the scene doesn't already have one."), _addEnvironment);

            EditorGUILayout.Space();
            DrawPlan();

            EditorGUILayout.Space();
            using (new EditorGUI.DisabledScope(_clipData == null))
            {
                if (GUILayout.Button("Create Crowd Setup", GUILayout.Height(30)))
                {
                    var log = VATCrowdSetup.Create(new VATCrowdSetupSettings
                    {
                        clipData = _clipData,
                        mesh = _mesh,
                        material = _material,
                        albedo = _albedo,
                        prefab = _prefab,
                        targetSubScene = _subSceneIndex > 0 ? _subScenes[_subSceneIndex - 1] : null,
                        count = _count,
                        radius = _radius,
                        center = _center,
                        addEnvironment = _addEnvironment
                    });
                    if (log != null)
                    {
                        SetClipData(_clipData); // pick up anything just created
                        RefreshSubScenes();
                        ShowNotification(new GUIContent("Crowd setup created. Save the scene and press Play."));
                    }
                }
            }

            EditorGUILayout.EndScrollView();
        }

        void DrawPlan()
        {
            if (_clipData == null)
            {
                EditorGUILayout.HelpBox("Pick the VAT Clip Data from a bake (Tools → VAT → Baker).", MessageType.Info);
                return;
            }

            string prefix = VATCrowdSetup.Prefix(_clipData);
            var lines = new List<string>
            {
                _material != null ? $"Use material '{_material.name}'" + (_material.enableInstancing ? "" : " (GPU instancing will be enabled)") : $"Create material {prefix}_VAT.mat",
                _prefab != null ? $"Use prefab '{_prefab.name}'" : $"Create prefab {prefix}_Character.prefab",
                $"Add or update a VAT Renderer in the active scene",
                _subSceneIndex > 0 ? $"Add a spawner to SubScene '{_subScenes[_subSceneIndex - 1].name}'" : "Create a SubScene with a spawner"
            };
            EditorGUILayout.HelpBox("This will:\n• " + string.Join("\n• ", lines), MessageType.None);

            if (_mesh == null)
                EditorGUILayout.HelpBox("No VAT mesh found next to the clip data. Assign the mesh from the same bake.", MessageType.Warning);
            else if (_mesh.vertexCount != _clipData.vertexCount)
                EditorGUILayout.HelpBox($"This mesh has {_mesh.vertexCount} vertices but the bake has {_clipData.vertexCount}. Use the mesh from the same bake.", MessageType.Error);
        }
    }
}

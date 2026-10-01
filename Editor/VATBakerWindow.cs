using UnityEditor;
using UnityEngine;

namespace AimeeBelke.VATCrowd.Editor
{
    public class VATBakerWindow : EditorWindow
    {
        private SkinnedMeshRenderer _smr;
        private AnimationClip _clipIdle;
        private AnimationClip _clipWalk;
        private AnimationClip _clipRun;
        private AnimationClip _clipAttack;
        private int _fps = 24;
        private string _savePath = "Assets/VATBaker/";
        private string _assetPrefix = "Character";
        private bool _correctRotation;
        private VATClipData _lastBake;

        [MenuItem("Tools/VAT/Baker", false, 1)]
        public static void Open()
        {
            GetWindow<VATBakerWindow>("VAT Baker").Show();
        }

        private void OnGUI()
        {
            // Header
            EditorGUILayout.LabelField("VAT Baker", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            // Source mesh
            _smr = (SkinnedMeshRenderer)EditorGUILayout.ObjectField("Source SMR", _smr, typeof(SkinnedMeshRenderer), true);

            // Animation clips in AIState order
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Clips (in AIState order)", EditorStyles.miniBoldLabel);
            _clipIdle = (AnimationClip)EditorGUILayout.ObjectField("Idle", _clipIdle, typeof(AnimationClip), false);
            _clipWalk = (AnimationClip)EditorGUILayout.ObjectField("Walk", _clipWalk, typeof(AnimationClip), false);
            _clipRun = (AnimationClip)EditorGUILayout.ObjectField("Run", _clipRun, typeof(AnimationClip), false);
            _clipAttack = (AnimationClip)EditorGUILayout.ObjectField("Attack", _clipAttack, typeof(AnimationClip), false);

            // Bake settings
            EditorGUILayout.Space();
            _fps = EditorGUILayout.IntField("FPS", _fps);
            _savePath = EditorGUILayout.TextField("Save Path", _savePath);
            _assetPrefix = EditorGUILayout.TextField(new GUIContent("Asset Prefix", "Prefix for the baked texture, mesh and clip data, so different characters don't overwrite each other."), _assetPrefix);
            _correctRotation = EditorGUILayout.Toggle(
                new GUIContent("Correct SMR Rotation", "Enable for models with a rotated mesh node (e.g. 3ds Max Z-up FBX imports)."),
                _correctRotation);

            // Bake button — delegates to VATBaker static class
            EditorGUILayout.Space();
            if (GUILayout.Button("Bake VAT"))
            {
                AnimationClip[] clips = { _clipIdle, _clipWalk, _clipRun, _clipAttack };
                string[] names = { "Idle", "Walk", "Run", "Attack" };
                _lastBake = VATBaker.Bake(_smr, clips, names, _fps, _savePath, _correctRotation, _assetPrefix);
            }

            // Hand the fresh bake straight to the crowd setup
            if (_lastBake != null)
            {
                EditorGUILayout.Space();
                EditorGUILayout.HelpBox($"Last bake: {_lastBake.name}", MessageType.None);
                if (GUILayout.Button("Set Up Crowd For This Bake →"))
                    VATCrowdSetupWindow.Open(_lastBake);
            }
        }
    }
}

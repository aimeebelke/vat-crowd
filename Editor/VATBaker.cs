using System.IO;
using UnityEditor;
using UnityEngine;

namespace AimeeBelke.VATCrowd.Editor
{
    public static class VATBaker
    {
        public static void Bake(SkinnedMeshRenderer smr, AnimationClip[] clips, string[] names, int fps, string savePath, bool correctRotation, string assetPrefix = "Character", bool showCompleteDialog = true)
        {
            #region 1 - Validate Inputs

            if (smr == null)
            {
                EditorUtility.DisplayDialog("VAT Baker", "Assign a SkinnedMeshRenderer.", "OK");
                return;
            }

            for (int i = 0; i < clips.Length; i++)
            {
                if (clips[i] == null)
                {
                    EditorUtility.DisplayDialog("VAT Baker", $"Assign the '{names[i]}' clip.", "OK");
                    return;
                }
            }

            if (fps <= 0)
            {
                EditorUtility.DisplayDialog("VAT Baker", "FPS must be > 0.", "OK");
                return;
            }

            // Check mesh has vertices
            int vertexCount = smr.sharedMesh.vertexCount;
            if (vertexCount == 0)
            {
                EditorUtility.DisplayDialog("VAT Baker", "Source mesh has no vertices.", "OK");
                return;
            }

            #endregion

            #region 2 - Calculate Frame Layout

            // How many frames per clip, sum = total texture height
            int totalFrames = 0;
            int[] clipFrameCounts = new int[clips.Length];
            for (int i = 0; i < clips.Length; i++)
            {
                int fc = Mathf.Max(1, Mathf.CeilToInt(clips[i].length * fps));
                clipFrameCounts[i] = fc;
                totalFrames += fc;
            }

            #endregion

            #region 3 - Create VAT Position Texture

            Directory.CreateDirectory(savePath);

            // Width = vertex count, height = total frames across all clips
            var positionTex = new Texture2D(vertexCount, totalFrames, TextureFormat.RGBAFloat, false, true);
            positionTex.filterMode = FilterMode.Point;
            positionTex.wrapMode = TextureWrapMode.Clamp;

            // Resolve animation root — SampleAnimation needs the GO the clip bindings target
            var animator = smr.GetComponentInParent<Animator>();
            GameObject root = animator != null ? animator.gameObject : smr.gameObject;
            float invFps = 1f / fps;
            int frameRow = 0;
            Quaternion smrRot = correctRotation ? smr.transform.localRotation : Quaternion.identity;

            // Capture root transform so we can restore it after baking
            Vector3 rootPos = root.transform.localPosition;
            Quaternion rootRot = root.transform.localRotation;

            #endregion

            try
            {
                #region 4 - Per-Clip Baking Loop

                for (int clipIdx = 0; clipIdx < clips.Length; clipIdx++)
                {
                    AnimationClip clip = clips[clipIdx];
                    int numFrames = clipFrameCounts[clipIdx];

                    // Centroid at frame 0 — baseline for locomotion cancellation
                    // We subtract per-frame XZ drift so the baked mesh stays origin-centred
                    // This strips root motion regardless of bone hierarchy or space conventions
                    clip.SampleAnimation(root, 0f);
                    var baseMesh = new Mesh();
                    smr.BakeMesh(baseMesh);

                    Vector3[] baseVerts = baseMesh.vertices;
                    Vector3 centroid0 = Vector3.zero;

                    for (int v = 0; v < vertexCount; v++)
                    {
                        centroid0 += smrRot * baseVerts[v];
                    }

                    centroid0 /= vertexCount;
                    Object.DestroyImmediate(baseMesh);

                    // Bake each frame of this clip into a texture row
                    for (int f = 0; f < numFrames; f++)
                    {
                        float progress = (float)frameRow / totalFrames;
                        EditorUtility.DisplayProgressBar("VAT Baker", $"Baking {names[clipIdx]} frame {f + 1}/{numFrames}", progress);

                        float t = Mathf.Min(f * invFps, clip.length);
                        clip.SampleAnimation(root, t);

                        var tmpMesh = new Mesh();
                        try
                        {
                            smr.BakeMesh(tmpMesh);
                            Vector3[] verts = tmpMesh.vertices;

                            // Cancel horizontal locomotion — compare this frame's centroid to frame 0
                            Vector3 centroidN = Vector3.zero;
                            for (int v = 0; v < vertexCount; v++)
                            {
                                centroidN += smrRot * verts[v];
                            }

                            centroidN /= vertexCount; // Divide sum of all vertex position by number of vertices to get avg pos
                            Vector3 locoOffset = centroidN - centroid0;
                            locoOffset.y = 0f;

                            // Write vertex positions as pixel colours into this texture row
                            Color[] pixels = new Color[vertexCount];
                            for (int v = 0; v < vertexCount; v++)
                            {
                                Vector3 pos = smrRot * verts[v] - locoOffset;
                                pixels[v] = new Color(pos.x, pos.y, pos.z, 1f);
                            }
                            positionTex.SetPixels(0, frameRow, vertexCount, 1, pixels);
                        }
                        finally
                        {
                            Object.DestroyImmediate(tmpMesh);
                        }

                        frameRow++;
                    }
                }

                // Restore root transform so the scene isn't left dirty
                root.transform.localPosition = rootPos;
                root.transform.localRotation = rootRot;
                positionTex.Apply();

                #endregion

                #region 5 - Build UV2 Mesh

                // Each vertex gets a UV2 coordinate pointing to its column in the texture
                // Pixel-centre offset: (v + 0.5) / vertexCount so the shader samples the middle of each pixel
                Mesh staticMesh = Object.Instantiate(smr.sharedMesh);
                staticMesh.name = assetPrefix + "VAT_Mesh";
                var uv2 = new Vector2[vertexCount];
                for (int v = 0; v < vertexCount; v++)
                {
                    uv2[v] = new Vector2((v + 0.5f) / vertexCount, 0f);
                }

                staticMesh.uv2 = uv2;

                #endregion

                #region 6 - Save Assets

                // Overwrite in place if they already exist, so GUIDs (and every reference to them) survive a rebake
                string texPath = savePath + assetPrefix + "_VAT_Positions.asset";
                string meshPath = savePath + assetPrefix + "VAT_Mesh.asset";
                string dataPath = savePath + assetPrefix + "_VATClipData.asset";
                positionTex = SaveOrReplace(positionTex, texPath);
                staticMesh = SaveOrReplace(staticMesh, meshPath);

                // Build VATClipData — describes the strip layout (which rows belong to which animation)
                var vatData = ScriptableObject.CreateInstance<VATClipData>();
                vatData.positionTexture = positionTex;
                vatData.vertexCount = vertexCount;
                vatData.clips = new VATClipData.ClipStrip[clips.Length];
                int startFrame = 0;

                for (int i = 0; i < clips.Length; i++)
                {
                    vatData.clips[i] = new VATClipData.ClipStrip
                    {
                        name = names[i],
                        startFrame = startFrame,
                        frameCount = clipFrameCounts[i],
                        fps = fps,
                        loop = (i != 3)
                    };
                    startFrame += clipFrameCounts[i];
                }
                SaveOrReplace(vatData, dataPath);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                Debug.Log($"[VATBaker] Done. Texture: {texPath} | Mesh: {meshPath} | Data: {dataPath}");
                if (showCompleteDialog)
                    EditorUtility.DisplayDialog("VAT Baker", "Bake complete!\n\n" + $"Texture : {texPath}\n" + $"Mesh: {meshPath}\n" + $"Data: {dataPath}", "OK");

                #endregion
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        // Creates the asset, or copies into the existing one at that path and returns the existing instance
        static T SaveOrReplace<T>(T asset, string path) where T : Object
        {
            asset.name = Path.GetFileNameWithoutExtension(path);
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing == null)
            {
                AssetDatabase.CreateAsset(asset, path);
                return asset;
            }

            EditorUtility.CopySerialized(asset, existing);
            EditorUtility.SetDirty(existing);
            Object.DestroyImmediate(asset);
            return existing;
        }
    }
}

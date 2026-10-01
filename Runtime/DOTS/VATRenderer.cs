using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.Rendering;

namespace AimeeBelke.VATCrowd
{
    // Main-thread bridge from ECS to the GPU: reads the transform + VATAnimation of every living
    // entity whose VATCharacter matches this renderer's clip data, then draws them with
    // Graphics.RenderMeshInstanced in batches of up to 1023.
    // Add one VATRenderer per baked character, each with that character's mesh, material and clip data.
    // Each batch gets its own ComputeBuffer of float4(startFrame, frameCount, animTime, 0),
    // bound as _VATInstanceData and indexed by instance ID in VAT.shader.
    // Lives in the regular scene (not the SubScene).
    public class VATRenderer : MonoBehaviour
    {
        const int BatchSize = 1023;

        [Header("Assets")]
        public Mesh vatMesh;
        public Material vatMaterial;
        public VATClipData vatClipData;

        [Header("Rendering")]
        public bool castShadows = true;

        [Header("Stats (read-only)")]
        [SerializeField] int renderedCount;
        [SerializeField] int batchCount;

        static readonly int VATInstanceDataId = Shader.PropertyToID("_VATInstanceData");
        static readonly int PosTexId = Shader.PropertyToID("_PosTex");
        static readonly int TexHeightId = Shader.PropertyToID("_TexHeight");

        World _world;
        EntityQuery _query;

        readonly List<ComputeBuffer> _buffers = new();
        readonly List<MaterialPropertyBlock> _blocks = new();
        readonly List<Matrix4x4[]> _matrices = new();
        readonly Vector4[] _data = new Vector4[BatchSize];

        void LateUpdate()
        {
            if (vatMesh == null || vatMaterial == null || vatClipData == null) return;
            if (!TryGetQuery(out var query)) return;

            query.SetSharedComponentFilter(new VATCharacter { ClipData = vatClipData });
            using var transforms = query.ToComponentDataArray<LocalTransform>(Allocator.Temp);
            using var vats = query.ToComponentDataArray<VATAnimation>(Allocator.Temp);

            renderedCount = transforms.Length;
            batchCount = (renderedCount + BatchSize - 1) / BatchSize;
            EnsureBatches(batchCount);

            var clips = vatClipData.clips;
            var rp = new RenderParams(vatMaterial)
            {
                shadowCastingMode = castShadows ? ShadowCastingMode.On : ShadowCastingMode.Off,
                receiveShadows = true,
                worldBounds = new Bounds(Vector3.zero, Vector3.one * 100000f)
            };

            for (int b = 0; b < batchCount; b++)
            {
                int start = b * BatchSize;
                int count = Mathf.Min(BatchSize, renderedCount - start);
                var matrices = _matrices[b];

                for (int i = 0; i < count; i++)
                {
                    var vat = vats[start + i];
                    var clip = clips[Mathf.Clamp(vat.AnimStrip, 0, clips.Length - 1)];
                    matrices[i] = transforms[start + i].ToMatrix();
                    _data[i] = new Vector4(clip.startFrame, clip.frameCount, vat.AnimTime, 0f);
                }

                _buffers[b].SetData(_data, 0, 0, count);
                _blocks[b].SetBuffer(VATInstanceDataId, _buffers[b]);
                // Taken from the clip data so a rebake with a different frame count can't go stale
                _blocks[b].SetTexture(PosTexId, vatClipData.positionTexture);
                _blocks[b].SetFloat(TexHeightId, vatClipData.positionTexture.height);
                rp.matProps = _blocks[b];
                Graphics.RenderMeshInstanced(rp, vatMesh, 0, matrices, count);
            }
        }

        bool TryGetQuery(out EntityQuery query)
        {
            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated)
            {
                query = default;
                return false;
            }

            if (world != _world)
            {
                _world = world;
                _query = world.EntityManager.CreateEntityQuery(new EntityQueryBuilder(Allocator.Temp)
                    .WithAll<LocalTransform, VATAnimation, VATCharacter>()
                    .WithNone<Dead>());
            }

            query = _query;
            return true;
        }

        // One buffer / property block / matrix array per batch: RenderMeshInstanced draws later in
        // the frame, so batches can't share a buffer without overwriting each other's data.
        void EnsureBatches(int needed)
        {
            while (_buffers.Count < needed)
            {
                _buffers.Add(new ComputeBuffer(BatchSize, 4 * sizeof(float)));
                _blocks.Add(new MaterialPropertyBlock());
                _matrices.Add(new Matrix4x4[BatchSize]);
            }
        }

        void OnDestroy()
        {
            foreach (var buffer in _buffers) buffer.Release();
            _buffers.Clear();
        }
    }
}

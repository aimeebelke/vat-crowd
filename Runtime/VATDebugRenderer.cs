using UnityEngine;

namespace AimeeBelke.VATCrowd
{
    // Standalone VAT animation preview
    // Wire up vatMesh, vatMaterial, vatClipData in the Inspector and hit Play
    // Use animStrip to pick the clip (0=Idle, 1=Walk, 2=Run, 3=Attack)
    // autoPlay cycles animTime at the correct clip speed, or scrub manually with autoPlay off
    public class VATDebugRenderer : MonoBehaviour
    {
        [Header("Assets")]
        public Mesh vatMesh;
        public Material vatMaterial;
        public VATClipData vatClipData;

        [Header("Playback")]
        [Range(0, 3)] public int animStrip = 0;
        [Range(0, 1)] public float animTime = 0f;
        public bool autoPlay = true;

        private ComputeBuffer _buffer;
        private MaterialPropertyBlock _mpb;
        private Matrix4x4[] _matrix = new Matrix4x4[1];
        private readonly Vector4[] _data = new Vector4[1];

        void Start()
        {
            // Single-element buffer: float4(startFrame, frameCount, animTime, 0)
            _buffer = new ComputeBuffer(1, 4 * sizeof(float));
            _mpb = new MaterialPropertyBlock();
        }

        void OnDestroy()
        {
            _buffer?.Release();
            _buffer = null;
        }

        void Update()
        {
            if (vatMesh == null || vatMaterial == null || vatClipData == null) return;

            // Resolve current clip strip
            int s = Mathf.Clamp(animStrip, 0, vatClipData.clips.Length - 1);
            var clip = vatClipData.clips[s];

            // Advance animation time if autoPlay is on
            if (autoPlay)
            {
                float clipDuration = clip.frameCount / Mathf.Max(clip.fps, 1f);
                animTime += Time.deltaTime / clipDuration;
                if (animTime >= 1f) animTime -= 1f;
            }

            // Upload per-instance data to the GPU buffer
            _data[0] = new Vector4(clip.startFrame, clip.frameCount, animTime, 0f);
            _buffer.SetData(_data);
            _mpb.SetBuffer("_VATInstanceData", _buffer);
            _mpb.SetTexture("_PosTex", vatClipData.positionTexture);
            _mpb.SetFloat("_TexHeight", vatClipData.positionTexture.height);

            // Render one instance at this GameObject's transform
            _matrix[0] = Matrix4x4.TRS(transform.position, transform.rotation, transform.localScale);
            Graphics.RenderMeshInstanced(new RenderParams(vatMaterial) { matProps = _mpb }, vatMesh, 0, _matrix, 1);
        }
    }
}

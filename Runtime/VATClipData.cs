using UnityEngine;

namespace AimeeBelke.VATCrowd
{
    // Describes the layout of animation strips packed into a VAT position texture
    [CreateAssetMenu(menuName = "VAT/VATClipData")]
    public class VATClipData : ScriptableObject
    {
        [System.Serializable]
        public struct ClipStrip
        {
            public string name;
            public int startFrame;
            public int frameCount;
            public float fps;
            public bool loop;
        }

        // RGBAFloat texture — width = vertex count, height = total frames
        public Texture2D positionTexture;
        public int vertexCount;

        // Index matches AIState: 0=Idle, 1=Walk, 2=Run, 3=Attack
        public ClipStrip[] clips;
    }
}

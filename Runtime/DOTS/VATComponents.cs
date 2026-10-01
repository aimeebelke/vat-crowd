using Unity.Entities;
using Unity.Mathematics;

namespace AimeeBelke.VATCrowd
{
    // Which baked VAT character an entity is, identified by its VATClipData asset
    // (one clip data per baked mesh). Shared, so entities are grouped into chunks per character,
    // and each VATRenderer draws only the entities whose ClipData matches its own.
    public struct VATCharacter : ISharedComponentData
    {
        public UnityObjectRef<VATClipData> ClipData;
    }

    // Per-entity VAT playback, read by VATRenderer and uploaded to the GPU.
    // Gameplay systems set RequestedStrip; VATAnimationSystem switches strips and advances AnimTime.
    public struct VATAnimation : IComponentData
    {
        public float AnimTime;        // 0-1 progress through the current strip
        public int AnimStrip;         // strip currently playing (index into VATClipData.clips)
        public int RequestedStrip;    // strip gameplay wants; switched to on the next update
        public float4 ClipDurations;  // seconds per strip, baked from VATClipData (first 4 strips)
    }

    // Zero-size tag: dead entities are skipped by animation, AI and rendering
    public struct Dead : IComponentData { }
}

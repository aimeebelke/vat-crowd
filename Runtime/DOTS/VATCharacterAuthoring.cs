using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace AimeeBelke.VATCrowd
{
    // Put this on any VAT character prefab inside a SubScene. Adds VATAnimation + VATCharacter;
    // pair it with a gameplay authoring such as AgentAuthoring. The VATRenderer whose clip data
    // matches this one draws the character.
    public class VATCharacterAuthoring : MonoBehaviour
    {
        public VATClipData clipData;

        class Baker : Baker<VATCharacterAuthoring>
        {
            public override void Bake(VATCharacterAuthoring authoring)
            {
                DependsOn(authoring.clipData);

                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddSharedComponent(entity, new VATCharacter { ClipData = authoring.clipData });
                AddComponent(entity, new VATAnimation
                {
                    AnimTime = 0f,
                    AnimStrip = 0,
                    RequestedStrip = 0,
                    ClipDurations = ClipDurations(authoring.clipData)
                });
            }

            // Duration in seconds of each of the first four strips
            static float4 ClipDurations(VATClipData data)
            {
                var d = new float4(1f);
                if (data == null || data.clips == null) return d;

                for (int i = 0; i < 4 && i < data.clips.Length; i++)
                {
                    var clip = data.clips[i];
                    d[i] = math.max(clip.frameCount / math.max(clip.fps, 1f), 0.01f);
                }
                return d;
            }
        }
    }
}

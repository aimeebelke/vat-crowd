using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;

namespace AimeeBelke.VATCrowd
{
    // Generic VAT playback for every character type: switches to RequestedStrip and
    // advances AnimTime at that strip's real speed. All strips loop.
    [BurstCompile]
    public partial struct VATAnimationSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            new AnimateJob { DeltaTime = SystemAPI.Time.DeltaTime }.ScheduleParallel();
        }

        [BurstCompile]
        [WithNone(typeof(Dead))]
        partial struct AnimateJob : IJobEntity
        {
            public float DeltaTime;

            void Execute(ref VATAnimation vat)
            {
                int strip = math.clamp(vat.RequestedStrip, 0, 3);
                if (strip != vat.AnimStrip)
                {
                    vat.AnimStrip = strip;
                    vat.AnimTime = 0f; // hard cut to the new clip
                }

                float duration = vat.ClipDurations[strip];
                vat.AnimTime = math.frac(vat.AnimTime + DeltaTime / duration);
            }
        }
    }
}

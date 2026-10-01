using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace AimeeBelke.VATCrowd
{
    // Roaming AI: each agent picks Idle / Walk / Run / Attack on a timer.
    // Walk and Run move toward a random point around Home; arriving drops back to Idle.
    // Writes the state into VATAnimation.RequestedStrip for VATAnimationSystem to play.
    [BurstCompile]
    [UpdateBefore(typeof(VATAnimationSystem))]
    public partial struct WanderSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            new WanderJob { DeltaTime = SystemAPI.Time.DeltaTime }.ScheduleParallel();
        }

        [BurstCompile]
        [WithNone(typeof(Dead))]
        partial struct WanderJob : IJobEntity
        {
            public float DeltaTime;

            void Execute(ref LocalTransform transform, ref NPCFollow npc, ref VATAnimation vat)
            {
                Wander(ref transform, ref npc);
                vat.RequestedStrip = (int)npc.State;
            }

            void Wander(ref LocalTransform transform, ref NPCFollow npc)
            {
                npc.StateTimer -= DeltaTime;
                if (npc.StateTimer <= 0f)
                    PickNextState(ref npc);

                if (npc.State != AIState.Walk && npc.State != AIState.Run)
                    return;

                float3 toTarget = npc.Target - transform.Position;
                toTarget.y = 0f;
                float dist = math.length(toTarget);

                if (dist < 0.5f)
                {
                    npc.State = AIState.Idle;
                    npc.StateTimer = npc.Rng.NextFloat(1f, 3f);
                    return;
                }

                float3 dir = toTarget / dist;
                float speed = npc.State == AIState.Run ? npc.RunSpeed : npc.WalkSpeed;
                transform.Position += dir * math.min(speed * DeltaTime, dist);

                quaternion facing = quaternion.LookRotationSafe(dir, math.up());
                transform.Rotation = math.slerp(transform.Rotation, facing, math.saturate(npc.TurnSpeed * DeltaTime));
            }

            static void PickNextState(ref NPCFollow npc)
            {
                float roll = npc.Rng.NextFloat();
                if (roll < 0.25f)
                {
                    npc.State = AIState.Idle;
                    npc.StateTimer = npc.Rng.NextFloat(1.5f, 4f);
                }
                else if (roll < 0.70f)
                {
                    npc.State = AIState.Walk;
                    npc.StateTimer = npc.Rng.NextFloat(4f, 10f);
                    npc.Target = RandomPointAroundHome(ref npc);
                }
                else if (roll < 0.90f)
                {
                    npc.State = AIState.Run;
                    npc.StateTimer = npc.Rng.NextFloat(2f, 5f);
                    npc.Target = RandomPointAroundHome(ref npc);
                }
                else
                {
                    npc.State = AIState.Attack;
                    npc.StateTimer = npc.Rng.NextFloat(2f, 4f);
                }
            }

            static float3 RandomPointAroundHome(ref NPCFollow npc)
            {
                float2 offset = npc.Rng.NextFloat2Direction() * math.sqrt(npc.Rng.NextFloat()) * npc.WanderRadius;
                return npc.Home + new float3(offset.x, 0f, offset.y);
            }
        }
    }
}

using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace AimeeBelke.VATCrowd
{
    // Instantiates every Spawner's prefab once, scattering instances in a disc, then removes the spawner.
    [BurstCompile]
    public partial struct SpawnSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<Spawner>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var query = SystemAPI.QueryBuilder().WithAll<Spawner>().Build();
            var spawnerEntities = query.ToEntityArray(Allocator.Temp);
            var spawners = query.ToComponentDataArray<Spawner>(Allocator.Temp);

            for (int s = 0; s < spawners.Length; s++)
            {
                var spawner = spawners[s];
                float3 centre = spawner.Center;
                var rng = Random.CreateFromIndex(spawner.Seed);

                var instances = state.EntityManager.Instantiate(spawner.Prefab, spawner.Count, Allocator.Temp);
                for (int i = 0; i < instances.Length; i++)
                {
                    float2 offset = rng.NextFloat2Direction() * math.sqrt(rng.NextFloat()) * spawner.Radius;
                    float3 pos = centre + new float3(offset.x, 0f, offset.y);
                    quaternion rot = quaternion.RotateY(rng.NextFloat(0f, 2f * math.PI));
                    SystemAPI.SetComponent(instances[i], LocalTransform.FromPositionRotation(pos, rot));

                    if (SystemAPI.HasComponent<NPCFollow>(instances[i]))
                    {
                        var follow = SystemAPI.GetComponent<NPCFollow>(instances[i]);
                        follow.Rng = Random.CreateFromIndex(spawner.Seed + (uint)i + 1u);
                        follow.Home = centre;
                        follow.Target = pos;
                        follow.State = AIState.Idle;
                        follow.StateTimer = rng.NextFloat(0f, 3f); // stagger first decisions
                        SystemAPI.SetComponent(instances[i], follow);
                    }

                    // Random start phase so the crowd isn't in lockstep
                    if (SystemAPI.HasComponent<VATAnimation>(instances[i]))
                    {
                        var vat = SystemAPI.GetComponent<VATAnimation>(instances[i]);
                        vat.AnimTime = rng.NextFloat();
                        SystemAPI.SetComponent(instances[i], vat);
                    }
                }

                state.EntityManager.RemoveComponent<Spawner>(spawnerEntities[s]);
            }
        }
    }
}

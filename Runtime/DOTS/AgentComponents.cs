using Unity.Entities;
using Unity.Mathematics;

namespace AimeeBelke.VATCrowd
{
    // Agent behaviour states. Values double as VAT strip indices, so bake clips in this order:
    // 0=Idle, 1=Walk, 2=Run, 3=Attack (the order in VATClipData.clips)
    public enum AIState : byte
    {
        Idle = 0,
        Walk = 1,
        Run = 2,
        Attack = 3
    }

    public struct Health : IComponentData
    {
        public int Current;
    }

    // Simple roaming brain: picks a state, walks/runs to a random point around Home, repeats
    public struct NPCFollow : IComponentData
    {
        public AIState State;
        public float StateTimer;
        public float3 Target;
        public float3 Home;
        public float WanderRadius;
        public float WalkSpeed;
        public float RunSpeed;
        public float TurnSpeed;
        public Random Rng;
    }

    // Spawns Count copies of Prefab once, then removes itself
    public struct Spawner : IComponentData
    {
        public Entity Prefab;
        public float3 Center;
        public int Count;
        public float Radius;
        public uint Seed;
    }
}

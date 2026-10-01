using Unity.Entities;
using UnityEngine;

namespace AimeeBelke.VATCrowd
{
    // Gameplay data for a roaming agent. Put this on a character prefab alongside VATCharacterAuthoring,
    // which supplies the animation side (VATAnimation + VATCharacter). Rendering is done by VATRenderer.
    public class AgentAuthoring : MonoBehaviour
    {
        [Header("Movement")]
        public float walkSpeed = 1.3f;
        public float runSpeed = 3.6f;
        public float turnSpeed = 6f;
        public float wanderRadius = 30f;

        [Header("Health")]
        public int health = 100;

        class Baker : Baker<AgentAuthoring>
        {
            public override void Bake(AgentAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);

                AddComponent(entity, new Health { Current = authoring.health });
                AddComponent(entity, new NPCFollow
                {
                    State = AIState.Idle,
                    WanderRadius = authoring.wanderRadius,
                    WalkSpeed = authoring.walkSpeed,
                    RunSpeed = authoring.runSpeed,
                    TurnSpeed = authoring.turnSpeed,
                    Rng = Unity.Mathematics.Random.CreateFromIndex(1) // re-seeded per instance by the spawner
                });
            }
        }
    }
}

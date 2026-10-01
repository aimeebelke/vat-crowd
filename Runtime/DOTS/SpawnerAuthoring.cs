using Unity.Entities;
using UnityEngine;

namespace AimeeBelke.VATCrowd
{
    // Put this inside the SubScene. Spawns the prefab entity in a disc around this object.
    public class SpawnerAuthoring : MonoBehaviour
    {
        public GameObject prefab;
        public int count = 2000;
        public float radius = 40f;
        public uint seed = 12345;

        class Baker : Baker<SpawnerAuthoring>
        {
            public override void Bake(SpawnerAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.None);
                AddComponent(entity, new Spawner
                {
                    Prefab = GetEntity(authoring.prefab, TransformUsageFlags.Dynamic),
                    Center = authoring.transform.position,
                    Count = authoring.count,
                    Radius = authoring.radius,
                    Seed = authoring.seed == 0 ? 1u : authoring.seed
                });
            }
        }
    }
}

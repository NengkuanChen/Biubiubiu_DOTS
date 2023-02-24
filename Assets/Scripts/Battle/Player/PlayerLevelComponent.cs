using Unity.Entities;

namespace Battle.Player
{
    public struct PlayerLevelComponent : IComponentData
    {
        public int Level;
        public int Experience;
    }
}
using Unity.Entities;

namespace Game
{
    public static class WorldGetter
    {
        public static World GetClientWorld()
        {
            foreach (var world in World.All)
            {
                if (world.Name == "ClientWorld")
                {
                    return world;
                }
            }

            return null;
        }
        
        public static World GetServerWorld()
        {
            foreach (var world in World.All)
            {
                if (world.Name == "ServerWorld")
                {
                    return world;
                }
            }

            return null;
        }
        
        public static World GetLocalWorld()
        {
            foreach (var world in World.All)
            {
                if (world.Flags == WorldFlags.Game)
                {
                    return world;
                }
            }

            return null;
        }
    }
}
using Unity.Entities;

namespace UI
{
    
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.ThinClientSimulation)]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public class UIUpdateSystemGroup : ComponentSystemGroup
    {
        
    }
}
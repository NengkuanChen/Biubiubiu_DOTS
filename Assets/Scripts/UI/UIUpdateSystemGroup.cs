using Unity.Entities;

namespace UI
{
    
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.ThinClientSimulation)]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial class UIUpdateSystemGroup : ComponentSystemGroup
    {
        
    }
}
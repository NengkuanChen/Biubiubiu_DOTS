
using Unity.Entities;
using Unity.Transforms;

/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

namespace Rukhanka
{
[UpdateBefore(typeof(TransformSystemGroup))]
public class RukhankaAnimationSystemGroup: ComponentSystemGroup { }
}

using Unity.Entities;

namespace Battle.Misc
{
    public struct CharacterDamageSourceRecordBuffer : ICleanupBufferElementData
    {
        public float Damage;
        public Entity SourcePlayer;
        public Entity SourceCharacter;
    }
}
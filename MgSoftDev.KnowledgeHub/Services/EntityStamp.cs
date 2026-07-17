using MgSoftDev.KnowledgeHub.Entities;

namespace MgSoftDev.KnowledgeHub.Services;

/// <summary>
/// Client-side generation of primary keys and audit values. Centralized so every storage
/// engine receives fully-populated entities and never needs server-side defaults.
/// </summary>
internal static class EntityStamp
{
    public static void PrepareNew(EntityBase entity, string? userName, DateTime now)
    {
        entity.Pk = Guid.CreateVersion7();
        entity.RowIsActive = true;
        entity.RowCreateDate = now;
        entity.RowUpdateDate = now;
        entity.RowUserCreate = userName;
        entity.RowUserUpdate = userName;
    }
}

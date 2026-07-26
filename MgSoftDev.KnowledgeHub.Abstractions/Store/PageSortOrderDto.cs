namespace MgSoftDev.KnowledgeHub.Store;

/// <summary>One page's new position, for the batch sort-order write.</summary>
public sealed record PageSortOrderDto(Guid Pk, int SortOrder);

namespace MgSoftDev.KnowledgeHub.Enums;

/// <summary>Direction of a one-step reorder among a page's siblings.</summary>
public enum PageMoveDirection
{
    /// <summary>Swap with the previous sibling (towards the top of the tree).</summary>
    Up,

    /// <summary>Swap with the next sibling (towards the bottom of the tree).</summary>
    Down
}

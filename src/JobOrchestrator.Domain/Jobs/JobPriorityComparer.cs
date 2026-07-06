namespace JobOrchestrator.Domain.Jobs;

/// <summary>Orders jobs High-priority first, then oldest-first (FIFO) within the same priority.</summary>
public sealed class JobPriorityComparer : IComparer<Job>
{
    public static readonly JobPriorityComparer Instance = new();

    public int Compare(Job? x, Job? y)
    {
        if (ReferenceEquals(x, y)) return 0;
        if (x is null) return 1;
        if (y is null) return -1;

        var rankComparison = x.Priority.SortRank().CompareTo(y.Priority.SortRank());
        return rankComparison != 0 ? rankComparison : x.CreatedAt.CompareTo(y.CreatedAt);
    }
}

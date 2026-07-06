namespace JobOrchestrator.Domain.Jobs;

public enum Priority
{
    Low = 0,
    High = 1,
}

public static class PriorityExtensions
{
    /// <summary>Lower rank sorts earlier in a priority-ordered queue (High before Low).</summary>
    public static int SortRank(this Priority priority) => priority == Priority.High ? 0 : 1;
}

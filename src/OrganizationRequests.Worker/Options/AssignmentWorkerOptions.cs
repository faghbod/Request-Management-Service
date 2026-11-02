using System.ComponentModel.DataAnnotations;

namespace OrganizationRequests.Worker.Options;

public sealed class AssignmentWorkerOptions
{
    public const string SectionName = "AssignmentWorker";

    [Range(1, int.MaxValue)]
    public int PollingIntervalSeconds { get; set; } = 30;
    [Range(1, 10)]
    public int MaxAssignmentAttempts { get; set; } = 3;
    [Range(0, int.MaxValue)]
    public int InitialRetryDelayMilliseconds { get; set; } = 100;
    [Range(0, int.MaxValue)]
    public int MaxRetryDelayMilliseconds { get; set; } = 1000;
}

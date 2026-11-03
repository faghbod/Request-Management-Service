using System.ComponentModel.DataAnnotations;

namespace OrganizationRequests.Application.Options;

// اندازه صفحه و آستانه افزایش اولویت درخواست منتظر.
public sealed class QueueProcessingOptions
{
    public const string SectionName = "QueueProcessing";

    [Range(1, 500)]
    public int BatchSize { get; set; } = 50;

    [Range(1, int.MaxValue)]
    public int AgingThresholdMinutes { get; set; } = 30;
}

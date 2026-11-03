using OrganizationRequests.Domain.Entities.Enums;

namespace OrganizationRequests.Domain.Entities;

// ارتباط میان کارشناس و موضوعی که در آن تخصص دارد.
public class ExpertSpecialty
{
    public int ExpertId { get; set; }
    public RequestSubject Subject { get; set; }
    public Expert Expert { get; set; } = null!;
}

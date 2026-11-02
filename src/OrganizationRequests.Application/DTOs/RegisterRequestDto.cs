using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using OrganizationRequests.Domain.Entities.Enums;

namespace OrganizationRequests.Application.DTOs;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class RegisterRequestDto : IValidatableObject
{
    public Guid RequestKey { get; set; }
    public RequestSubject Subject { get; set; }
    public RequestPriority Priority { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (RequestKey == Guid.Empty)
            yield return new ValidationResult("RequestKey must not be empty.", [nameof(RequestKey)]);
        if (!Enum.IsDefined(Subject))
            yield return new ValidationResult("Subject is invalid.", [nameof(Subject)]);
        if (!Enum.IsDefined(Priority))
            yield return new ValidationResult("Priority is invalid.", [nameof(Priority)]);
    }
}

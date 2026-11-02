namespace OrganizationRequests.Domain.Entities;

public class Expert : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public int Capacity { get; set; }
    public int ActiveRequestCount { get; set; }
    public bool IsActive { get; set; }
    public byte[] RowVersion { get; set; } = [];
    public ICollection<ExpertSpecialty> Specialties { get; set; } = [];
    public ICollection<ServiceRequest> AssignedRequests { get; set; } = [];
}

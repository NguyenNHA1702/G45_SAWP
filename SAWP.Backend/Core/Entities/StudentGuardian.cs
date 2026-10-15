using Core.Enums;

namespace Core.Entities;

public class StudentGuardian
{
    public int StudentId { get; set; }
    public virtual Student Student { get; set; } = null!;

    public int GuardianId { get; set; }
    public virtual Guardian Guardian { get; set; } = null!;

    public RelationshipTypes Relationship { get; set; } = RelationshipTypes.Guardian;
    public bool IsPrimaryContact { get; set; } = false;
}

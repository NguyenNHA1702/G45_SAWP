using Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Data.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.HasKey(u => u.Id);
        builder.Property(u => u.Username).HasMaxLength(100).IsRequired();
        builder.HasIndex(u => u.Username).IsUnique();
        builder.Property(u => u.Email).HasMaxLength(256).IsRequired();
        builder.Property(u => u.FullName).HasMaxLength(150).IsRequired();
        builder.Property(u => u.PhoneNumber).HasMaxLength(20);
    }
}

public class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> builder)
    {
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Name).HasMaxLength(50).IsRequired();
        builder.Property(r => r.Description).HasMaxLength(250);
    }
}

public class UserRoleConfiguration : IEntityTypeConfiguration<UserRole>
{
    public void Configure(EntityTypeBuilder<UserRole> builder)
    {
        builder.HasKey(ur => new { ur.UserId, ur.RoleId });

        builder.HasOne(ur => ur.User)
               .WithMany(u => u.UserRoles)
               .HasForeignKey(ur => ur.UserId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(ur => ur.Role)
               .WithMany(r => r.UserRoles)
               .HasForeignKey(ur => ur.RoleId)
               .OnDelete(DeleteBehavior.Cascade);
    }
}

public class StaffProfileConfiguration : IEntityTypeConfiguration<StaffProfile>
{
    public void Configure(EntityTypeBuilder<StaffProfile> builder)
    {
        builder.HasKey(sp => sp.Id);
        builder.Property(sp => sp.StaffCode).HasMaxLength(50).IsRequired();
        builder.HasIndex(sp => sp.StaffCode).IsUnique();
        builder.Property(sp => sp.Position).HasMaxLength(100);
        builder.Property(sp => sp.Department).HasMaxLength(100);

        builder.HasOne(sp => sp.User)
               .WithOne(u => u.StaffProfile)
               .HasForeignKey<StaffProfile>(sp => sp.UserId)
               .OnDelete(DeleteBehavior.Cascade);
    }
}

public class GuardianConfiguration : IEntityTypeConfiguration<Guardian>
{
    public void Configure(EntityTypeBuilder<Guardian> builder)
    {
        builder.HasKey(g => g.Id);
        builder.Property(g => g.IdentityCardNumber).HasMaxLength(50);
        builder.Property(g => g.Occupation).HasMaxLength(100);
        builder.Property(g => g.Address).HasMaxLength(250);

        builder.HasOne(g => g.User)
               .WithOne(u => u.Guardian)
               .HasForeignKey<Guardian>(g => g.UserId)
               .OnDelete(DeleteBehavior.Cascade);
    }
}

public class StudentConfiguration : IEntityTypeConfiguration<Student>
{
    public void Configure(EntityTypeBuilder<Student> builder)
    {
        builder.HasKey(s => s.Id);
        builder.Property(s => s.StudentCode).HasMaxLength(50).IsRequired();
        builder.HasIndex(s => s.StudentCode).IsUnique();
        builder.Property(s => s.FullName).HasMaxLength(150).IsRequired();
        builder.Property(s => s.Gender).HasMaxLength(10);
        builder.Property(s => s.Status).HasMaxLength(50);
    }
}

public class StudentGuardianConfiguration : IEntityTypeConfiguration<StudentGuardian>
{
    public void Configure(EntityTypeBuilder<StudentGuardian> builder)
    {
        builder.HasKey(sg => new { sg.StudentId, sg.GuardianId });

        builder.HasOne(sg => sg.Student)
               .WithMany(s => s.StudentGuardians)
               .HasForeignKey(sg => sg.StudentId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(sg => sg.Guardian)
               .WithMany(g => g.StudentGuardians)
               .HasForeignKey(sg => sg.GuardianId)
               .OnDelete(DeleteBehavior.Cascade);
    }
}

public class SchoolYearConfiguration : IEntityTypeConfiguration<SchoolYear>
{
    public void Configure(EntityTypeBuilder<SchoolYear> builder)
    {
        builder.HasKey(sy => sy.Id);
        builder.Property(sy => sy.YearName).HasMaxLength(50).IsRequired();
    }
}

public class SchoolCalendarDayConfiguration : IEntityTypeConfiguration<SchoolCalendarDay>
{
    public void Configure(EntityTypeBuilder<SchoolCalendarDay> builder)
    {
        builder.HasKey(sc => sc.Id);
        builder.Property(sc => sc.Note).HasMaxLength(250);

        builder.HasOne(sc => sc.SchoolYear)
               .WithMany(sy => sy.CalendarDays)
               .HasForeignKey(sc => sc.SchoolYearId)
               .OnDelete(DeleteBehavior.Cascade);
    }
}

public class ClassConfiguration : IEntityTypeConfiguration<Class>
{
    public void Configure(EntityTypeBuilder<Class> builder)
    {
        builder.HasKey(c => c.Id);
        builder.Property(c => c.ClassName).HasMaxLength(50).IsRequired();
        builder.Property(c => c.RoomNumber).HasMaxLength(50);
        builder.Property(c => c.Status).HasMaxLength(50);

        builder.HasOne(c => c.SchoolYear)
               .WithMany(sy => sy.Classes)
               .HasForeignKey(c => c.SchoolYearId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(c => c.HomeroomTeacher)
               .WithMany(sp => sp.HomeroomClasses)
               .HasForeignKey(c => c.HomeroomTeacherId)
               .OnDelete(DeleteBehavior.SetNull);
    }
}

public class ClassAssignmentConfiguration : IEntityTypeConfiguration<ClassAssignment>
{
    public void Configure(EntityTypeBuilder<ClassAssignment> builder)
    {
        builder.HasKey(ca => ca.Id);
        builder.Property(ca => ca.AcademicYear).HasMaxLength(50);

        builder.HasOne(ca => ca.Class)
               .WithMany(c => c.ClassAssignments)
               .HasForeignKey(ca => ca.ClassId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(ca => ca.Student)
               .WithMany(s => s.ClassAssignments)
               .HasForeignKey(ca => ca.StudentId)
               .OnDelete(DeleteBehavior.Cascade);
    }
}

public class AttendanceRecordConfiguration : IEntityTypeConfiguration<AttendanceRecord>
{
    public void Configure(EntityTypeBuilder<AttendanceRecord> builder)
    {
        builder.HasKey(ar => ar.Id);
        builder.Property(ar => ar.Remarks).HasMaxLength(250);

        builder.HasOne(ar => ar.Student)
               .WithMany(s => s.AttendanceRecords)
               .HasForeignKey(ar => ar.StudentId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(ar => ar.Class)
               .WithMany(c => c.AttendanceRecords)
               .HasForeignKey(ar => ar.ClassId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(ar => ar.RecordedByUser)
               .WithMany()
               .HasForeignKey(ar => ar.RecordedByUserId)
               .OnDelete(DeleteBehavior.Restrict);
    }
}

public class LeaveRequestConfiguration : IEntityTypeConfiguration<LeaveRequest>
{
    public void Configure(EntityTypeBuilder<LeaveRequest> builder)
    {
        builder.HasKey(lr => lr.Id);
        builder.Property(lr => lr.Reason).HasMaxLength(500).IsRequired();
        builder.Property(lr => lr.DoctorNoteAttachmentUrl).HasMaxLength(500);

        builder.HasOne(lr => lr.AdministrativeRequest)
               .WithOne(ar => ar.LeaveRequest)
               .HasForeignKey<LeaveRequest>(lr => lr.AdministrativeRequestId)
               .OnDelete(DeleteBehavior.Cascade);
    }
}

public class LunchServiceDayConfiguration : IEntityTypeConfiguration<LunchServiceDay>
{
    public void Configure(EntityTypeBuilder<LunchServiceDay> builder)
    {
        builder.HasKey(ls => ls.Id);
        builder.Property(ls => ls.Remarks).HasMaxLength(250);
    }
}

public class LunchRegistrationConfiguration : IEntityTypeConfiguration<LunchRegistration>
{
    public void Configure(EntityTypeBuilder<LunchRegistration> builder)
    {
        builder.HasKey(lr => lr.Id);
        builder.Property(lr => lr.Remarks).HasMaxLength(250);

        builder.HasOne(lr => lr.Student)
               .WithMany(s => s.LunchRegistrations)
               .HasForeignKey(lr => lr.StudentId)
               .OnDelete(DeleteBehavior.Cascade);
    }
}

public class AdministrativeRequestConfiguration : IEntityTypeConfiguration<AdministrativeRequest>
{
    public void Configure(EntityTypeBuilder<AdministrativeRequest> builder)
    {
        builder.HasKey(ar => ar.Id);
        builder.Property(ar => ar.RequestNumber).HasMaxLength(50).IsRequired();
        builder.HasIndex(ar => ar.RequestNumber).IsUnique();
        builder.Property(ar => ar.RequestType).HasMaxLength(100).IsRequired();
        builder.Property(ar => ar.ReviewerNotes).HasMaxLength(500);

        builder.HasOne(ar => ar.RequesterUser)
               .WithMany()
               .HasForeignKey(ar => ar.RequesterUserId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(ar => ar.Student)
               .WithMany(s => s.AdministrativeRequests)
               .HasForeignKey(ar => ar.StudentId)
               .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(ar => ar.ReviewerUser)
               .WithMany()
               .HasForeignKey(ar => ar.ReviewerUserId)
               .OnDelete(DeleteBehavior.Restrict);
    }
}

public class ClassTransferDetailConfiguration : IEntityTypeConfiguration<ClassTransferDetail>
{
    public void Configure(EntityTypeBuilder<ClassTransferDetail> builder)
    {
        builder.HasKey(ct => ct.Id);
        builder.Property(ct => ct.TransferReason).HasMaxLength(500).IsRequired();

        builder.HasOne(ct => ct.AdministrativeRequest)
               .WithOne(ar => ar.ClassTransferDetail)
               .HasForeignKey<ClassTransferDetail>(ct => ct.AdministrativeRequestId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(ct => ct.FromClass)
               .WithMany()
               .HasForeignKey(ct => ct.FromClassId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(ct => ct.ToClass)
               .WithMany()
               .HasForeignKey(ct => ct.ToClassId)
               .OnDelete(DeleteBehavior.Restrict);
    }
}

public class DocumentRequestDetailConfiguration : IEntityTypeConfiguration<DocumentRequestDetail>
{
    public void Configure(EntityTypeBuilder<DocumentRequestDetail> builder)
    {
        builder.HasKey(dr => dr.Id);
        builder.Property(dr => dr.DocumentType).HasMaxLength(150).IsRequired();
        builder.Property(dr => dr.Purpose).HasMaxLength(500).IsRequired();

        builder.HasOne(dr => dr.AdministrativeRequest)
               .WithOne(ar => ar.DocumentRequestDetail)
               .HasForeignKey<DocumentRequestDetail>(dr => dr.AdministrativeRequestId)
               .OnDelete(DeleteBehavior.Cascade);
    }
}

public class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.HasKey(n => n.Id);
        builder.Property(n => n.Title).HasMaxLength(200).IsRequired();
        builder.Property(n => n.Content).HasMaxLength(1000).IsRequired();
        builder.Property(n => n.LinkUrl).HasMaxLength(500);

        builder.HasOne(n => n.User)
               .WithMany(u => u.Notifications)
               .HasForeignKey(n => n.UserId)
               .OnDelete(DeleteBehavior.Cascade);
    }
}

public class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.HasKey(al => al.Id);
        builder.Property(al => al.Action).HasMaxLength(100).IsRequired();
        builder.Property(al => al.EntityName).HasMaxLength(100).IsRequired();
        builder.Property(al => al.EntityId).HasMaxLength(50);
        builder.Property(al => al.IpAddress).HasMaxLength(50);

        builder.HasOne(al => al.User)
               .WithMany(u => u.AuditLogs)
               .HasForeignKey(al => al.UserId)
               .OnDelete(DeleteBehavior.SetNull);
    }
}

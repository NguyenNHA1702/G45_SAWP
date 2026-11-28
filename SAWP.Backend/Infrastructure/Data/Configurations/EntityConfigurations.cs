using Core.Constants;
using Core.Enums;
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
        // Unique login email (BR-64); the database collation is case-insensitive.
        builder.HasIndex(u => u.Email).IsUnique();
        builder.Property(u => u.FullName).HasMaxLength(150).IsRequired();
        builder.Property(u => u.PhoneNumber).HasMaxLength(20);
        builder.Property(u => u.Status).HasDefaultValue(AccountStatus.Active);
        builder.HasIndex(u => new { u.CreatedAt, u.Id });
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
        builder.Property(g => g.FullName).HasMaxLength(150).IsRequired();
        builder.Property(g => g.PhoneNumber).HasMaxLength(20);
        builder.Property(g => g.Email).HasMaxLength(256);
        builder.Property(g => g.IdentityCardNumber).HasMaxLength(50);
        builder.Property(g => g.Occupation).HasMaxLength(100);
        builder.Property(g => g.Address).HasMaxLength(250);
        builder.HasIndex(g => g.Email);
        builder.HasIndex(g => new { g.UserId, g.FullName });

        // A guardian record may exist without an account (BR-64); deleting an account keeps the record.
        builder.HasOne(g => g.User)
               .WithOne(u => u.Guardian)
               .HasForeignKey<Guardian>(g => g.UserId)
               .IsRequired(false)
               .OnDelete(DeleteBehavior.SetNull);
    }
}

public class VerificationTokenConfiguration : IEntityTypeConfiguration<VerificationToken>
{
    public void Configure(EntityTypeBuilder<VerificationToken> builder)
    {
        builder.HasKey(t => t.Id);
        builder.Property(t => t.TokenHash).HasMaxLength(64).IsRequired();
        builder.HasIndex(t => t.TokenHash).IsUnique();
        builder.Property(t => t.NewEmail).HasMaxLength(256);
        builder.HasIndex(t => new { t.UserId, t.Purpose });

        builder.HasOne(t => t.User)
               .WithMany(u => u.VerificationTokens)
               .HasForeignKey(t => t.UserId)
               .OnDelete(DeleteBehavior.Cascade);
    }
}

public class AccountRequestConfiguration : IEntityTypeConfiguration<AccountRequest>
{
    public void Configure(EntityTypeBuilder<AccountRequest> builder)
    {
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Title).HasMaxLength(200).IsRequired();
        builder.Property(r => r.Note).HasMaxLength(1000);
        builder.Property(r => r.SourceReference).HasMaxLength(260);
        builder.HasIndex(r => r.Status);

        builder.HasOne(r => r.RequestedByUser)
               .WithMany()
               .HasForeignKey(r => r.RequestedByUserId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(r => r.FollowsUpRequest)
               .WithMany()
               .HasForeignKey(r => r.FollowsUpRequestId)
               .OnDelete(DeleteBehavior.Restrict);
    }
}

public class AccountRequestItemConfiguration : IEntityTypeConfiguration<AccountRequestItem>
{
    public void Configure(EntityTypeBuilder<AccountRequestItem> builder)
    {
        builder.HasKey(i => i.Id);
        builder.Property(i => i.Email).HasMaxLength(256);
        builder.Property(i => i.Reason).HasMaxLength(500);
        builder.HasIndex(i => new { i.AccountRequestId, i.GuardianId }).IsUnique();

        builder.HasOne(i => i.AccountRequest)
               .WithMany(r => r.Items)
               .HasForeignKey(i => i.AccountRequestId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(i => i.Guardian)
               .WithMany()
               .HasForeignKey(i => i.GuardianId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(i => i.CreatedUser)
               .WithMany()
               .HasForeignKey(i => i.CreatedUserId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(i => i.DecidedByUser)
               .WithMany()
               .HasForeignKey(i => i.DecidedByUserId)
               .OnDelete(DeleteBehavior.Restrict);
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
        builder.Property(s => s.ResidentialArea).HasMaxLength(200);
        builder.Property(s => s.SpecialNeedsNotes).HasMaxLength(250);
        builder.HasIndex(s => s.CreatedAt);
        builder.HasIndex(s => new { s.Status, s.FullName });
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
        builder.Property(sy => sy.Status).HasMaxLength(20).IsRequired().HasDefaultValue(SchoolYearStatuses.Active);
        builder.HasIndex(sy => new { sy.IsActive, sy.StartDate });
    }
}

public class SchoolCalendarDayConfiguration : IEntityTypeConfiguration<SchoolCalendarDay>
{
    public void Configure(EntityTypeBuilder<SchoolCalendarDay> builder)
    {
        builder.HasKey(sc => sc.Id);
        builder.Property(sc => sc.Note).HasMaxLength(250);
        builder.HasIndex(sc => new { sc.SchoolYearId, sc.Date });

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

        // Chống trùng bản ghi xếp lớp đang active cho cùng học sinh trong cùng lớp (ATT-12)
        builder.HasIndex(ca => new { ca.StudentId, ca.ClassId })
               .IsUnique()
               .HasFilter("[IsActive] = 1");
        builder.HasIndex(ca => new { ca.IsActive, ca.ClassId });
        builder.HasIndex(ca => new { ca.StudentId, ca.IsActive });
    }
}

public class AttendanceRecordConfiguration : IEntityTypeConfiguration<AttendanceRecord>
{
    public void Configure(EntityTypeBuilder<AttendanceRecord> builder)
    {
        builder.HasKey(ar => ar.Id);
        builder.Property(ar => ar.Remarks).HasMaxLength(500);
        builder.Property(ar => ar.CorrectionReason).HasMaxLength(500);
        // 1. Chỉ lưu phần Ngày (loại bỏ giờ phút giây ở SQL)
        builder.Property(ar => ar.Date).HasColumnType("date");
        // 2. Index chống trùng: 1 học sinh chỉ có 1 bản ghi điểm danh / buổi (Sáng/Chiều) / ngày (BR-20)
        builder.HasIndex(ar => new { ar.StudentId, ar.Date, ar.Session }).IsUnique();
        // 3. Index hỗ trợ truy vấn nhanh cả lớp theo ngày
        builder.HasIndex(ar => new { ar.ClassId, ar.Date });
        // FK cũ
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
        // ---- THÊM MỚI CHO WF 2 ----
        builder.HasOne(ar => ar.UpdatedByUser)
               .WithMany()
               .HasForeignKey(ar => ar.UpdatedByUserId)
               .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(ar => ar.LeaveRequest)
               .WithMany()
               .HasForeignKey(ar => ar.LeaveRequestId)
               .OnDelete(DeleteBehavior.Restrict);
    }
}

// OVR-10 (UC-23 A1, BR-62): contact notes on an unexcused absence
public class AbsenceContactNoteConfiguration : IEntityTypeConfiguration<AbsenceContactNote>
{
    public void Configure(EntityTypeBuilder<AbsenceContactNote> builder)
    {
        builder.HasKey(n => n.Id);
        builder.Property(n => n.Note).HasMaxLength(500).IsRequired();
        builder.Property(n => n.SupersedeReason).HasMaxLength(500);
        // Two users marking the same note superseded at once: the second update finds no row and fails (MSG09)
        builder.Property(n => n.SupersededAt).IsConcurrencyToken();
        builder.HasIndex(n => n.AttendanceRecordId);
        builder.HasOne(n => n.AttendanceRecord)
               .WithMany(ar => ar.ContactNotes)
               .HasForeignKey(n => n.AttendanceRecordId)
               .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(n => n.CreatedByUser)
               .WithMany()
               .HasForeignKey(n => n.CreatedByUserId)
               .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(n => n.SupersededByUser)
               .WithMany()
               .HasForeignKey(n => n.SupersededByUserId)
               .OnDelete(DeleteBehavior.Restrict);
    }
}

public class AttendanceReattendanceRequestConfiguration : IEntityTypeConfiguration<AttendanceReattendanceRequest>
{
    public void Configure(EntityTypeBuilder<AttendanceReattendanceRequest> builder)
    {
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Date).HasColumnType("date");
        builder.Property(r => r.Reason).HasMaxLength(500).IsRequired();
        builder.Property(r => r.ReviewNote).HasMaxLength(500);
        builder.HasIndex(r => new { r.ClassId, r.Date, r.Session, r.Status });
        builder.HasIndex(r => new { r.Status, r.CreatedAt });

        builder.HasOne(r => r.Class)
               .WithMany()
               .HasForeignKey(r => r.ClassId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(r => r.RequestedByUser)
               .WithMany()
               .HasForeignKey(r => r.RequestedByUserId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(r => r.ReviewedByUser)
               .WithMany()
               .HasForeignKey(r => r.ReviewedByUserId)
               .OnDelete(DeleteBehavior.Restrict);
    }
}

public class LeaveRequestConfiguration : IEntityTypeConfiguration<LeaveRequest>
{
    public void Configure(EntityTypeBuilder<LeaveRequest> builder)
    {
        builder.HasKey(lr => lr.Id);
        builder.Property(lr => lr.Reason).HasMaxLength(500).IsRequired();
        builder.Property(lr => lr.DoctorNoteAttachmentUrl).HasMaxLength(4000);

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
        builder.Property(lr => lr.Date).HasColumnType("date");
        builder.HasIndex(lr => new { lr.StudentId, lr.Date }).IsUnique();
        builder.Property(lr => lr.Remarks).HasMaxLength(250);
        builder.Property(lr => lr.CancellationReason).HasMaxLength(300);

        builder.HasOne(lr => lr.Student)
               .WithMany(s => s.LunchRegistrations)
               .HasForeignKey(lr => lr.StudentId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(lr => lr.CancelledByUser)
               .WithMany()
               .HasForeignKey(lr => lr.CancelledByUserId)
               .OnDelete(DeleteBehavior.Restrict);
    }
}

public class LunchMonthlyQuotaConfiguration : IEntityTypeConfiguration<LunchMonthlyQuota>
{
    public void Configure(EntityTypeBuilder<LunchMonthlyQuota> builder)
    {
        builder.HasKey(q => q.Id);
        builder.Property(q => q.Month).IsRequired().HasMaxLength(7);
        builder.HasIndex(q => q.Month).IsUnique();
        builder.HasOne(q => q.UpdatedByUser)
               .WithMany()
               .HasForeignKey(q => q.UpdatedByUserId)
               .OnDelete(DeleteBehavior.Restrict);
    }
}

public class BoardingIncidentConfiguration : IEntityTypeConfiguration<BoardingIncident>
{
    public void Configure(EntityTypeBuilder<BoardingIncident> builder)
    {
        builder.HasKey(bi => bi.Id);
        builder.Property(bi => bi.Date).HasColumnType("date");
        builder.Property(bi => bi.IncidentType).IsRequired().HasMaxLength(50);
        builder.Property(bi => bi.Note).IsRequired().HasMaxLength(1000);

        builder.HasOne(bi => bi.Student)
               .WithMany()
               .HasForeignKey(bi => bi.StudentId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(bi => bi.Class)
               .WithMany()
               .HasForeignKey(bi => bi.ClassId)
               .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(bi => bi.RecordedByUser)
               .WithMany()
               .HasForeignKey(bi => bi.RecordedByUserId)
               .OnDelete(DeleteBehavior.Restrict);
    }
}

public class ClassScheduleConfiguration : IEntityTypeConfiguration<ClassSchedule>
{
    public void Configure(EntityTypeBuilder<ClassSchedule> builder)
    {
        builder.HasKey(cs => cs.Id);
        builder.Property(cs => cs.Subject).HasMaxLength(100).IsRequired();
        builder.Property(cs => cs.EffectiveFrom).HasColumnType("date");
        builder.Property(cs => cs.EffectiveTo).HasColumnType("date");

        builder.HasIndex(cs => new { cs.ClassId, cs.DayOfWeek, cs.Period, cs.EffectiveFrom }).IsUnique();
        builder.HasIndex(cs => new { cs.TeacherStaffId, cs.DayOfWeek });

        builder.HasOne(cs => cs.Class)
               .WithMany()
               .HasForeignKey(cs => cs.ClassId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(cs => cs.SchoolYear)
               .WithMany()
               .HasForeignKey(cs => cs.SchoolYearId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(cs => cs.TeacherStaff)
               .WithMany()
               .HasForeignKey(cs => cs.TeacherStaffId)
               .OnDelete(DeleteBehavior.Restrict);
    }
}

public class AdministrativeRequestConfiguration : IEntityTypeConfiguration<AdministrativeRequest>
{
    public void Configure(EntityTypeBuilder<AdministrativeRequest> builder)
    {
        builder.HasKey(ar => ar.Id);
        builder.Property(ar => ar.RequestNumber).HasMaxLength(50).IsRequired();
        builder.HasIndex(ar => ar.RequestNumber).IsUnique();
        builder.HasIndex(ar => new { ar.RequestType, ar.Status, ar.SubmissionDate });
        builder.HasIndex(ar => new { ar.RequestType, ar.SubmissionDate });
        builder.HasIndex(ar => new { ar.RequesterUserId, ar.RequestType, ar.SubmissionDate });
        builder.Property(ar => ar.RequestType).HasMaxLength(100).IsRequired();
        builder.Property(ar => ar.ReviewerNotes).HasMaxLength(1000);
        builder.Property(ar => ar.AttachmentUrl).HasMaxLength(4000);
        builder.Property(ar => ar.AmendmentNotes).HasMaxLength(1000);
        builder.Property(ar => ar.SchoolBoardNotes).HasMaxLength(1000);

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
        builder.Property(ct => ct.TransferReason).HasMaxLength(1000).IsRequired();
        builder.Property(ct => ct.StaffRecommendation).HasMaxLength(1000);

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
        builder.Property(dr => dr.Purpose).HasMaxLength(1000).IsRequired();
        builder.Property(dr => dr.StaffEndorsement).HasMaxLength(1000);
        builder.Property(dr => dr.IssuedDocumentUrl).HasMaxLength(500);
        builder.Property(dr => dr.CertificateNumber).HasMaxLength(100);
        builder.Property(dr => dr.DigitalSignatureInfo).HasMaxLength(500);

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
        builder.Property(n => n.Type).HasMaxLength(50).IsRequired();
        builder.Property(n => n.ReferenceKey).HasMaxLength(100);

        builder.HasIndex(n => new { n.UserId, n.IsRead });
        builder.HasIndex(n => new { n.UserId, n.ReferenceKey })
               .IsUnique()
               .HasFilter("[ReferenceKey] IS NOT NULL");

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
        builder.HasIndex(al => new { al.EntityName, al.EntityId, al.Timestamp });

        builder.HasOne(al => al.User)
               .WithMany(u => u.AuditLogs)
               .HasForeignKey(al => al.UserId)
               .OnDelete(DeleteBehavior.SetNull);
    }
}


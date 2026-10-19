using Core.Entities;
using Microsoft.EntityFrameworkCore;
using System.Reflection;

namespace Infrastructure.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<StaffProfile> StaffProfiles => Set<StaffProfile>();
    public DbSet<Guardian> Guardians => Set<Guardian>();
    public DbSet<Student> Students => Set<Student>();
    public DbSet<StudentGuardian> StudentGuardians => Set<StudentGuardian>();
    public DbSet<SchoolYear> SchoolYears => Set<SchoolYear>();
    public DbSet<SchoolCalendarDay> SchoolCalendarDays => Set<SchoolCalendarDay>();
    public DbSet<Class> Classes => Set<Class>();
    public DbSet<ClassAssignment> ClassAssignments => Set<ClassAssignment>();
    public DbSet<AttendanceRecord> AttendanceRecords => Set<AttendanceRecord>();
    public DbSet<LeaveRequest> LeaveRequests => Set<LeaveRequest>();
    public DbSet<LunchServiceDay> LunchServiceDays => Set<LunchServiceDay>();
    public DbSet<LunchRegistration> LunchRegistrations => Set<LunchRegistration>();
    public DbSet<AdministrativeRequest> AdministrativeRequests => Set<AdministrativeRequest>();
    public DbSet<ClassTransferDetail> ClassTransferDetails => Set<ClassTransferDetail>();
    public DbSet<DocumentRequestDetail> DocumentRequestDetails => Set<DocumentRequestDetail>();
    public DbSet<GuardianLinkDetail> GuardianLinkDetails => Set<GuardianLinkDetail>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
    }
}

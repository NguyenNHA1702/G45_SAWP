using Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Data.Configurations;

public class PermissionConfiguration : IEntityTypeConfiguration<Permission>
{
    public void Configure(EntityTypeBuilder<Permission> builder)
    {
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Code).HasMaxLength(100).IsRequired();
        builder.Property(p => p.Name).HasMaxLength(150).IsRequired();
        builder.Property(p => p.Module).HasMaxLength(80).IsRequired();
        builder.Property(p => p.Description).HasMaxLength(300);
        builder.HasIndex(p => p.Code).IsUnique();
    }
}

public class RolePermissionConfiguration : IEntityTypeConfiguration<RolePermission>
{
    public void Configure(EntityTypeBuilder<RolePermission> builder)
    {
        builder.HasKey(rp => rp.Id);
        builder.HasIndex(rp => new { rp.RoleId, rp.PermissionId }).IsUnique();

        builder.HasOne(rp => rp.Role)
               .WithMany()
               .HasForeignKey(rp => rp.RoleId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(rp => rp.Permission)
               .WithMany(p => p.RolePermissions)
               .HasForeignKey(rp => rp.PermissionId)
               .OnDelete(DeleteBehavior.Cascade);
    }
}

public class ReferenceItemConfiguration : IEntityTypeConfiguration<ReferenceItem>
{
    public void Configure(EntityTypeBuilder<ReferenceItem> builder)
    {
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Category).HasMaxLength(80).IsRequired();
        builder.Property(r => r.Code).HasMaxLength(80).IsRequired();
        builder.Property(r => r.Label).HasMaxLength(200).IsRequired();
        builder.HasIndex(r => new { r.Category, r.Code }).IsUnique();
    }
}

public class NotificationTemplateConfiguration : IEntityTypeConfiguration<NotificationTemplate>
{
    public void Configure(EntityTypeBuilder<NotificationTemplate> builder)
    {
        builder.HasKey(nt => nt.Id);
        builder.Property(nt => nt.Code).HasMaxLength(80).IsRequired();
        builder.Property(nt => nt.TitleTemplate).HasMaxLength(200).IsRequired();
        builder.Property(nt => nt.BodyTemplate).HasMaxLength(1000).IsRequired();
        builder.HasIndex(nt => nt.Code).IsUnique();
    }
}

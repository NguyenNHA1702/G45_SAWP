using Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Data.Configurations;

public class ClassPostConfiguration : IEntityTypeConfiguration<ClassPost>
{
    public void Configure(EntityTypeBuilder<ClassPost> builder)
    {
        builder.HasKey(cp => cp.Id);
        builder.Property(cp => cp.Title).HasMaxLength(250).IsRequired();
        builder.Property(cp => cp.Content).HasMaxLength(4000).IsRequired();
        builder.Property(cp => cp.Category).HasMaxLength(50).HasDefaultValue("General");

        builder.HasIndex(cp => new { cp.ClassId, cp.CreatedAt });
        builder.HasIndex(cp => new { cp.ClassId, cp.IsPinned });

        builder.HasOne(cp => cp.Class)
               .WithMany()
               .HasForeignKey(cp => cp.ClassId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(cp => cp.AuthorUser)
               .WithMany()
               .HasForeignKey(cp => cp.AuthorUserId)
               .OnDelete(DeleteBehavior.Restrict);
    }
}

public class PostAttachmentConfiguration : IEntityTypeConfiguration<PostAttachment>
{
    public void Configure(EntityTypeBuilder<PostAttachment> builder)
    {
        builder.HasKey(pa => pa.Id);
        builder.Property(pa => pa.FileUrl).HasMaxLength(500).IsRequired();
        builder.Property(pa => pa.FileName).HasMaxLength(255).IsRequired();
        builder.Property(pa => pa.FileType).HasMaxLength(50).HasDefaultValue("Image");

        builder.HasOne(pa => pa.Post)
               .WithMany(p => p.Attachments)
               .HasForeignKey(pa => pa.PostId)
               .OnDelete(DeleteBehavior.Cascade);
    }
}

public class PostCommentConfiguration : IEntityTypeConfiguration<PostComment>
{
    public void Configure(EntityTypeBuilder<PostComment> builder)
    {
        builder.HasKey(pc => pc.Id);
        builder.Property(pc => pc.Content).HasMaxLength(500).IsRequired();

        builder.HasIndex(pc => new { pc.PostId, pc.CreatedAt });

        builder.HasOne(pc => pc.Post)
               .WithMany(p => p.Comments)
               .HasForeignKey(pc => pc.PostId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(pc => pc.User)
               .WithMany()
               .HasForeignKey(pc => pc.UserId)
               .OnDelete(DeleteBehavior.Restrict);
    }
}

public class PostReactionConfiguration : IEntityTypeConfiguration<PostReaction>
{
    public void Configure(EntityTypeBuilder<PostReaction> builder)
    {
        builder.HasKey(pr => pr.Id);
        builder.Property(pr => pr.ReactionType).HasMaxLength(50).HasDefaultValue("Heart");

        // Unique per user per post
        builder.HasIndex(pr => new { pr.PostId, pr.UserId }).IsUnique();

        builder.HasOne(pr => pr.Post)
               .WithMany(p => p.Reactions)
               .HasForeignKey(pr => pr.PostId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(pr => pr.User)
               .WithMany()
               .HasForeignKey(pr => pr.UserId)
               .OnDelete(DeleteBehavior.Restrict);
    }
}

public class StudentEvaluationConfiguration : IEntityTypeConfiguration<StudentEvaluation>
{
    public void Configure(EntityTypeBuilder<StudentEvaluation> builder)
    {
        builder.HasKey(se => se.Id);
        builder.Property(se => se.Rating).HasMaxLength(50).HasDefaultValue("Good");
        builder.Property(se => se.TeacherNote).HasMaxLength(1500).IsRequired();
        builder.Property(se => se.PrivateRemind).HasMaxLength(500);
        builder.Property(se => se.ParentFeedback).HasMaxLength(500);
        builder.Property(se => se.BadgesJson).HasMaxLength(1000).HasDefaultValue("[]");

        builder.HasIndex(se => new { se.StudentId, se.WeekNumber });
        builder.HasIndex(se => new { se.ClassId, se.WeekNumber });

        builder.HasOne(se => se.Student)
               .WithMany()
               .HasForeignKey(se => se.StudentId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(se => se.Class)
               .WithMany()
               .HasForeignKey(se => se.ClassId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(se => se.TeacherUser)
               .WithMany()
               .HasForeignKey(se => se.TeacherUserId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(se => se.AcknowledgedByGuardian)
               .WithMany()
               .HasForeignKey(se => se.AcknowledgedByGuardianId)
               .OnDelete(DeleteBehavior.Restrict);
    }
}

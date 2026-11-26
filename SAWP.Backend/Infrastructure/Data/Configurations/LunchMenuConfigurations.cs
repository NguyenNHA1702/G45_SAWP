using Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Data.Configurations;

public class DishConfiguration : IEntityTypeConfiguration<Dish>
{
    public void Configure(EntityTypeBuilder<Dish> builder)
    {
        builder.HasKey(d => d.Id);
        builder.Property(d => d.Name).IsRequired().HasMaxLength(120);
        builder.HasIndex(d => d.Name).IsUnique();
        builder.Property(d => d.SuitableMealTypes).IsRequired().HasMaxLength(100);
        builder.Property(d => d.AllergenTags).HasMaxLength(200);
    }
}

public class WeeklyMenuTemplateConfiguration : IEntityTypeConfiguration<WeeklyMenuTemplate>
{
    public void Configure(EntityTypeBuilder<WeeklyMenuTemplate> builder)
    {
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Name).IsRequired().HasMaxLength(120);
        builder.HasIndex(t => t.Name).IsUnique();
        builder.Property(t => t.Description).HasMaxLength(250);

        builder.HasOne(t => t.CreatedByUser)
            .WithMany()
            .HasForeignKey(t => t.CreatedByUserId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}

public class WeeklyMenuTemplateItemConfiguration : IEntityTypeConfiguration<WeeklyMenuTemplateItem>
{
    public void Configure(EntityTypeBuilder<WeeklyMenuTemplateItem> builder)
    {
        builder.HasKey(i => i.Id);
        builder.HasIndex(i => new { i.TemplateId, i.DayOfWeek, i.MealType, i.DishId }).IsUnique();

        builder.HasOne(i => i.Template)
            .WithMany(t => t.Items)
            .HasForeignKey(i => i.TemplateId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(i => i.Dish)
            .WithMany(d => d.TemplateItems)
            .HasForeignKey(i => i.DishId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class LunchServiceDayDishConfiguration : IEntityTypeConfiguration<LunchServiceDayDish>
{
    public void Configure(EntityTypeBuilder<LunchServiceDayDish> builder)
    {
        builder.HasKey(sdd => sdd.Id);
        builder.HasIndex(sdd => new { sdd.LunchServiceDayId, sdd.MealType, sdd.DishId }).IsUnique();

        builder.HasOne(sdd => sdd.LunchServiceDay)
            .WithMany(ls => ls.Dishes)
            .HasForeignKey(sdd => sdd.LunchServiceDayId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(sdd => sdd.Dish)
            .WithMany(d => d.ServiceDayDishes)
            .HasForeignKey(sdd => sdd.DishId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class StudentMealProfileConfiguration : IEntityTypeConfiguration<StudentMealProfile>
{
    public void Configure(EntityTypeBuilder<StudentMealProfile> builder)
    {
        builder.HasKey(p => p.Id);
        builder.HasIndex(p => p.StudentId).IsUnique();
        builder.Property(p => p.AllergenTags).HasMaxLength(200);
        builder.Property(p => p.AllergyNotes).HasMaxLength(250);

        builder.HasOne(p => p.Student)
            .WithOne()
            .HasForeignKey<StudentMealProfile>(p => p.StudentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(p => p.UpdatedByUser)
            .WithMany()
            .HasForeignKey(p => p.UpdatedByUserId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}

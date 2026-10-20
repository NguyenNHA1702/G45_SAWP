using Core.Entities;
using Core.Enums;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Data;

public static class DbInitializer
{
    public static async Task SeedAsync(ApplicationDbContext context)
    {
        // 1. Roles
        var roleNames = Enum.GetNames<UserRoles>();
        foreach (var rName in roleNames)
        {
            if (!await context.Roles.AnyAsync(r => r.Name == rName))
            {
                context.Roles.Add(new Role { Name = rName, Description = $"System role for {rName}" });
            }
        }
        await context.SaveChangesAsync();

        var rolesMap = await context.Roles.ToDictionaryAsync(r => r.Name, r => r.Id);

        // Password hash for 'Password@123'
        var defaultPasswordHash = BCrypt.Net.BCrypt.HashPassword("Password@123");

        // 2. Demo Users (Full 6 System Roles)

        // 1. Admin (Quản trị viên Hệ thống)
        if (!await context.Users.AnyAsync(u => u.Username == "admin"))
        {
            var adminUser = new User
            {
                Username = "admin",
                Email = "admin@sawp.edu.vn",
                FullName = "Quản trị viên Hệ thống",
                PasswordHash = defaultPasswordHash,
                IsActive = true
            };
            context.Users.Add(adminUser);
            await context.SaveChangesAsync();

            context.UserRoles.Add(new UserRole { UserId = adminUser.Id, RoleId = rolesMap[UserRoles.Admin.ToString()] });
        }

        // 2. SchoolBoard (Ban Giám Hiệu / Hiệu trưởng)
        if (!await context.Users.AnyAsync(u => u.Username == "board1"))
        {
            var boardUser = new User
            {
                Username = "board1",
                Email = "hieutruong@sawp.edu.vn",
                FullName = "Thầy Trần Đức Minh (Hiệu trưởng)",
                PhoneNumber = "0901234567",
                PasswordHash = defaultPasswordHash,
                IsActive = true
            };
            context.Users.Add(boardUser);
            await context.SaveChangesAsync();

            context.UserRoles.Add(new UserRole { UserId = boardUser.Id, RoleId = rolesMap[UserRoles.SchoolBoard.ToString()] });

            var staffProfile = new StaffProfile
            {
                UserId = boardUser.Id,
                StaffCode = "BGH001",
                Position = "Hiệu trưởng",
                Department = "Ban Giám hiệu",
                IsHomeroomTeacher = false
            };
            context.StaffProfiles.Add(staffProfile);
        }

        // 3. OfficeStaff (Giáo vụ / Nhân viên văn phòng)
        if (!await context.Users.AnyAsync(u => u.Username == "office1"))
        {
            var officeUser = new User
            {
                Username = "office1",
                Email = "giaovu@sawp.edu.vn",
                FullName = "Cô Phạm Thu Hà (Giáo vụ)",
                PhoneNumber = "0944556677",
                PasswordHash = defaultPasswordHash,
                IsActive = true
            };
            context.Users.Add(officeUser);
            await context.SaveChangesAsync();

            context.UserRoles.Add(new UserRole { UserId = officeUser.Id, RoleId = rolesMap[UserRoles.OfficeStaff.ToString()] });

            var staffProfile = new StaffProfile
            {
                UserId = officeUser.Id,
                StaffCode = "GVU001",
                Position = "Chuyên viên Giáo vụ",
                Department = "Phòng Giáo vụ",
                IsHomeroomTeacher = false
            };
            context.StaffProfiles.Add(staffProfile);
        }

        // 4. Teacher (Giáo viên chủ nhiệm)
        if (!await context.Users.AnyAsync(u => u.Username == "teacher1"))
        {
            var teacherUser = new User
            {
                Username = "teacher1",
                Email = "teacher1@sawp.edu.vn",
                FullName = "Cô Nguyễn Mai Hương (GVCN)",
                PhoneNumber = "0987654321",
                PasswordHash = defaultPasswordHash,
                IsActive = true
            };
            context.Users.Add(teacherUser);
            await context.SaveChangesAsync();

            context.UserRoles.Add(new UserRole { UserId = teacherUser.Id, RoleId = rolesMap[UserRoles.Teacher.ToString()] });

            var staffProfile = new StaffProfile
            {
                UserId = teacherUser.Id,
                StaffCode = "GV001",
                Position = "Giáo viên chủ nhiệm",
                Department = "Khối 1",
                IsHomeroomTeacher = true
            };
            context.StaffProfiles.Add(staffProfile);
        }

        // 5. ServiceStaff (Nhân viên phục vụ bán trú / Bếp)
        if (!await context.Users.AnyAsync(u => u.Username == "kitchen1"))
        {
            var kitchenUser = new User
            {
                Username = "kitchen1",
                Email = "kitchen1@sawp.edu.vn",
                FullName = "Bếp trưởng Lê Thị Bích",
                PhoneNumber = "0933221100",
                PasswordHash = defaultPasswordHash,
                IsActive = true
            };
            context.Users.Add(kitchenUser);
            await context.SaveChangesAsync();

            context.UserRoles.Add(new UserRole { UserId = kitchenUser.Id, RoleId = rolesMap[UserRoles.ServiceStaff.ToString()] });

            var staffProfile = new StaffProfile
            {
                UserId = kitchenUser.Id,
                StaffCode = "NV001",
                Position = "Bếp trưởng",
                Department = "Tổ Bán trú",
                IsHomeroomTeacher = false
            };
            context.StaffProfiles.Add(staffProfile);
        }

        // 6. Parent (Phụ huynh học sinh)
        if (!await context.Users.AnyAsync(u => u.Username == "parent1"))
        {
            var parentUser = new User
            {
                Username = "parent1",
                Email = "parent1@gmail.com",
                FullName = "Phụ huynh Trần Văn Nam",
                PhoneNumber = "0912345678",
                PasswordHash = defaultPasswordHash,
                IsActive = true
            };
            context.Users.Add(parentUser);
            await context.SaveChangesAsync();

            context.UserRoles.Add(new UserRole { UserId = parentUser.Id, RoleId = rolesMap[UserRoles.Parent.ToString()] });

            var guardian = new Guardian
            {
                UserId = parentUser.Id,
                IdentityCardNumber = "001200123456",
                Occupation = "Kỹ sư",
                Address = "Hà Nội"
            };
            context.Guardians.Add(guardian);
            await context.SaveChangesAsync();
        }

        // 3. Demo School Year & Class 1A
        var activeYear = await context.SchoolYears.FirstOrDefaultAsync(y => y.IsActive);
        if (activeYear == null)
        {
            activeYear = new SchoolYear
            {
                YearName = "Năm học 2026-2027",
                StartDate = new DateTime(2026, 9, 5),
                EndDate = new DateTime(2027, 5, 31),
                IsActive = true
            };
            context.SchoolYears.Add(activeYear);
            await context.SaveChangesAsync();
        }

        var teacherProfile = await context.StaffProfiles.FirstOrDefaultAsync(sp => sp.StaffCode == "GV001");
        var class1A = await context.Classes.FirstOrDefaultAsync(c => c.ClassName == "1A");
        if (class1A == null)
        {
            class1A = new Class
            {
                SchoolYearId = activeYear.Id,
                ClassName = "1A",
                Grade = 1,
                RoomNumber = "Phòng 101",
                Capacity = 35,
                HomeroomTeacherId = teacherProfile?.Id,
                Status = "Active"
            };
            context.Classes.Add(class1A);
            await context.SaveChangesAsync();
        }

        // 4. Demo Students & Guardian Linkage
        var guardianObj = await context.Guardians.FirstOrDefaultAsync();
        if (!await context.Students.AnyAsync(s => s.StudentCode == "HS001"))
        {
            var student1 = new Student
            {
                StudentCode = "HS001",
                FullName = "Trần Đức Anh",
                DateOfBirth = new DateTime(2020, 5, 12),
                Gender = "Nam",
                EnrollmentDate = new DateTime(2026, 9, 5),
                Status = "Enrolled"
            };
            var student2 = new Student
            {
                StudentCode = "HS002",
                FullName = "Nguyễn Minh Châu",
                DateOfBirth = new DateTime(2020, 8, 22),
                Gender = "Nữ",
                EnrollmentDate = new DateTime(2026, 9, 5),
                Status = "Enrolled"
            };
            var student3 = new Student
            {
                StudentCode = "HS003",
                FullName = "Lê Hoàng Long",
                DateOfBirth = new DateTime(2020, 2, 10),
                Gender = "Nam",
                EnrollmentDate = new DateTime(2026, 9, 5),
                Status = "Enrolled"
            };

            context.Students.AddRange(student1, student2, student3);
            await context.SaveChangesAsync();

            // Assign to Class 1A
            context.ClassAssignments.AddRange(
                new ClassAssignment { ClassId = class1A.Id, StudentId = student1.Id, AcademicYear = "2026-2027", IsActive = true, StartDate = new DateTime(2026, 9, 5) },
                new ClassAssignment { ClassId = class1A.Id, StudentId = student2.Id, AcademicYear = "2026-2027", IsActive = true, StartDate = new DateTime(2026, 9, 5) },
                new ClassAssignment { ClassId = class1A.Id, StudentId = student3.Id, AcademicYear = "2026-2027", IsActive = true, StartDate = new DateTime(2026, 9, 5) }
            );

            // Link parent1 to student1
            if (guardianObj != null)
            {
                context.StudentGuardians.Add(new StudentGuardian
                {
                    GuardianId = guardianObj.Id,
                    StudentId = student1.Id,
                    Relationship = RelationshipTypes.Father,
                    IsPrimaryContact = true
                });
            }

            // Register Lunch for student1 & student2
            context.LunchRegistrations.AddRange(
                new LunchRegistration { StudentId = student1.Id, Date = DateTime.Today, IsActive = true, MealType = MealType.StandardLunch },
                new LunchRegistration { StudentId = student1.Id, Date = DateTime.Today.AddDays(1), IsActive = true, MealType = MealType.StandardLunch },
                new LunchRegistration { StudentId = student2.Id, Date = DateTime.Today, IsActive = true, MealType = MealType.StandardLunch }
            );

            await context.SaveChangesAsync();
        }

        await context.SaveChangesAsync();
    }
}

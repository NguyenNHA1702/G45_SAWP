using Core.Configuration;
using Core.Constants;
using Core.Entities;
using Core.Enums;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Data;

public enum FirstAdminResult
{
    AdminExists,
    NotConfigured,
    Created
}

public static class DbInitializer
{
    // Reference data every environment needs. Safe to run on every startup.
    public static async Task SeedRolesAsync(ApplicationDbContext context)
    {
        // ====================================================================
        // 1. ROLES
        // ====================================================================
        var roleNames = Enum.GetNames<UserRoles>();
        foreach (var rName in roleNames)
        {
            if (!await context.Roles.AnyAsync(r => r.Name == rName))
            {
                context.Roles.Add(new Role { Name = rName, Description = $"System role for {rName}" });
            }
        }
        await context.SaveChangesAsync();
    }

    // Creates the first Admin account from configuration while no Admin account exists.
    // Throws when the settings are incomplete, the password breaks the password policy,
    // or the username/email already belongs to another account.
    public static async Task<FirstAdminResult> SeedFirstAdminAsync(ApplicationDbContext context, FirstAdminOptions? options)
    {
        var adminRoleName = UserRoles.Admin.ToString();
        if (await context.UserRoles.AnyAsync(ur => ur.Role.Name == adminRoleName))
        {
            return FirstAdminResult.AdminExists;
        }

        if (options == null || !options.IsConfigured)
        {
            return FirstAdminResult.NotConfigured;
        }

        var section = FirstAdminOptions.SectionName;
        var username = options.Username?.Trim();
        var email = options.Email?.Trim();
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(email) || string.IsNullOrEmpty(options.Password))
        {
            throw new InvalidOperationException($"{section}: Username, Email and Password are all required to create the first admin account.");
        }

        if (!PasswordPolicy.IsValid(options.Password))
        {
            throw new InvalidOperationException($"{section}:Password must have at least {PasswordPolicy.MinLength} characters with an upper-case letter, a lower-case letter, a digit and a special character.");
        }

        var taken = await context.Users.AnyAsync(u => u.Username.ToLower() == username.ToLower() || u.Email.ToLower() == email.ToLower());
        if (taken)
        {
            throw new InvalidOperationException($"{section}: the username or email already belongs to another account.");
        }

        var adminRoleId = await context.Roles.Where(r => r.Name == adminRoleName).Select(r => r.Id).SingleAsync();
        var admin = new User
        {
            Username = username,
            Email = email,
            FullName = string.IsNullOrWhiteSpace(options.FullName) ? username : options.FullName.Trim(),
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(options.Password),
            Status = AccountStatus.Active
        };
        admin.UserRoles.Add(new UserRole { RoleId = adminRoleId });
        context.Users.Add(admin);
        await context.SaveChangesAsync();

        return FirstAdminResult.Created;
    }

    // Demo accounts (shared default password) and sample school data.
    // Development and demo environments only; never run against production.
    public static async Task SeedDemoDataAsync(ApplicationDbContext context)
    {
        await SeedRolesAsync(context);

        var rolesMap = await context.Roles.ToDictionaryAsync(r => r.Name, r => r.Id);
        var defaultPasswordHash = BCrypt.Net.BCrypt.HashPassword("Password@123");

        // ====================================================================
        // 2. USERS & STAFF/GUARDIAN PROFILES
        // ====================================================================

        // 1. Admin
        var adminUser = await context.Users.FirstOrDefaultAsync(u => u.Username == "admin");
        if (adminUser == null)
        {
            adminUser = new User
            {
                Username = "admin",
                Email = "admin@sawp.edu.vn",
                FullName = "Quản trị viên Hệ thống",
                PasswordHash = defaultPasswordHash,
                Status = AccountStatus.Active
            };
            context.Users.Add(adminUser);
            await context.SaveChangesAsync();
            context.UserRoles.Add(new UserRole { UserId = adminUser.Id, RoleId = rolesMap[UserRoles.Admin.ToString()] });
        }

        // 2. SchoolBoard
        var boardUser = await context.Users.FirstOrDefaultAsync(u => u.Username == "board1");
        if (boardUser == null)
        {
            boardUser = new User
            {
                Username = "board1",
                Email = "hieutruong@sawp.edu.vn",
                FullName = "Thầy Trần Đức Minh (Hiệu trưởng)",
                PhoneNumber = "0901234567",
                PasswordHash = defaultPasswordHash,
                Status = AccountStatus.Active
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

        // 3. OfficeStaff
        var officeUser = await context.Users.FirstOrDefaultAsync(u => u.Username == "office1");
        if (officeUser == null)
        {
            officeUser = new User
            {
                Username = "office1",
                Email = "giaovu@sawp.edu.vn",
                FullName = "Cô Phạm Thu Hà (Giáo vụ)",
                PhoneNumber = "0944556677",
                PasswordHash = defaultPasswordHash,
                Status = AccountStatus.Active
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

        // 4. Teacher (teacher1 - GVCN lớp 1A)
        var teacherUser = await context.Users.FirstOrDefaultAsync(u => u.Username == "teacher1");
        if (teacherUser == null)
        {
            teacherUser = new User
            {
                Username = "teacher1",
                Email = "teacher1@sawp.edu.vn",
                FullName = "Cô Nguyễn Mai Hương (GVCN)",
                PhoneNumber = "0987654321",
                PasswordHash = defaultPasswordHash,
                Status = AccountStatus.Active
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

        // 5. ServiceStaff
        var kitchenUser = await context.Users.FirstOrDefaultAsync(u => u.Username == "kitchen1");
        if (kitchenUser == null)
        {
            kitchenUser = new User
            {
                Username = "kitchen1",
                Email = "kitchen1@sawp.edu.vn",
                FullName = "Bếp trưởng Lê Thị Bích",
                PhoneNumber = "0933221100",
                PasswordHash = defaultPasswordHash,
                Status = AccountStatus.Active
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

        // 6. Parent (parent1)
        var parentUser = await context.Users.FirstOrDefaultAsync(u => u.Username == "parent1");
        if (parentUser == null)
        {
            parentUser = new User
            {
                Username = "parent1",
                Email = "parent1@gmail.com",
                FullName = "Phụ huynh Trần Văn Nam",
                PhoneNumber = "0912345678",
                PasswordHash = defaultPasswordHash,
                Status = AccountStatus.Active
            };
            context.Users.Add(parentUser);
            await context.SaveChangesAsync();
            context.UserRoles.Add(new UserRole { UserId = parentUser.Id, RoleId = rolesMap[UserRoles.Parent.ToString()] });

            var guardian = new Guardian
            {
                UserId = parentUser.Id,
                FullName = parentUser.FullName,
                PhoneNumber = parentUser.PhoneNumber,
                Email = parentUser.Email,
                IdentityCardNumber = "001200123456",
                Occupation = "Kỹ sư",
                Address = "Hà Nội"
            };
            context.Guardians.Add(guardian);
        }

        await context.SaveChangesAsync();

        var teacherStaff = await context.StaffProfiles.FirstOrDefaultAsync(sp => sp.UserId == teacherUser.Id);
        var guardianObj = await context.Guardians.FirstOrDefaultAsync(g => g.UserId == parentUser.Id);

        // ====================================================================
        // 3. SCHOOL YEAR & CALENDAR
        // ====================================================================
        var schoolYear = await context.SchoolYears.FirstOrDefaultAsync(sy => sy.YearName == "2026-2027" || sy.YearName == "Năm học 2026-2027" || sy.IsActive);
        if (schoolYear == null)
        {
            schoolYear = new SchoolYear
            {
                YearName = "2026-2027",
                StartDate = new DateTime(2026, 9, 1),
                EndDate = new DateTime(2027, 6, 30),
                IsActive = true
            };
            context.SchoolYears.Add(schoolYear);
            await context.SaveChangesAsync();
        }
        else if (schoolYear.YearName != "2026-2027" && !schoolYear.YearName.Contains("2026-2027"))
        {
            schoolYear.YearName = "2026-2027";
            await context.SaveChangesAsync();
        }

        // Đảm bảo ngày hôm nay là ngày học (nếu rơi vào cuối tuần thì đánh dấu học bù để luôn có dữ liệu vận hành hôm nay)
        var today = DateTime.Today;
        var existingTodayCal = await context.SchoolCalendarDays
            .FirstOrDefaultAsync(d => d.Date == today && d.SchoolYearId == schoolYear.Id);
        bool isWeekendToday = today.DayOfWeek == DayOfWeek.Saturday || today.DayOfWeek == DayOfWeek.Sunday;
        if (existingTodayCal == null)
        {
            context.SchoolCalendarDays.Add(new SchoolCalendarDay
            {
                SchoolYearId = schoolYear.Id,
                Date = today,
                IsSchoolDay = true,
                Note = isWeekendToday ? "Ngày học bù" : "Ngày học bình thường"
            });
            await context.SaveChangesAsync();
        }
        else if (!existingTodayCal.IsSchoolDay)
        {
            existingTodayCal.IsSchoolDay = true;
            existingTodayCal.Note = isWeekendToday ? "Ngày học bù" : "Ngày học bình thường";
            await context.SaveChangesAsync();
        }


        // ====================================================================
        // 4. CLASSES
        // ====================================================================
        var class1A = await context.Classes.FirstOrDefaultAsync(c => c.ClassName == "1A" && c.SchoolYearId == schoolYear.Id);
        if (class1A == null)
        {
            class1A = new Class
            {
                SchoolYearId = schoolYear.Id,
                HomeroomTeacherId = teacherStaff?.Id,
                ClassName = "1A",
                Grade = 1,
                RoomNumber = "P101",
                Capacity = 35,
                Status = "Active"
            };
            context.Classes.Add(class1A);
            await context.SaveChangesAsync();
        }
        else if (class1A.HomeroomTeacherId == null && teacherStaff != null)
        {
            class1A.HomeroomTeacherId = teacherStaff.Id;
            await context.SaveChangesAsync();
        }

        var class1B = await context.Classes.FirstOrDefaultAsync(c => c.ClassName == "1B" && c.SchoolYearId == schoolYear.Id);
        if (class1B == null)
        {
            class1B = new Class
            {
                SchoolYearId = schoolYear.Id,
                HomeroomTeacherId = null,
                ClassName = "1B",
                Grade = 1,
                RoomNumber = "P102",
                Capacity = 35,
                Status = "Active"
            };
            context.Classes.Add(class1B);
            await context.SaveChangesAsync();
        }

        var class1C = await context.Classes.FirstOrDefaultAsync(c => c.ClassName == "1C" && c.SchoolYearId == schoolYear.Id);
        if (class1C == null)
        {
            class1C = new Class
            {
                SchoolYearId = schoolYear.Id,
                HomeroomTeacherId = null,
                ClassName = "1C",
                Grade = 1,
                RoomNumber = "P103",
                Capacity = 35,
                Status = "Active"
            };
            context.Classes.Add(class1C);
            await context.SaveChangesAsync();
        }


        // ====================================================================
        // 5. CLASS SCHEDULES (Phân công thời khóa biểu tiết 1 cho teacher1)
        // ====================================================================
        if (teacherStaff != null && !await context.ClassSchedules.AnyAsync(cs => cs.ClassId == class1B.Id && cs.TeacherStaffId == teacherStaff.Id))
        {
            foreach (DayOfWeek dow in new[] { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday })
            {
                context.ClassSchedules.Add(new ClassSchedule
                {
                    ClassId = class1B.Id,
                    SchoolYearId = schoolYear.Id,
                    DayOfWeek = dow,
                    Period = 1,
                    Subject = "Tiếng Việt",
                    TeacherStaffId = teacherStaff.Id,
                    EffectiveFrom = new DateTime(2026, 9, 1),
                    IsActive = true
                });
            }
            await context.SaveChangesAsync();
        }

        // ====================================================================
        // 6. STUDENTS & GUARDIAN LINK & CLASS ASSIGNMENT (30 em 1A, 28 em 1B, 28 em 1C)
        // ====================================================================
        var studentsData = new (string Code, string Name, string Gender, DateTime Dob, string Area, bool IsSpecial, string? SpecialNotes)[]
        {
            // ---- LỚP 1A (30 HỌC SINH: HS001 -> HS030) ----
            ("HS001", "Trần Gia Bảo", "Nam", new DateTime(2020, 3, 15), "Phường Bến Nghé", false, "Dị ứng hải sản (tôm, cua, sò)"),
            ("HS002", "Nguyễn An Nhiên", "Nữ", new DateTime(2020, 5, 20), "Phường Bến Nghé", false, null),
            ("HS003", "Lê Quang Minh", "Nam", new DateTime(2020, 8, 10), "Phường Đa Kao", true, "Hòa nhập khiếm thính nhẹ"),
            ("HS004", "Phạm Thùy Linh", "Nữ", new DateTime(2020, 2, 14), "Phường Đa Kao", false, null),
            ("HS005", "Hoàng Đức Anh", "Nam", new DateTime(2020, 11, 25), "Phường Tân Định", false, null),
            ("HS006", "Vũ Mai Phương", "Nữ", new DateTime(2020, 7, 8), "Phường Tân Định", false, null),
            ("HS007", "Đỗ Tuấn Kiệt", "Nam", new DateTime(2020, 1, 12), "Phường Bến Nghé", false, null),
            ("HS008", "Bùi Khánh Linh", "Nữ", new DateTime(2020, 4, 18), "Phường Đa Kao", false, null),
            ("HS009", "Phan Gia Huy", "Nam", new DateTime(2020, 6, 22), "Phường Tân Định", false, null),
            ("HS010", "Trịnh Thảo My", "Nữ", new DateTime(2020, 9, 5), "Phường Bến Nghé", false, null),
            ("HS011", "Đặng Tiến Đạt", "Nam", new DateTime(2020, 10, 14), "Phường Đa Kao", false, null),
            ("HS012", "Dương Diệu Linh", "Nữ", new DateTime(2020, 12, 3), "Phường Tân Định", false, null),
            ("HS013", "Ngô Quốc Anh", "Nam", new DateTime(2020, 2, 28), "Phường Bến Nghé", false, null),
            ("HS014", "Lý Bảo Trâm", "Nữ", new DateTime(2020, 8, 19), "Phường Đa Kao", false, null),
            ("HS015", "Trương Minh Khang", "Nam", new DateTime(2020, 7, 11), "Phường Tân Định", true, "Hòa nhập vận động nhẹ"),
            ("HS016", "Đoàn Phương Thảo", "Nữ", new DateTime(2020, 3, 24), "Phường Bến Nghé", false, null),
            ("HS017", "Mai Nhật Nam", "Nam", new DateTime(2020, 5, 9), "Phường Đa Kao", false, null),
            ("HS018", "Cao Ngọc Ánh", "Nữ", new DateTime(2020, 11, 17), "Phường Tân Định", false, null),
            ("HS019", "Đinh Hữu Phước", "Nam", new DateTime(2020, 4, 2), "Phường Bến Nghé", false, null),
            ("HS020", "Tạ Quỳnh Anh", "Nữ", new DateTime(2020, 10, 21), "Phường Đa Kao", false, null),
            ("HS021", "Lương Minh Quân", "Nam", new DateTime(2020, 6, 30), "Phường Tân Định", false, null),
            ("HS022", "Hà Kim Ngân", "Nữ", new DateTime(2020, 1, 15), "Phường Bến Nghé", false, null),
            ("HS023", "Chu Đức Thắng", "Nam", new DateTime(2020, 8, 27), "Phường Đa Kao", false, null),
            ("HS024", "Lâm Mỹ Duyên", "Nữ", new DateTime(2020, 12, 19), "Phường Tân Định", false, null),
            ("HS025", "Võ Minh Trí", "Nam", new DateTime(2020, 3, 8), "Phường Bến Nghé", false, null),
            ("HS026", "Diệp Thục Quyên", "Nữ", new DateTime(2020, 9, 14), "Phường Đa Kao", false, null),
            ("HS027", "Quách Hải Đăng", "Nam", new DateTime(2020, 5, 26), "Phường Tân Định", false, null),
            ("HS028", "Nghiêm Bảo Châu", "Nữ", new DateTime(2020, 7, 2), "Phường Bến Nghé", false, null),
            ("HS029", "Tôn Thất Khoa", "Nam", new DateTime(2020, 11, 4), "Phường Đa Kao", false, null),
            ("HS030", "Vương Yến Nhi", "Nữ", new DateTime(2020, 2, 18), "Phường Tân Định", false, null),

            // ---- LỚP 1B (28 HỌC SINH: HS031 -> HS058) ----
            ("HS031", "Trần Bảo Ngọc", "Nữ", new DateTime(2020, 5, 15), "Phường Bến Nghé", false, null),
            ("HS032", "Lê Hữu Đạt", "Nam", new DateTime(2020, 4, 10), "Phường Đa Kao", false, null),
            ("HS033", "Nguyễn Hà My", "Nữ", new DateTime(2020, 7, 22), "Phường Tân Định", false, null),
            ("HS034", "Phạm Gia Hưng", "Nam", new DateTime(2020, 9, 18), "Phường Bến Nghé", false, null),
            ("HS035", "Vũ Thùy Chi", "Nữ", new DateTime(2020, 1, 25), "Phường Đa Kao", false, null),
            ("HS036", "Hoàng Minh Triết", "Nam", new DateTime(2020, 11, 12), "Phường Tân Định", false, null),
            ("HS037", "Đặng Lan Hương", "Nữ", new DateTime(2020, 3, 5), "Phường Bến Nghé", false, null),
            ("HS038", "Bùi Quang Hải", "Nam", new DateTime(2020, 6, 14), "Phường Đa Kao", false, null),
            ("HS039", "Đỗ Ngọc Hân", "Nữ", new DateTime(2020, 8, 30), "Phường Tân Định", false, null),
            ("HS040", "Ngô Tuấn Anh", "Nam", new DateTime(2020, 12, 21), "Phường Bến Nghé", true, "Hòa nhập ngôn ngữ nhẹ"),
            ("HS041", "Trịnh Kim Oanh", "Nữ", new DateTime(2020, 2, 9), "Phường Đa Kao", false, null),
            ("HS042", "Lý Hoàng Long", "Nam", new DateTime(2020, 5, 17), "Phường Tân Định", false, null),
            ("HS043", "Dương Thảo Nguyên", "Nữ", new DateTime(2020, 10, 28), "Phường Bến Nghé", false, null),
            ("HS044", "Mai Văn Quyết", "Nam", new DateTime(2020, 4, 3), "Phường Đa Kao", false, null),
            ("HS045", "Cao Thị Cẩm Tú", "Nữ", new DateTime(2020, 8, 16), "Phường Tân Định", false, null),
            ("HS046", "Trương Quốc Bảo", "Nam", new DateTime(2020, 1, 29), "Phường Bến Nghé", false, null),
            ("HS047", "Đoàn Như Quỳnh", "Nữ", new DateTime(2020, 6, 7), "Phường Đa Kao", false, null),
            ("HS048", "Đinh Thế Vinh", "Nam", new DateTime(2020, 9, 24), "Phường Tân Định", false, null),
            ("HS049", "Tạ Thùy Dung", "Nữ", new DateTime(2020, 11, 8), "Phường Bến Nghé", false, null),
            ("HS050", "Lương Gia Phúc", "Nam", new DateTime(2020, 3, 19), "Phường Đa Kao", false, null),
            ("HS051", "Hà Cát Tường", "Nữ", new DateTime(2020, 7, 13), "Phường Tân Định", false, null),
            ("HS052", "Chu Thành Long", "Nam", new DateTime(2020, 12, 5), "Phường Bến Nghé", false, null),
            ("HS053", "Lâm Minh Thư", "Nữ", new DateTime(2020, 2, 23), "Phường Đa Kao", false, null),
            ("HS054", "Võ Thành Đạt", "Nam", new DateTime(2020, 5, 31), "Phường Tân Định", false, null),
            ("HS055", "Diệp Hoàng Lan", "Nữ", new DateTime(2020, 10, 12), "Phường Bến Nghé", false, null),
            ("HS056", "Quách Trọng Tấn", "Nam", new DateTime(2020, 4, 26), "Phường Đa Kao", false, null),
            ("HS057", "Nghiêm Thúy Vy", "Nữ", new DateTime(2020, 8, 4), "Phường Tân Định", false, null),
            ("HS058", "Tôn Nữ Bích Trâm", "Nữ", new DateTime(2020, 1, 18), "Phường Bến Nghé", false, null),

            // ---- LỚP 1C (28 HỌC SINH: HS059 -> HS086) ----
            ("HS059", "Nguyễn Tấn Phát", "Nam", new DateTime(2020, 3, 21), "Cụm 1 Tân Lập", false, null),
            ("HS060", "Trần Quỳnh Chi", "Nữ", new DateTime(2020, 6, 15), "Cụm 1 Tân Lập", false, null),
            ("HS061", "Lê Đăng Khoa", "Nam", new DateTime(2020, 9, 2), "Cụm 2 Tân Lập", false, null),
            ("HS062", "Phạm Ngọc Mai", "Nữ", new DateTime(2020, 11, 19), "Cụm 2 Tân Lập", false, null),
            ("HS063", "Hoàng Bá Thịnh", "Nam", new DateTime(2020, 1, 8), "Xã Tân Lập", false, null),
            ("HS064", "Vũ Thảo Nhi", "Nữ", new DateTime(2020, 4, 27), "Xã Tân Lập", false, null),
            ("HS065", "Đỗ Gia Khiêm", "Nam", new DateTime(2020, 7, 14), "Cụm 1 Tân Lập", false, null),
            ("HS066", "Bùi Minh Hạnh", "Nữ", new DateTime(2020, 10, 31), "Cụm 1 Tân Lập", false, null),
            ("HS067", "Phan Nhật Minh", "Nam", new DateTime(2020, 2, 12), "Cụm 2 Tân Lập", false, null),
            ("HS068", "Trịnh Phương Linh", "Nữ", new DateTime(2020, 5, 25), "Cụm 2 Tân Lập", false, null),
            ("HS069", "Đặng Quang Huy", "Nam", new DateTime(2020, 8, 18), "Xã Tân Lập", false, null),
            ("HS070", "Dương Tú Uyên", "Nữ", new DateTime(2020, 12, 7), "Xã Tân Lập", false, null),
            ("HS071", "Ngô Bảo Lâm", "Nam", new DateTime(2020, 3, 14), "Cụm 1 Tân Lập", false, null),
            ("HS072", "Lý Kiều Oanh", "Nữ", new DateTime(2020, 6, 29), "Cụm 1 Tân Lập", false, null),
            ("HS073", "Mai Trọng Nghĩa", "Nam", new DateTime(2020, 9, 11), "Cụm 2 Tân Lập", false, null),
            ("HS074", "Cao Diệu My", "Nữ", new DateTime(2020, 11, 23), "Cụm 2 Tân Lập", false, null),
            ("HS075", "Trương Vĩnh Phúc", "Nam", new DateTime(2020, 1, 30), "Xã Tân Lập", true, "Hòa nhập khiếm thị nhẹ"),
            ("HS076", "Đoàn Khánh Thi", "Nữ", new DateTime(2020, 4, 16), "Xã Tân Lập", false, null),
            ("HS077", "Đinh Hoàng Bách", "Nam", new DateTime(2020, 7, 9), "Cụm 1 Tân Lập", false, null),
            ("HS078", "Tạ Hải Yến", "Nữ", new DateTime(2020, 10, 4), "Cụm 1 Tân Lập", false, null),
            ("HS079", "Lương Anh Khoa", "Nam", new DateTime(2020, 2, 21), "Cụm 2 Tân Lập", false, null),
            ("HS080", "Hà Phương Vy", "Nữ", new DateTime(2020, 5, 13), "Cụm 2 Tân Lập", false, null),
            ("HS081", "Chu Thế Anh", "Nam", new DateTime(2020, 8, 6), "Xã Tân Lập", false, null),
            ("HS082", "Lâm Cẩm Ly", "Nữ", new DateTime(2020, 11, 28), "Xã Tân Lập", false, null),
            ("HS083", "Võ Hoàng Nam", "Nam", new DateTime(2020, 3, 17), "Cụm 1 Tân Lập", false, null),
            ("HS084", "Diệp Trúc Mai", "Nữ", new DateTime(2020, 6, 24), "Cụm 1 Tân Lập", false, null),
            ("HS085", "Quách Minh Khôi", "Nam", new DateTime(2020, 9, 15), "Cụm 2 Tân Lập", false, null),
            ("HS086", "Nghiêm Tuệ Mẫn", "Nữ", new DateTime(2020, 12, 10), "Xã Tân Lập", false, null)
        };

        var studentEntities = new List<Student>();
        foreach (var (code, name, gender, dob, area, isSpecial, specialNotes) in studentsData)
        {
            var student = await context.Students.FirstOrDefaultAsync(s => s.StudentCode == code);
            if (student == null)
            {
                student = new Student
                {
                    StudentCode = code,
                    FullName = name,
                    Gender = gender,
                    DateOfBirth = dob,
                    Status = "Active",
                    ResidentialArea = area,
                    IsSpecialNeeds = isSpecial,
                    SpecialNeedsNotes = specialNotes,
                    EnrollmentDate = new DateTime(2026, 9, 1)
                };
                context.Students.Add(student);
                await context.SaveChangesAsync();
            }
            else
            {
                student.FullName = name;
                student.Gender = gender;
                student.DateOfBirth = dob;
                student.Status = "Active";
                student.ResidentialArea = area;
                student.IsSpecialNeeds = isSpecial;
                student.SpecialNeedsNotes = specialNotes;
                await context.SaveChangesAsync();
            }
            studentEntities.Add(student);

            // Xác định lớp gán (1A: 1-30, 1B: 31-58, 1C: 59-86)
            int targetClassId = class1A.Id;
            if (int.TryParse(code.Replace("HS", ""), out int num))
            {
                if (num >= 59) targetClassId = class1C.Id;
                else if (num >= 31) targetClassId = class1B.Id;
                else targetClassId = class1A.Id;
            }

            if (!await context.ClassAssignments.AnyAsync(ca => ca.ClassId == targetClassId && ca.StudentId == student.Id))
            {
                // Ngừng kích hoạt các phân lớp cũ nếu có
                var oldAssignments = await context.ClassAssignments.Where(ca => ca.StudentId == student.Id && ca.ClassId != targetClassId).ToListAsync();
                foreach (var oa in oldAssignments) oa.IsActive = false;

                context.ClassAssignments.Add(new ClassAssignment
                {
                    ClassId = targetClassId,
                    StudentId = student.Id,
                    AcademicYear = "2026-2027",
                    StartDate = new DateTime(2026, 9, 1),
                    IsActive = true
                });
                await context.SaveChangesAsync();
            }
        }
        await context.SaveChangesAsync();

        // Đăng ký suất ăn bán trú cho toàn bộ học sinh từ đầu tháng hiện tại đến +7 ngày (các ngày học T2-T6)
        var lunchSeedStart = new DateTime(today.Year, today.Month, 1);
        var lunchSeedEnd = today.AddDays(7);
        var lunchSeedDates = new List<DateTime>();
        for (var cur = lunchSeedStart; cur <= lunchSeedEnd; cur = cur.AddDays(1))
        {
            if (cur == today || (cur.DayOfWeek != DayOfWeek.Saturday && cur.DayOfWeek != DayOfWeek.Sunday))
            {
                lunchSeedDates.Add(cur);
            }
        }

        var existingLunchRows = await context.LunchRegistrations
            .Where(lr => lr.Date >= lunchSeedStart && lr.Date <= lunchSeedEnd)
            .ToListAsync();
        var existingLunchMap = existingLunchRows
            .GroupBy(lr => (lr.StudentId, lr.Date.Date))
            .ToDictionary(g => g.Key, g => g.First());

        // Dọn chuỗi thừa (UC-30) nếu còn tồn tại trong dữ liệu cũ
        foreach (var row in existingLunchRows)
        {
            if (!string.IsNullOrEmpty(row.CancellationReason) && row.CancellationReason.Contains(" (UC-30)"))
            {
                row.CancellationReason = row.CancellationReason.Replace(" (UC-30)", string.Empty);
            }
        }

        foreach (var student in studentEntities)
        {
            var code = student.StudentCode;
            bool isVegetarian = code is "HS004" or "HS017" or "HS022" or "HS035" or "HS062";
            bool isSpecialDiet = code is "HS006" or "HS015" or "HS028" or "HS040";
            bool isCancelled = code is "HS005" or "HS012" or "HS038" or "HS050";

            string mealDishRemarks = isVegetarian
                ? "Cơm trắng dẻo, Đậu phụ sốt cà chua, Nấm đùi gà kho xì dầu, Rau củ quả luộc, Canh nấm hạt sen | Ghi chú: Suất ăn chay thanh đạm"
                : isSpecialDiet
                    ? (code == "HS006"
                        ? "Cơm mềm dẻo, Thịt gà phi lê áp chảo, Rau củ hấp mềm, Canh rau ngót thịt băm, Sữa chua | Ghi chú: Dị ứng tôm & hải sản có vỏ"
                        : "Cháo dinh dưỡng thịt băm, Cá hồi hấp xì dầu, Bí đỏ hấp mềm, Canh rau mồng tơi, Sữa chua | Ghi chú: Chế độ ăn mềm dễ tiêu hóa")
                    : string.Empty;

            foreach (var mealDate in lunchSeedDates)
            {
                if (!existingLunchMap.TryGetValue((student.Id, mealDate), out var existingLunch))
                {
                    context.LunchRegistrations.Add(new LunchRegistration
                    {
                        StudentId = student.Id,
                        Date = mealDate,
                        MealType = isVegetarian ? MealType.VegetarianLunch : isSpecialDiet ? MealType.SpecialDiet : MealType.StandardLunch,
                        IsActive = !isCancelled,
                        Remarks = mealDishRemarks,
                        CancelledAt = isCancelled ? mealDate.AddHours(7).AddMinutes(45) : null,
                        CancellationSource = isCancelled ? (code is "HS005" or "HS038" ? LunchCancellationSource.Leave : LunchCancellationSource.Parent) : null,
                        CancellationReason = isCancelled ? (code is "HS005" or "HS038" ? "Đơn xin nghỉ ốm đã được GVCN duyệt trước 08h15" : "Phụ huynh hủy trực tiếp trên cổng trước 08h15") : null
                    });
                }
                else if (existingLunch.Remarks == "Suất ăn bán trú tiêu chuẩn" || existingLunch.Remarks == "Suất ăn chay dinh dưỡng" || existingLunch.Remarks == "Thực đơn dinh dưỡng đặc biệt")
                {
                    bool existVeg = existingLunch.MealType == MealType.VegetarianLunch;
                    bool existSpec = existingLunch.MealType == MealType.SpecialDiet;
                    existingLunch.Remarks = existVeg
                        ? "Cơm trắng dẻo, Đậu phụ sốt cà chua, Nấm đùi gà kho xì dầu, Rau củ quả luộc, Canh nấm hạt sen | Ghi chú: Suất ăn chay thanh đạm"
                        : existSpec
                            ? "Cơm mềm dẻo, Thịt gà phi lê áp chảo, Rau củ hấp mềm, Canh rau ngót thịt băm, Sữa chua | Ghi chú: Dị ứng tôm & hải sản có vỏ"
                            : null;
                }
            }
        }
        await context.SaveChangesAsync();

        // Dọn dẹp bản ghi điểm danh tương lai (nếu từng được seed trước đó - AT-10)
        var futureAttendance = await context.AttendanceRecords
            .Where(ar => ar.Date > today)
            .ToListAsync();
        if (futureAttendance.Count > 0)
        {
            context.AttendanceRecords.RemoveRange(futureAttendance);
            await context.SaveChangesAsync();
        }

        // Điểm danh chuyên cần toàn diện cho toàn bộ học sinh (1A, 1B, 1C) từ đầu năm học 2026 đến ngày học gần nhất (Ca sáng & Ca chiều)
        var schoolDays = new List<DateTime>();
        var checkDate = new DateTime(2026, 9, 1);
        var endSchoolDate = today;
        while (checkDate <= endSchoolDate)
        {
            if (checkDate == today || (checkDate.DayOfWeek != DayOfWeek.Saturday && checkDate.DayOfWeek != DayOfWeek.Sunday && checkDate != new DateTime(2026, 9, 2)))
            {
                schoolDays.Add(checkDate);
            }
            checkDate = checkDate.AddDays(1);
        }

        var activeAssignments = await context.ClassAssignments
            .Where(ca => ca.IsActive)
            .Include(ca => ca.Student)
            .ToListAsync();

        var existingAttendanceKeys = (await context.AttendanceRecords
            .Select(ar => new { ar.StudentId, ar.Date, ar.Session })
            .ToListAsync())
            .Select(ar => (ar.StudentId, ar.Date, ar.Session))
            .ToHashSet();

        for (int dayIdx = 0; dayIdx < schoolDays.Count; dayIdx++)
        {
            var sDate = schoolDays[dayIdx];
            foreach (var session in new[] { AttendanceSession.Morning, AttendanceSession.Afternoon })
            {
                foreach (var ca in activeAssignments)
                {
                    if (!existingAttendanceKeys.Contains((ca.StudentId, sDate, session)))
                    {
                        var stCode = ca.Student.StudentCode;
                        var attStatus = AttendanceStatus.Present;
                        string? attRem = null;

                        if (stCode == "HS005" && sDate >= today.AddDays(-2))
                        {
                            attStatus = AttendanceStatus.ExcusedAbsence;
                            attRem = "Nghỉ ốm có đơn xin phép từ phụ huynh";
                        }
                        else if (stCode == "HS011" && session == AttendanceSession.Morning && dayIdx % 3 == 0)
                        {
                            attStatus = AttendanceStatus.Late;
                            attRem = "Đến muộn 15 phút do kẹt xe";
                        }
                        else if (stCode == "HS038" && sDate >= today.AddDays(-1))
                        {
                            attStatus = AttendanceStatus.ExcusedAbsence;
                            attRem = "Gia đình có việc hiếu hỉ có đơn phép";
                        }
                        else if ((ca.StudentId + dayIdx * 7 + (int)session) % 37 == 0)
                        {
                            attStatus = AttendanceStatus.Late;
                            attRem = "Đến muộn 10-15 phút do thời tiết / kẹt xe";
                        }
                        else if ((ca.StudentId + dayIdx * 11 + (int)session) % 41 == 0)
                        {
                            attStatus = AttendanceStatus.ExcusedAbsence;
                            attRem = "Nghỉ có đơn xin phép từ gia đình";
                        }
                        else if ((ca.StudentId + dayIdx * 13 + (int)session) % 89 == 0)
                        {
                            attStatus = AttendanceStatus.UnexcusedAbsence;
                            attRem = "Vắng không rõ lý do, GVCN đã liên hệ";
                        }

                        context.AttendanceRecords.Add(new AttendanceRecord
                        {
                            StudentId = ca.StudentId,
                            ClassId = ca.ClassId,
                            Date = sDate,
                            Session = session,
                            Status = attStatus,
                            Remarks = attRem,
                            RecordedByUserId = teacherUser.Id,
                            CreatedAt = DateTime.SpecifyKind(sDate.AddHours(session == AttendanceSession.Morning ? 1 : 7), DateTimeKind.Utc)
                        });
                    }
                }
            }
        }
        await context.SaveChangesAsync();

        // Liên kết phụ huynh `parent1` với học sinh `HS001` (Trần Gia Bảo - Lớp 1A)
        var student1 = studentEntities.FirstOrDefault(s => s.StudentCode == "HS001");
        if (student1 != null && string.IsNullOrWhiteSpace(student1.SpecialNeedsNotes))
        {
            student1.SpecialNeedsNotes = "Dị ứng hải sản (tôm, cua, sò)";
            await context.SaveChangesAsync();
        }
        if (student1 != null && guardianObj != null)
        {
            if (!await context.StudentGuardians.AnyAsync(sg => sg.StudentId == student1.Id && sg.GuardianId == guardianObj.Id))
            {
                context.StudentGuardians.Add(new StudentGuardian
                {
                    StudentId = student1.Id,
                    GuardianId = guardianObj.Id,
                    Relationship = RelationshipTypes.Father,
                    IsPrimaryContact = true
                });
                await context.SaveChangesAsync();
            }
        }

        // Liên kết con thứ 2 của phụ huynh `parent1`: HS031 (Trần Bảo Ngọc - Lớp 1B)
        var student31 = studentEntities.FirstOrDefault(s => s.StudentCode == "HS031");
        if (student31 != null && guardianObj != null)
        {
            if (!await context.StudentGuardians.AnyAsync(sg => sg.StudentId == student31.Id && sg.GuardianId == guardianObj.Id))
            {
                context.StudentGuardians.Add(new StudentGuardian
                {
                    StudentId = student31.Id,
                    GuardianId = guardianObj.Id,
                    Relationship = RelationshipTypes.Father,
                    IsPrimaryContact = false
                });
                await context.SaveChangesAsync();
            }
        }

        // ====================================================================
        // 6.1 TỰ ĐỘNG KHỞI TẠO PHỤ HUYNH & SĐT LIÊN HỆ CHO TẤT CẢ HỌC SINH
        // ====================================================================
        var allStudents = await context.Students.Include(s => s.StudentGuardians).ToListAsync();
        var parentRoleId = rolesMap[UserRoles.Parent.ToString()];

        var fatherNamesList = new[] { "Văn Hùng", "Quốc Tuấn", "Minh Trí", "Đức Thắng", "Hoàng Long", "Thanh Hải", "Tiến Đạt", "Gia Huy", "Thành Nam", "Văn Minh" };
        var motherNamesList = new[] { "Nguyễn Thị Mai", "Trần Thị Lan", "Lê Thị Hương", "Phạm Mỹ Hạnh", "Hoàng Thúy Nga", "Vũ Ngọc Bích", "Đỗ Thanh Thảo", "Bùi Kim Ngân", "Phan Cẩm Tú", "Dương Thảo Trang" };
        var occupations1 = new[] { "Kỹ sư phần mềm", "Bác sĩ đa khoa", "Giáo viên", "Kinh doanh tự do", "Kế toán trưởng", "Kiến trúc sư", "Chuyên viên ngân hàng", "Công chức nhà nước" };
        var occupations2 = new[] { "Giáo viên", "Dược sĩ", "Kế toán viên", "Nhân viên văn phòng", "Bác sĩ nha khoa", "Quản lý nhân sự", "Chuyên viên tư vấn", "Kinh doanh" };

        for (int idx = 0; idx < allStudents.Count; idx++)
        {
            var st = allStudents[idx];
            var codeLower = st.StudentCode.ToLowerInvariant();
            var surname = st.FullName.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "Nguyễn";

            // 1. Phụ huynh chính (Bố/Mẹ)
            if (!st.StudentGuardians.Any(sg => sg.IsPrimaryContact))
            {
                var username = $"ph_{codeLower}";
                var email = $"parent_{codeLower}@sawp.edu.vn";
                var fatherFullName = $"{surname} {fatherNamesList[idx % fatherNamesList.Length]}";
                var phone = $"09{(10 + (idx % 70)):D2}{(123450 + idx * 77):D6}";
                var cccd = $"001090{(100000 + idx * 2):D6}";
                var occ = occupations1[idx % occupations1.Length];
                var addr = $"{12 + idx} Đường số {1 + (idx % 12)}, {st.ResidentialArea ?? "Phường Bến Nghé"}, TP. Hồ Chí Minh";

                var user = await context.Users.FirstOrDefaultAsync(u => u.Username == username);
                if (user == null)
                {
                    user = new User
                    {
                        Username = username,
                        Email = email,
                        PasswordHash = defaultPasswordHash,
                        FullName = fatherFullName,
                        PhoneNumber = phone,
                        Status = AccountStatus.Active,
                        CreatedAt = DateTime.UtcNow
                    };
                    context.Users.Add(user);
                    await context.SaveChangesAsync();

                    context.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = parentRoleId, AssignedAt = DateTime.UtcNow });
                    var guardian = new Guardian
                    {
                        UserId = user.Id,
                        FullName = fatherFullName,
                        PhoneNumber = phone,
                        Email = email,
                        IdentityCardNumber = cccd,
                        Occupation = occ,
                        Address = addr,
                        CreatedAt = DateTime.UtcNow
                    };
                    context.Guardians.Add(guardian);
                    await context.SaveChangesAsync();

                    context.StudentGuardians.Add(new StudentGuardian
                    {
                        StudentId = st.Id,
                        GuardianId = guardian.Id,
                        Relationship = RelationshipTypes.Father,
                        IsPrimaryContact = true
                    });
                    await context.SaveChangesAsync();
                }
            }

            // 2. Phụ huynh liên hệ khẩn cấp (Mẹ/Bố)
            if (!st.StudentGuardians.Any(sg => !sg.IsPrimaryContact))
            {
                var username2 = $"ph2_{codeLower}";
                var email2 = $"emergency_{codeLower}@sawp.edu.vn";
                var motherFullName = motherNamesList[idx % motherNamesList.Length];
                var phone2 = $"098{(234500 + idx * 89):D7}";
                var cccd2 = $"001090{(100001 + idx * 2):D6}";
                var occ2 = occupations2[(idx + 3) % occupations2.Length];
                var addr = $"{12 + idx} Đường số {1 + (idx % 12)}, {st.ResidentialArea ?? "Phường Bến Nghé"}, TP. Hồ Chí Minh";

                var user2 = await context.Users.FirstOrDefaultAsync(u => u.Username == username2);
                if (user2 == null)
                {
                    user2 = new User
                    {
                        Username = username2,
                        Email = email2,
                        PasswordHash = defaultPasswordHash,
                        FullName = motherFullName,
                        PhoneNumber = phone2,
                        Status = AccountStatus.Active,
                        CreatedAt = DateTime.UtcNow
                    };
                    context.Users.Add(user2);
                    await context.SaveChangesAsync();

                    context.UserRoles.Add(new UserRole { UserId = user2.Id, RoleId = parentRoleId, AssignedAt = DateTime.UtcNow });
                    var guardian2 = new Guardian
                    {
                        UserId = user2.Id,
                        FullName = motherFullName,
                        PhoneNumber = phone2,
                        Email = email2,
                        IdentityCardNumber = cccd2,
                        Occupation = occ2,
                        Address = addr,
                        CreatedAt = DateTime.UtcNow
                    };
                    context.Guardians.Add(guardian2);
                    await context.SaveChangesAsync();

                    context.StudentGuardians.Add(new StudentGuardian
                    {
                        StudentId = st.Id,
                        GuardianId = guardian2.Id,
                        Relationship = RelationshipTypes.Mother,
                        IsPrimaryContact = false
                    });
                    await context.SaveChangesAsync();
                }
            }
        }

        // ====================================================================
        // 7. MẪU ĐƠN XIN NGHỈ PHÉP (Dữ liệu thật đến hôm nay - idempotent)
        // ====================================================================
        if (parentUser != null)
        {
            DateTime GetNearestSchoolDay(DateTime start, int step)
            {
                var d = start.Date;
                while (d.DayOfWeek == DayOfWeek.Saturday || d.DayOfWeek == DayOfWeek.Sunday)
                {
                    d = d.AddDays(step);
                }
                return d;
            }

            var lastSchoolDay = GetNearestSchoolDay(today, -1);
            var prevSchoolDay = GetNearestSchoolDay(lastSchoolDay.AddDays(-1), -1);
            var nextSchoolDay = GetNearestSchoolDay(today.AddDays(1), 1);
            var nextSchoolDay2 = GetNearestSchoolDay(nextSchoolDay.AddDays(1), 1);
            var nextSchoolDay3 = GetNearestSchoolDay(nextSchoolDay2.AddDays(1), 1);

            var leaveSamples = new (string ReqNo, string StudentCode, RequestStatus Status, DateTime Start, DateTime End, LeaveSession Session, bool KeepLunch, bool IsLunchCancelled, string Reason, int? ReviewerId, string? ReviewerNotes)[]
            {
                ("DON-2026-001", "HS001", RequestStatus.Approved, prevSchoolDay, prevSchoolDay, LeaveSession.FullDay, false, true, "Học sinh bị cảm sốt nhẹ, gia đình xin phép nghỉ 1 ngày theo dõi tại nhà.", teacherUser?.Id, "Đã phê duyệt nghỉ ốm và tự động hủy suất ăn bán trú."),
                ("DON-2026-002", "HS001", RequestStatus.Submitted, nextSchoolDay, nextSchoolDay, LeaveSession.Morning, true, false, "Gia đình đưa con đi khám răng định kỳ buổi sáng, chiều con đi học lại bình thường.", null, null),
                ("DON-2026-003", "HS002", RequestStatus.Approved, prevSchoolDay, lastSchoolDay, LeaveSession.FullDay, false, true, "Học sinh bị viêm họng theo chỉ định bác sĩ, xin nghỉ 2 ngày dưỡng sức.", teacherUser?.Id, "GVCN đã xác nhận và phê duyệt đơn nghỉ."),
                ("DON-2026-004", "HS003", RequestStatus.Submitted, nextSchoolDay, nextSchoolDay3, LeaveSession.FullDay, false, false, "Gia đình có việc hiếu ở quê, xin phép cho con nghỉ 3 ngày học liên tiếp.", null, null),
                ("DON-2026-005", "HS005", RequestStatus.Rejected, lastSchoolDay, lastSchoolDay, LeaveSession.Afternoon, false, false, "Xin nghỉ buổi chiều không nêu rõ lý do.", teacherUser?.Id, "Vui lòng bổ sung lý do cụ thể và liên hệ GVCN trước khi gửi lại.")
            };

            foreach (var sample in leaveSamples)
            {
                var targetStudent = studentEntities.FirstOrDefault(s => s.StudentCode == sample.StudentCode);
                if (targetStudent == null) continue;
                if (await context.AdministrativeRequests.AnyAsync(ar => ar.RequestNumber == sample.ReqNo)) continue;

                var adminReq = new AdministrativeRequest
                {
                    RequestNumber = sample.ReqNo,
                    RequesterUserId = parentUser.Id,
                    StudentId = targetStudent.Id,
                    RequestType = RequestTypes.Leave,
                    Status = sample.Status,
                    SubmissionDate = DateTime.UtcNow.AddDays(-1),
                    ProcessedDate = sample.Status == RequestStatus.Submitted ? null : DateTime.UtcNow.AddHours(-6),
                    ReviewerUserId = sample.ReviewerId,
                    ReviewerNotes = sample.ReviewerNotes
                };
                context.AdministrativeRequests.Add(adminReq);
                await context.SaveChangesAsync();

                context.LeaveRequests.Add(new LeaveRequest
                {
                    AdministrativeRequestId = adminReq.Id,
                    StartDate = sample.Start,
                    EndDate = sample.End,
                    Session = sample.Session,
                    KeepLunchService = sample.KeepLunch,
                    IsLunchCancelled = sample.IsLunchCancelled,
                    Reason = sample.Reason
                });
                await context.SaveChangesAsync();
            }
        }

        // ====================================================================
        // 8. LỊCH SỬ ĐIỂM DANH MẪU (Cho HS001 để Phụ huynh xem lịch tháng)
        // ====================================================================
        if (student1 != null)
        {
            var pastDays = new[]
            {
                (today.AddDays(-4), AttendanceStatus.Present, (string?)null),
                (today.AddDays(-3), AttendanceStatus.Present, (string?)null),
                (today.AddDays(-2), AttendanceStatus.Late, "Đến muộn 15 phút do kẹt xe"),
                (today.AddDays(-1), AttendanceStatus.Present, (string?)null)
            };

            foreach (var (pDate, status, remarks) in pastDays)
            {
                if (pDate.DayOfWeek != DayOfWeek.Saturday && pDate.DayOfWeek != DayOfWeek.Sunday)
                {
                    if (!await context.AttendanceRecords.AnyAsync(ar => ar.StudentId == student1.Id && ar.Date == pDate))
                    {
                        context.AttendanceRecords.Add(new AttendanceRecord
                        {
                            StudentId = student1.Id,
                            ClassId = class1A.Id,
                            Date = pDate,
                            Status = status,
                            Remarks = remarks,
                            RecordedByUserId = teacherUser.Id,
                            CreatedAt = DateTime.UtcNow.AddDays((pDate - today).TotalDays)
                        });
                    }
                }
            }
            await context.SaveChangesAsync();
        }

        // ====================================================================
        // 9. LUNCH SERVICE DAY (Thực đơn từng ngày lưu thật trong DB)
        // ====================================================================
        var weekdayMenus = new[]
        {
            "Cơm trắng dẻo, Thịt gà kho nấm hương, Rau cải ngọt xào tỏi, Canh bí đỏ nấu tôm tươi, Chuối tiêu",
            "Cơm trắng dẻo, Cá basa sốt cà chua, Đậu cove xào thịt băm, Canh cua mồng tơi, Sữa chua",
            "Cơm trắng dẻo, Thịt lợn rim tiêu, Đậu phụ nhồi thịt sốt cà, Canh rau ngót thịt băm, Dưa hấu",
            "Cơm trắng dẻo, Trứng đúc thịt hấp, Su su xào cà rốt, Canh sườn non hầm củ quả, Cam tươi",
            "Cơm trắng dẻo, Bò hầm khoai tây cà rốt, Bắp cải luộc chấm trứng, Canh chua cá lóc, Bánh flan"
        };

        var serviceSeedStart = new DateTime(today.Year, today.Month, 1);
        var serviceSeedEnd = today.AddDays(10);
        for (var sDate = serviceSeedStart; sDate <= serviceSeedEnd; sDate = sDate.AddDays(1))
        {
            if (sDate != today && (sDate.DayOfWeek == DayOfWeek.Saturday || sDate.DayOfWeek == DayOfWeek.Sunday)) continue;

            var menuIdx = Math.Abs((int)sDate.DayOfWeek - 1) % weekdayMenus.Length;
            var menuStr = weekdayMenus[menuIdx];
            bool shouldLockPast = sDate < today.AddDays(-1);

            var existingServiceDay = await context.LunchServiceDays.FirstOrDefaultAsync(ls => ls.Date == sDate);
            if (existingServiceDay == null)
            {
                context.LunchServiceDays.Add(new LunchServiceDay
                {
                    Date = sDate,
                    IsServing = true,
                    CutoffTime = new TimeSpan(8, 15, 0),
                    IsLocked = shouldLockPast,
                    LockedAt = shouldLockPast ? DateTime.SpecifyKind(sDate.AddHours(1).AddMinutes(15), DateTimeKind.Utc) : null,
                    Remarks = menuStr
                });
            }
            else
            {
                if (sDate == today && !existingServiceDay.IsServing)
                {
                    existingServiceDay.IsServing = true;
                }
                if (string.IsNullOrWhiteSpace(existingServiceDay.Remarks) ||
                    existingServiceDay.Remarks == "Phục vụ bán trú bình thường" ||
                    !existingServiceDay.Remarks.Contains(','))
                {
                    existingServiceDay.Remarks = menuStr;
                }
            }
        }
        await context.SaveChangesAsync();

        await SeedLunchMenuCatalogAsync(context);

        // ====================================================================
        // 10. ADMINISTRATIVE REQUESTS SAMPLE SEED (Class Transfer & Documents)
        // ====================================================================
        if (student1 != null && parentUser != null)
        {
            // Sample 1: Đơn xin chuyển lớp cho HS031 (Trần Bảo Ngọc) từ 1B -> 1A (Mới nộp - Submitted)
            var student7Obj = await context.Students.FirstOrDefaultAsync(s => s.StudentCode == "HS031" || s.StudentCode == "HS007");
            if (student7Obj != null && !await context.AdministrativeRequests.AnyAsync(r => r.RequestNumber == "REQ-CT-SAMPLE-001"))
            {
                var ctReq1 = new AdministrativeRequest
                {
                    RequestNumber = "REQ-CT-SAMPLE-001",
                    RequesterUserId = parentUser.Id,
                    StudentId = student7Obj.Id,
                    RequestType = RequestTypes.ClassTransfer,
                    Status = RequestStatus.Submitted,
                    SubmissionDate = DateTime.UtcNow.AddDays(-1),
                    AttachmentUrl = "https://example.com/don-chuyen-lop-gia-dinh.pdf"
                };
                context.AdministrativeRequests.Add(ctReq1);
                await context.SaveChangesAsync();

                context.ClassTransferDetails.Add(new ClassTransferDetail
                {
                    AdministrativeRequestId = ctReq1.Id,
                    FromClassId = class1B.Id,
                    ToClassId = class1A.Id,
                    TransferReason = "Gia đình có nguyện vọng chuyển cháu về lớp 1A để học cùng khối với anh trai HS001 tiện việc đưa đón.",
                    IsExecuted = false
                });
                await context.SaveChangesAsync();
            }

            // Sample 2: Đơn xin cấp Giấy xác nhận cho HS001 (Yêu cầu bổ sung thông tin - Amendment)
            if (!await context.AdministrativeRequests.AnyAsync(r => r.RequestNumber == "REQ-DOC-SAMPLE-002"))
            {
                var docReq2 = new AdministrativeRequest
                {
                    RequestNumber = "REQ-DOC-SAMPLE-002",
                    RequesterUserId = parentUser.Id,
                    StudentId = student1.Id,
                    RequestType = RequestTypes.Document,
                    Status = RequestStatus.Submitted,
                    SubmissionDate = DateTime.UtcNow.AddDays(-2),
                    RequiresAmendment = true,
                    AmendmentNotes = "Vui lòng ghi rõ mục đích nộp cơ quan nào (Bảo hiểm hay cơ quan công tác của phụ huynh) để nhà trường cấp đúng mẫu quy định.",
                    ReviewerUserId = officeUser.Id
                };
                context.AdministrativeRequests.Add(docReq2);
                await context.SaveChangesAsync();

                context.DocumentRequestDetails.Add(new DocumentRequestDetail
                {
                    AdministrativeRequestId = docReq2.Id,
                    DocumentType = "Giấy xác nhận học sinh đang theo học",
                    NumberOfCopies = 2,
                    Purpose = "Bổ sung hồ sơ cơ quan phụ huynh"
                });
                await context.SaveChangesAsync();
            }

            // Sample 3: Đơn xin chuyển lớp đã qua Giáo vụ thẩm định, đang trình BGH (UnderReview)
            if (student7Obj != null && !await context.AdministrativeRequests.AnyAsync(r => r.RequestNumber == "REQ-CT-SAMPLE-003"))
            {
                var ctReq3 = new AdministrativeRequest
                {
                    RequestNumber = "REQ-CT-SAMPLE-003",
                    RequesterUserId = parentUser.Id,
                    StudentId = student7Obj.Id,
                    RequestType = RequestTypes.ClassTransfer,
                    Status = RequestStatus.UnderReview,
                    SubmissionDate = DateTime.UtcNow.AddDays(-3),
                    ReviewerUserId = officeUser.Id
                };
                context.AdministrativeRequests.Add(ctReq3);
                await context.SaveChangesAsync();

                context.ClassTransferDetails.Add(new ClassTransferDetail
                {
                    AdministrativeRequestId = ctReq3.Id,
                    FromClassId = class1A.Id,
                    ToClassId = class1B.Id,
                    TransferReason = "Cháu đổi chỗ ở sang gần khu vực của các bạn lớp 1B, xin chuyển để thuận tiện đi lại.",
                    StaffRecommendation = "Đã kiểm tra sĩ số lớp 1B hiện tại còn 4 chỗ, đủ điều kiện tiếp nhận. Đề xuất BGH xem xét phê duyệt.",
                    TargetClassCapacitySnapshot = 28,
                    IsExecuted = false
                });
                await context.SaveChangesAsync();
            }

            // Sample 4: Đơn giấy tờ đã được BGH duyệt và cấp phát hành chính thức (Approved)
            if (!await context.AdministrativeRequests.AnyAsync(r => r.RequestNumber == "REQ-DOC-SAMPLE-004"))
            {
                var docReq4 = new AdministrativeRequest
                {
                    RequestNumber = "REQ-DOC-SAMPLE-004",
                    RequesterUserId = parentUser.Id,
                    StudentId = student1.Id,
                    RequestType = RequestTypes.Document,
                    Status = RequestStatus.Approved,
                    SubmissionDate = DateTime.UtcNow.AddDays(-5),
                    ProcessedDate = DateTime.UtcNow.AddDays(-4),
                    ReviewerUserId = boardUser.Id,
                    SchoolBoardApprovedAt = DateTime.UtcNow.AddDays(-4),
                    SchoolBoardNotes = "Đồng ý cấp theo đề xuất của bộ phận Giáo vụ."
                };
                context.AdministrativeRequests.Add(docReq4);
                await context.SaveChangesAsync();

                context.DocumentRequestDetails.Add(new DocumentRequestDetail
                {
                    AdministrativeRequestId = docReq4.Id,
                    DocumentType = "Giấy xác nhận học sinh đang theo học",
                    NumberOfCopies = 1,
                    Purpose = "Làm thủ tục xin trợ cấp phúc lợi gia đình",
                    StaffEndorsement = "Hồ sơ đầy đủ, học sinh có hạnh kiểm Tốt.",
                    DocumentContentDraft = $"Trường Tiểu học SAWP xác nhận học sinh Trần Gia Bảo (Mã: HS001) hiện đang học tại Lớp 1A năm học 2026-2027.",
                    CertificateNumber = "SAWP-2026-XN089",
                    DigitalSignatureInfo = "Ký số điện tử bởi Ban Giám Hiệu ngày 22/10/2026",
                    IssuedDate = DateTime.UtcNow.AddDays(-4),
                    IssuedDocumentUrl = $"/api/requests/{docReq4.Id}/download-document"
                });
                await context.SaveChangesAsync();
            }
        }
    }

    private static async Task SeedLunchMenuCatalogAsync(ApplicationDbContext context)
    {
        if (!await context.Dishes.AnyAsync())
        {
            var defaultDishes = new List<Dish>
            {
                // Tinh bột
                new() { Name = "Cơm trắng dẻo", Category = DishCategory.Starch, SuitableMealTypes = "StandardLunch,VegetarianLunch" },
                new() { Name = "Cơm mềm / Cháo dinh dưỡng", Category = DishCategory.Starch, SuitableMealTypes = "SpecialDiet" },
                // Món mặn
                new() { Name = "Thịt gà kho nấm hương", Category = DishCategory.MainDish, SuitableMealTypes = "StandardLunch" },
                new() { Name = "Cá basa sốt cà chua", Category = DishCategory.MainDish, SuitableMealTypes = "StandardLunch", AllergenTags = "seafood" },
                new() { Name = "Thịt lợn rim tiêu", Category = DishCategory.MainDish, SuitableMealTypes = "StandardLunch" },
                new() { Name = "Trứng đúc thịt hấp", Category = DishCategory.MainDish, SuitableMealTypes = "StandardLunch", AllergenTags = "egg" },
                new() { Name = "Bò hầm khoai tây cà rốt", Category = DishCategory.MainDish, SuitableMealTypes = "StandardLunch" },
                new() { Name = "Thịt nạc thăn hấp mềm", Category = DishCategory.MainDish, SuitableMealTypes = "StandardLunch,SpecialDiet" },
                new() { Name = "Đậu phụ sốt cà chua", Category = DishCategory.MainDish, SuitableMealTypes = "VegetarianLunch,StandardLunch", AllergenTags = "soy" },
                new() { Name = "Nấm đùi gà kho xì dầu", Category = DishCategory.MainDish, SuitableMealTypes = "VegetarianLunch", AllergenTags = "soy" },
                // Món rau / phụ
                new() { Name = "Rau cải ngọt xào tỏi", Category = DishCategory.SideDish, SuitableMealTypes = "StandardLunch,VegetarianLunch" },
                new() { Name = "Đậu cove xào thịt băm", Category = DishCategory.SideDish, SuitableMealTypes = "StandardLunch" },
                new() { Name = "Su su xào cà rốt", Category = DishCategory.SideDish, SuitableMealTypes = "StandardLunch,VegetarianLunch" },
                new() { Name = "Rau củ luộc thập cẩm", Category = DishCategory.SideDish, SuitableMealTypes = "StandardLunch,VegetarianLunch,SpecialDiet" },
                // Canh
                new() { Name = "Canh bí đỏ nấu tôm tươi", Category = DishCategory.Soup, SuitableMealTypes = "StandardLunch", AllergenTags = "seafood" },
                new() { Name = "Canh cua mồng tơi", Category = DishCategory.Soup, SuitableMealTypes = "StandardLunch", AllergenTags = "seafood" },
                new() { Name = "Canh rau ngót thịt băm", Category = DishCategory.Soup, SuitableMealTypes = "StandardLunch,SpecialDiet" },
                new() { Name = "Canh sườn non hầm củ quả", Category = DishCategory.Soup, SuitableMealTypes = "StandardLunch" },
                new() { Name = "Canh nấm hạt sen", Category = DishCategory.Soup, SuitableMealTypes = "VegetarianLunch,SpecialDiet" },
                // Tráng miệng
                new() { Name = "Chuối tiêu tráng miệng", Category = DishCategory.Dessert, SuitableMealTypes = "StandardLunch,VegetarianLunch,SpecialDiet" },
                new() { Name = "Sữa chua men sống", Category = DishCategory.Dessert, SuitableMealTypes = "StandardLunch,VegetarianLunch,SpecialDiet", AllergenTags = "dairy" },
                new() { Name = "Dưa hấu tươi", Category = DishCategory.Dessert, SuitableMealTypes = "StandardLunch,VegetarianLunch" },
                new() { Name = "Cam tươi", Category = DishCategory.Dessert, SuitableMealTypes = "StandardLunch,VegetarianLunch,SpecialDiet" },
                new() { Name = "Bánh flan caramel", Category = DishCategory.Dessert, SuitableMealTypes = "StandardLunch", AllergenTags = "egg,dairy" },
            };

            context.Dishes.AddRange(defaultDishes);
            await context.SaveChangesAsync();
        }

        var dishByName = await context.Dishes.ToDictionaryAsync(d => d.Name, d => d.Id);

        if (!await context.WeeklyMenuTemplates.AnyAsync())
        {
            var templateA = new WeeklyMenuTemplate
            {
                Name = "Thực đơn Tuần A – Cân bằng dinh dưỡng",
                Description = "Thực đơn chuẩn 5 ngày Thứ 2 – Thứ 6, đầy đủ 3 chế độ (Tiêu chuẩn, Ăn chay, Đặc biệt).",
                IsActive = true
            };
            var templateB = new WeeklyMenuTemplate
            {
                Name = "Thực đơn Tuần B – Đổi vị nhẹ bụng",
                Description = "Thực đơn tuần luân phiên ưu tiên món hấp, canh thanh mát cho học sinh tiểu học.",
                IsActive = true
            };
            context.WeeklyMenuTemplates.AddRange(templateA, templateB);
            await context.SaveChangesAsync();

            var stdWeekA = new Dictionary<int, string[]>
            {
                [1] = new[] { "Cơm trắng dẻo", "Thịt gà kho nấm hương", "Rau cải ngọt xào tỏi", "Canh bí đỏ nấu tôm tươi", "Chuối tiêu tráng miệng" },
                [2] = new[] { "Cơm trắng dẻo", "Cá basa sốt cà chua", "Đậu cove xào thịt băm", "Canh cua mồng tơi", "Sữa chua men sống" },
                [3] = new[] { "Cơm trắng dẻo", "Thịt lợn rim tiêu", "Đậu phụ sốt cà chua", "Canh rau ngót thịt băm", "Dưa hấu tươi" },
                [4] = new[] { "Cơm trắng dẻo", "Trứng đúc thịt hấp", "Su su xào cà rốt", "Canh sườn non hầm củ quả", "Cam tươi" },
                [5] = new[] { "Cơm trắng dẻo", "Bò hầm khoai tây cà rốt", "Rau củ luộc thập cẩm", "Canh rau ngót thịt băm", "Bánh flan caramel" },
            };

            var vegCommon = new[] { "Cơm trắng dẻo", "Đậu phụ sốt cà chua", "Nấm đùi gà kho xì dầu", "Rau củ luộc thập cẩm", "Canh nấm hạt sen", "Chuối tiêu tráng miệng" };
            var spcCommon = new[] { "Cơm mềm / Cháo dinh dưỡng", "Thịt nạc thăn hấp mềm", "Rau củ luộc thập cẩm", "Canh rau ngót thịt băm", "Cam tươi" };

            void AddDayItems(WeeklyMenuTemplate tpl, int dayOfWeek, MealType mealType, string[] names)
            {
                for (int i = 0; i < names.Length; i++)
                {
                    if (!dishByName.TryGetValue(names[i], out var dishId)) continue;
                    tpl.Items.Add(new WeeklyMenuTemplateItem
                    {
                        DayOfWeek = dayOfWeek,
                        MealType = mealType,
                        DishId = dishId,
                        SortOrder = i + 1
                    });
                }
            }

            foreach (var kv in stdWeekA)
            {
                AddDayItems(templateA, kv.Key, MealType.StandardLunch, kv.Value);
                AddDayItems(templateA, kv.Key, MealType.VegetarianLunch, vegCommon);
                AddDayItems(templateA, kv.Key, MealType.SpecialDiet, spcCommon);

                var shiftedDay = kv.Key == 5 ? 1 : kv.Key + 1;
                AddDayItems(templateB, kv.Key, MealType.StandardLunch, stdWeekA[shiftedDay]);
                AddDayItems(templateB, kv.Key, MealType.VegetarianLunch, vegCommon);
                AddDayItems(templateB, kv.Key, MealType.SpecialDiet, spcCommon);
            }

            await context.SaveChangesAsync();
        }
        else
        {
            var legacyTemplates = await context.WeeklyMenuTemplates
                .Where(t => t.Name.StartsWith("Mẫu Tuần "))
                .ToListAsync();
            if (legacyTemplates.Count > 0)
            {
                foreach (var lt in legacyTemplates)
                {
                    lt.Name = "Thực đơn " + lt.Name["Mẫu ".Length..];
                }
                await context.SaveChangesAsync();
            }
        }

        // Gắn thực đơn chi tiết cho các LunchServiceDay đang phục vụ chưa có món trong DB
        var serviceDaysWithoutDishes = await context.LunchServiceDays
            .Include(d => d.Dishes)
            .Where(d => d.IsServing && !d.Dishes.Any())
            .ToListAsync();

        if (serviceDaysWithoutDishes.Count > 0)
        {
            var templateA = await context.WeeklyMenuTemplates
                .Include(t => t.Items)
                .FirstOrDefaultAsync();
            if (templateA != null)
            {
                foreach (var sDay in serviceDaysWithoutDishes)
                {
                    var dow = sDay.Date.DayOfWeek == DayOfWeek.Sunday ? 7 : (int)sDay.Date.DayOfWeek;
                    var effectiveDow = dow is >= 1 and <= 5 ? dow : 1;
                    var matchingItems = templateA.Items.Where(i => i.DayOfWeek == effectiveDow).ToList();
                    foreach (var item in matchingItems)
                    {
                        context.LunchServiceDayDishes.Add(new LunchServiceDayDish
                        {
                            LunchServiceDayId = sDay.Id,
                            MealType = item.MealType,
                            DishId = item.DishId,
                            SortOrder = item.SortOrder
                        });
                    }
                }
                await context.SaveChangesAsync();
            }
        }
    }
}

using Core.Constants;
using Core.Entities;
using Core.Enums;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Data;

public static class DbInitializer
{
    public static async Task SeedAsync(ApplicationDbContext context)
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
                IsActive = true
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

        // Ngày học hôm nay
        var today = DateTime.Today;
        if (!await context.SchoolCalendarDays.AnyAsync(d => d.Date == today && d.SchoolYearId == schoolYear.Id))
        {
            context.SchoolCalendarDays.Add(new SchoolCalendarDay
            {
                SchoolYearId = schoolYear.Id,
                Date = today,
                IsSchoolDay = true,
                Note = "Ngày học bình thường"
            });
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
                Status = "Active",
                IsPlacementPublished = true
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
                Status = "Active",
                IsPlacementPublished = true
            };
            context.Classes.Add(class1C);
            await context.SaveChangesAsync();
        }

        class1A.IsPlacementPublished = true;
        class1B.IsPlacementPublished = true;
        class1C.IsPlacementPublished = true;
        await context.SaveChangesAsync();

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
            ("HS001", "Trần Gia Bảo", "Nam", new DateTime(2020, 3, 15), "Phường Bến Nghé", false, null),
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

            // Đăng ký suất ăn bán trú trong ngày (UC-29)
            var existingLunch = await context.LunchRegistrations.FirstOrDefaultAsync(lr => lr.StudentId == student.Id && lr.Date == today);
            if (existingLunch == null)
            {
                bool isVegetarian = code is "HS004" or "HS017" or "HS035" or "HS062";
                bool isSpecialDiet = code is "HS006" or "HS040";
                bool isCancelled = code is "HS005" or "HS012" or "HS038" or "HS050";

                context.LunchRegistrations.Add(new LunchRegistration
                {
                    StudentId = student.Id,
                    Date = today,
                    MealType = isVegetarian ? MealType.VegetarianLunch : isSpecialDiet ? MealType.SpecialDiet : MealType.StandardLunch,
                    IsActive = !isCancelled,
                    Remarks = isVegetarian ? "Suất ăn chay dinh dưỡng" : isSpecialDiet ? "Thực đơn dinh dưỡng đặc biệt" : "Suất ăn bán trú tiêu chuẩn",
                    CancelledAt = isCancelled ? today.AddHours(7).AddMinutes(45) : null,
                    CancellationReason = isCancelled ? (code is "HS005" or "HS038" ? "Đơn xin nghỉ ốm đã được GVCN duyệt trước 08h15" : "Phụ huynh hủy trực tiếp trên cổng trước 08h15 (UC-30)") : null
                });
            }
        }
        await context.SaveChangesAsync();

        // Điểm danh chuyên cần toàn diện cho toàn bộ 86 học sinh (1A, 1B, 1C) trong 15 ngày học gần nhất (Ca sáng & Ca chiều)
        var schoolDays = new List<DateTime>();
        var checkDate = today.AddDays(-22);
        while (checkDate <= today)
        {
            if (checkDate.DayOfWeek != DayOfWeek.Saturday && checkDate.DayOfWeek != DayOfWeek.Sunday)
            {
                schoolDays.Add(checkDate);
            }
            checkDate = checkDate.AddDays(1);
        }

        var activeAssignments = await context.ClassAssignments
            .Where(ca => ca.IsActive)
            .Include(ca => ca.Student)
            .ToListAsync();

        for (int dayIdx = 0; dayIdx < schoolDays.Count; dayIdx++)
        {
            var sDate = schoolDays[dayIdx];
            foreach (var session in new[] { AttendanceSession.Morning, AttendanceSession.Afternoon })
            {
                foreach (var ca in activeAssignments)
                {
                    if (!await context.AttendanceRecords.AnyAsync(ar => ar.StudentId == ca.StudentId && ar.Date == sDate && ar.Session == session))
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
                            CreatedAt = sDate.AddHours(session == AttendanceSession.Morning ? 8 : 14)
                        });
                    }
                }
            }
        }
        await context.SaveChangesAsync();

        // Liên kết phụ huynh `parent1` với học sinh `HS001` (Trần Gia Bảo - Lớp 1A)
        var student1 = studentEntities.FirstOrDefault(s => s.StudentCode == "HS001");
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
        // 7. MẪU ĐƠN NGHỈ PHÉP (Đã tắt để người dùng tự tạo đơn từ đầu)
        // ====================================================================
        /*
        var student2 = studentEntities.FirstOrDefault(s => s.StudentCode == "HS002");
        if (student2 != null && !await context.AdministrativeRequests.AnyAsync(ar => ar.StudentId == student2.Id && ar.RequestType == RequestTypes.Leave))
        {
            var adminReq = new AdministrativeRequest
            {
                RequestNumber = "DON-2026-001",
                RequesterUserId = parentUser.Id,
                StudentId = student2.Id,
                RequestType = RequestTypes.Leave,
                Status = RequestStatus.Approved,
                SubmissionDate = DateTime.UtcNow.AddDays(-1),
                ProcessedDate = DateTime.UtcNow.AddHours(-12),
                ReviewerUserId = boardUser.Id,
                ReviewerNotes = "Đã phê duyệt nghỉ ốm có chỉ định của bác sĩ."
            };
            context.AdministrativeRequests.Add(adminReq);
            await context.SaveChangesAsync();

            var leaveReq = new LeaveRequest
            {
                AdministrativeRequestId = adminReq.Id,
                StartDate = today.AddDays(-1),
                EndDate = today.AddDays(1),
                Reason = "Học sinh bị cảm sốt, gia đình xin phép nghỉ dưỡng sức 2 ngày.",
                DoctorNoteAttachmentUrl = "/attachments/giay_kham_benh.pdf"
            };
            context.LeaveRequests.Add(leaveReq);
            await context.SaveChangesAsync();
        }
        */

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
        // 9. LUNCH SERVICE DAY
        // ====================================================================
        if (!await context.LunchServiceDays.AnyAsync(ls => ls.Date == today))
        {
            context.LunchServiceDays.Add(new LunchServiceDay
            {
                Date = today,
                IsServing = true,
                Remarks = "Phục vụ bán trú bình thường"
            });
            await context.SaveChangesAsync();
        }

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
}

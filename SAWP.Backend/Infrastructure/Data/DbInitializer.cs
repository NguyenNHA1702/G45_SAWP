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
                Status = "Active"
            };
            context.Classes.Add(class1B);
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
        // 6. STUDENTS & GUARDIAN LINK & CLASS ASSIGNMENT
        // ====================================================================
        var studentsData = new[]
        {
            ("HS001", "Trần Gia Bảo", "Nam", new DateTime(2020, 3, 15), "Phường Bến Nghé", false, (string?)null),
            ("HS002", "Nguyễn An Nhiên", "Nữ", new DateTime(2020, 5, 20), "Phường Bến Nghé", false, (string?)null),
            ("HS003", "Lê Quang Minh", "Nam", new DateTime(2020, 8, 10), "Phường Đa Kao", true, "Hòa nhập khiếm thính nhẹ"),
            ("HS004", "Phạm Thùy Linh", "Nữ", new DateTime(2020, 2, 14), "Phường Đa Kao", false, (string?)null),
            ("HS005", "Hoàng Đức Anh", "Nam", new DateTime(2020, 11, 25), "Phường Tân Định", false, (string?)null),
            ("HS006", "Vũ Mai Phương", "Nữ", new DateTime(2020, 7, 8), "Phường Tân Định", false, (string?)null),
            ("HS007", "Trần Gia Bảo", "Nam", new DateTime(2020, 9, 12), "Phường Bến Nghé", false, (string?)null), // Trùng họ tên hoàn toàn với HS001
            ("HS008", "Đỗ Bảo Trâm", "Nữ", new DateTime(2020, 4, 18), "Phường Đa Kao", false, (string?)null),
            ("HS009", "Phan Gia Huy", "Nam", new DateTime(2020, 6, 22), "Phường Tân Định", false, (string?)null),
            ("HS010", "Nguyễn Minh Khang", "Nam", new DateTime(2020, 1, 30), "Phường Bến Nghé", true, "Hòa nhập vận động nhẹ"),
            ("HS011", "Trịnh Thảo My", "Nữ", new DateTime(2020, 10, 5), "Phường Bến Nghé", false, (string?)null),
            ("HS012", "Lý Tuấn Kiệt", "Nam", new DateTime(2020, 12, 14), "Phường Đa Kao", false, (string?)null),
            ("HS013", "Bùi Khánh Linh", "Nữ", new DateTime(2020, 3, 28), "Phường Tân Định", false, (string?)null),
            ("HS014", "Nguyễn An Nhiên", "Nữ", new DateTime(2020, 8, 19), "Phường Tân Định", false, (string?)null), // Trùng họ tên hoàn toàn với HS002
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
                // Ensure status is active
                if (student.Status != "Active")
                {
                    student.Status = "Active";
                }

                // Update residential area and special needs if missing
                student.ResidentialArea = area;
                student.IsSpecialNeeds = isSpecial;
                student.SpecialNeedsNotes = specialNotes;
                await context.SaveChangesAsync();
            }
            studentEntities.Add(student);

            // Gán 6 học sinh đầu vào lớp 1A mặc định ban đầu nếu chưa có
            if (code is "HS001" or "HS002" or "HS003" or "HS004" or "HS005" or "HS006")
            {
                if (!await context.ClassAssignments.AnyAsync(ca => ca.ClassId == class1A.Id && ca.StudentId == student.Id))
                {
                    context.ClassAssignments.Add(new ClassAssignment
                    {
                        ClassId = class1A.Id,
                        StudentId = student.Id,
                        AcademicYear = "2026-2027",
                        StartDate = new DateTime(2026, 9, 1),
                        IsActive = true
                    });
                }
            }

            // Đăng ký suất ăn bán trú trong ngày
            if (!await context.LunchRegistrations.AnyAsync(lr => lr.StudentId == student.Id && lr.Date == today))
            {
                context.LunchRegistrations.Add(new LunchRegistration
                {
                    StudentId = student.Id,
                    Date = today,
                    MealType = code == "HS004" ? MealType.VegetarianLunch : MealType.StandardLunch,
                    IsActive = true,
                    Remarks = "Suất ăn bán trú tiêu chuẩn"
                });
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

        // Liên kết con thứ 2 của phụ huynh `parent1`: HS007 (Trần Bảo Ngọc - Lớp 1B)
        if (guardianObj != null)
        {
            var student7 = await context.Students.FirstOrDefaultAsync(s => s.StudentCode == "HS007");
            if (student7 == null)
            {
                student7 = new Student
                {
                    StudentCode = "HS007",
                    FullName = "Trần Bảo Ngọc",
                    Gender = "Female",
                    DateOfBirth = new DateTime(2020, 5, 15),
                    Status = "Active",
                    EnrollmentDate = new DateTime(2026, 9, 1)
                };
                context.Students.Add(student7);
                await context.SaveChangesAsync();

                if (!await context.ClassAssignments.AnyAsync(ca => ca.ClassId == class1B.Id && ca.StudentId == student7.Id))
                {
                    context.ClassAssignments.Add(new ClassAssignment
                    {
                        ClassId = class1B.Id,
                        StudentId = student7.Id,
                        AcademicYear = "2026-2027",
                        StartDate = new DateTime(2026, 9, 1),
                        IsActive = true
                    });
                    await context.SaveChangesAsync();
                }
            }

            if (!await context.StudentGuardians.AnyAsync(sg => sg.StudentId == student7.Id && sg.GuardianId == guardianObj.Id))
            {
                context.StudentGuardians.Add(new StudentGuardian
                {
                    StudentId = student7.Id,
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
            // Sample 1: Đơn xin chuyển lớp cho HS007 (Trần Bảo Ngọc) từ 1B -> 1A (Mới nộp - Submitted)
            var student7Obj = await context.Students.FirstOrDefaultAsync(s => s.StudentCode == "HS007");
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

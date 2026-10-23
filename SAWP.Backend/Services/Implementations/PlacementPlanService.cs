using Core.Constants;
using Core.DTOs;
using Core.Entities;
using Core.Enums;
using Core.Interfaces;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Services.Interfaces;

namespace Services.Implementations;

public class PlacementPlanService : IPlacementPlanService
{
    private readonly ApplicationDbContext _context;
    private readonly IAuditService _auditService;

    public PlacementPlanService(ApplicationDbContext context, IAuditService auditService)
    {
        _context = context;
        _auditService = auditService;
    }

    public async Task<ApiResponse<PlacementPlanDetailDto>> GenerateDraftPlanAsync(
        GeneratePlacementPlanRequestDto request,
        int actorUserId,
        CancellationToken cancellationToken = default)
    {
        // 1. Validate School Year
        var schoolYear = await _context.SchoolYears.FirstOrDefaultAsync(sy => sy.Id == request.SchoolYearId, cancellationToken);
        if (schoolYear == null)
        {
            return ApiResponse<PlacementPlanDetailDto>.Fail(SystemMessages.MSG01, new List<string> { "Năm học không tồn tại." });
        }

        // 2. Fetch target classes
        List<Class> targetClasses;
        if (request.TargetClassIds != null && request.TargetClassIds.Any())
        {
            targetClasses = await _context.Classes
                .Where(c => request.TargetClassIds.Contains(c.Id) && c.SchoolYearId == request.SchoolYearId)
                .OrderBy(c => c.ClassName)
                .ToListAsync(cancellationToken);
        }
        else
        {
            targetClasses = await _context.Classes
                .Where(c => c.SchoolYearId == request.SchoolYearId && c.Grade == request.Grade)
                .OrderBy(c => c.ClassName)
                .ToListAsync(cancellationToken);
        }

        // Auto-create classes seamlessly if none exist and requested
        if (!targetClasses.Any() && request.AutoCreateClassesIfMissing)
        {
            var suffixes = (request.AutoCreateSectionSuffixes != null && request.AutoCreateSectionSuffixes.Any())
                ? request.AutoCreateSectionSuffixes
                : new List<string> { "A", "B", "C", "D", "E" }.Take(Math.Max(1, request.AutoCreateClassCount)).ToList();

            var newClasses = new List<Class>();
            foreach (var suffix in suffixes)
            {
                var s = suffix.Trim().ToUpperInvariant();
                var className = $"{request.Grade}{s}";
                var newClass = new Class
                {
                    SchoolYearId = request.SchoolYearId,
                    ClassName = className,
                    Grade = request.Grade,
                    RoomNumber = $"Phòng {request.Grade}0{s}",
                    Capacity = request.TargetCapacity > 0 ? request.TargetCapacity : 35,
                    Status = "Active",
                    IsPlacementPublished = false
                };
                _context.Classes.Add(newClass);
                newClasses.Add(newClass);
            }

            await _context.SaveChangesAsync(cancellationToken);
            targetClasses = newClasses;
        }

        if (!targetClasses.Any())
        {
            return ApiResponse<PlacementPlanDetailDto>.Fail(
                SystemMessages.MSG01,
                new List<string> { $"Không tìm thấy lớp học nào cho Khối {request.Grade} trong Năm học {schoolYear.YearName}. Vui lòng bật tùy chọn tự động khởi tạo lớp tiếp nhận." }
            );
        }

        // 3. Fetch eligible students for this grade based on age cohort and enrolment status
        // Primary school admission rule (Thông tư 28/2020/TT-BGDĐT):
        // Grade 1 = 6 years old (schoolYear.StartDate.Year - 6, allowing 6-8 for disadvantaged/special needs)
        var startYear = schoolYear.StartDate.Year > 2000 ? schoolYear.StartDate.Year : DateTime.UtcNow.Year;
        var expectedBirthYear = startYear - (5 + request.Grade);
        var targetClassIds = targetClasses.Select(c => c.Id).ToList();

        var query = _context.Students
            .Include(s => s.ClassAssignments)
                .ThenInclude(ca => ca.Class)
            .Where(s => s.Status == "Active" &&
                (s.ClassAssignments.Any(ca => ca.IsActive && targetClassIds.Contains(ca.ClassId)) ||
                 !s.ClassAssignments.Any(ca => ca.IsActive && ca.Class.SchoolYearId == request.SchoolYearId)));

        var candidateStudents = await query.ToListAsync(cancellationToken);

        List<Student> students;
        if (request.Grade == 1)
        {
            // Grade 1 pool: prioritize students of age 6 to 8 (born within startYear - 8 to startYear - 5)
            var grade1Cohort = candidateStudents
                .Where(s => s.DateOfBirth.Year <= startYear - 5 && s.DateOfBirth.Year >= startYear - 9)
                .ToList();

            students = grade1Cohort.Any() ? grade1Cohort : candidateStudents;
        }
        else
        {
            // Grade 2-5 pool: prioritize students who completed prior grade (request.Grade - 1) or matching birth year
            var prevGrade = request.Grade - 1;
            var promotedCohort = candidateStudents
                .Where(s => s.ClassAssignments.Any(ca => ca.Class.Grade == prevGrade) ||
                            (s.DateOfBirth.Year <= expectedBirthYear + 1 && s.DateOfBirth.Year >= expectedBirthYear - 2))
                .ToList();

            students = promotedCohort.Any() ? promotedCohort : candidateStudents;
        }

        // If no unassigned students found, fallback to any active students up to target capacity
        if (!students.Any())
        {
            students = await _context.Students
                .Include(s => s.ClassAssignments)
                .Where(s => s.Status == "Active")
                .Take(targetClasses.Count * request.TargetCapacity)
                .ToListAsync(cancellationToken);
        }

        if (!students.Any())
        {
            return ApiResponse<PlacementPlanDetailDto>.Fail(SystemMessages.MSG01, new List<string> { "Không có học sinh nào khả dụng để xếp lớp cho khối này." });
        }

        // 4. Calculate Version
        var previousCount = await _context.PlacementPlans
            .CountAsync(p => p.SchoolYearId == request.SchoolYearId && p.Grade == request.Grade, cancellationToken);
        var version = previousCount + 1;

        var planName = !string.IsNullOrWhiteSpace(request.PlanName)
            ? request.PlanName.Trim()
            : $"Phương án xếp lớp Khối {request.Grade} - Đợt {version}";

        var plan = new PlacementPlan
        {
            SchoolYearId = request.SchoolYearId,
            Grade = request.Grade,
            PlanName = planName,
            Version = version,
            TargetClassCount = targetClasses.Count,
            TargetCapacity = request.TargetCapacity,
            Status = "Draft",
            CreatedByUserId = actorUserId,
            CreatedAt = DateTime.UtcNow
        };

        _context.PlacementPlans.Add(plan);
        await _context.SaveChangesAsync(cancellationToken);

        // 5. Execute Smart Placement Algorithm
        var assignments = RunPlacementAlgorithm(students, targetClasses, request);

        foreach (var (studentId, classId) in assignments)
        {
            plan.Items.Add(new PlacementPlanItem
            {
                PlacementPlanId = plan.Id,
                StudentId = studentId,
                ClassId = classId,
                IsManualOverride = false
            });
        }

        await _context.SaveChangesAsync(cancellationToken);

        await _auditService.LogActionAsync(
            actorUserId,
            "GeneratePlacementPlanDraft",
            nameof(PlacementPlan),
            plan.Id.ToString(),
            null,
            $"Generated draft placement plan '{plan.PlanName}' (v{plan.Version}) with {assignments.Count} students across {targetClasses.Count} classes",
            null,
            cancellationToken);

        return await GetPlanByIdAsync(plan.Id, cancellationToken);
    }

    public async Task<ApiResponse<PlacementPlanDetailDto>> GetPlanByIdAsync(int planId, CancellationToken cancellationToken = default)
    {
        var plan = await _context.PlacementPlans
            .Include(p => p.SchoolYear)
            .Include(p => p.CreatedByUser)
            .Include(p => p.ReviewedByUser)
            .Include(p => p.Items)
                .ThenInclude(i => i.Student)
            .Include(p => p.Items)
                .ThenInclude(i => i.Class)
            .FirstOrDefaultAsync(p => p.Id == planId, cancellationToken);

        if (plan == null)
        {
            return ApiResponse<PlacementPlanDetailDto>.Fail(SystemMessages.MSG01, new List<string> { "Không tìm thấy phương án xếp lớp." });
        }

        var classesInGrade = await _context.Classes
            .Where(c => c.SchoolYearId == plan.SchoolYearId && c.Grade == plan.Grade)
            .OrderBy(c => c.ClassName)
            .ToListAsync(cancellationToken);

        // Group items by class
        var classPreviewList = new List<PlacementPlanClassPreviewDto>();
        var itemsByClass = plan.Items.GroupBy(i => i.ClassId).ToDictionary(g => g.Key, g => g.ToList());

        foreach (var cls in classesInGrade)
        {
            var items = itemsByClass.ContainsKey(cls.Id) ? itemsByClass[cls.Id] : new List<PlacementPlanItem>();
            var studentList = items.Select(i => new PlacementStudentItemDto
            {
                StudentId = i.Student.Id,
                StudentCode = i.Student.StudentCode,
                FullName = i.Student.FullName,
                Gender = i.Student.Gender,
                DateOfBirth = i.Student.DateOfBirth,
                ResidentialArea = i.Student.ResidentialArea,
                IsSpecialNeeds = i.Student.IsSpecialNeeds,
                SpecialNeedsNotes = i.Student.SpecialNeedsNotes,
                IsManualOverride = i.IsManualOverride,
                Notes = i.Notes
            }).OrderBy(s => s.FullName).ToList();

            var maleCount = studentList.Count(s => s.Gender.Equals("Nam", StringComparison.OrdinalIgnoreCase));
            var femaleCount = studentList.Count(s => s.Gender.Equals("Nữ", StringComparison.OrdinalIgnoreCase));
            var specialNeedsCount = studentList.Count(s => s.IsSpecialNeeds);

            var areaCounts = studentList
                .GroupBy(s => string.IsNullOrWhiteSpace(s.ResidentialArea) ? "Chưa xác định" : s.ResidentialArea.Trim())
                .ToDictionary(g => g.Key, g => g.Count());

            classPreviewList.Add(new PlacementPlanClassPreviewDto
            {
                ClassId = cls.Id,
                ClassName = cls.ClassName,
                Grade = cls.Grade,
                Capacity = cls.Capacity,
                TotalStudents = studentList.Count,
                MaleCount = maleCount,
                FemaleCount = femaleCount,
                SpecialNeedsCount = specialNeedsCount,
                ResidentialAreaCounts = areaCounts,
                Students = studentList
            });
        }

        // Validate criteria and evaluate warnings
        var criteriaWarnings = EvaluateCriteriaWarnings(classPreviewList, plan.TargetCapacity);

        var detailDto = new PlacementPlanDetailDto
        {
            Id = plan.Id,
            SchoolYearId = plan.SchoolYearId,
            SchoolYearName = plan.SchoolYear?.YearName ?? string.Empty,
            Grade = plan.Grade,
            PlanName = plan.PlanName,
            Version = plan.Version,
            Status = plan.Status,
            TargetClassCount = plan.TargetClassCount,
            TargetCapacity = plan.TargetCapacity,
            TotalStudents = plan.Items.Count,
            CreatedByUserId = plan.CreatedByUserId,
            CreatedByName = plan.CreatedByUser?.FullName ?? string.Empty,
            CreatedAt = plan.CreatedAt,
            SubmittedAt = plan.SubmittedAt,
            ReviewedByUserId = plan.ReviewedByUserId,
            ReviewedByName = plan.ReviewedByUser?.FullName,
            ReviewedAt = plan.ReviewedAt,
            ReviewNotes = plan.ReviewNotes,
            HasWarnings = criteriaWarnings.Any(w => w.Severity == "Warning" || w.Severity == "Violation"),
            ViolationCount = criteriaWarnings.Count(w => w.Severity == "Violation"),
            Classes = classPreviewList,
            CriteriaWarnings = criteriaWarnings
        };

        return ApiResponse<PlacementPlanDetailDto>.Ok(detailDto);
    }

    public async Task<ApiResponse<List<PlacementPlanSummaryDto>>> GetPlanHistoryAsync(
        int? schoolYearId = null,
        int? grade = null,
        CancellationToken cancellationToken = default)
    {
        var query = _context.PlacementPlans
            .Include(p => p.SchoolYear)
            .Include(p => p.CreatedByUser)
            .Include(p => p.ReviewedByUser)
            .Include(p => p.Items)
                .ThenInclude(i => i.Student)
            .AsQueryable();

        if (schoolYearId.HasValue)
        {
            query = query.Where(p => p.SchoolYearId == schoolYearId.Value);
        }

        if (grade.HasValue)
        {
            query = query.Where(p => p.Grade == grade.Value);
        }

        var plans = await query
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync(cancellationToken);

        var result = plans.Select(p =>
        {
            // Simple check for warnings
            var itemsByClass = p.Items.GroupBy(i => i.ClassId).ToList();
            var hasSpecialNeedsViolation = itemsByClass.Any(g => g.Count(i => i.Student.IsSpecialNeeds) > 2);
            var hasDuplicateName = itemsByClass.Any(g => g.GroupBy(i => i.Student.FullName.Trim().ToLower()).Any(ng => ng.Count() > 1));

            return new PlacementPlanSummaryDto
            {
                Id = p.Id,
                SchoolYearId = p.SchoolYearId,
                SchoolYearName = p.SchoolYear?.YearName ?? string.Empty,
                Grade = p.Grade,
                PlanName = p.PlanName,
                Version = p.Version,
                Status = p.Status,
                TargetClassCount = p.TargetClassCount,
                TargetCapacity = p.TargetCapacity,
                TotalStudents = p.Items.Count,
                CreatedByUserId = p.CreatedByUserId,
                CreatedByName = p.CreatedByUser?.FullName ?? string.Empty,
                CreatedAt = p.CreatedAt,
                SubmittedAt = p.SubmittedAt,
                ReviewedByUserId = p.ReviewedByUserId,
                ReviewedByName = p.ReviewedByUser?.FullName,
                ReviewedAt = p.ReviewedAt,
                ReviewNotes = p.ReviewNotes,
                HasWarnings = hasSpecialNeedsViolation || hasDuplicateName,
                ViolationCount = (hasSpecialNeedsViolation ? 1 : 0) + (hasDuplicateName ? 1 : 0)
            };
        }).ToList();

        return ApiResponse<List<PlacementPlanSummaryDto>>.Ok(result);
    }

    public async Task<ApiResponse<PlacementPlanDetailDto>> MoveStudentAsync(
        int planId,
        MoveStudentInPlanDto request,
        int actorUserId,
        CancellationToken cancellationToken = default)
    {
        var plan = await _context.PlacementPlans
            .Include(p => p.Items)
            .FirstOrDefaultAsync(p => p.Id == planId, cancellationToken);

        if (plan == null)
        {
            return ApiResponse<PlacementPlanDetailDto>.Fail(SystemMessages.MSG01, new List<string> { "Không tìm thấy phương án xếp lớp." });
        }

        if (plan.Status == "Approved")
        {
            return ApiResponse<PlacementPlanDetailDto>.Fail(SystemMessages.MSG01, new List<string> { "Phương án đã được Ban Giám hiệu phê duyệt, không thể chỉnh sửa." });
        }

        var item = plan.Items.FirstOrDefault(i => i.StudentId == request.StudentId);
        if (item == null)
        {
            return ApiResponse<PlacementPlanDetailDto>.Fail(SystemMessages.MSG01, new List<string> { "Không tìm thấy học sinh trong phương án này." });
        }

        var targetClass = await _context.Classes.FirstOrDefaultAsync(c => c.Id == request.ToClassId, cancellationToken);
        if (targetClass == null)
        {
            return ApiResponse<PlacementPlanDetailDto>.Fail(SystemMessages.MSG01, new List<string> { "Lớp chuyển đến không tồn tại." });
        }

        item.ClassId = request.ToClassId;
        item.IsManualOverride = true;
        item.Notes = request.Notes ?? "Điều chỉnh chuyển lớp thủ công";

        await _context.SaveChangesAsync(cancellationToken);

        await _auditService.LogActionAsync(
            actorUserId,
            "MoveStudentInPlan",
            nameof(PlacementPlan),
            plan.Id.ToString(),
            null,
            $"Moved student {request.StudentId} to class {targetClass.ClassName} in plan {plan.PlanName}",
            null,
            cancellationToken);

        return await GetPlanByIdAsync(planId, cancellationToken);
    }

    public async Task<ApiResponse<PlacementPlanDetailDto>> SwapStudentsAsync(
        int planId,
        SwapStudentsInPlanDto request,
        int actorUserId,
        CancellationToken cancellationToken = default)
    {
        var plan = await _context.PlacementPlans
            .Include(p => p.Items)
            .FirstOrDefaultAsync(p => p.Id == planId, cancellationToken);

        if (plan == null)
        {
            return ApiResponse<PlacementPlanDetailDto>.Fail(SystemMessages.MSG01, new List<string> { "Không tìm thấy phương án xếp lớp." });
        }

        if (plan.Status == "Approved")
        {
            return ApiResponse<PlacementPlanDetailDto>.Fail(SystemMessages.MSG01, new List<string> { "Phương án đã được Ban Giám hiệu phê duyệt, không thể chỉnh sửa." });
        }

        var item1 = plan.Items.FirstOrDefault(i => i.StudentId == request.Student1Id);
        var item2 = plan.Items.FirstOrDefault(i => i.StudentId == request.Student2Id);

        if (item1 == null || item2 == null)
        {
            return ApiResponse<PlacementPlanDetailDto>.Fail(SystemMessages.MSG01, new List<string> { "Không tìm thấy thông tin một trong hai học sinh cần đổi chỗ." });
        }

        item1.ClassId = request.Class2Id;
        item1.IsManualOverride = true;
        item1.Notes = request.Notes ?? "Đổi chỗ học sinh thủ công";

        item2.ClassId = request.Class1Id;
        item2.IsManualOverride = true;
        item2.Notes = request.Notes ?? "Đổi chỗ học sinh thủ công";

        await _context.SaveChangesAsync(cancellationToken);

        await _auditService.LogActionAsync(
            actorUserId,
            "SwapStudentsInPlan",
            nameof(PlacementPlan),
            plan.Id.ToString(),
            null,
            $"Swapped student {request.Student1Id} and {request.Student2Id} in plan {plan.PlanName}",
            null,
            cancellationToken);

        return await GetPlanByIdAsync(planId, cancellationToken);
    }

    public async Task<ApiResponse<PlacementPlanSummaryDto>> SubmitPlanAsync(
        int planId,
        int actorUserId,
        CancellationToken cancellationToken = default)
    {
        var plan = await _context.PlacementPlans
            .Include(p => p.SchoolYear)
            .Include(p => p.CreatedByUser)
            .Include(p => p.Items)
            .FirstOrDefaultAsync(p => p.Id == planId, cancellationToken);

        if (plan == null)
        {
            return ApiResponse<PlacementPlanSummaryDto>.Fail(SystemMessages.MSG01, new List<string> { "Không tìm thấy phương án xếp lớp." });
        }

        if (plan.Status == "Approved")
        {
            return ApiResponse<PlacementPlanSummaryDto>.Fail(SystemMessages.MSG01, new List<string> { "Phương án đã được phê duyệt trước đó." });
        }

        plan.Status = "Submitted";
        plan.SubmittedAt = DateTime.UtcNow;

        // Send notification to SchoolBoard members
        var boardUsers = await _context.UserRoles
            .Where(ur => ur.Role.Name == UserRoles.SchoolBoard.ToString())
            .Select(ur => ur.UserId)
            .ToListAsync(cancellationToken);

        foreach (var bUserId in boardUsers)
        {
            _context.Notifications.Add(new Notification
            {
                UserId = bUserId,
                Title = "Phương án xếp lớp cần phê duyệt",
                Content = $"Giáo vụ đã trình phương án '{plan.PlanName}' (Khối {plan.Grade}, {plan.Items.Count} học sinh) để Ban Giám hiệu xem xét và phê duyệt.",
                LinkUrl = $"/classes?tab=placement&planId={plan.Id}",
                CreatedAt = DateTime.UtcNow,
                IsRead = false
            });
        }

        await _context.SaveChangesAsync(cancellationToken);

        await _auditService.LogActionAsync(
            actorUserId,
            "SubmitPlacementPlan",
            nameof(PlacementPlan),
            plan.Id.ToString(),
            null,
            $"Submitted placement plan {plan.PlanName} for School Board approval",
            null,
            cancellationToken);

        return ApiResponse<PlacementPlanSummaryDto>.Ok(new PlacementPlanSummaryDto
        {
            Id = plan.Id,
            SchoolYearId = plan.SchoolYearId,
            SchoolYearName = plan.SchoolYear?.YearName ?? string.Empty,
            Grade = plan.Grade,
            PlanName = plan.PlanName,
            Version = plan.Version,
            Status = plan.Status,
            TargetClassCount = plan.TargetClassCount,
            TargetCapacity = plan.TargetCapacity,
            TotalStudents = plan.Items.Count,
            CreatedByUserId = plan.CreatedByUserId,
            CreatedByName = plan.CreatedByUser?.FullName ?? string.Empty,
            CreatedAt = plan.CreatedAt,
            SubmittedAt = plan.SubmittedAt
        }, SystemMessages.MSG08);
    }

    public async Task<ApiResponse<PlacementPlanSummaryDto>> ReviewPlanAsync(
        int planId,
        ReviewPlacementPlanDto request,
        int reviewerUserId,
        CancellationToken cancellationToken = default)
    {
        var plan = await _context.PlacementPlans
            .Include(p => p.SchoolYear)
            .Include(p => p.CreatedByUser)
            .Include(p => p.Items)
                .ThenInclude(i => i.Class)
            .FirstOrDefaultAsync(p => p.Id == planId, cancellationToken);

        if (plan == null)
        {
            return ApiResponse<PlacementPlanSummaryDto>.Fail(SystemMessages.MSG01, new List<string> { "Không tìm thấy phương án xếp lớp." });
        }

        if (plan.Status == "Approved")
        {
            return ApiResponse<PlacementPlanSummaryDto>.Fail(SystemMessages.MSG01, new List<string> { "Phương án này đã được phê duyệt trước đó." });
        }

        var reviewer = await _context.Users.FindAsync(new object[] { reviewerUserId }, cancellationToken);

        using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            if (request.IsApproved)
            {
                plan.Status = "Approved";
                plan.ReviewedByUserId = reviewerUserId;
                plan.ReviewedAt = DateTime.UtcNow;
                plan.ReviewNotes = request.ReviewNotes ?? "Phê duyệt phương án xếp lớp chính thức.";

                // Commit: ONLY after approval, establish official ClassAssignments
                var academicYear = plan.SchoolYear?.YearName ?? DateTime.UtcNow.Year.ToString();
                var planStudentIds = plan.Items.Select(i => i.StudentId).ToList();

                // Deactivate any existing active assignments for these students in this school year
                var existingActiveAssignments = await _context.ClassAssignments
                    .Where(ca => ca.IsActive && ca.Class.SchoolYearId == plan.SchoolYearId && planStudentIds.Contains(ca.StudentId))
                    .ToListAsync(cancellationToken);

                foreach (var oldCa in existingActiveAssignments)
                {
                    oldCa.IsActive = false;
                    oldCa.EndDate = DateTime.UtcNow;
                }

                // Create official ClassAssignment records for all items in the plan
                foreach (var item in plan.Items)
                {
                    _context.ClassAssignments.Add(new ClassAssignment
                    {
                        ClassId = item.ClassId,
                        StudentId = item.StudentId,
                        AcademicYear = academicYear,
                        StartDate = DateTime.UtcNow,
                        IsActive = true
                    });
                }

                // Publish classes and notify homeroom teachers
                var distinctClassIds = plan.Items.Select(i => i.ClassId).Distinct().ToList();
                var classes = await _context.Classes
                    .Include(c => c.HomeroomTeacher)
                    .Where(c => distinctClassIds.Contains(c.Id))
                    .ToListAsync(cancellationToken);

                foreach (var cls in classes)
                {
                    cls.IsPlacementPublished = true;
                    cls.Status = "Active";

                    if (cls.HomeroomTeacher != null)
                    {
                        _context.Notifications.Add(new Notification
                        {
                            UserId = cls.HomeroomTeacher.UserId,
                            Title = "Danh sách lớp chính thức đã được phê duyệt",
                            Content = $"Ban Giám hiệu đã phê duyệt phương án xếp lớp {cls.ClassName} năm học {academicYear}. Thầy/Cô đã có thể xem danh sách lớp chính thức.",
                            LinkUrl = $"/teacher/classes/{cls.Id}/roster",
                            CreatedAt = DateTime.UtcNow,
                            IsRead = false
                        });
                    }
                }

                // Notify creator (Office Staff)
                _context.Notifications.Add(new Notification
                {
                    UserId = plan.CreatedByUserId,
                    Title = "Phương án xếp lớp đã được phê duyệt",
                    Content = $"Hiệu trưởng ({reviewer?.FullName}) đã phê duyệt phương án '{plan.PlanName}'. Danh sách phân lớp chính thức đã được thiết lập thành công.",
                    LinkUrl = $"/classes?tab=placement&planId={plan.Id}",
                    CreatedAt = DateTime.UtcNow,
                    IsRead = false
                });
            }
            else
            {
                plan.Status = "Rejected";
                plan.ReviewedByUserId = reviewerUserId;
                plan.ReviewedAt = DateTime.UtcNow;
                plan.ReviewNotes = request.ReviewNotes ?? "Yêu cầu Giáo vụ điều chỉnh lại phương án xếp lớp.";

                // Notify creator (Office Staff)
                _context.Notifications.Add(new Notification
                {
                    UserId = plan.CreatedByUserId,
                    Title = "Phương án xếp lớp được trả lại để điều chỉnh",
                    Content = $"Hiệu trưởng ({reviewer?.FullName}) đã trả lại phương án '{plan.PlanName}'. Lý do: {plan.ReviewNotes}",
                    LinkUrl = $"/classes?tab=placement&planId={plan.Id}",
                    CreatedAt = DateTime.UtcNow,
                    IsRead = false
                });
            }

            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            await _auditService.LogActionAsync(
                reviewerUserId,
                request.IsApproved ? "ApprovePlacementPlan" : "RejectPlacementPlan",
                nameof(PlacementPlan),
                plan.Id.ToString(),
                null,
                $"School Board review for plan {plan.PlanName}: {(request.IsApproved ? "Approved" : "Rejected")}. Notes: {plan.ReviewNotes}",
                null,
                cancellationToken);

            return ApiResponse<PlacementPlanSummaryDto>.Ok(new PlacementPlanSummaryDto
            {
                Id = plan.Id,
                SchoolYearId = plan.SchoolYearId,
                SchoolYearName = plan.SchoolYear?.YearName ?? string.Empty,
                Grade = plan.Grade,
                PlanName = plan.PlanName,
                Version = plan.Version,
                Status = plan.Status,
                TargetClassCount = plan.TargetClassCount,
                TargetCapacity = plan.TargetCapacity,
                TotalStudents = plan.Items.Count,
                CreatedByUserId = plan.CreatedByUserId,
                CreatedByName = plan.CreatedByUser?.FullName ?? string.Empty,
                CreatedAt = plan.CreatedAt,
                SubmittedAt = plan.SubmittedAt,
                ReviewedByUserId = plan.ReviewedByUserId,
                ReviewedByName = reviewer?.FullName,
                ReviewedAt = plan.ReviewedAt,
                ReviewNotes = plan.ReviewNotes
            }, SystemMessages.MSG08);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            return ApiResponse<PlacementPlanSummaryDto>.Fail(SystemMessages.MSG01, new List<string> { $"Lỗi khi phê duyệt phương án: {ex.Message}" });
        }
    }

    #region Algorithmic Placement Logic

    private static List<(int StudentId, int ClassId)> RunPlacementAlgorithm(
        List<Student> students,
        List<Class> classes,
        GeneratePlacementPlanRequestDto options)
    {
        var classIds = classes.Select(c => c.Id).ToList();
        var assignments = new List<(int StudentId, int ClassId)>();
        var classStudentMap = classIds.ToDictionary(id => id, _ => new List<Student>());

        // 1. Separate Special Needs students (đối tượng cần hỗ trợ / khuyết tật hòa nhập)
        var specialNeedsStudents = students.Where(s => s.IsSpecialNeeds).ToList();
        var nonSpecialStudents = students.Where(s => !s.IsSpecialNeeds).ToList();

        // Distribute Special Needs students (max 2 / class rule)
        // Sort or shuffle slightly, then round-robin across classes to ensure <= 2 per class
        int specialIndex = 0;
        foreach (var student in specialNeedsStudents)
        {
            // Pick class with lowest special needs count so far
            var targetClassId = classIds
                .OrderBy(cid => classStudentMap[cid].Count(s => s.IsSpecialNeeds))
                .ThenBy(cid => classStudentMap[cid].Count)
                .First();

            classStudentMap[targetClassId].Add(student);
            assignments.Add((student.Id, targetClassId));
            specialIndex++;
        }

        // 2. Identify students with identical full names (tránh học sinh trùng họ tên hoàn toàn)
        var duplicateNameGroups = nonSpecialStudents
            .GroupBy(s => s.FullName.Trim().ToLowerInvariant())
            .Where(g => g.Count() > 1)
            .ToList();

        var duplicateStudentIds = duplicateNameGroups.SelectMany(g => g.Select(s => s.Id)).ToHashSet();
        var remainingStudents = nonSpecialStudents.Where(s => !duplicateStudentIds.Contains(s.Id)).ToList();

        // Distribute identical name groups across different classes
        foreach (var group in duplicateNameGroups)
        {
            var groupStudents = group.ToList();
            var usedClassesForThisName = new HashSet<int>();

            foreach (var student in groupStudents)
            {
                // Find a class that does NOT already contain this name, and has lowest count
                var candidateClasses = classIds
                    .Where(cid => !usedClassesForThisName.Contains(cid) &&
                                  !classStudentMap[cid].Any(s => s.FullName.Trim().Equals(student.FullName.Trim(), StringComparison.OrdinalIgnoreCase)))
                    .ToList();

                int chosenClassId;
                if (candidateClasses.Any())
                {
                    chosenClassId = candidateClasses
                        .OrderBy(cid => classStudentMap[cid].Count(s => s.Gender == student.Gender))
                        .ThenBy(cid => classStudentMap[cid].Count)
                        .First();
                }
                else
                {
                    // If all classes have this name (e.g. more identical names than classes), pick class with lowest total count
                    chosenClassId = classIds.OrderBy(cid => classStudentMap[cid].Count).First();
                }

                usedClassesForThisName.Add(chosenClassId);
                classStudentMap[chosenClassId].Add(student);
                assignments.Add((student.Id, chosenClassId));
            }
        }

        // 3. Distribute remaining students: Prioritize Residential Area & Balance Gender and Sĩ số
        // Group remaining students by residential area (địa bàn cư trú)
        var areaGroups = remainingStudents
            .GroupBy(s => string.IsNullOrWhiteSpace(s.ResidentialArea) ? "DEFAULT_AREA" : s.ResidentialArea.Trim().ToLowerInvariant())
            .OrderByDescending(g => g.Count())
            .ToList();

        foreach (var areaGroup in areaGroups)
        {
            var boys = areaGroup.Where(s => s.Gender.Equals("Nam", StringComparison.OrdinalIgnoreCase)).ToList();
            var girls = areaGroup.Where(s => !s.Gender.Equals("Nam", StringComparison.OrdinalIgnoreCase)).ToList();

            // Distribute boys of this residential area
            foreach (var boy in boys)
            {
                var chosenClassId = PickBestClassForStudent(boy, classIds, classStudentMap, areaGroup.Key);
                classStudentMap[chosenClassId].Add(boy);
                assignments.Add((boy.Id, chosenClassId));
            }

            // Distribute girls of this residential area
            foreach (var girl in girls)
            {
                var chosenClassId = PickBestClassForStudent(girl, classIds, classStudentMap, areaGroup.Key);
                classStudentMap[chosenClassId].Add(girl);
                assignments.Add((girl.Id, chosenClassId));
            }
        }

        return assignments;
    }

    private static int PickBestClassForStudent(
        Student student,
        List<int> classIds,
        Dictionary<int, List<Student>> classStudentMap,
        string studentAreaKey)
    {
        // Criteria hierarchy:
        // 1. Min effective load across classes (sĩ số thực tế có tính định mức giảm tải cho HS khuyết tật hòa nhập theo TT 03/2018/TT-BGDĐT)
        // Mỗi học sinh hòa nhập tương đương tải trọng thêm 1 HS, giúp lớp hòa nhập có sĩ số thực tế thấp hơn 1-2 em
        int GetEffectiveLoad(int cid) => classStudentMap[cid].Count + classStudentMap[cid].Count(s => s.IsSpecialNeeds);

        var minEffectiveLoad = classIds.Min(GetEffectiveLoad);
        var eligibleClasses = classIds.Where(cid => GetEffectiveLoad(cid) <= minEffectiveLoad + 1).ToList();

        // 2. Among eligible classes, find classes with the fewest students of the same gender (cân bằng nam/nữ)
        var minGenderCount = eligibleClasses.Min(cid => classStudentMap[cid].Count(s => s.Gender == student.Gender));
        var bestGenderClasses = eligibleClasses.Where(cid => classStudentMap[cid].Count(s => s.Gender == student.Gender) == minGenderCount).ToList();

        // 3. Affinity for same residential area (ưu tiên địa bàn cư trú)
        if (studentAreaKey != "DEFAULT_AREA")
        {
            var bestWithArea = bestGenderClasses
                .OrderByDescending(cid => classStudentMap[cid].Count(s => (s.ResidentialArea ?? "").Trim().ToLowerInvariant() == studentAreaKey))
                .ThenBy(GetEffectiveLoad)
                .First();

            return bestWithArea;
        }

        return bestGenderClasses.OrderBy(GetEffectiveLoad).First();
    }

    private static List<PlacementCriteriaWarningDto> EvaluateCriteriaWarnings(
        List<PlacementPlanClassPreviewDto> classes,
        int targetCapacity)
    {
        var warnings = new List<PlacementCriteriaWarningDto>();

        if (!classes.Any())
        {
            return warnings;
        }

        // Rule 1: Special Needs Limit (Tối đa 2 HS khuyết tật hòa nhập / lớp)
        foreach (var cls in classes)
        {
            if (cls.SpecialNeedsCount > 2)
            {
                warnings.Add(new PlacementCriteriaWarningDto
                {
                    Severity = "Violation",
                    ClassId = cls.ClassId,
                    ClassName = cls.ClassName,
                    RuleName = "SpecialNeedsLimit",
                    Message = $"Lớp {cls.ClassName} có {cls.SpecialNeedsCount} học sinh khuyết tật hòa nhập (vượt quá giới hạn tối đa 2 học sinh/lớp)."
                });
            }
            else if (cls.SpecialNeedsCount == 2)
            {
                warnings.Add(new PlacementCriteriaWarningDto
                {
                    Severity = "Info",
                    ClassId = cls.ClassId,
                    ClassName = cls.ClassName,
                    RuleName = "SpecialNeedsLimit",
                    Message = $"Lớp {cls.ClassName} đã đạt mức tối đa 2 học sinh khuyết tật hòa nhập."
                });
            }
        }

        // Rule 2: Duplicate Full Name Avoidance (Tránh học sinh trùng họ tên hoàn toàn trong cùng 1 lớp)
        foreach (var cls in classes)
        {
            var duplicates = cls.Students
                .GroupBy(s => s.FullName.Trim().ToLowerInvariant())
                .Where(g => g.Count() > 1)
                .ToList();

            foreach (var dup in duplicates)
            {
                warnings.Add(new PlacementCriteriaWarningDto
                {
                    Severity = "Violation",
                    ClassId = cls.ClassId,
                    ClassName = cls.ClassName,
                    RuleName = "DuplicateName",
                    Message = $"Lớp {cls.ClassName} có {dup.Count()} học sinh trùng hoàn toàn họ tên '{dup.First().FullName}'."
                });
            }
        }

        // Rule 3: Balance Class Size (Cân bằng sĩ số giữa các lớp)
        var minSize = classes.Min(c => c.TotalStudents);
        var maxSize = classes.Max(c => c.TotalStudents);
        if (maxSize - minSize > 2)
        {
            warnings.Add(new PlacementCriteriaWarningDto
            {
                Severity = "Warning",
                RuleName = "SizeImbalance",
                Message = $"Sĩ số giữa các lớp chưa cân bằng: Lớp đông nhất có {maxSize} học sinh, lớp ít nhất có {minSize} học sinh (chênh lệch {maxSize - minSize} em)."
            });
        }

        // Rule 4: Balance Gender Ratio (Cân bằng tỷ lệ nam/nữ)
        var minBoys = classes.Min(c => c.MaleCount);
        var maxBoys = classes.Max(c => c.MaleCount);
        if (maxBoys - minBoys > 3)
        {
            warnings.Add(new PlacementCriteriaWarningDto
            {
                Severity = "Warning",
                RuleName = "GenderImbalance",
                Message = $"Tỷ lệ giới tính giữa các lớp chênh lệch tương đối lớn: Số học sinh nam chênh lệch tối đa {maxBoys - minBoys} em giữa các lớp."
            });
        }

        // Rule 5: Capacity check
        foreach (var cls in classes)
        {
            if (cls.TotalStudents > cls.Capacity)
            {
                warnings.Add(new PlacementCriteriaWarningDto
                {
                    Severity = "Violation",
                    ClassId = cls.ClassId,
                    ClassName = cls.ClassName,
                    RuleName = "CapacityExceeded",
                    Message = $"Lớp {cls.ClassName} có {cls.TotalStudents} học sinh, vượt quá sức chứa tối đa ({cls.Capacity})."
                });
            }
        }

        return warnings;
    }

    #endregion
}

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Core.Configuration;
using Core.Constants;
using Core.DTOs;
using Core.Entities;
using Core.Enums;
using Core.Interfaces;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Moq;
using Services.Implementations;
using Services.Interfaces;
using Xunit;

namespace SAWP.UnitTests;

public class LeaveRequestServiceTests : TestBase
{
    private static readonly DateTime DefaultTestDate = new(2026, 11, 24); // Thứ 3 (ngày học cố định cho test)
    private readonly Mock<ILunchService> _lunchServiceMock;
    private readonly Mock<ISchoolClock> _clockMock;
    private readonly Mock<Infrastructure.ExternalServices.IFileStorageService> _fileStorageMock;
    private readonly LeaveRequestService _service;

    public LeaveRequestServiceTests()
    {
        _lunchServiceMock = new Mock<ILunchService>();
        _clockMock = new Mock<ISchoolClock>();
        _fileStorageMock = new Mock<Infrastructure.ExternalServices.IFileStorageService>();
        _clockMock.Setup(c => c.Today).Returns(DefaultTestDate);
        _clockMock.Setup(c => c.Now).Returns(DefaultTestDate.AddHours(7).AddMinutes(45));

        var options = Options.Create(new SchoolOptions
        {
            LunchCutoffTime = "08:15",
            AfternoonCutoffTime = "13:30",
            LeaveEscalationThresholdDays = 3,
            MaxLeaveSchoolDays = 30,
            MaxLeaveCalendarSpanDays = 60
        });
        _service = new LeaveRequestService(UnitOfWorkMock.Object, _lunchServiceMock.Object, _clockMock.Object, options, _fileStorageMock.Object, AuditServiceMock.Object);
    }

    // ==================== 46. SubmitLeaveRequestAsync (7 TCs) ====================

    [Fact]
    public async Task UTCID01_SubmitLeaveRequest_Success()
    {
        // Arrange
        var guardian = new Guardian
        {
            Id = 1,
            UserId = 20,
            StudentGuardians = new List<StudentGuardian> { new() { StudentId = 1, GuardianId = 1 } }
        };
        SetupRepository(new List<Guardian> { guardian });

        var adminRequests = new List<AdministrativeRequest>();
        var leaveRequests = new List<LeaveRequest>();
        SetupRepository(adminRequests);

        var leaveRepoMock = SetupRepository(leaveRequests);
        leaveRepoMock.Setup(r => r.AddAsync(It.IsAny<LeaveRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((LeaveRequest lr, CancellationToken _) =>
            {
                lr.Id = 1;
                leaveRequests.Add(lr);
                var parent = adminRequests.Find(a => a.Id == lr.AdministrativeRequestId);
                if (parent != null) parent.LeaveRequest = lr;
                return lr;
            });

        SetupRepository(new List<ClassAssignment>
        {
            new() { StudentId = 1, ClassId = 1, IsActive = true, Class = new Class { ClassName = "1A", HomeroomTeacher = new StaffProfile { UserId = 30 } } }
        });
        SetupRepository(new List<Notification>());

        var request = new CreateLeaveRequestDto
        {
            StudentId = 1,
            StartDate = DefaultTestDate,
            EndDate = DefaultTestDate,
            Session = LeaveSession.FullDay,
            KeepLunchService = false,
            Reason = "Sốt virus"
        };

        // Act
        var result = await _service.SubmitLeaveRequestAsync(20, request);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Code.Should().Be(SystemMessages.MSG21.Code);
        result.Data.Should().NotBeNull();
        result.Data!.StudentId.Should().Be(1);
    }

    [Fact]
    public async Task UTCID02_SubmitLeaveRequest_GuardianNotFound_Fails()
    {
        // Arrange
        SetupRepository(new List<Guardian>());
        var request = new CreateLeaveRequestDto { StudentId = 1, StartDate = DefaultTestDate, EndDate = DefaultTestDate, Reason = "Cháu bị ốm" };

        // Act
        var result = await _service.SubmitLeaveRequestAsync(999, request);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("Không tìm thấy thông tin phụ huynh");
    }

    [Fact]
    public async Task UTCID03_SubmitLeaveRequest_ParentNotLinked_Fails()
    {
        // Arrange
        var guardian = new Guardian { Id = 1, UserId = 25, StudentGuardians = new List<StudentGuardian>() };
        SetupRepository(new List<Guardian> { guardian });
        var request = new CreateLeaveRequestDto { StudentId = 1, StartDate = DefaultTestDate, EndDate = DefaultTestDate, Reason = "Cháu bị ốm" };

        // Act
        var result = await _service.SubmitLeaveRequestAsync(25, request);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("Bạn không có quyền gửi đơn cho học sinh này");
    }

    [Fact]
    public async Task UTCID04_SubmitLeaveRequest_PastMoreThan7Days_Fails()
    {
        // Arrange
        var guardian = new Guardian
        {
            Id = 1,
            UserId = 20,
            StudentGuardians = new List<StudentGuardian> { new() { StudentId = 1, GuardianId = 1 } }
        };
        SetupRepository(new List<Guardian> { guardian });
        var request = new CreateLeaveRequestDto
        {
            StudentId = 1,
            StartDate = DefaultTestDate.AddDays(-8),
            EndDate = DefaultTestDate.AddDays(-8),
            Reason = "Nghỉ bù"
        };

        // Act
        var result = await _service.SubmitLeaveRequestAsync(20, request);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("Không thể xin nghỉ học cho ngày trong quá khứ");
    }

    [Fact]
    public async Task UTCID05_SubmitLeaveRequest_StartDateAfterEndDate_Fails()
    {
        // Arrange
        var guardian = new Guardian
        {
            Id = 1,
            UserId = 20,
            StudentGuardians = new List<StudentGuardian> { new() { StudentId = 1, GuardianId = 1 } }
        };
        SetupRepository(new List<Guardian> { guardian });
        var request = new CreateLeaveRequestDto
        {
            StudentId = 1,
            StartDate = DefaultTestDate.AddDays(2),
            EndDate = DefaultTestDate.AddDays(1),
            Reason = "Ốm"
        };

        // Act
        var result = await _service.SubmitLeaveRequestAsync(20, request);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("Ngày bắt đầu nghỉ không được lớn hơn ngày kết thúc");
    }

    [Fact]
    public async Task UTCID06_SubmitLeaveRequest_EmptyReason_Fails()
    {
        // Arrange
        var guardian = new Guardian
        {
            Id = 1,
            UserId = 20,
            StudentGuardians = new List<StudentGuardian> { new() { StudentId = 1, GuardianId = 1 } }
        };
        SetupRepository(new List<Guardian> { guardian });
        var request = new CreateLeaveRequestDto { StudentId = 1, StartDate = DefaultTestDate, EndDate = DefaultTestDate, Reason = "   " };

        // Act
        var result = await _service.SubmitLeaveRequestAsync(20, request);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("Vui lòng nhập lý do xin nghỉ học");
    }

    [Fact]
    public async Task UTCID07_SubmitLeaveRequest_DuplicateOverlappingDate_Fails()
    {
        // Arrange
        var guardian = new Guardian
        {
            Id = 1,
            UserId = 20,
            StudentGuardians = new List<StudentGuardian> { new() { StudentId = 1, GuardianId = 1 } }
        };
        SetupRepository(new List<Guardian> { guardian });
        var existingLeave = new AdministrativeRequest
        {
            Id = 10,
            StudentId = 1,
            RequestType = RequestTypes.Leave,
            Status = RequestStatus.Submitted,
            LeaveRequest = new LeaveRequest { StartDate = DefaultTestDate, EndDate = DefaultTestDate }
        };
        SetupRepository(new List<AdministrativeRequest> { existingLeave });
        var request = new CreateLeaveRequestDto { StudentId = 1, StartDate = DefaultTestDate, EndDate = DefaultTestDate, Reason = "Cháu bị ốm" };

        // Act
        var result = await _service.SubmitLeaveRequestAsync(20, request);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("đã có đơn xin nghỉ trùng khoảng thời gian");
    }

    // ==================== 47. GetMyLeaveRequestsAsync (2 TCs) ====================

    [Fact]
    public async Task UTCID01_GetMyLeaveRequests_HasRequests_ReturnsList()
    {
        // Arrange
        var requests = new List<AdministrativeRequest>
        {
            new()
            {
                Id = 1,
                RequestNumber = "LR-20261028-001",
                RequesterUserId = 20,
                StudentId = 1,
                RequestType = RequestTypes.Leave,
                Status = RequestStatus.Submitted,
                SubmissionDate = DateTime.UtcNow,
                Student = new Student { Id = 1, FullName = "Nguyen Van A", StudentCode = "HS001" },
                LeaveRequest = new LeaveRequest { StartDate = DefaultTestDate, EndDate = DefaultTestDate, Reason = "Ốm" }
            }
        };
        SetupRepository(requests);
        SetupRepository(new List<ClassAssignment>());

        // Act
        var result = await _service.GetMyLeaveRequestsAsync(20);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Data.Should().HaveCount(1);
    }

    [Fact]
    public async Task UTCID02_GetMyLeaveRequests_NoRequests_ReturnsEmpty()
    {
        // Arrange
        SetupRepository(new List<AdministrativeRequest>());

        // Act
        var result = await _service.GetMyLeaveRequestsAsync(25);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Data.Should().BeEmpty();
    }

    // ==================== 48. GetClassLeaveRequestsAsync (4 TCs) ====================

    [Fact]
    public async Task UTCID01_GetClassLeaveRequests_HomeroomTeacher_ReturnsClassRequests()
    {
        // Arrange
        SetupRepository(new List<UserRole> { new() { UserId = 30, Role = new Role { Name = "Teacher" } } });
        SetupRepository(new List<StaffProfile> { new() { Id = 10, UserId = 30 } });
        SetupRepository(new List<Class> { new() { Id = 1, ClassName = "1A", HomeroomTeacherId = 10 } });
        SetupRepository(new List<ClassAssignment> { new() { ClassId = 1, StudentId = 1, IsActive = true, Class = new Class { ClassName = "1A" } } });
        SetupRepository(new List<AdministrativeRequest>
        {
            new()
            {
                Id = 1,
                StudentId = 1,
                RequestType = RequestTypes.Leave,
                Status = RequestStatus.Submitted,
                LeaveRequest = new LeaveRequest { StartDate = DefaultTestDate, EndDate = DefaultTestDate, Reason = "Ốm" }
            }
        });

        // Act
        var result = await _service.GetClassLeaveRequestsAsync(30);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Data.Should().HaveCount(1);
    }

    [Fact]
    public async Task UTCID02_GetClassLeaveRequests_WithFilters_ReturnsFiltered()
    {
        // Arrange
        SetupRepository(new List<UserRole> { new() { UserId = 30, Role = new Role { Name = "Teacher" } } });
        SetupRepository(new List<StaffProfile> { new() { Id = 10, UserId = 30 } });
        SetupRepository(new List<Class> { new() { Id = 1, ClassName = "1A", HomeroomTeacherId = 10 } });
        SetupRepository(new List<ClassAssignment> { new() { ClassId = 1, StudentId = 1, IsActive = true } });
        SetupRepository(new List<AdministrativeRequest>
        {
            new()
            {
                Id = 1,
                StudentId = 1,
                RequestType = RequestTypes.Leave,
                Status = RequestStatus.Submitted,
                LeaveRequest = new LeaveRequest { StartDate = DefaultTestDate, EndDate = DefaultTestDate, Reason = "Ốm" }
            }
        });

        // Act
        var result = await _service.GetClassLeaveRequestsAsync(30, 1, "Submitted", DefaultTestDate);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Data.Should().HaveCount(1);
    }

    [Fact]
    public async Task UTCID03_GetClassLeaveRequests_SchoolBoardRole_ReturnsAllClasses()
    {
        // Arrange
        SetupRepository(new List<UserRole> { new() { UserId = 2, Role = new Role { Name = "SchoolBoard" } } });
        SetupRepository(new List<Class> { new() { Id = 1, ClassName = "1A" }, new() { Id = 2, ClassName = "2B" } });
        SetupRepository(new List<ClassAssignment> { new() { ClassId = 1, StudentId = 1, IsActive = true } });
        SetupRepository(new List<AdministrativeRequest>
        {
            new()
            {
                Id = 5,
                StudentId = 1,
                RequestType = RequestTypes.Leave,
                Status = RequestStatus.UnderReview,
                LeaveRequest = new LeaveRequest { StartDate = DefaultTestDate, EndDate = DefaultTestDate.AddDays(3), Reason = "Nghỉ dài ngày" }
            }
        });

        // Act
        var result = await _service.GetClassLeaveRequestsAsync(2, null, "UnderReview", null);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Data.Should().HaveCount(1);
    }

    [Fact]
    public async Task UTCID04_GetClassLeaveRequests_NoStaffProfile_ReturnsEmpty()
    {
        // Arrange
        SetupRepository(new List<UserRole> { new() { UserId = 99, Role = new Role { Name = "Teacher" } } });
        SetupRepository(new List<Class>());
        SetupRepository(new List<StaffProfile>());

        // Act
        var result = await _service.GetClassLeaveRequestsAsync(99);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Data.Should().BeEmpty();
    }

    [Fact]
    public async Task GetClassLeaveRequests_ParentWithClassId_ReturnsEmpty()
    {
        // Arrange: Phụ huynh truyền classId = 1 dù lớp 1A có đơn xin nghỉ hợp lệ -> không rò rỉ dữ liệu
        var class1A = new Class { Id = 1, ClassName = "1A", HomeroomTeacherId = 10 };
        SetupRepository(new List<UserRole> { new() { UserId = 20, Role = new Role { Name = "Parent" } } });
        SetupRepository(new List<StaffProfile> { new() { Id = 10, UserId = 30 } });
        SetupRepository(new List<Class> { class1A });
        SetupRepository(new List<ClassAssignment> { new() { ClassId = 1, StudentId = 1, IsActive = true, Class = class1A } });
        SetupRepository(new List<AdministrativeRequest>
        {
            new()
            {
                Id = 1,
                StudentId = 1,
                RequestType = RequestTypes.Leave,
                Status = RequestStatus.Submitted,
                LeaveRequest = new LeaveRequest { StartDate = DefaultTestDate, EndDate = DefaultTestDate, Reason = "Ốm" }
            }
        });

        // Act
        var result = await _service.GetClassLeaveRequestsAsync(20, classId: 1);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Data.Should().BeEmpty();
    }

    [Fact]
    public async Task GetClassLeaveRequests_TeacherWithoutStaffProfileWithClassId_ReturnsEmpty()
    {
        // Arrange: Giáo viên chưa có StaffProfile truyền classId = 1 -> không rò rỉ dữ liệu
        var class1A = new Class { Id = 1, ClassName = "1A", HomeroomTeacherId = 10 };
        SetupRepository(new List<UserRole> { new() { UserId = 99, Role = new Role { Name = "Teacher" } } });
        SetupRepository(new List<StaffProfile> { new() { Id = 10, UserId = 30 } });
        SetupRepository(new List<Class> { class1A });
        SetupRepository(new List<ClassAssignment> { new() { ClassId = 1, StudentId = 1, IsActive = true, Class = class1A } });
        SetupRepository(new List<AdministrativeRequest>
        {
            new()
            {
                Id = 1,
                StudentId = 1,
                RequestType = RequestTypes.Leave,
                Status = RequestStatus.Submitted,
                LeaveRequest = new LeaveRequest { StartDate = DefaultTestDate, EndDate = DefaultTestDate, Reason = "Ốm" }
            }
        });

        // Act
        var result = await _service.GetClassLeaveRequestsAsync(99, classId: 1);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Data.Should().BeEmpty();
    }

    // ==================== 49. GetLeaveRequestDetailsAsync (2 TCs) ====================

    [Fact]
    public async Task UTCID01_GetLeaveRequestDetails_Found_ReturnsDto()
    {
        // Arrange
        var req = new AdministrativeRequest
        {
            Id = 10,
            RequestNumber = "LR-20261028-001",
            RequesterUserId = 20,
            StudentId = 1,
            RequestType = RequestTypes.Leave,
            Status = RequestStatus.Submitted,
            Student = new Student { Id = 1, FullName = "Nguyen Van A", StudentCode = "HS001" },
            LeaveRequest = new LeaveRequest { StartDate = DefaultTestDate, EndDate = DefaultTestDate, Reason = "Ốm" }
        };
        SetupRepository(new List<AdministrativeRequest> { req });
        SetupRepository(new List<ClassAssignment>());

        // Act
        var result = await _service.GetLeaveRequestDetailsAsync(10, 20);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Data!.RequestNumber.Should().Be("LR-20261028-001");
    }

    [Fact]
    public async Task UTCID02_GetLeaveRequestDetails_NotFound_Fails()
    {
        // Arrange
        SetupRepository(new List<AdministrativeRequest>());

        // Act
        var result = await _service.GetLeaveRequestDetailsAsync(999, 20);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("Không tìm thấy đơn xin nghỉ học");
    }

    // ==================== 50. ReviewLeaveRequestAsync (7 TCs) ====================

    [Fact]
    public async Task UTCID01_ReviewLeaveRequest_Approve_SyncsAttendanceAndCancelsLunch()
    {
        // Arrange
        var req = new AdministrativeRequest
        {
            Id = 1,
            RequestNumber = "LR-001",
            RequesterUserId = 20,
            StudentId = 1,
            RequestType = RequestTypes.Leave,
            Status = RequestStatus.Submitted,
            Student = new Student { Id = 1, FullName = "Em A" },
            LeaveRequest = new LeaveRequest { Id = 10, StartDate = DefaultTestDate, EndDate = DefaultTestDate, Session = LeaveSession.FullDay, KeepLunchService = false }
        };
        SetupRepository(new List<AdministrativeRequest> { req });
        SetupRepository(new List<ClassAssignment> { new() { StudentId = 1, ClassId = 1, IsActive = true } });
        SetupRepository(new List<AttendanceRecord>());
        SetupRepository(new List<AuditLog>());
        SetupRepository(new List<Notification>());
        _lunchServiceMock.Setup(l => l.CancelMealForLeaveAsync(1, DefaultTestDate, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ApiResponse<bool>.Ok(true));

        var review = new ReviewLeaveRequestDto { IsApproved = true, ReviewerNotes = "Đồng ý" };

        // Act
        var result = await _service.ReviewLeaveRequestAsync(1, 30, review);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Data!.Status.Should().Be(RequestStatus.Approved);
        result.Data.CancelledMealDates.Should().HaveCount(1);
    }

    [Fact]
    public async Task UTCID02_ReviewLeaveRequest_ForwardToSchoolBoard_Success()
    {
        // Arrange
        var req = new AdministrativeRequest
        {
            Id = 2,
            RequestNumber = "LR-002",
            RequesterUserId = 20,
            StudentId = 1,
            RequestType = RequestTypes.Leave,
            Status = RequestStatus.Submitted,
            Student = new Student { Id = 1, FullName = "Em A" },
            // 4 ngày học (T3 -> T6) => > 3 ngày học, hợp lệ để chuyển BGH (A2)
            LeaveRequest = new LeaveRequest { StartDate = DefaultTestDate, EndDate = DefaultTestDate.AddDays(3) }
        };
        SetupRepository(new List<AdministrativeRequest> { req });
        SetupRepository(new List<Role> { new() { Id = 2, Name = "SchoolBoard" } });
        SetupRepository(new List<UserRole> { new() { UserId = 5, RoleId = 2 } });
        SetupRepository(new List<Notification>());

        var review = new ReviewLeaveRequestDto { IsForwardToSchoolBoard = true, ReviewerNotes = "Kính chuyển BGH" };

        // Act
        var result = await _service.ReviewLeaveRequestAsync(2, 30, review);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        req.Status.Should().Be(RequestStatus.UnderReview);
    }

    [Fact]
    public async Task UTCID03_ReviewLeaveRequest_RejectWithReason_Success()
    {
        // Arrange
        var req = new AdministrativeRequest
        {
            Id = 3,
            RequestNumber = "LR-003",
            RequesterUserId = 20,
            StudentId = 1,
            RequestType = RequestTypes.Leave,
            Status = RequestStatus.Submitted,
            Student = new Student { Id = 1, FullName = "Em A" },
            LeaveRequest = new LeaveRequest { StartDate = DefaultTestDate, EndDate = DefaultTestDate }
        };
        SetupRepository(new List<AdministrativeRequest> { req });
        SetupRepository(new List<AuditLog>());
        SetupRepository(new List<Notification>());

        var review = new ReviewLeaveRequestDto { IsApproved = false, ReviewerNotes = "Lý do không chính đáng" };

        // Act
        var result = await _service.ReviewLeaveRequestAsync(3, 30, review);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        req.Status.Should().Be(RequestStatus.Rejected);
    }

    [Fact]
    public async Task UTCID04_ReviewLeaveRequest_NotFound_Fails()
    {
        // Arrange
        SetupRepository(new List<AdministrativeRequest>());
        var review = new ReviewLeaveRequestDto { IsApproved = true };

        // Act
        var result = await _service.ReviewLeaveRequestAsync(999, 30, review);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("Không tìm thấy đơn xin nghỉ học");
    }

    [Fact]
    public async Task UTCID05_ReviewLeaveRequest_AlreadyProcessed_Fails()
    {
        // Arrange
        var req = new AdministrativeRequest
        {
            Id = 4,
            RequestType = RequestTypes.Leave,
            Status = RequestStatus.Approved,
            LeaveRequest = new LeaveRequest { StartDate = DefaultTestDate, EndDate = DefaultTestDate }
        };
        SetupRepository(new List<AdministrativeRequest> { req });
        var review = new ReviewLeaveRequestDto { IsApproved = true };

        // Act
        var result = await _service.ReviewLeaveRequestAsync(4, 30, review);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("Đơn đã được xử lý trước đó");
    }

    [Fact]
    public async Task UTCID06_ReviewLeaveRequest_ForwardLessThan3Days_Fails()
    {
        // Arrange
        var req = new AdministrativeRequest
        {
            Id = 1,
            RequestType = RequestTypes.Leave,
            Status = RequestStatus.Submitted,
            LeaveRequest = new LeaveRequest { StartDate = DefaultTestDate, EndDate = DefaultTestDate }
        };
        SetupRepository(new List<AdministrativeRequest> { req });
        var review = new ReviewLeaveRequestDto { IsForwardToSchoolBoard = true };

        // Act
        var result = await _service.ReviewLeaveRequestAsync(1, 30, review);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("trên 3 ngày học");
    }

    [Fact]
    public async Task UTCID07_ReviewLeaveRequest_RejectWithoutReason_Fails()
    {
        // Arrange
        var req = new AdministrativeRequest
        {
            Id = 3,
            RequestType = RequestTypes.Leave,
            Status = RequestStatus.Submitted,
            LeaveRequest = new LeaveRequest { StartDate = DefaultTestDate, EndDate = DefaultTestDate }
        };
        SetupRepository(new List<AdministrativeRequest> { req });
        var review = new ReviewLeaveRequestDto { IsApproved = false, ReviewerNotes = "" };

        // Act
        var result = await _service.ReviewLeaveRequestAsync(3, 30, review);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("bắt buộc phải nhập lý do");
    }

    // ==================== 51. CancelLeaveRequestAsync (3 TCs) ====================

    [Fact]
    public async Task UTCID01_CancelLeaveRequest_Success()
    {
        // Arrange
        var req = new AdministrativeRequest { Id = 1, RequesterUserId = 20, Status = RequestStatus.Submitted };
        SetupRepository(new List<AdministrativeRequest> { req });

        // Act
        var result = await _service.CancelLeaveRequestAsync(1, 20);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        req.Status.Should().Be(RequestStatus.Cancelled);
    }

    [Fact]
    public async Task UTCID02_CancelLeaveRequest_NotFound_Fails()
    {
        // Arrange
        SetupRepository(new List<AdministrativeRequest>());

        // Act
        var result = await _service.CancelLeaveRequestAsync(999, 20);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("Không tìm thấy đơn xin nghỉ học");
    }

    [Fact]
    public async Task UTCID03_CancelLeaveRequest_AlreadyApproved_Fails()
    {
        // Arrange
        var req = new AdministrativeRequest { Id = 2, RequesterUserId = 20, Status = RequestStatus.Approved };
        SetupRepository(new List<AdministrativeRequest> { req });

        // Act
        var result = await _service.CancelLeaveRequestAsync(2, 20);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("Chỉ có thể hủy đơn khi đơn đang ở trạng thái Chờ duyệt");
    }

    [Fact]
    public async Task UTCID08_ReviewLeaveRequest_TeacherCannotDecideEscalatedRequest_Fails()
    {
        // Arrange (TC_LEV_30 / SSAWP-40)
        var req = new AdministrativeRequest
        {
            Id = 50,
            RequestType = RequestTypes.Leave,
            Status = RequestStatus.UnderReview,
            LeaveRequest = new LeaveRequest { StartDate = DefaultTestDate, EndDate = DefaultTestDate.AddDays(3) }
        };
        SetupRepository(new List<AdministrativeRequest> { req });
        var review = new ReviewLeaveRequestDto { IsApproved = true };

        // Act
        var result = await _service.ReviewLeaveRequestAsync(50, 30, review, new[] { UserRoles.Teacher });

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("Chỉ Ban Giám Hiệu");
    }

    [Fact]
    public async Task UTCID09_ReviewLeaveRequest_ApproveAndReject_NotificationsContainMsg22AndMsg23()
    {
        // Arrange (TC_LEV_15 / SSAWP-30 & TC_LEV_20 / SSAWP-35)
        var notifRepoMock = SetupRepository(new List<Notification>());
        var class1A = new Class { Id = 1, ClassName = "1A", HomeroomTeacherId = 50, Status = "Active" };
        SetupRepository(new List<StaffProfile> { new() { Id = 50, UserId = 30 } });
        SetupRepository(new List<ClassAssignment> { new() { Id = 1, StudentId = 1, ClassId = 1, Class = class1A, IsActive = true } });
        SetupRepository(new List<AttendanceRecord>());
        SetupRepository(new List<LunchServiceDay>());
        _lunchServiceMock
            .Setup(l => l.CancelMealForLeaveAsync(It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ApiResponse<bool>.Ok(true));

        var approveReq = new AdministrativeRequest
        {
            Id = 51,
            RequestNumber = "LV-051",
            RequesterUserId = 20,
            StudentId = 1,
            Student = new Student { Id = 1, FullName = "Trần Gia Bảo" },
            RequestType = RequestTypes.Leave,
            Status = RequestStatus.Submitted,
            LeaveRequest = new LeaveRequest { Id = 51, StartDate = DefaultTestDate, EndDate = DefaultTestDate, Session = LeaveSession.FullDay }
        };
        var rejectReq = new AdministrativeRequest
        {
            Id = 52,
            RequestNumber = "LV-052",
            RequesterUserId = 20,
            StudentId = 1,
            Student = new Student { Id = 1, FullName = "Trần Gia Bảo" },
            RequestType = RequestTypes.Leave,
            Status = RequestStatus.Submitted,
            LeaveRequest = new LeaveRequest { Id = 52, StartDate = DefaultTestDate, EndDate = DefaultTestDate, Session = LeaveSession.FullDay }
        };
        SetupRepository(new List<AdministrativeRequest> { approveReq, rejectReq });

        // Act
        var approveRes = await _service.ReviewLeaveRequestAsync(51, 30, new ReviewLeaveRequestDto { IsApproved = true, ReviewerNotes = "Đồng ý" }, new[] { UserRoles.Teacher });
        var rejectRes = await _service.ReviewLeaveRequestAsync(52, 30, new ReviewLeaveRequestDto { IsApproved = false, ReviewerNotes = "Có bài khảo sát" }, new[] { UserRoles.Teacher });

        // Assert
        approveRes.Success.Should().BeTrue();
        rejectRes.Success.Should().BeTrue();
        notifRepoMock.Verify(n => n.AddAsync(
            It.Is<Notification>(nf => nf.UserId == 20 && nf.Content.Contains(SystemMessages.MSG22.Content)),
            It.IsAny<CancellationToken>()), Times.Once);
        notifRepoMock.Verify(n => n.AddAsync(
            It.Is<Notification>(nf => nf.UserId == 20 && nf.Content.Contains(SystemMessages.MSG23.Content) && nf.Content.Contains("Có bài khảo sát")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UTCID10_ReviewLeaveRequest_ParentOrNonHomeroomTeacher_BlockedWithMsg10()
    {
        // Arrange (SSAWP-133)
        var class1B = new Class { Id = 2, ClassName = "1B", HomeroomTeacherId = 99, Status = "Active" };
        SetupRepository(new List<StaffProfile> { new() { Id = 50, UserId = 30 } }); // teacher1 is homeroom of 1A (50 != 99)
        SetupRepository(new List<ClassAssignment> { new() { Id = 2, StudentId = 31, ClassId = 2, Class = class1B, IsActive = true } });

        var req = new AdministrativeRequest
        {
            Id = 60,
            RequestNumber = "LV-060",
            RequesterUserId = 20,
            StudentId = 31,
            RequestType = RequestTypes.Leave,
            Status = RequestStatus.Submitted,
            LeaveRequest = new LeaveRequest { Id = 60, StartDate = DefaultTestDate, EndDate = DefaultTestDate, Session = LeaveSession.FullDay }
        };
        SetupRepository(new List<AdministrativeRequest> { req });

        // Act
        var parentRes = await _service.ReviewLeaveRequestAsync(60, 20, new ReviewLeaveRequestDto { IsApproved = true }, new[] { UserRoles.Parent });
        var wrongTeacherRes = await _service.ReviewLeaveRequestAsync(60, 30, new ReviewLeaveRequestDto { IsApproved = true }, new[] { UserRoles.Teacher });

        // Assert
        parentRes.Success.Should().BeFalse();
        parentRes.Code.Should().Be(SystemMessages.MSG10.Code);
        wrongTeacherRes.Success.Should().BeFalse();
        wrongTeacherRes.Code.Should().Be(SystemMessages.MSG10.Code);
    }

    [Fact]
    public async Task UTCID11_SubmitLeaveRequest_AfternoonAllowedWhenMorningExistsSameDay_DuplicateMorningBlocked()
    {
        // Arrange (SSAWP-138)
        var targetDate = DefaultTestDate;
        var student = new Student { Id = 1, StudentCode = "HS001", FullName = "Trần Gia Bảo" };
        var guardian = new Guardian
        {
            Id = 1,
            UserId = 20,
            StudentGuardians = new List<StudentGuardian> { new() { StudentId = 1, Student = student } }
        };
        SetupRepository(new List<Guardian> { guardian });
        SetupRepository(new List<ClassAssignment>());
        SetupRepository(new List<Notification>());
        SetupRepository(new List<LeaveRequest>());
        SetupRepository(new List<AdministrativeRequest>
        {
            new()
            {
                Id = 70,
                StudentId = 1,
                RequestType = RequestTypes.Leave,
                Status = RequestStatus.Approved,
                LeaveRequest = new LeaveRequest { StartDate = targetDate, EndDate = targetDate, Session = LeaveSession.Morning }
            }
        });

        // Act
        var afternoonRes = await _service.SubmitLeaveRequestAsync(20, new CreateLeaveRequestDto
        {
            StudentId = 1,
            StartDate = targetDate,
            EndDate = targetDate,
            Session = LeaveSession.Afternoon,
            Reason = "Buổi chiều cháu sốt lại"
        });
        var dupMorningRes = await _service.SubmitLeaveRequestAsync(20, new CreateLeaveRequestDto
        {
            StudentId = 1,
            StartDate = targetDate,
            EndDate = targetDate,
            Session = LeaveSession.Morning,
            Reason = "Trùng buổi sáng"
        });

        // Assert
        afternoonRes.Success.Should().BeTrue();
        dupMorningRes.Success.Should().BeFalse();
        dupMorningRes.Message.Should().Contain("trùng khoảng thời gian");
    }

    [Fact]
    public async Task UTCID12_A2_ThreeSchoolDaysAndFriToMon_NotEscalated_TeacherApprovesDirectly()
    {
        // Arrange: Đơn T6 (27/11/2026) -> T2 (30/11/2026) = 4 ngày lịch nhưng chỉ có 2 ngày học (A2)
        var friday = new DateTime(2026, 11, 27);
        var monday = new DateTime(2026, 11, 30);
        var class1A = new Class { Id = 1, ClassName = "1A", HomeroomTeacherId = 50, Status = "Active" };
        SetupRepository(new List<StaffProfile> { new() { Id = 50, UserId = 30 } });
        SetupRepository(new List<ClassAssignment> { new() { Id = 1, StudentId = 1, ClassId = 1, Class = class1A, IsActive = true } });
        SetupRepository(new List<AttendanceRecord>());
        SetupRepository(new List<Notification>());
        SetupRepository(new List<LunchServiceDay>());
        SetupRepository(new List<SchoolCalendarDay>());
        _lunchServiceMock
            .Setup(l => l.CancelMealForLeaveAsync(1, It.IsAny<DateTime>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ApiResponse<bool>.Ok(true));

        var friMonReq = new AdministrativeRequest
        {
            Id = 80,
            RequestNumber = "LR-080",
            RequesterUserId = 20,
            StudentId = 1,
            Student = new Student { Id = 1, FullName = "Trần Gia Bảo" },
            RequestType = RequestTypes.Leave,
            Status = RequestStatus.Submitted,
            LeaveRequest = new LeaveRequest { Id = 80, StartDate = friday, EndDate = monday, Session = LeaveSession.FullDay }
        };
        // Đơn 3 ngày học (T3 -> T5: 24, 25, 26/11) vẫn do GVCN duyệt thẳng (A2: > 3 mới chuyển)
        var threeDayReq = new AdministrativeRequest
        {
            Id = 81,
            RequestNumber = "LR-081",
            RequesterUserId = 20,
            StudentId = 1,
            Student = new Student { Id = 1, FullName = "Trần Gia Bảo" },
            RequestType = RequestTypes.Leave,
            Status = RequestStatus.Submitted,
            LeaveRequest = new LeaveRequest { Id = 81, StartDate = new DateTime(2026, 11, 24), EndDate = new DateTime(2026, 11, 26), Session = LeaveSession.FullDay }
        };
        SetupRepository(new List<AdministrativeRequest> { friMonReq, threeDayReq });

        // Act
        var friMonDto = await _service.GetLeaveRequestDetailsAsync(80, 20);
        var friMonApprove = await _service.ReviewLeaveRequestAsync(80, 30, new ReviewLeaveRequestDto { IsApproved = true }, new[] { UserRoles.Teacher });
        var threeDayApprove = await _service.ReviewLeaveRequestAsync(81, 30, new ReviewLeaveRequestDto { IsApproved = true }, new[] { UserRoles.Teacher });

        // Assert
        friMonDto.Data!.TotalDays.Should().Be(2);
        friMonDto.Data.IsEscalated.Should().BeFalse();
        friMonApprove.Success.Should().BeTrue();
        friMonApprove.Code.Should().Be(SystemMessages.MSG22.Code);
        threeDayApprove.Success.Should().BeTrue();
    }

    [Fact]
    public async Task UTCID13_A2_FourSchoolDays_TeacherCannotApproveDirectly_RequiresBoard()
    {
        // Arrange: 4 ngày học (T3 24/11 -> T6 27/11) => > 3 ngày học
        var class1A = new Class { Id = 1, ClassName = "1A", HomeroomTeacherId = 50, Status = "Active" };
        SetupRepository(new List<StaffProfile> { new() { Id = 50, UserId = 30 } });
        SetupRepository(new List<ClassAssignment> { new() { Id = 1, StudentId = 1, ClassId = 1, Class = class1A, IsActive = true } });
        SetupRepository(new List<SchoolCalendarDay>());
        var req = new AdministrativeRequest
        {
            Id = 82,
            RequestNumber = "LR-082",
            RequesterUserId = 20,
            StudentId = 1,
            RequestType = RequestTypes.Leave,
            Status = RequestStatus.Submitted,
            LeaveRequest = new LeaveRequest { Id = 82, StartDate = new DateTime(2026, 11, 24), EndDate = new DateTime(2026, 11, 27), Session = LeaveSession.FullDay }
        };
        SetupRepository(new List<AdministrativeRequest> { req });

        // Act
        var teacherRes = await _service.ReviewLeaveRequestAsync(82, 30, new ReviewLeaveRequestDto { IsApproved = true }, new[] { UserRoles.Teacher });

        // Assert
        teacherRes.Success.Should().BeFalse();
        teacherRes.Message.Should().Contain("Ban Giám Hiệu");
    }

    [Fact]
    public async Task UTCID14_A3_ApproveAcrossWeekendAndExistingPresent_SkipsWeekendAndPreservesPresent()
    {
        // Arrange: Đơn T6 (27/11) -> T2 (30/11); Thứ 6 đã được GV chấm Có mặt (Present), Thứ 2 chưa chấm
        var friday = new DateTime(2026, 11, 27);
        var saturday = new DateTime(2026, 11, 28);
        var sunday = new DateTime(2026, 11, 29);
        var monday = new DateTime(2026, 11, 30);

        var class1A = new Class { Id = 1, ClassName = "1A", HomeroomTeacherId = 50, Status = "Active", HomeroomTeacher = new StaffProfile { Id = 50, UserId = 30 } };
        SetupRepository(new List<StaffProfile> { new() { Id = 50, UserId = 30 } });
        SetupRepository(new List<ClassAssignment> { new() { Id = 1, StudentId = 1, ClassId = 1, Class = class1A, IsActive = true } });
        SetupRepository(new List<SchoolCalendarDay>());
        SetupRepository(new List<LunchServiceDay>());
        var notifRepoMock = SetupRepository(new List<Notification>());

        var friMorningPresent = new AttendanceRecord
        {
            Id = 500,
            StudentId = 1,
            ClassId = 1,
            Date = friday,
            Session = AttendanceSession.Morning,
            Status = AttendanceStatus.Present,
            RecordedByUserId = 30
        };
        var attendanceList = new List<AttendanceRecord> { friMorningPresent };
        var attRepoMock = SetupRepository(attendanceList);
        attRepoMock.Setup(a => a.AddAsync(It.IsAny<AttendanceRecord>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((AttendanceRecord rec, CancellationToken _) => { attendanceList.Add(rec); return rec; });

        _lunchServiceMock
            .Setup(l => l.CancelMealForLeaveAsync(1, It.IsAny<DateTime>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ApiResponse<bool>.Ok(true));

        var req = new AdministrativeRequest
        {
            Id = 83,
            RequestNumber = "LR-083",
            RequesterUserId = 20,
            StudentId = 1,
            Student = new Student { Id = 1, FullName = "Trần Gia Bảo" },
            RequestType = RequestTypes.Leave,
            Status = RequestStatus.Submitted,
            LeaveRequest = new LeaveRequest { Id = 83, StartDate = friday, EndDate = monday, Session = LeaveSession.FullDay }
        };
        SetupRepository(new List<AdministrativeRequest> { req });

        // Act
        var res = await _service.ReviewLeaveRequestAsync(83, 30, new ReviewLeaveRequestDto { IsApproved = true }, new[] { UserRoles.Teacher });

        // Assert
        res.Success.Should().BeTrue();
        // Thứ 6 buổi sáng vẫn giữ Present, KHÔNG bị ghi đè sang ExcusedAbsence
        friMorningPresent.Status.Should().Be(AttendanceStatus.Present);
        // Thứ 7 và Chủ nhật không được ghi điểm danh hay hủy suất ăn
        attendanceList.Should().NotContain(a => a.Date == saturday || a.Date == sunday);
        _lunchServiceMock.Verify(l => l.CancelMealForLeaveAsync(1, friday, It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _lunchServiceMock.Verify(l => l.CancelMealForLeaveAsync(1, saturday, It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _lunchServiceMock.Verify(l => l.CancelMealForLeaveAsync(1, monday, It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
        // Cảnh báo cho giáo viên & trong response
        res.Data!.AttendanceWarnings.Should().NotBeEmpty();
        notifRepoMock.Verify(n => n.AddAsync(It.Is<Notification>(nf => nf.UserId == 30 && nf.Title.Contains("Kiểm tra điểm danh")), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UTCID15_A4_WeekendEvenWhenTodayIsSunday_Blocked_ButMakeupSaturdayAllowed()
    {
        // Arrange: Giả lập hôm nay là Chủ nhật 22/11/2026 (A4)
        var sunday = new DateTime(2026, 11, 22);
        var makeupSaturday = new DateTime(2026, 12, 12);
        _clockMock.Setup(c => c.Today).Returns(sunday);
        _clockMock.Setup(c => c.Now).Returns(sunday.AddHours(8));

        var guardian = new Guardian
        {
            Id = 1,
            UserId = 20,
            StudentGuardians = new List<StudentGuardian> { new() { StudentId = 1, GuardianId = 1 } }
        };
        SetupRepository(new List<Guardian> { guardian });
        var adminRequests = new List<AdministrativeRequest>();
        var leaveRequests = new List<LeaveRequest>();
        SetupRepository(adminRequests);
        var leaveRepoMock = SetupRepository(leaveRequests);
        leaveRepoMock.Setup(r => r.AddAsync(It.IsAny<LeaveRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((LeaveRequest lr, CancellationToken _) =>
            {
                lr.Id = 1;
                leaveRequests.Add(lr);
                var parent = adminRequests.Find(a => a.Id == lr.AdministrativeRequestId);
                if (parent != null) parent.LeaveRequest = lr;
                return lr;
            });
        SetupRepository(new List<ClassAssignment>());
        SetupRepository(new List<Notification>());
        SetupRepository(new List<SchoolCalendarDay>
        {
            new() { Id = 1, Date = makeupSaturday, IsSchoolDay = true, Note = "Học bù" }
        });

        // Act
        var sundayRes = await _service.SubmitLeaveRequestAsync(20, new CreateLeaveRequestDto
        {
            StudentId = 1,
            StartDate = sunday,
            EndDate = sunday,
            Reason = "Xin nghỉ Chủ nhật"
        });
        var makeupSatRes = await _service.SubmitLeaveRequestAsync(20, new CreateLeaveRequestDto
        {
            StudentId = 1,
            StartDate = makeupSaturday,
            EndDate = makeupSaturday,
            Reason = "Xin nghỉ ngày học bù T7 12/12"
        });

        // Assert
        sundayRes.Success.Should().BeFalse();
        sundayRes.Message.Should().Contain("cuối tuần");
        makeupSatRes.Success.Should().BeTrue();
        makeupSatRes.Data!.TotalDays.Should().Be(1);
    }

    [Fact]
    public async Task UTCID16_A6_InvalidSession_Span122Days_EvilUrl_AndInvalidUploadExt_AllBlocked()
    {
        // Arrange
        var guardian = new Guardian
        {
            Id = 1,
            UserId = 20,
            StudentGuardians = new List<StudentGuardian> { new() { StudentId = 1, GuardianId = 1 } }
        };
        SetupRepository(new List<Guardian> { guardian });
        SetupRepository(new List<SchoolCalendarDay>());

        // Act
        var badSessionRes = await _service.SubmitLeaveRequestAsync(20, new CreateLeaveRequestDto
        {
            StudentId = 1,
            StartDate = DefaultTestDate,
            EndDate = DefaultTestDate,
            Session = (LeaveSession)9,
            Reason = "Test session 9"
        });
        var span122Res = await _service.SubmitLeaveRequestAsync(20, new CreateLeaveRequestDto
        {
            StudentId = 1,
            StartDate = DefaultTestDate,
            EndDate = DefaultTestDate.AddDays(121),
            Reason = "Nghỉ 122 ngày"
        });
        var evilUrlRes = await _service.SubmitLeaveRequestAsync(20, new CreateLeaveRequestDto
        {
            StudentId = 1,
            StartDate = DefaultTestDate,
            EndDate = DefaultTestDate,
            Reason = "URL độc hại",
            DoctorNoteAttachmentUrl = "http://evil.example.com/x.exe"
        });
        using var ms = new MemoryStream(new byte[] { 1, 2, 3 });
        var docxRes = await _service.UploadAttachmentAsync(ms, "don.docx", 3);
        var webpRes = await _service.UploadAttachmentAsync(ms, "anh.webp", 3);

        // Assert
        badSessionRes.Success.Should().BeFalse();
        badSessionRes.Message.Should().Contain("Buổi xin nghỉ không hợp lệ");
        span122Res.Success.Should().BeFalse();
        span122Res.Message.Should().Contain("vượt quá 60 ngày lịch");
        evilUrlRes.Success.Should().BeFalse();
        evilUrlRes.Message.Should().Contain("Đường dẫn tệp đính kèm không hợp lệ");
        docxRes.Success.Should().BeFalse();
        webpRes.Success.Should().BeFalse();
    }

    [Fact]
    public async Task UTCID17_A8_WithdrawApprovedLeave_BeforeCutoff_RestoresMealRemovesAutoAttendance()
    {
        // Arrange: Đơn 1 ngày đã duyệt trước giờ chốt (07:45 < 08:15)
        var leaveDate = DefaultTestDate;
        _clockMock.Setup(c => c.Now).Returns(leaveDate.AddHours(7).AddMinutes(45));

        var autoRecord = new AttendanceRecord
        {
            Id = 601,
            StudentId = 1,
            ClassId = 1,
            Date = leaveDate,
            Session = AttendanceSession.Morning,
            Status = AttendanceStatus.ExcusedAbsence,
            LeaveRequestId = 90
        };
        var attRepoMock = SetupRepository(new List<AttendanceRecord> { autoRecord });
        SetupRepository(new List<LunchRegistration>
        {
            new() { Id = 701, StudentId = 1, Date = leaveDate, IsActive = false, CancellationSource = LunchCancellationSource.Leave }
        });
        SetupRepository(new List<LunchServiceDay>());
        SetupRepository(new List<SchoolCalendarDay>());
        SetupRepository(new List<ClassAssignment>
        {
            new() { Id = 1, StudentId = 1, ClassId = 1, IsActive = true, Class = new Class { ClassName = "1A", HomeroomTeacher = new StaffProfile { UserId = 30 } } }
        });
        SetupRepository(new List<Notification>());

        var req = new AdministrativeRequest
        {
            Id = 90,
            RequestNumber = "LR-090",
            RequesterUserId = 20,
            StudentId = 1,
            Student = new Student { Id = 1, FullName = "Trần Gia Bảo" },
            RequestType = RequestTypes.Leave,
            Status = RequestStatus.Approved,
            LeaveRequest = new LeaveRequest { Id = 90, StartDate = leaveDate, EndDate = leaveDate, Session = LeaveSession.FullDay, IsLunchCancelled = true }
        };
        SetupRepository(new List<AdministrativeRequest> { req });

        _lunchServiceMock
            .Setup(l => l.RestoreMealForStudentAsync(1, leaveDate, It.IsAny<string>(), 20, It.IsAny<IReadOnlyList<UserRoles>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ApiResponse<bool>.Ok(true));

        // Act
        var res = await _service.WithdrawLeaveRequestAsync(90, 20, "Cháu đi học lại");

        // Assert
        res.Success.Should().BeTrue();
        req.Status.Should().Be(RequestStatus.Cancelled);
        req.LeaveRequest!.IsLunchCancelled.Should().BeFalse();
        attRepoMock.Verify(a => a.DeleteAsync(autoRecord, It.IsAny<CancellationToken>()), Times.Once);
        _lunchServiceMock.Verify(l => l.RestoreMealForStudentAsync(1, leaveDate, It.IsAny<string>(), 20, It.IsAny<IReadOnlyList<UserRoles>>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UTCID18_A10_RequestMoreInfo_AndResubmit_TransitionsAndLogsAudit()
    {
        // Arrange
        var req = new AdministrativeRequest
        {
            Id = 95,
            RequestNumber = "LR-095",
            RequesterUserId = 20,
            StudentId = 1,
            Student = new Student { Id = 1, FullName = "Trần Gia Bảo" },
            RequestType = RequestTypes.Leave,
            Status = RequestStatus.Submitted,
            SubmissionDate = DateTime.UtcNow,
            LeaveRequest = new LeaveRequest { Id = 95, StartDate = DefaultTestDate, EndDate = DefaultTestDate, Session = LeaveSession.FullDay, Reason = "Bị ốm" }
        };
        SetupRepository(new List<AdministrativeRequest> { req });
        SetupRepository(new List<ClassAssignment> { new() { Id = 1, StudentId = 1, ClassId = 1, IsActive = true, Class = new Class { ClassName = "1A", HomeroomTeacher = new StaffProfile { Id = 50, UserId = 30 }, HomeroomTeacherId = 50 } } });
        SetupRepository(new List<StaffProfile> { new() { Id = 50, UserId = 30 } });
        SetupRepository(new List<Notification>());
        SetupRepository(new List<SchoolCalendarDay>());
        SetupRepository(new List<LunchServiceDay>());
        SetupRepository(new List<AuditLog>());

        // Act 1: Giáo viên yêu cầu bổ sung thông tin
        var moreInfoRes = await _service.ReviewLeaveRequestAsync(95, 30, new ReviewLeaveRequestDto
        {
            IsRequestMoreInfo = true,
            ReviewerNotes = "Vui lòng bổ sung giấy khám bệnh"
        }, new[] { UserRoles.Teacher });

        // Act 2: Phụ huynh sửa và gửi lại
        var updateRes = await _service.UpdateLeaveRequestAsync(95, 20, new UpdateLeaveRequestDto
        {
            StartDate = DefaultTestDate,
            EndDate = DefaultTestDate,
            Session = LeaveSession.FullDay,
            Reason = "Sốt siêu vi, đã có đơn thuốc",
            DoctorNoteAttachmentUrl = "/api/LeaveRequests/attachment-file?path=attachments/giaykham.pdf"
        });

        // Assert
        moreInfoRes.Success.Should().BeTrue();
        moreInfoRes.Data!.Status.Should().Be(RequestStatus.NeedMoreInfo);
        updateRes.Success.Should().BeTrue();
        req.Status.Should().Be(RequestStatus.Submitted);
        req.RequiresAmendment.Should().BeFalse();
        req.LeaveRequest!.DoctorNoteAttachmentUrl.Should().Contain("giaykham.pdf");
    }

    [Fact]
    public async Task UTCID19_GetLeaveRequestDetails_UnauthorizedParent_BlockedWithMSG10()
    {
        // Arrange
        var req = new AdministrativeRequest
        {
            Id = 110,
            RequestNumber = "LR-110",
            RequesterUserId = 20,
            StudentId = 1,
            RequestType = RequestTypes.Leave,
            Status = RequestStatus.Submitted,
            LeaveRequest = new LeaveRequest { Id = 110, StartDate = DefaultTestDate, EndDate = DefaultTestDate, Reason = "Bị ốm" }
        };
        SetupRepository(new List<AdministrativeRequest> { req });
        SetupRepository(new List<ClassAssignment>());

        // Act
        var otherParentRes = await _service.GetLeaveRequestDetailsAsync(110, 99, new[] { UserRoles.Parent });
        var boardRes = await _service.GetLeaveRequestDetailsAsync(110, 88, new[] { UserRoles.SchoolBoard });

        // Assert
        otherParentRes.Success.Should().BeFalse();
        otherParentRes.Code.Should().Be(SystemMessages.MSG10.Code);
        boardRes.Success.Should().BeTrue();
    }

    [Fact]
    public async Task UTCID20_GetAttachment_BlocksTraversalRootedPathsAndUnauthorizedUsers()
    {
        // Arrange
        var savedPath = "attachments/1234_doc.pdf";
        var req = new AdministrativeRequest
        {
            Id = 111,
            RequesterUserId = 20,
            StudentId = 1,
            RequestType = RequestTypes.Leave,
            LeaveRequest = new LeaveRequest
            {
                Id = 111,
                DoctorNoteAttachmentUrl = $"/api/LeaveRequests/attachment-file?path={Uri.EscapeDataString(savedPath)}"
            }
        };
        SetupRepository(new List<AdministrativeRequest> { req });
        _fileStorageMock.Setup(f => f.GetFileAsync(savedPath, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new System.IO.MemoryStream(new byte[] { 1, 2, 3 }));

        // Act
        var rooted = await _service.GetAttachmentAsync("C:/Windows/win.ini", 20, new[] { UserRoles.Parent });
        var traversal = await _service.GetAttachmentAsync("attachments/../appsettings.json", 20, new[] { UserRoles.Parent });
        var wrongFolder = await _service.GetAttachmentAsync("contactbook/photo.png", 20, new[] { UserRoles.Parent });
        var otherParent = await _service.GetAttachmentAsync(savedPath, 99, new[] { UserRoles.Parent });
        var owner = await _service.GetAttachmentAsync(savedPath, 20, new[] { UserRoles.Parent });

        // Assert
        rooted.Should().BeNull();
        traversal.Should().BeNull();
        wrongFolder.Should().BeNull();
        otherParent.Should().BeNull();
        owner.Should().NotBeNull();
        owner!.ContentType.Should().Be("application/pdf");
    }

    [Fact]
    public async Task UTCID21_WithdrawApprovedLeave_AllPastCutoff_ReturnsMSG27()
    {
        // Arrange: 09:00 đã qua mốc chốt 08:15 của buổi sáng/cả ngày
        var leaveDate = DefaultTestDate;
        _clockMock.Setup(c => c.Now).Returns(leaveDate.AddHours(9));
        var req = new AdministrativeRequest
        {
            Id = 112,
            RequestNumber = "LR-112",
            RequesterUserId = 20,
            StudentId = 1,
            RequestType = RequestTypes.Leave,
            Status = RequestStatus.Approved,
            LeaveRequest = new LeaveRequest { Id = 112, StartDate = leaveDate, EndDate = leaveDate, Session = LeaveSession.FullDay }
        };
        SetupRepository(new List<AdministrativeRequest> { req });
        SetupRepository(new List<SchoolCalendarDay>());
        SetupRepository(new List<LunchServiceDay>());

        // Act
        var res = await _service.WithdrawLeaveRequestAsync(112, 20, "Muốn thu hồi muộn");

        // Assert
        res.Success.Should().BeFalse();
        res.Code.Should().Be(SystemMessages.MSG27.Code);
        req.Status.Should().Be(RequestStatus.Approved);
    }

    [Fact]
    public async Task UTCID22_WithdrawApprovedLeave_AfternoonAllowedAfter0815_AndPartialUpdatesIsLunchCancelled()
    {
        // Arrange: 09:00 (sau 08:15 sáng nhưng trước 13:30 chiều)
        var day1 = new DateTime(2026, 11, 24);
        var day2 = new DateTime(2026, 11, 25);
        _clockMock.Setup(c => c.Now).Returns(day1.AddHours(9));

        var afternoonReq = new AdministrativeRequest
        {
            Id = 113,
            RequestNumber = "LR-113",
            RequesterUserId = 20,
            StudentId = 1,
            Student = new Student { Id = 1, FullName = "Trần Gia Bảo" },
            RequestType = RequestTypes.Leave,
            Status = RequestStatus.Approved,
            LeaveRequest = new LeaveRequest { Id = 113, StartDate = day1, EndDate = day1, Session = LeaveSession.Afternoon }
        };

        var multiDayReq = new AdministrativeRequest
        {
            Id = 114,
            RequestNumber = "LR-114",
            RequesterUserId = 20,
            StudentId = 1,
            Student = new Student { Id = 1, FullName = "Trần Gia Bảo" },
            RequestType = RequestTypes.Leave,
            Status = RequestStatus.Approved,
            LeaveRequest = new LeaveRequest { Id = 114, StartDate = day1, EndDate = day2, Session = LeaveSession.FullDay, IsLunchCancelled = true }
        };

        SetupRepository(new List<AdministrativeRequest> { afternoonReq, multiDayReq });
        SetupRepository(new List<SchoolCalendarDay>());
        SetupRepository(new List<LunchServiceDay>());
        SetupRepository(new List<AttendanceRecord>());
        // Ngày 1 không hủy suất ăn (đang Active hoặc không đăng ký); chỉ Ngày 2 từng hủy do Leave
        SetupRepository(new List<LunchRegistration>
        {
            new() { Id = 2, StudentId = 1, Date = day2, IsActive = false, CancellationSource = LunchCancellationSource.Leave }
        });
        SetupRepository(new List<ClassAssignment>());
        SetupRepository(new List<Notification>());
        _lunchServiceMock.Setup(l => l.RestoreMealForStudentAsync(1, day2, It.IsAny<string>(), 20, It.IsAny<IReadOnlyList<UserRoles>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ApiResponse<bool>.Ok(true));

        // Act
        var afternoonWithdraw = await _service.WithdrawLeaveRequestAsync(113, 20, "Chiều cháu vẫn đi học");
        var partialWithdraw = await _service.WithdrawLeaveRequestAsync(114, 20, "Ngày mai cháu đi học lại");

        // Assert
        afternoonWithdraw.Success.Should().BeTrue();
        afternoonReq.Status.Should().Be(RequestStatus.Cancelled);

        partialWithdraw.Success.Should().BeTrue();
        multiDayReq.Status.Should().Be(RequestStatus.Approved);
        multiDayReq.LeaveRequest!.EndDate.Should().Be(day1);
        multiDayReq.LeaveRequest.IsLunchCancelled.Should().BeFalse();
    }

    // ==================== Round 2 Regression Tests (L-A..L-F) ====================

    [Fact]
    public async Task LA_SchoolBoardReviewsShortSubmittedLeave_UsesSchoolBoardLabelsNotTeacher()
    {
        // Arrange: Đơn 1 ngày (Submitted), Hiệu trưởng (SchoolBoard) yêu cầu bổ sung rồi sau đó duyệt trực tiếp
        var req = new AdministrativeRequest
        {
            Id = 120,
            RequestNumber = "LR-120",
            RequesterUserId = 20,
            StudentId = 1,
            Student = new Student { Id = 1, FullName = "Nguyễn Minh Anh" },
            RequestType = RequestTypes.Leave,
            Status = RequestStatus.Submitted,
            LeaveRequest = new LeaveRequest { Id = 120, StartDate = DefaultTestDate, EndDate = DefaultTestDate, Session = LeaveSession.FullDay, Reason = "Bị sốt cao" }
        };
        SetupRepository(new List<AdministrativeRequest> { req });
        SetupRepository(new List<SchoolCalendarDay>());
        SetupRepository(new List<LunchServiceDay>());
        SetupRepository(new List<Notification>());

        // Act: BGH yêu cầu bổ sung trên đơn 1 ngày Submitted
        var moreInfoRes = await _service.ReviewLeaveRequestAsync(120, 99, new ReviewLeaveRequestDto
        {
            IsRequestMoreInfo = true,
            ReviewerNotes = "Vui lòng bổ sung giấy khám bệnh"
        }, new[] { UserRoles.SchoolBoard });

        // Assert
        moreInfoRes.Success.Should().BeTrue();
        req.ReviewerNotes.Should().StartWith("[BGH yêu cầu bổ sung]:");
    }

    [Fact]
    public async Task LB_ApproveFutureLeaveWithoutLunch_ShowsUnregisteredNotPastCutoff()
    {
        // Arrange: Đơn nghỉ ngày tương lai (chưa tới 08:15), không có đăng ký bán trú (CancelMealForLeaveAsync trả false)
        var futureDate = DefaultTestDate.AddDays(2);
        _clockMock.Setup(c => c.Now).Returns(DefaultTestDate.AddHours(9));
        var req = new AdministrativeRequest
        {
            Id = 121,
            RequestNumber = "LR-121",
            RequesterUserId = 20,
            StudentId = 1,
            Student = new Student { Id = 1, FullName = "Nguyễn Minh Anh" },
            RequestType = RequestTypes.Leave,
            Status = RequestStatus.Submitted,
            LeaveRequest = new LeaveRequest { Id = 121, StartDate = futureDate, EndDate = futureDate, Session = LeaveSession.FullDay, Reason = "Gia đình có việc" }
        };
        SetupRepository(new List<AdministrativeRequest> { req });
        SetupRepository(new List<ClassAssignment>());
        SetupRepository(new List<AttendanceRecord>());
        SetupRepository(new List<SchoolCalendarDay>());
        SetupRepository(new List<LunchServiceDay>());
        SetupRepository(new List<Notification>());
        _lunchServiceMock.Setup(l => l.CancelMealForLeaveAsync(1, futureDate, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ApiResponse<bool>.Ok(false));

        // Act
        var res = await _service.ReviewLeaveRequestAsync(121, 99, new ReviewLeaveRequestDto
        {
            IsApproved = true
        }, new[] { UserRoles.SchoolBoard });

        // Assert
        res.Success.Should().BeTrue();
        res.Data!.MealSyncMessage.Should().Contain("chưa đăng ký suất ăn bán trú");
        res.Data.MealSyncMessage.Should().NotContain("đã qua giờ chốt");
    }

    [Fact]
    public async Task LC_ReviewRejectOrMoreInfo_ShortNotesUnder5Chars_FailsWithMSG01()
    {
        var req = new AdministrativeRequest
        {
            Id = 122,
            RequestNumber = "LR-122",
            RequesterUserId = 20,
            StudentId = 1,
            RequestType = RequestTypes.Leave,
            Status = RequestStatus.Submitted,
            LeaveRequest = new LeaveRequest { Id = 122, StartDate = DefaultTestDate, EndDate = DefaultTestDate, Session = LeaveSession.FullDay, Reason = "Xin nghỉ học" }
        };
        SetupRepository(new List<AdministrativeRequest> { req });
        SetupRepository(new List<SchoolCalendarDay>());

        var shortMoreInfo = await _service.ReviewLeaveRequestAsync(122, 99, new ReviewLeaveRequestDto
        {
            IsRequestMoreInfo = true,
            ReviewerNotes = "ok"
        }, new[] { UserRoles.SchoolBoard });

        var shortReject = await _service.ReviewLeaveRequestAsync(122, 99, new ReviewLeaveRequestDto
        {
            IsApproved = false,
            ReviewerNotes = "no"
        }, new[] { UserRoles.SchoolBoard });

        shortMoreInfo.Success.Should().BeFalse();
        shortMoreInfo.Code.Should().Be(SystemMessages.MSG01.Code);
        shortReject.Success.Should().BeFalse();
        shortReject.Code.Should().Be(SystemMessages.MSG01.Code);
    }

    [Fact]
    public async Task LD_UpdateNeedMoreInfo_MissingRequestedAttachmentOrNoChanges_FailsWithMSG01()
    {
        var req = new AdministrativeRequest
        {
            Id = 123,
            RequestNumber = "LR-123",
            RequesterUserId = 20,
            StudentId = 1,
            RequestType = RequestTypes.Leave,
            Status = RequestStatus.NeedMoreInfo,
            RequiresAmendment = true,
            AmendmentNotes = "Vui lòng đính kèm giấy khám bệnh",
            LeaveRequest = new LeaveRequest { Id = 123, StartDate = DefaultTestDate, EndDate = DefaultTestDate, Session = LeaveSession.FullDay, Reason = "Cháu bị sốt cao" }
        };
        SetupRepository(new List<AdministrativeRequest> { req });
        SetupRepository(new List<SchoolCalendarDay>());

        // Gửi lại không có tệp đính kèm khi yêu cầu bổ sung giấy khám -> phải chặn
        var res = await _service.UpdateLeaveRequestAsync(123, 20, new UpdateLeaveRequestDto
        {
            StartDate = DefaultTestDate,
            EndDate = DefaultTestDate,
            Session = LeaveSession.FullDay,
            Reason = "Cháu bị sốt cao 39 độ",
            DoctorNoteAttachmentUrl = null
        });

        res.Success.Should().BeFalse();
        res.Code.Should().Be(SystemMessages.MSG01.Code);
        res.Errors.Should().Contain(e => e.Contains("giấy khám"));
    }

    [Fact]
    public async Task LE_GetMyLeaveRequests_DistinguishesWithdrawnFromCancelled()
    {
        var withdrawnReq = new AdministrativeRequest
        {
            Id = 124,
            RequestNumber = "LR-124",
            RequesterUserId = 20,
            StudentId = 1,
            RequestType = RequestTypes.Leave,
            Status = RequestStatus.Cancelled,
            SubmissionDate = DateTime.UtcNow.AddHours(-2),
            LeaveRequest = new LeaveRequest { Id = 124, StartDate = DefaultTestDate, EndDate = DefaultTestDate, Session = LeaveSession.FullDay, Reason = "Bị ốm nhẹ" }
        };
        var cancelledReq = new AdministrativeRequest
        {
            Id = 125,
            RequestNumber = "LR-125",
            RequesterUserId = 20,
            StudentId = 1,
            RequestType = RequestTypes.Leave,
            Status = RequestStatus.Cancelled,
            SubmissionDate = DateTime.UtcNow.AddHours(-1),
            LeaveRequest = new LeaveRequest { Id = 125, StartDate = DefaultTestDate, EndDate = DefaultTestDate, Session = LeaveSession.FullDay, Reason = "Việc gia đình" }
        };
        SetupRepository(new List<AdministrativeRequest> { withdrawnReq, cancelledReq });
        SetupRepository(new List<ClassAssignment>());
        SetupRepository(new List<SchoolCalendarDay>());
        SetupRepository(new List<LunchServiceDay>());
        SetupRepository(new List<AuditLog>
        {
            new() { Id = 1, EntityName = nameof(AdministrativeRequest), EntityId = "124", Action = AuditActions.LeaveRequestWithdraw, Timestamp = DateTime.UtcNow },
            new() { Id = 2, EntityName = nameof(AdministrativeRequest), EntityId = "125", Action = AuditActions.LeaveRequestCancel, Timestamp = DateTime.UtcNow }
        });
        SetupRepository(new List<User>());

        var res = await _service.GetMyLeaveRequestsAsync(20);

        res.Success.Should().BeTrue();
        var dto124 = res.Data!.Single(x => x.Id == 124);
        var dto125 = res.Data!.Single(x => x.Id == 125);
        dto124.IsWithdrawn.Should().BeTrue();
        dto124.StatusName.Should().Be("Đã thu hồi");
        dto125.IsWithdrawn.Should().BeFalse();
        dto125.StatusName.Should().Be("Đã hủy");
    }

    // ==================== Round 3 Regression Tests (LV-01..04, GEN-01) ====================

    [Fact]
    public async Task LV01_SubmitLeaveRequest_BeyondActiveSchoolYearEnd_FailsWithMSG01()
    {
        // Arrange
        var guardian = new Guardian
        {
            Id = 1,
            UserId = 20,
            StudentGuardians = new List<StudentGuardian> { new() { StudentId = 1, GuardianId = 1 } }
        };
        SetupRepository(new List<Guardian> { guardian });
        SetupRepository(new List<SchoolYear>
        {
            new() { Id = 1, YearName = "2026-2027", StartDate = new DateTime(2026, 9, 1), EndDate = new DateTime(2027, 5, 31), IsActive = true }
        });
        SetupRepository(new List<SchoolCalendarDay>());
        SetupRepository(new List<AdministrativeRequest>());

        // Act: đặt ngày nghỉ sau ngày kết thúc năm học (15/06/2027 > 31/05/2027)
        var res = await _service.SubmitLeaveRequestAsync(20, new CreateLeaveRequestDto
        {
            StudentId = 1,
            StartDate = new DateTime(2027, 6, 15),
            EndDate = new DateTime(2027, 6, 15),
            Session = LeaveSession.FullDay,
            Reason = "Xin nghỉ quá xa trong tương lai"
        });

        // Assert
        res.Success.Should().BeFalse();
        res.Code.Should().Be(SystemMessages.MSG01.Code);
        res.Errors.Should().Contain(e => e.Contains("31/05/2027"));
    }

    [Fact]
    public async Task LV02_SubmitLeaveRequest_MoreThan5Attachments_FailsWithMSG01()
    {
        // Arrange
        var guardian = new Guardian
        {
            Id = 1,
            UserId = 20,
            StudentGuardians = new List<StudentGuardian> { new() { StudentId = 1, GuardianId = 1 } }
        };
        SetupRepository(new List<Guardian> { guardian });
        SetupRepository(new List<SchoolCalendarDay>());
        SetupRepository(new List<AdministrativeRequest>());

        var sixFiles = string.Join(" | ", Enumerable.Range(1, 6).Select(i => $"/api/LeaveRequests/attachment-file?path=attachments/f{i}.pdf"));

        // Act
        var res = await _service.SubmitLeaveRequestAsync(20, new CreateLeaveRequestDto
        {
            StudentId = 1,
            StartDate = DefaultTestDate,
            EndDate = DefaultTestDate,
            Session = LeaveSession.FullDay,
            Reason = "Đính kèm 6 tệp minh chứng",
            DoctorNoteAttachmentUrl = sixFiles
        });

        // Assert
        res.Success.Should().BeFalse();
        res.Code.Should().Be(SystemMessages.MSG01.Code);
        res.Errors.Should().Contain(e => e.Contains("tối đa 5"));
    }

    [Fact]
    public async Task LV03_SubmitLeaveRequest_KeepLunchService_ValidatedAgainstSessionAndActiveLunch()
    {
        // Arrange
        var guardian = new Guardian
        {
            Id = 1,
            UserId = 20,
            StudentGuardians = new List<StudentGuardian> { new() { StudentId = 1, GuardianId = 1 } }
        };
        SetupRepository(new List<Guardian> { guardian });
        SetupRepository(new List<SchoolCalendarDay>());
        var adminReqs = new List<AdministrativeRequest>();
        var leaveReqs = new List<LeaveRequest>();
        SetupRepository(adminReqs);
        var leaveRepoMock = SetupRepository(leaveReqs);
        leaveRepoMock.Setup(r => r.AddAsync(It.IsAny<LeaveRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((LeaveRequest lr, CancellationToken _) =>
            {
                lr.Id = 1;
                leaveReqs.Add(lr);
                var parent = adminReqs.Find(a => a.Id == lr.AdministrativeRequestId);
                if (parent != null) parent.LeaveRequest = lr;
                return lr;
            });
        SetupRepository(new List<ClassAssignment>());
        SetupRepository(new List<Notification>());
        var lunchRegs = new List<LunchRegistration>();
        SetupRepository(lunchRegs);

        // Act 1: Nghỉ cả ngày + giữ suất ăn -> chặn MSG01
        var fullDayRes = await _service.SubmitLeaveRequestAsync(20, new CreateLeaveRequestDto
        {
            StudentId = 1,
            StartDate = DefaultTestDate,
            EndDate = DefaultTestDate,
            Session = LeaveSession.FullDay,
            KeepLunchService = true,
            Reason = "Nghỉ cả ngày nhưng chọn giữ cơm"
        });

        // Act 2: Nghỉ buổi sáng + giữ suất ăn nhưng HS chưa đăng ký bán trú -> chặn MSG01
        var noLunchRes = await _service.SubmitLeaveRequestAsync(20, new CreateLeaveRequestDto
        {
            StudentId = 1,
            StartDate = DefaultTestDate,
            EndDate = DefaultTestDate,
            Session = LeaveSession.Morning,
            KeepLunchService = true,
            Reason = "Nghỉ sáng, không có đăng ký cơm"
        });

        // Act 3: Có đăng ký bán trú active -> cho phép
        lunchRegs.Add(new LunchRegistration { Id = 1, StudentId = 1, Date = DefaultTestDate, IsActive = true, IsWaitlisted = false });
        var validRes = await _service.SubmitLeaveRequestAsync(20, new CreateLeaveRequestDto
        {
            StudentId = 1,
            StartDate = DefaultTestDate,
            EndDate = DefaultTestDate,
            Session = LeaveSession.Morning,
            KeepLunchService = true,
            Reason = "Nghỉ sáng, trưa vào ăn cơm bán trú"
        });

        // Assert
        fullDayRes.Success.Should().BeFalse();
        fullDayRes.Code.Should().Be(SystemMessages.MSG01.Code);
        noLunchRes.Success.Should().BeFalse();
        noLunchRes.Code.Should().Be(SystemMessages.MSG01.Code);
        validRes.Success.Should().BeTrue();
        validRes.Data!.KeepLunchService.Should().BeTrue();
    }

    [Fact]
    public async Task LV04_ClassWithoutHomeroomTeacher_NotifiesBoard_ShowsInBoardUnderReviewAndSetsStatusName()
    {
        // Arrange: Lớp 1B chưa có GVCN (HomeroomTeacherId = null)
        var class1B = new Class { Id = 2, ClassName = "1B", HomeroomTeacherId = null, HomeroomTeacher = null, Status = "Active" };
        var guardian = new Guardian
        {
            Id = 1,
            UserId = 20,
            StudentGuardians = new List<StudentGuardian> { new() { StudentId = 1, GuardianId = 1 } }
        };
        var boardRole = new Role { Id = 4, Name = "SchoolBoard" };
        SetupRepository(new List<Guardian> { guardian });
        SetupRepository(new List<Class> { class1B });
        SetupRepository(new List<ClassAssignment>
        {
            new() { Id = 1, StudentId = 1, ClassId = 2, Class = class1B, IsActive = true, StartDate = new DateTime(2026, 9, 1) }
        });
        SetupRepository(new List<Role> { boardRole });
        SetupRepository(new List<UserRole> { new() { UserId = 99, RoleId = 4, Role = boardRole } });
        var notifRepoMock = SetupRepository(new List<Notification>());
        SetupRepository(new List<SchoolCalendarDay>());
        SetupRepository(new List<LunchServiceDay>());
        SetupRepository(new List<AuditLog>());

        var adminReqs = new List<AdministrativeRequest>();
        var leaveReqs = new List<LeaveRequest>();
        var arRepoMock = SetupRepository(adminReqs);
        arRepoMock.Setup(r => r.AddAsync(It.IsAny<AdministrativeRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((AdministrativeRequest ar, CancellationToken _) =>
            {
                ar.Id = 301;
                adminReqs.Add(ar);
                return ar;
            });
        var lrRepoMock = SetupRepository(leaveReqs);
        lrRepoMock.Setup(r => r.AddAsync(It.IsAny<LeaveRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((LeaveRequest lr, CancellationToken _) =>
            {
                lr.Id = 301;
                leaveReqs.Add(lr);
                var parent = adminReqs.Find(a => a.Id == lr.AdministrativeRequestId);
                if (parent != null) parent.LeaveRequest = lr;
                return lr;
            });

        // Act 1: Phụ huynh gửi đơn 1 ngày cho HS lớp chưa có GVCN
        var submitRes = await _service.SubmitLeaveRequestAsync(20, new CreateLeaveRequestDto
        {
            StudentId = 1,
            StartDate = DefaultTestDate,
            EndDate = DefaultTestDate,
            Session = LeaveSession.FullDay,
            Reason = "Cháu bị cảm nhẹ xin nghỉ 1 ngày"
        });

        // Act 2: Ban Giám Hiệu mở tab Chờ BGH duyệt (UnderReview)
        var boardQueueRes = await _service.GetClassLeaveRequestsAsync(99, status: "UnderReview");

        // Assert
        submitRes.Success.Should().BeTrue();
        submitRes.Data!.HasHomeroomTeacher.Should().BeFalse();
        submitRes.Data.StatusName.Should().Be("Chờ BGH duyệt (lớp chưa có GVCN)");
        notifRepoMock.Verify(n => n.AddAsync(
            It.Is<Notification>(nf => nf.UserId == 99 && nf.Title.Contains("lớp chưa có GVCN")),
            It.IsAny<CancellationToken>()), Times.Once);
        boardQueueRes.Success.Should().BeTrue();
        boardQueueRes.Data.Should().ContainSingle(x => x.Id == 301 && !x.HasHomeroomTeacher);
    }

    [Fact]
    public async Task GEN01_GetMyLeaveRequests_NormalizesTimestampsToUtcKind()
    {
        var unspec = new DateTime(2026, 11, 24, 2, 0, 0, DateTimeKind.Unspecified);
        var req = new AdministrativeRequest
        {
            Id = 400,
            RequestNumber = "LR-400",
            RequesterUserId = 20,
            StudentId = 1,
            RequestType = RequestTypes.Leave,
            Status = RequestStatus.Approved,
            SubmissionDate = unspec,
            ProcessedDate = unspec.AddMinutes(30),
            LeaveRequest = new LeaveRequest { Id = 400, StartDate = DefaultTestDate, EndDate = DefaultTestDate, Session = LeaveSession.FullDay, Reason = "Bị cảm" }
        };
        SetupRepository(new List<AdministrativeRequest> { req });
        SetupRepository(new List<ClassAssignment>());
        SetupRepository(new List<SchoolCalendarDay>());
        SetupRepository(new List<LunchServiceDay>());
        SetupRepository(new List<AuditLog>
        {
            new() { Id = 1, EntityName = nameof(AdministrativeRequest), EntityId = "400", Action = AuditActions.LeaveRequestSubmit, Timestamp = unspec }
        });
        SetupRepository(new List<User>());

        var res = await _service.GetMyLeaveRequestsAsync(20);

        res.Success.Should().BeTrue();
        var item = res.Data!.Single();
        item.SubmissionDate.Kind.Should().Be(DateTimeKind.Utc);
        item.ProcessedDate!.Value.Kind.Should().Be(DateTimeKind.Utc);
        item.StatusHistory.Should().ContainSingle();
        item.StatusHistory[0].Timestamp.Kind.Should().Be(DateTimeKind.Utc);
    }

    // ==================== Round 4 Regression Test (AT-09) ====================

    [Fact]
    public async Task AT09_WithdrawApprovedLeave_PreservesTeacherConfirmedExcused_DeletesOnlyAutoGenerated()
    {
        // Arrange: Đơn 2 ngày đã duyệt; ngày 1 GV đã xác nhận (UpdatedByUserId != null), ngày 2 là bản ghi tự sinh (UpdatedByUserId == null)
        var day1 = DefaultTestDate;
        var day2 = DefaultTestDate.AddDays(1);
        _clockMock.Setup(c => c.Today).Returns(day1);
        _clockMock.Setup(c => c.Now).Returns(day1.AddHours(7).AddMinutes(30));

        var teacherConfirmedRec = new AttendanceRecord
        {
            Id = 801,
            StudentId = 1,
            ClassId = 1,
            Date = day1,
            Session = AttendanceSession.Morning,
            Status = AttendanceStatus.ExcusedAbsence,
            LeaveRequestId = 410,
            RecordedByUserId = 30,
            UpdatedByUserId = 30,
            UpdatedAt = DateTime.UtcNow
        };
        var autoGeneratedRec = new AttendanceRecord
        {
            Id = 802,
            StudentId = 1,
            ClassId = 1,
            Date = day2,
            Session = AttendanceSession.Morning,
            Status = AttendanceStatus.ExcusedAbsence,
            LeaveRequestId = 410,
            RecordedByUserId = 30,
            UpdatedByUserId = null,
            UpdatedAt = null
        };

        var attRepoMock = SetupRepository(new List<AttendanceRecord> { teacherConfirmedRec, autoGeneratedRec });
        SetupRepository(new List<LunchRegistration>());
        SetupRepository(new List<LunchServiceDay>());
        SetupRepository(new List<SchoolCalendarDay>());
        SetupRepository(new List<ClassAssignment>
        {
            new() { Id = 1, StudentId = 1, ClassId = 1, IsActive = true, Class = new Class { ClassName = "1A", HomeroomTeacher = new StaffProfile { UserId = 30 } } }
        });
        SetupRepository(new List<Notification>());

        var req = new AdministrativeRequest
        {
            Id = 410,
            RequestNumber = "LR-410",
            RequesterUserId = 20,
            StudentId = 1,
            Student = new Student { Id = 1, FullName = "Trần Gia Bảo" },
            RequestType = RequestTypes.Leave,
            Status = RequestStatus.Approved,
            LeaveRequest = new LeaveRequest { Id = 410, StartDate = day1, EndDate = day2, Session = LeaveSession.Morning }
        };
        SetupRepository(new List<AdministrativeRequest> { req });

        // Act
        var res = await _service.WithdrawLeaveRequestAsync(410, 20, "Cháu đi học lại");

        // Assert: thu hồi toàn phần (cả 2 ngày đều trước giờ chốt), chỉ xóa bản ghi tự sinh và giữ nguyên bản ghi GV đã xác nhận
        res.Success.Should().BeTrue();
        req.Status.Should().Be(RequestStatus.Cancelled);
        attRepoMock.Verify(a => a.DeleteAsync(autoGeneratedRec, It.IsAny<CancellationToken>()), Times.Once);
        attRepoMock.Verify(a => a.DeleteAsync(teacherConfirmedRec, It.IsAny<CancellationToken>()), Times.Never);
    }

    // ==================== Additional Coverage: All 12 LeaveRequest Methods (N/A/B) ====================

    [Fact]
    public async Task SubmitLeaveRequest_Yesterday_FailsWithMSG24()
    {
        // Arrange (Ngày hôm qua không còn được phép nộp)
        var guardian = new Guardian
        {
            Id = 1,
            UserId = 20,
            StudentGuardians = new List<StudentGuardian> { new() { StudentId = 1, GuardianId = 1 } }
        };
        SetupRepository(new List<Guardian> { guardian });

        var request = new CreateLeaveRequestDto
        {
            StudentId = 1,
            StartDate = DefaultTestDate.AddDays(-1),
            EndDate = DefaultTestDate.AddDays(-1),
            Reason = "Xin nghỉ ngày hôm qua"
        };

        // Act
        var res = await _service.SubmitLeaveRequestAsync(20, request);

        // Assert
        res.Success.Should().BeFalse();
        res.Code.Should().Be(SystemMessages.MSG24.Code);
    }

    [Fact]
    public async Task SubmitLeaveRequest_ReasonTooShortOrTooLong_FailsWithMSG01()
    {
        // Arrange
        var guardian = new Guardian
        {
            Id = 1,
            UserId = 20,
            StudentGuardians = new List<StudentGuardian> { new() { StudentId = 1, GuardianId = 1 } }
        };
        SetupRepository(new List<Guardian> { guardian });
        SetupRepository(new List<SchoolYear>());
        SetupRepository(new List<SchoolCalendarDay>());

        // Act
        var shortRes = await _service.SubmitLeaveRequestAsync(20, new CreateLeaveRequestDto
        {
            StudentId = 1,
            StartDate = DefaultTestDate,
            EndDate = DefaultTestDate,
            Reason = "Ốm" // 2 chars < 5
        });
        var longRes = await _service.SubmitLeaveRequestAsync(20, new CreateLeaveRequestDto
        {
            StudentId = 1,
            StartDate = DefaultTestDate,
            EndDate = DefaultTestDate,
            Reason = new string('A', 501)
        });

        // Assert
        shortRes.Success.Should().BeFalse();
        shortRes.Code.Should().Be(SystemMessages.MSG01.Code);
        longRes.Success.Should().BeFalse();
        longRes.Code.Should().Be(SystemMessages.MSG01.Code);
    }

    [Fact]
    public async Task SubmitLeaveRequest_SchoolDaysExceedsMax30_FailsWithMSG01()
    {
        // Arrange: 45 ngày lịch (T3 24/11/2026 -> 07/01/2027) có 32 ngày học (> MaxLeaveSchoolDays = 30)
        var guardian = new Guardian
        {
            Id = 1,
            UserId = 20,
            StudentGuardians = new List<StudentGuardian> { new() { StudentId = 1, GuardianId = 1 } }
        };
        SetupRepository(new List<Guardian> { guardian });
        SetupRepository(new List<SchoolYear>());
        SetupRepository(new List<SchoolCalendarDay>());

        // Act
        var res = await _service.SubmitLeaveRequestAsync(20, new CreateLeaveRequestDto
        {
            StudentId = 1,
            StartDate = DefaultTestDate,
            EndDate = DefaultTestDate.AddDays(44),
            Reason = "Nghỉ điều trị dài ngày"
        });

        // Assert
        res.Success.Should().BeFalse();
        res.Code.Should().Be(SystemMessages.MSG01.Code);
        res.Errors.Should().Contain(e => e.Contains("30 ngày học"));
    }

    [Fact]
    public async Task SubmitLeaveRequest_NoActiveSchoolYearAndBeyond180Days_FailsWithMSG01()
    {
        // Arrange
        var guardian = new Guardian
        {
            Id = 1,
            UserId = 20,
            StudentGuardians = new List<StudentGuardian> { new() { StudentId = 1, GuardianId = 1 } }
        };
        SetupRepository(new List<Guardian> { guardian });
        SetupRepository(new List<SchoolYear>());

        // Act
        var res = await _service.SubmitLeaveRequestAsync(20, new CreateLeaveRequestDto
        {
            StudentId = 1,
            StartDate = DefaultTestDate.AddDays(181),
            EndDate = DefaultTestDate.AddDays(181),
            Reason = "Xin nghỉ quá 180 ngày"
        });

        // Assert
        res.Success.Should().BeFalse();
        res.Code.Should().Be(SystemMessages.MSG01.Code);
        res.Errors.Should().Contain(e => e.Contains("180 ngày"));
    }

    [Fact]
    public async Task GetMyLeaveRequestsPaged_PaginationAndFilters_ReturnsPageAndStats()
    {
        // Arrange
        var baseTime = new DateTime(2026, 11, 24, 1, 0, 0, DateTimeKind.Utc);
        var requests = new List<AdministrativeRequest>
        {
            new()
            {
                Id = 1, RequestNumber = "LR-001", RequesterUserId = 20, StudentId = 1,
                RequestType = RequestTypes.Leave, Status = RequestStatus.Submitted, SubmissionDate = baseTime.AddMinutes(1),
                Student = new Student { Id = 1, FullName = "Em A", StudentCode = "HS001" },
                LeaveRequest = new LeaveRequest { Id = 1, StartDate = DefaultTestDate, EndDate = DefaultTestDate, Reason = "Ốm 1" }
            },
            new()
            {
                Id = 2, RequestNumber = "LR-002", RequesterUserId = 20, StudentId = 1,
                RequestType = RequestTypes.Leave, Status = RequestStatus.Approved, SubmissionDate = baseTime.AddMinutes(2),
                Student = new Student { Id = 1, FullName = "Em A", StudentCode = "HS001" },
                LeaveRequest = new LeaveRequest { Id = 2, StartDate = DefaultTestDate.AddDays(1), EndDate = DefaultTestDate.AddDays(1), Reason = "Ốm 2" }
            },
            new()
            {
                Id = 3, RequestNumber = "LR-003", RequesterUserId = 20, StudentId = 1,
                RequestType = RequestTypes.Leave, Status = RequestStatus.Submitted, SubmissionDate = baseTime.AddMinutes(3),
                Student = new Student { Id = 1, FullName = "Em A", StudentCode = "HS001" },
                LeaveRequest = new LeaveRequest { Id = 3, StartDate = DefaultTestDate.AddDays(2), EndDate = DefaultTestDate.AddDays(2), Reason = "Ốm 3" }
            },
            new()
            {
                Id = 4, RequestNumber = "LR-004", RequesterUserId = 20, StudentId = 1,
                RequestType = RequestTypes.Leave, Status = RequestStatus.Submitted, SubmissionDate = baseTime.AddMinutes(4),
                Student = new Student { Id = 1, FullName = "Em A", StudentCode = "HS001" },
                LeaveRequest = new LeaveRequest { Id = 4, StartDate = DefaultTestDate.AddDays(3), EndDate = DefaultTestDate.AddDays(3), Reason = "Ốm 4" }
            },
            new()
            {
                Id = 5, RequestNumber = "LR-005", RequesterUserId = 20, StudentId = 2,
                RequestType = RequestTypes.Leave, Status = RequestStatus.Submitted, SubmissionDate = baseTime.AddMinutes(5),
                Student = new Student { Id = 2, FullName = "Em B", StudentCode = "HS002" },
                LeaveRequest = new LeaveRequest { Id = 5, StartDate = DefaultTestDate, EndDate = DefaultTestDate, Reason = "Con khác" }
            }
        };
        SetupRepository(requests);
        SetupRepository(new List<ClassAssignment>());
        SetupRepository(new List<SchoolCalendarDay>());
        SetupRepository(new List<LunchServiceDay>());
        SetupRepository(new List<AuditLog>());

        // Act: lọc studentId=1, status="Submitted", trang 2 size 2 -> còn 1 đơn (LR-001)
        var res = await _service.GetMyLeaveRequestsPagedAsync(20, studentId: 1, status: "Submitted", pageIndex: 2, pageSize: 2);

        // Assert
        res.Success.Should().BeTrue();
        res.Data!.TotalCount.Should().Be(3);
        res.Data.TotalPages.Should().Be(2);
        res.Data.PageIndex.Should().Be(2);
        res.Data.PageSize.Should().Be(2);
        res.Data.Items.Should().ContainSingle(x => x.RequestNumber == "LR-001");
        res.Data.Stats.TotalCount.Should().Be(4);
        res.Data.Stats.SubmittedCount.Should().Be(3);
        res.Data.Stats.ApprovedCount.Should().Be(1);
    }

    [Fact]
    public async Task GetMyLeaveRequestsPaged_EmptyOrPageOutOfRange_ReturnsEmptyItems()
    {
        // Arrange
        var requests = new List<AdministrativeRequest>
        {
            new()
            {
                Id = 1, RequestNumber = "LR-001", RequesterUserId = 20, StudentId = 1,
                RequestType = RequestTypes.Leave, Status = RequestStatus.Submitted, SubmissionDate = DateTime.UtcNow,
                LeaveRequest = new LeaveRequest { Id = 1, StartDate = DefaultTestDate, EndDate = DefaultTestDate, Reason = "Ốm" }
            }
        };
        SetupRepository(requests);

        // Act
        var emptyParent = await _service.GetMyLeaveRequestsPagedAsync(999);
        var outOfRange = await _service.GetMyLeaveRequestsPagedAsync(20, pageIndex: 5, pageSize: 10);

        // Assert
        emptyParent.Success.Should().BeTrue();
        emptyParent.Data!.TotalCount.Should().Be(0);
        emptyParent.Data.Items.Should().BeEmpty();

        outOfRange.Success.Should().BeTrue();
        outOfRange.Data!.TotalCount.Should().Be(1);
        outOfRange.Data.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task GetMyLeaveRequestsPaged_BoundaryPageIndexAndPageSize_ClampedCorrectly()
    {
        // Arrange
        SetupRepository(new List<AdministrativeRequest>());

        // Act
        var lowClamp = await _service.GetMyLeaveRequestsPagedAsync(20, status: "ALL", pageIndex: 0, pageSize: -5);
        var highClamp = await _service.GetMyLeaveRequestsPagedAsync(20, status: null, pageIndex: -2, pageSize: 500);

        // Assert
        lowClamp.Success.Should().BeTrue();
        lowClamp.Data!.PageIndex.Should().Be(1);
        lowClamp.Data.PageSize.Should().Be(1);
        highClamp.Success.Should().BeTrue();
        highClamp.Data!.PageIndex.Should().Be(1);
        highClamp.Data.PageSize.Should().Be(100);
    }

    [Fact]
    public async Task GetClassLeaveRequestsPaged_HomeroomTeacher_ReturnsPagedWithStats()
    {
        // Arrange
        var class1A = new Class { Id = 1, ClassName = "1A", HomeroomTeacherId = 10 };
        var class1B = new Class { Id = 2, ClassName = "1B", HomeroomTeacherId = 99 };
        SetupRepository(new List<UserRole> { new() { UserId = 30, Role = new Role { Name = "Teacher" } } });
        SetupRepository(new List<StaffProfile> { new() { Id = 10, UserId = 30 } });
        SetupRepository(new List<Class> { class1A, class1B });
        SetupRepository(new List<ClassAssignment>
        {
            new() { ClassId = 1, StudentId = 1, IsActive = true, Class = class1A },
            new() { ClassId = 2, StudentId = 2, IsActive = true, Class = class1B }
        });
        SetupRepository(new List<SchoolCalendarDay>());
        SetupRepository(new List<LunchServiceDay>());
        SetupRepository(new List<AuditLog>());
        SetupRepository(new List<AdministrativeRequest>
        {
            new()
            {
                Id = 1, RequestNumber = "LR-101", StudentId = 1, RequestType = RequestTypes.Leave, Status = RequestStatus.Submitted,
                SubmissionDate = DateTime.UtcNow.AddMinutes(-10),
                Student = new Student { Id = 1, FullName = "Nguyen Van A", StudentCode = "HS001" },
                LeaveRequest = new LeaveRequest { Id = 1, StartDate = DefaultTestDate, EndDate = DefaultTestDate, Reason = "Ốm" }
            },
            new()
            {
                Id = 2, RequestNumber = "LR-102", StudentId = 1, RequestType = RequestTypes.Leave, Status = RequestStatus.Approved,
                SubmissionDate = DateTime.UtcNow,
                Student = new Student { Id = 1, FullName = "Nguyen Van A", StudentCode = "HS001" },
                LeaveRequest = new LeaveRequest { Id = 2, StartDate = DefaultTestDate.AddDays(1), EndDate = DefaultTestDate.AddDays(1), Reason = "Việc riêng" }
            },
            new()
            {
                Id = 3, RequestNumber = "LR-999", StudentId = 2, RequestType = RequestTypes.Leave, Status = RequestStatus.Submitted,
                SubmissionDate = DateTime.UtcNow,
                Student = new Student { Id = 2, FullName = "Tran Van B", StudentCode = "HS002" },
                LeaveRequest = new LeaveRequest { Id = 3, StartDate = DefaultTestDate, EndDate = DefaultTestDate, Reason = "Lớp khác" }
            }
        });

        // Act
        var res = await _service.GetClassLeaveRequestsPagedAsync(30, pageIndex: 1, pageSize: 1);

        // Assert
        res.Success.Should().BeTrue();
        res.Data!.TotalCount.Should().Be(2);
        res.Data.Items.Should().ContainSingle(x => x.RequestNumber == "LR-102");
        res.Data.Stats.TotalCount.Should().Be(2);
        res.Data.Stats.SubmittedCount.Should().Be(1);
        res.Data.Stats.ApprovedCount.Should().Be(1);
    }

    [Fact]
    public async Task GetClassLeaveRequestsPaged_SchoolBoard_UnderReviewIncludesNoHomeroomAndFiltersByDateAndSearch()
    {
        // Arrange
        var classNoGvcn = new Class { Id = 2, ClassName = "1B", HomeroomTeacherId = null };
        SetupRepository(new List<UserRole> { new() { UserId = 2, Role = new Role { Name = "SchoolBoard" } } });
        SetupRepository(new List<Class> { classNoGvcn });
        SetupRepository(new List<ClassAssignment>
        {
            new() { ClassId = 2, StudentId = 5, IsActive = true, Class = classNoGvcn }
        });
        SetupRepository(new List<SchoolCalendarDay>());
        SetupRepository(new List<LunchServiceDay>());
        SetupRepository(new List<AuditLog>());
        SetupRepository(new List<AdministrativeRequest>
        {
            new()
            {
                Id = 10, RequestNumber = "LR-NO-GVCN", StudentId = 5, RequestType = RequestTypes.Leave, Status = RequestStatus.Submitted,
                SubmissionDate = DateTime.UtcNow,
                Student = new Student { Id = 5, FullName = "Le Minh Tam", StudentCode = "HS005" },
                RequesterUser = new User { Id = 20, FullName = "Phu Huynh Tam" },
                LeaveRequest = new LeaveRequest { Id = 10, StartDate = DefaultTestDate, EndDate = DefaultTestDate.AddDays(1), Reason = "Sốt cao" }
            },
            new()
            {
                Id = 11, RequestNumber = "LR-OTHER-DAY", StudentId = 5, RequestType = RequestTypes.Leave, Status = RequestStatus.UnderReview,
                SubmissionDate = DateTime.UtcNow,
                Student = new Student { Id = 5, FullName = "Le Minh Tam", StudentCode = "HS005" },
                RequesterUser = new User { Id = 20, FullName = "Phu Huynh Tam" },
                LeaveRequest = new LeaveRequest { Id = 11, StartDate = DefaultTestDate.AddDays(5), EndDate = DefaultTestDate.AddDays(8), Reason = "Về quê" }
            }
        });

        // Act
        var res = await _service.GetClassLeaveRequestsPagedAsync(2, status: "UnderReview", date: DefaultTestDate, search: "hs005");

        // Assert
        res.Success.Should().BeTrue();
        res.Data!.TotalCount.Should().Be(1);
        res.Data.Items.Should().ContainSingle(x => x.RequestNumber == "LR-NO-GVCN" && !x.HasHomeroomTeacher);
        res.Data.Stats.UnderReviewCount.Should().Be(1);
    }

    [Fact]
    public async Task GetClassLeaveRequestsPaged_NoStaffProfile_ReturnsEmptyPaged()
    {
        // Arrange
        SetupRepository(new List<UserRole> { new() { UserId = 99, Role = new Role { Name = "Teacher" } } });
        SetupRepository(new List<StaffProfile>());
        SetupRepository(new List<Class>());

        // Act
        var res = await _service.GetClassLeaveRequestsPagedAsync(99);

        // Assert
        res.Success.Should().BeTrue();
        res.Data!.TotalCount.Should().Be(0);
        res.Data.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task GetClassLeaveRequestsPaged_BoundaryPageParamsAndEmptyFilter_Works()
    {
        // Arrange
        var class1A = new Class { Id = 1, ClassName = "1A", HomeroomTeacherId = 10 };
        SetupRepository(new List<UserRole> { new() { UserId = 30, Role = new Role { Name = "Teacher" } } });
        SetupRepository(new List<StaffProfile> { new() { Id = 10, UserId = 30 } });
        SetupRepository(new List<Class> { class1A });
        SetupRepository(new List<ClassAssignment> { new() { ClassId = 1, StudentId = 1, IsActive = true, Class = class1A } });
        SetupRepository(new List<AdministrativeRequest>
        {
            new()
            {
                Id = 1, RequestNumber = "LR-101", StudentId = 1, RequestType = RequestTypes.Leave, Status = RequestStatus.Submitted,
                Student = new Student { Id = 1, FullName = "Em A", StudentCode = "HS001" },
                LeaveRequest = new LeaveRequest { Id = 1, StartDate = DefaultTestDate, EndDate = DefaultTestDate, Reason = "Ốm" }
            }
        });

        // Act
        var res = await _service.GetClassLeaveRequestsPagedAsync(30, search: "KHONG_TIM_THAY", pageIndex: 0, pageSize: 200);

        // Assert
        res.Success.Should().BeTrue();
        res.Data!.PageIndex.Should().Be(1);
        res.Data.PageSize.Should().Be(100);
        res.Data.TotalCount.Should().Be(0);
        res.Data.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task GetClassLeaveRequestsPaged_ParentWithClassId_ReturnsEmptyPaged()
    {
        // Arrange: Phụ huynh truyền classId = 1 -> không rò rỉ dữ liệu phân trang hay thống kê
        var class1A = new Class { Id = 1, ClassName = "1A", HomeroomTeacherId = 10 };
        SetupRepository(new List<UserRole> { new() { UserId = 20, Role = new Role { Name = "Parent" } } });
        SetupRepository(new List<StaffProfile> { new() { Id = 10, UserId = 30 } });
        SetupRepository(new List<Class> { class1A });
        SetupRepository(new List<ClassAssignment> { new() { ClassId = 1, StudentId = 1, IsActive = true, Class = class1A } });
        SetupRepository(new List<AdministrativeRequest>
        {
            new()
            {
                Id = 1, RequestNumber = "LR-101", StudentId = 1, RequestType = RequestTypes.Leave, Status = RequestStatus.Submitted,
                Student = new Student { Id = 1, FullName = "Nguyen Van A", StudentCode = "HS001" },
                LeaveRequest = new LeaveRequest { Id = 1, StartDate = DefaultTestDate, EndDate = DefaultTestDate, Reason = "Ốm" }
            }
        });

        // Act
        var res = await _service.GetClassLeaveRequestsPagedAsync(20, classId: 1);

        // Assert
        res.Success.Should().BeTrue();
        res.Data!.TotalCount.Should().Be(0);
        res.Data.Items.Should().BeEmpty();
        res.Data.Stats.TotalCount.Should().Be(0);
    }

    [Fact]
    public async Task GetClassLeaveRequestsPaged_TeacherWithoutStaffProfileWithClassId_ReturnsEmptyPaged()
    {
        // Arrange: Giáo viên chưa có StaffProfile truyền classId = 1 -> không rò rỉ dữ liệu phân trang hay thống kê
        var class1A = new Class { Id = 1, ClassName = "1A", HomeroomTeacherId = 10 };
        SetupRepository(new List<UserRole> { new() { UserId = 99, Role = new Role { Name = "Teacher" } } });
        SetupRepository(new List<StaffProfile> { new() { Id = 10, UserId = 30 } });
        SetupRepository(new List<Class> { class1A });
        SetupRepository(new List<ClassAssignment> { new() { ClassId = 1, StudentId = 1, IsActive = true, Class = class1A } });
        SetupRepository(new List<AdministrativeRequest>
        {
            new()
            {
                Id = 1, RequestNumber = "LR-101", StudentId = 1, RequestType = RequestTypes.Leave, Status = RequestStatus.Submitted,
                Student = new Student { Id = 1, FullName = "Nguyen Van A", StudentCode = "HS001" },
                LeaveRequest = new LeaveRequest { Id = 1, StartDate = DefaultTestDate, EndDate = DefaultTestDate, Reason = "Ốm" }
            }
        });

        // Act
        var res = await _service.GetClassLeaveRequestsPagedAsync(99, classId: 1);

        // Assert
        res.Success.Should().BeTrue();
        res.Data!.TotalCount.Should().Be(0);
        res.Data.Items.Should().BeEmpty();
        res.Data.Stats.TotalCount.Should().Be(0);
    }

    [Fact]
    public async Task GetLeaveRequestDetails_HomeroomTeacherAllowed_NonHomeroomTeacherDeniedMSG10()
    {
        // Arrange
        var class1A = new Class { Id = 1, ClassName = "1A", HomeroomTeacherId = 10 };
        SetupRepository(new List<StaffProfile>
        {
            new() { Id = 10, UserId = 30 },
            new() { Id = 11, UserId = 31 }
        });
        SetupRepository(new List<ClassAssignment>
        {
            new() { Id = 1, StudentId = 1, ClassId = 1, Class = class1A, IsActive = true, StartDate = DefaultTestDate.AddDays(-10) }
        });
        SetupRepository(new List<SchoolCalendarDay>());
        SetupRepository(new List<LunchServiceDay>());
        SetupRepository(new List<AuditLog>());
        SetupRepository(new List<AdministrativeRequest>
        {
            new()
            {
                Id = 200, RequestNumber = "LR-200", RequesterUserId = 20, StudentId = 1,
                RequestType = RequestTypes.Leave, Status = RequestStatus.Submitted,
                Student = new Student { Id = 1, FullName = "Em A", StudentCode = "HS001" },
                LeaveRequest = new LeaveRequest { Id = 200, StartDate = DefaultTestDate, EndDate = DefaultTestDate, Reason = "Ốm" }
            }
        });

        // Act
        var homeroomRes = await _service.GetLeaveRequestDetailsAsync(200, 30, new[] { UserRoles.Teacher });
        var otherTeacherRes = await _service.GetLeaveRequestDetailsAsync(200, 31, new[] { UserRoles.Teacher });

        // Assert
        homeroomRes.Success.Should().BeTrue();
        homeroomRes.Data!.ClassName.Should().Be("1A");
        otherTeacherRes.Success.Should().BeFalse();
        otherTeacherRes.Code.Should().Be(SystemMessages.MSG10.Code);
    }

    [Fact]
    public async Task ReviewLeaveRequest_SchoolBoardApprovesEscalatedUnderReview_Succeeds()
    {
        // Arrange: đơn 4 ngày học đã ở UnderReview, BGH phê duyệt
        var req = new AdministrativeRequest
        {
            Id = 210, RequestNumber = "LR-210", RequesterUserId = 20, StudentId = 1,
            Student = new Student { Id = 1, FullName = "Em A" },
            RequestType = RequestTypes.Leave, Status = RequestStatus.UnderReview,
            LeaveRequest = new LeaveRequest { Id = 210, StartDate = DefaultTestDate, EndDate = DefaultTestDate.AddDays(3), Session = LeaveSession.FullDay }
        };
        SetupRepository(new List<AdministrativeRequest> { req });
        SetupRepository(new List<SchoolCalendarDay>());
        SetupRepository(new List<LunchServiceDay>());
        SetupRepository(new List<AttendanceRecord>());
        SetupRepository(new List<Notification>());
        _lunchServiceMock.Setup(l => l.CancelMealForLeaveAsync(1, It.IsAny<DateTime>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ApiResponse<bool>.Ok(true));

        // Act
        var res = await _service.ReviewLeaveRequestAsync(210, 88, new ReviewLeaveRequestDto { IsApproved = true, ReviewerNotes = "BGH đồng ý" }, new[] { UserRoles.SchoolBoard });

        // Assert
        res.Success.Should().BeTrue();
        res.Data!.Status.Should().Be(RequestStatus.Approved);
        res.Data.CancelledMealDates.Should().HaveCount(4);
        req.ReviewerNotes.Should().StartWith("[BGH phê duyệt]:");
    }

    [Fact]
    public async Task UpdateLeaveRequest_SubmittedRequest_UpdatesAndLogsResubmit()
    {
        // Arrange: chỉnh sửa đơn đang Submitted trên chính ngày cũ (excludeRequestId tránh báo trùng)
        var req = new AdministrativeRequest
        {
            Id = 220, RequestNumber = "LR-220", RequesterUserId = 20, StudentId = 1,
            Student = new Student { Id = 1, FullName = "Em A", StudentCode = "HS001" },
            RequestType = RequestTypes.Leave, Status = RequestStatus.Submitted, SubmissionDate = DateTime.UtcNow,
            LeaveRequest = new LeaveRequest { Id = 220, StartDate = DefaultTestDate, EndDate = DefaultTestDate, Session = LeaveSession.FullDay, Reason = "Lý do ban đầu" }
        };
        SetupRepository(new List<AdministrativeRequest> { req });
        SetupRepository(new List<SchoolYear>());
        SetupRepository(new List<SchoolCalendarDay>());
        SetupRepository(new List<LunchServiceDay>());
        SetupRepository(new List<ClassAssignment>());
        SetupRepository(new List<Notification>());
        SetupRepository(new List<AuditLog>());

        // Act
        var res = await _service.UpdateLeaveRequestAsync(220, 20, new UpdateLeaveRequestDto
        {
            StartDate = DefaultTestDate,
            EndDate = DefaultTestDate.AddDays(1),
            Session = LeaveSession.Morning,
            Reason = "Cập nhật sang nghỉ 2 buổi sáng"
        });

        // Assert
        res.Success.Should().BeTrue();
        req.Status.Should().Be(RequestStatus.Submitted);
        req.LeaveRequest!.EndDate.Should().Be(DefaultTestDate.AddDays(1));
        req.LeaveRequest.Session.Should().Be(LeaveSession.Morning);
        AuditServiceMock.Verify(a => a.LogActionAsync(20, AuditActions.LeaveRequestResubmit, nameof(AdministrativeRequest), "220",
            It.IsAny<object?>(), It.IsAny<object?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateLeaveRequest_NeedMoreInfo_NoChanges_FailsWithMSG01()
    {
        // Arrange
        var req = new AdministrativeRequest
        {
            Id = 221, RequestNumber = "LR-221", RequesterUserId = 20, StudentId = 1,
            RequestType = RequestTypes.Leave, Status = RequestStatus.NeedMoreInfo, RequiresAmendment = true,
            AmendmentNotes = "Vui lòng bổ sung thông tin lý do chi tiết hơn",
            LeaveRequest = new LeaveRequest { Id = 221, StartDate = DefaultTestDate, EndDate = DefaultTestDate, Session = LeaveSession.FullDay, KeepLunchService = false, Reason = "Cháu bị ốm" }
        };
        SetupRepository(new List<AdministrativeRequest> { req });
        SetupRepository(new List<SchoolYear>());
        SetupRepository(new List<SchoolCalendarDay>());

        // Act: gửi lại y nguyên không thay đổi gì
        var res = await _service.UpdateLeaveRequestAsync(221, 20, new UpdateLeaveRequestDto
        {
            StartDate = DefaultTestDate,
            EndDate = DefaultTestDate,
            Session = LeaveSession.FullDay,
            KeepLunchService = false,
            Reason = "Cháu bị ốm"
        });

        // Assert
        res.Success.Should().BeFalse();
        res.Code.Should().Be(SystemMessages.MSG01.Code);
        res.Errors.Should().Contain(e => e.Contains("bổ sung hoặc chỉnh sửa"));
    }

    [Fact]
    public async Task UpdateLeaveRequest_WrongParentOrNotFound_Fails()
    {
        // Arrange
        var req = new AdministrativeRequest
        {
            Id = 222, RequesterUserId = 20, StudentId = 1, RequestType = RequestTypes.Leave, Status = RequestStatus.Submitted,
            LeaveRequest = new LeaveRequest { Id = 222, StartDate = DefaultTestDate, EndDate = DefaultTestDate, Reason = "Ốm" }
        };
        SetupRepository(new List<AdministrativeRequest> { req });

        var dto = new UpdateLeaveRequestDto { StartDate = DefaultTestDate, EndDate = DefaultTestDate, Reason = "Sửa đơn người khác" };

        // Act
        var wrongParent = await _service.UpdateLeaveRequestAsync(222, 99, dto);
        var notFound = await _service.UpdateLeaveRequestAsync(999, 20, dto);

        // Assert
        wrongParent.Success.Should().BeFalse();
        wrongParent.Message.Should().Contain("Không tìm thấy đơn xin nghỉ học");
        notFound.Success.Should().BeFalse();
        notFound.Message.Should().Contain("Không tìm thấy đơn xin nghỉ học");
    }

    [Fact]
    public async Task UpdateLeaveRequest_ApprovedStatus_FailsWithMSG09()
    {
        // Arrange
        var req = new AdministrativeRequest
        {
            Id = 223, RequesterUserId = 20, StudentId = 1, RequestType = RequestTypes.Leave, Status = RequestStatus.Approved,
            LeaveRequest = new LeaveRequest { Id = 223, StartDate = DefaultTestDate, EndDate = DefaultTestDate, Reason = "Ốm" }
        };
        SetupRepository(new List<AdministrativeRequest> { req });

        // Act
        var res = await _service.UpdateLeaveRequestAsync(223, 20, new UpdateLeaveRequestDto
        {
            StartDate = DefaultTestDate,
            EndDate = DefaultTestDate,
            Reason = "Cố tình sửa đơn đã duyệt"
        });

        // Assert
        res.Success.Should().BeFalse();
        res.Code.Should().Be(SystemMessages.MSG09.Code);
    }

    [Fact]
    public async Task UpdateLeaveRequest_InvalidDates_FailsWithMSG24()
    {
        // Arrange
        var req = new AdministrativeRequest
        {
            Id = 224, RequesterUserId = 20, StudentId = 1, RequestType = RequestTypes.Leave, Status = RequestStatus.Submitted,
            LeaveRequest = new LeaveRequest { Id = 224, StartDate = DefaultTestDate, EndDate = DefaultTestDate, Reason = "Ốm" }
        };
        SetupRepository(new List<AdministrativeRequest> { req });

        // Act
        var pastRes = await _service.UpdateLeaveRequestAsync(224, 20, new UpdateLeaveRequestDto
        {
            StartDate = DefaultTestDate.AddDays(-1),
            EndDate = DefaultTestDate,
            Reason = "Sửa sang ngày quá khứ"
        });
        var invertedRes = await _service.UpdateLeaveRequestAsync(224, 20, new UpdateLeaveRequestDto
        {
            StartDate = DefaultTestDate.AddDays(2),
            EndDate = DefaultTestDate.AddDays(1),
            Reason = "Ngày bắt đầu sau ngày kết thúc"
        });

        // Assert
        pastRes.Success.Should().BeFalse();
        pastRes.Code.Should().Be(SystemMessages.MSG24.Code);
        invertedRes.Success.Should().BeFalse();
        invertedRes.Code.Should().Be(SystemMessages.MSG24.Code);
    }

    [Fact]
    public async Task CancelLeaveRequest_NeedMoreInfoStatus_Succeeds()
    {
        // Arrange
        var req = new AdministrativeRequest { Id = 230, RequesterUserId = 20, Status = RequestStatus.NeedMoreInfo };
        SetupRepository(new List<AdministrativeRequest> { req });

        // Act
        var res = await _service.CancelLeaveRequestAsync(230, 20);

        // Assert
        res.Success.Should().BeTrue();
        req.Status.Should().Be(RequestStatus.Cancelled);
    }

    [Fact]
    public async Task CancelLeaveRequest_WrongParent_Fails()
    {
        // Arrange
        var req = new AdministrativeRequest { Id = 231, RequesterUserId = 20, Status = RequestStatus.Submitted };
        SetupRepository(new List<AdministrativeRequest> { req });

        // Act
        var res = await _service.CancelLeaveRequestAsync(231, 99);

        // Assert
        res.Success.Should().BeFalse();
        res.Message.Should().Contain("Không tìm thấy đơn xin nghỉ học");
    }

    [Fact]
    public async Task WithdrawLeaveRequest_WrongParentOrNonApproved_Fails()
    {
        // Arrange
        var req = new AdministrativeRequest
        {
            Id = 240, RequesterUserId = 20, StudentId = 1, RequestType = RequestTypes.Leave, Status = RequestStatus.Submitted,
            LeaveRequest = new LeaveRequest { Id = 240, StartDate = DefaultTestDate, EndDate = DefaultTestDate, Session = LeaveSession.FullDay }
        };
        SetupRepository(new List<AdministrativeRequest> { req });

        // Act
        var wrongParent = await _service.WithdrawLeaveRequestAsync(240, 99);
        var notApproved = await _service.WithdrawLeaveRequestAsync(240, 20);

        // Assert
        wrongParent.Success.Should().BeFalse();
        wrongParent.Message.Should().Contain("Không tìm thấy đơn xin nghỉ học");
        notApproved.Success.Should().BeFalse();
        notApproved.Message.Should().Contain("đã được phê duyệt");
    }

    [Fact]
    public async Task WithdrawLeaveRequest_LockedServiceDayBeforeCutoffTime_FailsWithMSG27()
    {
        // Arrange: 07:30 < 08:15 nhưng bếp đã khóa sổ (IsLocked = true)
        _clockMock.Setup(c => c.Now).Returns(DefaultTestDate.AddHours(7).AddMinutes(30));
        var req = new AdministrativeRequest
        {
            Id = 241, RequesterUserId = 20, StudentId = 1, RequestType = RequestTypes.Leave, Status = RequestStatus.Approved,
            LeaveRequest = new LeaveRequest { Id = 241, StartDate = DefaultTestDate, EndDate = DefaultTestDate, Session = LeaveSession.FullDay }
        };
        SetupRepository(new List<AdministrativeRequest> { req });
        SetupRepository(new List<SchoolCalendarDay>());
        SetupRepository(new List<LunchServiceDay> { new() { Id = 1, Date = DefaultTestDate, IsLocked = true } });

        // Act
        var res = await _service.WithdrawLeaveRequestAsync(241, 20, "Xin thu hồi");

        // Assert
        res.Success.Should().BeFalse();
        res.Code.Should().Be(SystemMessages.MSG27.Code);
    }

    [Fact]
    public async Task UploadAttachment_ValidPdfAndJpg_ReturnsUploadResult()
    {
        // Arrange
        _fileStorageMock.Setup(f => f.SaveFileAsync(It.IsAny<Stream>(), "giaykham.pdf", "attachments", It.IsAny<CancellationToken>()))
            .ReturnsAsync("attachments\\2026_giaykham.pdf");
        _fileStorageMock.Setup(f => f.SaveFileAsync(It.IsAny<Stream>(), "donthuoc.JPG", "attachments", It.IsAny<CancellationToken>()))
            .ReturnsAsync("attachments/2026_donthuoc.JPG");
        using var ms = new MemoryStream(new byte[] { 1, 2, 3, 4 });

        // Act
        var pdfRes = await _service.UploadAttachmentAsync(ms, "giaykham.pdf", 4);
        var jpgRes = await _service.UploadAttachmentAsync(ms, "donthuoc.JPG", 4);

        // Assert
        pdfRes.Success.Should().BeTrue();
        pdfRes.Data!.SavedPath.Should().Be("attachments/2026_giaykham.pdf");
        pdfRes.Data.Url.Should().Be($"/api/LeaveRequests/attachment-file?path={Uri.EscapeDataString("attachments/2026_giaykham.pdf")}");
        jpgRes.Success.Should().BeTrue();
        jpgRes.Data!.FileSize.Should().Be(4);
    }

    [Fact]
    public async Task UploadAttachment_EmptyLengthZero_Fails()
    {
        // Arrange
        using var ms = new MemoryStream();

        // Act
        var res = await _service.UploadAttachmentAsync(ms, "empty.pdf", 0);

        // Assert
        res.Success.Should().BeFalse();
        res.Message.Should().Contain("chọn tệp tin");
    }

    [Fact]
    public async Task UploadAttachment_ExceedsMaxSize_Fails_AndExact10MbSucceeds()
    {
        // Arrange
        const long maxBytes = 10 * 1024 * 1024;
        _fileStorageMock.Setup(f => f.SaveFileAsync(It.IsAny<Stream>(), "exact10mb.png", "attachments", It.IsAny<CancellationToken>()))
            .ReturnsAsync("attachments/exact10mb.png");
        using var ms = new MemoryStream(new byte[] { 1 });

        // Act
        var tooBigRes = await _service.UploadAttachmentAsync(ms, "big.pdf", maxBytes + 1);
        var exactRes = await _service.UploadAttachmentAsync(ms, "exact10mb.png", maxBytes);

        // Assert
        tooBigRes.Success.Should().BeFalse();
        tooBigRes.Message.Should().Contain("10MB");
        exactRes.Success.Should().BeTrue();
    }

    [Fact]
    public async Task UploadAttachment_InvalidExtension_FailsWithMSG41()
    {
        // Arrange
        using var ms = new MemoryStream(new byte[] { 1, 2 });

        // Act
        var res = await _service.UploadAttachmentAsync(ms, "payload.exe", 2);

        // Assert
        res.Success.Should().BeFalse();
        res.Code.Should().Be(SystemMessages.MSG41.Code);
    }

    [Fact]
    public async Task GetAttachment_HomeroomTeacherAndSchoolBoard_AllowedWithCorrectContentType()
    {
        // Arrange
        var jpgPath = "attachments/1234_photo.jpg";
        var pngPath = "attachments/1234_scan.png";
        var class1A = new Class { Id = 1, ClassName = "1A", HomeroomTeacherId = 10 };
        SetupRepository(new List<StaffProfile> { new() { Id = 10, UserId = 30 } });
        SetupRepository(new List<ClassAssignment> { new() { Id = 1, StudentId = 1, ClassId = 1, Class = class1A, IsActive = true, StartDate = DefaultTestDate.AddDays(-5) } });
        SetupRepository(new List<AdministrativeRequest>
        {
            new()
            {
                Id = 250, RequesterUserId = 20, StudentId = 1, RequestType = RequestTypes.Leave,
                LeaveRequest = new LeaveRequest { Id = 250, DoctorNoteAttachmentUrl = $"/api/LeaveRequests/attachment-file?path={Uri.EscapeDataString(jpgPath)}" }
            },
            new()
            {
                Id = 251, RequesterUserId = 20, StudentId = 1, RequestType = RequestTypes.Leave,
                LeaveRequest = new LeaveRequest { Id = 251, DoctorNoteAttachmentUrl = $"/api/LeaveRequests/attachment-file?path={Uri.EscapeDataString(pngPath)}" }
            }
        });
        _fileStorageMock.Setup(f => f.GetFileAsync(jpgPath, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MemoryStream(new byte[] { 1, 2 }));
        _fileStorageMock.Setup(f => f.GetFileAsync(pngPath, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MemoryStream(new byte[] { 3, 4 }));

        // Act
        var teacherFile = await _service.GetAttachmentAsync(jpgPath, 30, new[] { UserRoles.Teacher });
        var boardFile = await _service.GetAttachmentAsync(pngPath, 88, new[] { UserRoles.SchoolBoard });

        // Assert
        teacherFile.Should().NotBeNull();
        teacherFile!.ContentType.Should().Be("image/jpeg");
        boardFile.Should().NotBeNull();
        boardFile!.ContentType.Should().Be("image/png");
    }

    [Fact]
    public async Task GetAttachment_NonHomeroomTeacherAndUnlinkedParent_Blocked()
    {
        // Arrange
        var savedPath = "attachments/1234_doc.pdf";
        var class1A = new Class { Id = 1, ClassName = "1A", HomeroomTeacherId = 10 };
        SetupRepository(new List<StaffProfile> { new() { Id = 99, UserId = 45 } });
        SetupRepository(new List<ClassAssignment> { new() { Id = 1, StudentId = 1, ClassId = 1, Class = class1A, IsActive = true } });
        SetupRepository(new List<AdministrativeRequest>
        {
            new()
            {
                Id = 252, RequesterUserId = 20, StudentId = 1, RequestType = RequestTypes.Leave,
                LeaveRequest = new LeaveRequest { Id = 252, DoctorNoteAttachmentUrl = $"/api/LeaveRequests/attachment-file?path={Uri.EscapeDataString(savedPath)}" }
            }
        });

        // Act
        var otherTeacher = await _service.GetAttachmentAsync(savedPath, 45, new[] { UserRoles.Teacher });
        var unlinkedParent = await _service.GetAttachmentAsync(savedPath, 99, new[] { UserRoles.Parent });

        // Assert
        otherTeacher.Should().BeNull();
        unlinkedParent.Should().BeNull();
    }

    [Fact]
    public async Task GetAttachment_InvalidUserOrInvalidExtensionOrMissingFile_ReturnsNull()
    {
        // Arrange
        var missingPath = "attachments/missing.pdf";
        SetupRepository(new List<AdministrativeRequest>());
        _fileStorageMock.Setup(f => f.GetFileAsync(missingPath, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Stream?)null);

        // Act
        var zeroUser = await _service.GetAttachmentAsync(missingPath, 0, new[] { UserRoles.Parent });
        var badExt = await _service.GetAttachmentAsync("attachments/script.exe", 20, new[] { UserRoles.Parent });
        var missingFile = await _service.GetAttachmentAsync(missingPath, 20, new[] { UserRoles.Parent });

        // Assert
        zeroUser.Should().BeNull();
        badExt.Should().BeNull();
        missingFile.Should().BeNull();
    }
}

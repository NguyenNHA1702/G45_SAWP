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
    private readonly Mock<ILunchService> _lunchServiceMock;
    private readonly Mock<ISchoolClock> _clockMock;
    private readonly LeaveRequestService _service;

    public LeaveRequestServiceTests()
    {
        _lunchServiceMock = new Mock<ILunchService>();
        _clockMock = new Mock<ISchoolClock>();
        _clockMock.Setup(c => c.Today).Returns(DateTime.Today);
        _clockMock.Setup(c => c.Now).Returns(DateTime.Now);

        var options = Options.Create(new SchoolOptions { LunchCutoffTime = "08:15" });
        _service = new LeaveRequestService(UnitOfWorkMock.Object, _lunchServiceMock.Object, _clockMock.Object, options);
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
            StartDate = DateTime.Today,
            EndDate = DateTime.Today,
            Session = LeaveSession.FullDay,
            KeepLunchService = false,
            Reason = "Sốt virus"
        };

        // Act
        var result = await _service.SubmitLeaveRequestAsync(20, request);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Code.Should().Be(SystemMessages.MSG08.Code);
        result.Data.Should().NotBeNull();
        result.Data!.StudentId.Should().Be(1);
    }

    [Fact]
    public async Task UTCID02_SubmitLeaveRequest_GuardianNotFound_Fails()
    {
        // Arrange
        SetupRepository(new List<Guardian>());
        var request = new CreateLeaveRequestDto { StudentId = 1, StartDate = DateTime.Today, EndDate = DateTime.Today, Reason = "Ốm" };

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
        var request = new CreateLeaveRequestDto { StudentId = 1, StartDate = DateTime.Today, EndDate = DateTime.Today, Reason = "Ốm" };

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
            StartDate = DateTime.Today.AddDays(-8),
            EndDate = DateTime.Today.AddDays(-8),
            Reason = "Nghỉ bù"
        };

        // Act
        var result = await _service.SubmitLeaveRequestAsync(20, request);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("quá 7 ngày trước");
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
            StartDate = DateTime.Today.AddDays(2),
            EndDate = DateTime.Today.AddDays(1),
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
        var request = new CreateLeaveRequestDto { StudentId = 1, StartDate = DateTime.Today, EndDate = DateTime.Today, Reason = "   " };

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
            LeaveRequest = new LeaveRequest { StartDate = DateTime.Today, EndDate = DateTime.Today }
        };
        SetupRepository(new List<AdministrativeRequest> { existingLeave });
        var request = new CreateLeaveRequestDto { StudentId = 1, StartDate = DateTime.Today, EndDate = DateTime.Today, Reason = "Ốm" };

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
                LeaveRequest = new LeaveRequest { StartDate = DateTime.Today, EndDate = DateTime.Today, Reason = "Ốm" }
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
                LeaveRequest = new LeaveRequest { StartDate = DateTime.Today, EndDate = DateTime.Today, Reason = "Ốm" }
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
                LeaveRequest = new LeaveRequest { StartDate = DateTime.Today, EndDate = DateTime.Today, Reason = "Ốm" }
            }
        });

        // Act
        var result = await _service.GetClassLeaveRequestsAsync(30, 1, "Submitted", DateTime.Today);

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
                LeaveRequest = new LeaveRequest { StartDate = DateTime.Today, EndDate = DateTime.Today.AddDays(3), Reason = "Nghỉ dài ngày" }
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

    // ==================== 49. GetLeaveRequestDetailsAsync (2 TCs) ====================

    [Fact]
    public async Task UTCID01_GetLeaveRequestDetails_Found_ReturnsDto()
    {
        // Arrange
        var req = new AdministrativeRequest
        {
            Id = 10,
            RequestNumber = "LR-20261028-001",
            StudentId = 1,
            RequestType = RequestTypes.Leave,
            Status = RequestStatus.Submitted,
            Student = new Student { Id = 1, FullName = "Nguyen Van A", StudentCode = "HS001" },
            LeaveRequest = new LeaveRequest { StartDate = DateTime.Today, EndDate = DateTime.Today, Reason = "Ốm" }
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
            LeaveRequest = new LeaveRequest { Id = 10, StartDate = DateTime.Today, EndDate = DateTime.Today, Session = LeaveSession.FullDay, KeepLunchService = false }
        };
        SetupRepository(new List<AdministrativeRequest> { req });
        SetupRepository(new List<ClassAssignment> { new() { StudentId = 1, ClassId = 1, IsActive = true } });
        SetupRepository(new List<AttendanceRecord>());
        SetupRepository(new List<AuditLog>());
        SetupRepository(new List<Notification>());
        _lunchServiceMock.Setup(l => l.CancelMealForLeaveAsync(1, DateTime.Today, It.IsAny<string>(), It.IsAny<CancellationToken>()))
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
            LeaveRequest = new LeaveRequest { StartDate = DateTime.Today, EndDate = DateTime.Today.AddDays(2) }
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
            LeaveRequest = new LeaveRequest { StartDate = DateTime.Today, EndDate = DateTime.Today }
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
            LeaveRequest = new LeaveRequest { StartDate = DateTime.Today, EndDate = DateTime.Today }
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
            LeaveRequest = new LeaveRequest { StartDate = DateTime.Today, EndDate = DateTime.Today }
        };
        SetupRepository(new List<AdministrativeRequest> { req });
        var review = new ReviewLeaveRequestDto { IsForwardToSchoolBoard = true };

        // Act
        var result = await _service.ReviewLeaveRequestAsync(1, 30, review);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("Chỉ đơn xin nghỉ từ 3 ngày trở lên");
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
            LeaveRequest = new LeaveRequest { StartDate = DateTime.Today, EndDate = DateTime.Today }
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
}

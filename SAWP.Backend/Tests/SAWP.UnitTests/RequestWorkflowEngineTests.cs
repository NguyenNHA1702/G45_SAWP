using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Core.Constants;
using Core.DTOs;
using Core.Entities;
using Core.Enums;
using FluentAssertions;
using Moq;
using Services.Implementations;
using Xunit;

namespace SAWP.UnitTests;

public class RequestWorkflowEngineTests : TestBase
{
    private readonly RequestWorkflowEngine _service;

    public RequestWorkflowEngineTests()
    {
        _service = new RequestWorkflowEngine(UnitOfWorkMock.Object, AuditServiceMock.Object);

        SetupRepository(new List<UserRole>());
        SetupRepository(new List<Notification>());
        SetupRepository(new List<StudentGuardian>());
        SetupRepository(new List<ClassAssignment>());
        SetupRepository(new List<Class>());
        SetupRepository(new List<AdministrativeRequest>());
        SetupRepository(new List<ClassTransferDetail>());
        SetupRepository(new List<DocumentRequestDetail>());
        SetupRepository(new List<Guardian>());
    }

    #region Method 33: SubmitClassTransferRequestAsync (UTCID01 - UTCID08)

    [Fact]
    public async Task UTCID01_SubmitClassTransfer_Success()
    {
        // Arrange
        var guardian = new Guardian
        {
            Id = 1,
            UserId = 20,
            StudentGuardians = new List<StudentGuardian> { new() { StudentId = 10, GuardianId = 1 } }
        };
        SetupRepository(new List<Guardian> { guardian });

        var student = new Student { Id = 10, FullName = "Em A" };
        StudentRepoMock.Setup(r => r.GetByIdAsync(10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(student);

        var currentClass = new Class { Id = 1, ClassName = "1A", Grade = 1 };
        var targetClass = new Class { Id = 2, ClassName = "1B", Grade = 1 };
        SetupRepository(new List<Class> { currentClass, targetClass });

        var assignment = new ClassAssignment { Id = 100, StudentId = 10, ClassId = 1, Class = currentClass, IsActive = true };
        SetupRepository(new List<ClassAssignment> { assignment });

        var request = new ClassTransferRequestDto
        {
            StudentId = 10,
            TargetClassId = 2,
            TransferReason = "Chuyển gần nhà bạn thân"
        };

        // Act
        var result = await _service.SubmitClassTransferRequestAsync(20, request);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Code.Should().Be(SystemMessages.MSG30.Code);
    }

    [Fact]
    public async Task UTCID02_SubmitClassTransfer_MissingRequiredFields_Fails()
    {
        // Arrange
        var request = new ClassTransferRequestDto { StudentId = 0, TargetClassId = 0, TransferReason = "" };

        // Act
        var result = await _service.SubmitClassTransferRequestAsync(20, request);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.Code.Should().Be(SystemMessages.MSG01.Code);
    }

    [Fact]
    public async Task UTCID03_SubmitClassTransfer_RequesterNotLinked_Fails()
    {
        // Arrange
        SetupRepository(new List<Guardian>()); // No guardian or unlinked
        var request = new ClassTransferRequestDto { StudentId = 10, TargetClassId = 2, TransferReason = "Reason" };

        // Act
        var result = await _service.SubmitClassTransferRequestAsync(20, request);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.Code.Should().Be(SystemMessages.MSG10.Code);
    }

    [Fact]
    public async Task UTCID04_SubmitClassTransfer_StudentNotFound_Fails()
    {
        // Arrange
        var guardian = new Guardian
        {
            Id = 1,
            UserId = 20,
            StudentGuardians = new List<StudentGuardian> { new() { StudentId = 999, GuardianId = 1 } }
        };
        SetupRepository(new List<Guardian> { guardian });
        StudentRepoMock.Setup(r => r.GetByIdAsync(999, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Student?)null);

        var request = new ClassTransferRequestDto { StudentId = 999, TargetClassId = 2, TransferReason = "Reason" };

        // Act
        var result = await _service.SubmitClassTransferRequestAsync(20, request);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.Code.Should().Be(SystemMessages.MSG11.Code);
    }

    [Fact]
    public async Task UTCID05_SubmitClassTransfer_StudentHasNoActiveClass_Fails()
    {
        // Arrange
        var guardian = new Guardian
        {
            Id = 1,
            UserId = 20,
            StudentGuardians = new List<StudentGuardian> { new() { StudentId = 10, GuardianId = 1 } }
        };
        SetupRepository(new List<Guardian> { guardian });
        StudentRepoMock.Setup(r => r.GetByIdAsync(10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Student { Id = 10, FullName = "Em A" });

        SetupRepository(new List<ClassAssignment>()); // No active assignment

        var request = new ClassTransferRequestDto { StudentId = 10, TargetClassId = 2, TransferReason = "Reason" };

        // Act
        var result = await _service.SubmitClassTransferRequestAsync(20, request);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.Code.Should().Be(SystemMessages.MSG01.Code);
    }

    [Fact]
    public async Task UTCID06_SubmitClassTransfer_TargetClassSameAsCurrent_Fails()
    {
        // Arrange
        var guardian = new Guardian
        {
            Id = 1,
            UserId = 20,
            StudentGuardians = new List<StudentGuardian> { new() { StudentId = 10, GuardianId = 1 } }
        };
        SetupRepository(new List<Guardian> { guardian });
        StudentRepoMock.Setup(r => r.GetByIdAsync(10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Student { Id = 10, FullName = "Em A" });

        var currentClass = new Class { Id = 1, ClassName = "1A", Grade = 1 };
        SetupRepository(new List<ClassAssignment> { new() { StudentId = 10, ClassId = 1, Class = currentClass, IsActive = true } });

        var request = new ClassTransferRequestDto { StudentId = 10, TargetClassId = 1, TransferReason = "Reason" };

        // Act
        var result = await _service.SubmitClassTransferRequestAsync(20, request);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.Code.Should().Be(SystemMessages.MSG01.Code);
    }

    [Fact]
    public async Task UTCID07_SubmitClassTransfer_TargetClassNotFound_Fails()
    {
        // Arrange
        var guardian = new Guardian
        {
            Id = 1,
            UserId = 20,
            StudentGuardians = new List<StudentGuardian> { new() { StudentId = 10, GuardianId = 1 } }
        };
        SetupRepository(new List<Guardian> { guardian });
        StudentRepoMock.Setup(r => r.GetByIdAsync(10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Student { Id = 10, FullName = "Em A" });

        var currentClass = new Class { Id = 1, ClassName = "1A", Grade = 1 };
        SetupRepository(new List<ClassAssignment> { new() { StudentId = 10, ClassId = 1, Class = currentClass, IsActive = true } });
        SetupRepository(new List<Class> { currentClass }); // target class 999 does not exist

        var request = new ClassTransferRequestDto { StudentId = 10, TargetClassId = 999, TransferReason = "Reason" };

        // Act
        var result = await _service.SubmitClassTransferRequestAsync(20, request);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.Code.Should().Be(SystemMessages.MSG01.Code);
    }

    [Fact]
    public async Task UTCID08_SubmitClassTransfer_CrossGradeTransfer_Fails()
    {
        // Arrange
        var guardian = new Guardian
        {
            Id = 1,
            UserId = 20,
            StudentGuardians = new List<StudentGuardian> { new() { StudentId = 10, GuardianId = 1 } }
        };
        SetupRepository(new List<Guardian> { guardian });
        StudentRepoMock.Setup(r => r.GetByIdAsync(10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Student { Id = 10, FullName = "Em A" });

        var currentClass = new Class { Id = 1, ClassName = "1A", Grade = 1 };
        var targetClass = new Class { Id = 5, ClassName = "2A", Grade = 2 };
        SetupRepository(new List<Class> { currentClass, targetClass });
        SetupRepository(new List<ClassAssignment> { new() { StudentId = 10, ClassId = 1, Class = currentClass, IsActive = true } });

        var request = new ClassTransferRequestDto { StudentId = 10, TargetClassId = 5, TransferReason = "Reason" };

        // Act
        var result = await _service.SubmitClassTransferRequestAsync(20, request);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.Code.Should().Be(SystemMessages.MSG01.Code);
    }

    #endregion

    #region Method 34: SubmitDocumentRequestAsync (UTCID01 - UTCID05)

    [Fact]
    public async Task UTCID01_SubmitDocumentRequest_Success()
    {
        // Arrange
        var guardian = new Guardian
        {
            Id = 1,
            UserId = 20,
            StudentGuardians = new List<StudentGuardian> { new() { StudentId = 10, GuardianId = 1 } }
        };
        SetupRepository(new List<Guardian> { guardian });
        StudentRepoMock.Setup(r => r.GetByIdAsync(10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Student { Id = 10, FullName = "Em A" });

        var request = new DocumentIssuanceRequestDto
        {
            StudentId = 10,
            DocumentType = "Giấy xác nhận học sinh",
            Purpose = "Xin visa",
            NumberOfCopies = 2
        };

        // Act
        var result = await _service.SubmitDocumentRequestAsync(20, request);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Code.Should().Be(SystemMessages.MSG30.Code);
    }

    [Fact]
    public async Task UTCID02_SubmitDocumentRequest_MissingFields_Fails()
    {
        // Arrange
        var request = new DocumentIssuanceRequestDto { StudentId = 10, DocumentType = "", Purpose = "" };

        // Act
        var result = await _service.SubmitDocumentRequestAsync(20, request);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.Code.Should().Be(SystemMessages.MSG01.Code);
    }

    [Fact]
    public async Task UTCID03_SubmitDocumentRequest_RequesterNotLinked_Fails()
    {
        // Arrange
        SetupRepository(new List<Guardian>());
        var request = new DocumentIssuanceRequestDto { StudentId = 10, DocumentType = "Giấy xác nhận", Purpose = "Visa" };

        // Act
        var result = await _service.SubmitDocumentRequestAsync(20, request);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.Code.Should().Be(SystemMessages.MSG10.Code);
    }

    [Fact]
    public async Task UTCID04_SubmitDocumentRequest_StudentNotFound_Fails()
    {
        // Arrange
        var guardian = new Guardian
        {
            Id = 1,
            UserId = 20,
            StudentGuardians = new List<StudentGuardian> { new() { StudentId = 999, GuardianId = 1 } }
        };
        SetupRepository(new List<Guardian> { guardian });
        StudentRepoMock.Setup(r => r.GetByIdAsync(999, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Student?)null);

        var request = new DocumentIssuanceRequestDto { StudentId = 999, DocumentType = "Giấy xác nhận", Purpose = "Visa" };

        // Act
        var result = await _service.SubmitDocumentRequestAsync(20, request);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.Code.Should().Be(SystemMessages.MSG11.Code);
    }

    [Fact]
    public async Task UTCID05_SubmitDocumentRequest_DefaultNumberOfCopies_WhenZero()
    {
        // Arrange
        var guardian = new Guardian
        {
            Id = 1,
            UserId = 20,
            StudentGuardians = new List<StudentGuardian> { new() { StudentId = 10, GuardianId = 1 } }
        };
        SetupRepository(new List<Guardian> { guardian });
        StudentRepoMock.Setup(r => r.GetByIdAsync(10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Student { Id = 10, FullName = "Em A" });

        var docDetails = new List<DocumentRequestDetail>();
        SetupRepository(docDetails);

        var request = new DocumentIssuanceRequestDto
        {
            StudentId = 10,
            DocumentType = "Giấy xác nhận học sinh",
            Purpose = "Xin visa",
            NumberOfCopies = 0 // boundary
        };

        // Act
        var result = await _service.SubmitDocumentRequestAsync(20, request);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        docDetails.Should().HaveCount(1);
        docDetails[0].NumberOfCopies.Should().Be(1);
    }

    #endregion

    #region Method 35: GetMyRequestsAsync (UTCID01 - UTCID03)

    [Fact]
    public async Task UTCID01_GetMyRequests_ReturnsList()
    {
        // Arrange
        var requests = new List<AdministrativeRequest>
        {
            new() { Id = 1, RequesterUserId = 20, RequestType = RequestTypes.ClassTransfer, Status = RequestStatus.Submitted, Student = new Student { FullName = "Em A" } }
        };
        SetupRepository(requests);

        // Act
        var result = await _service.GetMyRequestsAsync(20);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Data.Should().HaveCount(1);
    }

    [Fact]
    public async Task UTCID02_GetMyRequests_FilterByStudent_ReturnsFiltered()
    {
        // Arrange
        var requests = new List<AdministrativeRequest>
        {
            new() { Id = 1, RequesterUserId = 20, StudentId = 10, RequestType = RequestTypes.ClassTransfer, Status = RequestStatus.Submitted },
            new() { Id = 2, RequesterUserId = 20, StudentId = 11, RequestType = RequestTypes.Document, Status = RequestStatus.Submitted }
        };
        SetupRepository(requests);

        // Act
        var result = await _service.GetMyRequestsAsync(20, 10);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Data.Should().HaveCount(1);
        result.Data![0].StudentId.Should().Be(10);
    }

    [Fact]
    public async Task UTCID03_GetMyRequests_Empty_ReturnsEmptyList()
    {
        // Arrange
        SetupRepository(new List<AdministrativeRequest>());

        // Act
        var result = await _service.GetMyRequestsAsync(20);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Data.Should().BeEmpty();
    }

    #endregion

    #region Method 36: GetRequestDetailAsync (UTCID01 - UTCID02)

    [Fact]
    public async Task UTCID01_GetRequestDetail_Found()
    {
        // Arrange
        var req = new AdministrativeRequest
        {
            Id = 1,
            RequestNumber = "REQ-CT-001",
            RequestType = RequestTypes.ClassTransfer,
            RequesterUser = new User { FullName = "Phu Huynh A" },
            Student = new Student { FullName = "Em A" }
        };
        SetupRepository(new List<AdministrativeRequest> { req });

        // Act
        var result = await _service.GetRequestDetailAsync(1, 20);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Data!.RequestNumber.Should().Be("REQ-CT-001");
    }

    [Fact]
    public async Task UTCID02_GetRequestDetail_NotFound_Fails()
    {
        // Arrange
        SetupRepository(new List<AdministrativeRequest>());

        // Act
        var result = await _service.GetRequestDetailAsync(999, 20);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
    }

    #endregion

    #region Method 37: ResubmitRequestAsync (UTCID01 - UTCID04)

    [Fact]
    public async Task UTCID01_ResubmitRequest_Success()
    {
        // Arrange
        var req = new AdministrativeRequest
        {
            Id = 1,
            RequesterUserId = 20,
            RequiresAmendment = true,
            ClassTransferDetail = new ClassTransferDetail { TransferReason = "Old Reason" }
        };
        SetupRepository(new List<AdministrativeRequest> { req });

        var resubmit = new ResubmitRequestDto { TransferReason = "Updated Reason" };

        // Act
        var result = await _service.ResubmitRequestAsync(1, 20, resubmit);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Code.Should().Be(SystemMessages.MSG08.Code);
        req.RequiresAmendment.Should().BeFalse();
        req.ClassTransferDetail.TransferReason.Should().Be("Updated Reason");
    }

    [Fact]
    public async Task UTCID02_ResubmitRequest_NotFound_Fails()
    {
        // Arrange
        SetupRepository(new List<AdministrativeRequest>());
        var resubmit = new ResubmitRequestDto { TransferReason = "Updated" };

        // Act
        var result = await _service.ResubmitRequestAsync(999, 20, resubmit);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
    }

    [Fact]
    public async Task UTCID03_ResubmitRequest_NotRequester_Forbidden()
    {
        // Arrange
        var req = new AdministrativeRequest { Id = 1, RequesterUserId = 20, RequiresAmendment = true };
        SetupRepository(new List<AdministrativeRequest> { req });

        var resubmit = new ResubmitRequestDto();

        // Act
        var result = await _service.ResubmitRequestAsync(1, 25, resubmit); // 25 != 20

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.Code.Should().Be(SystemMessages.MSG10.Code);
    }

    [Fact]
    public async Task UTCID04_ResubmitRequest_NotRequiresAmendment_Fails()
    {
        // Arrange
        var req = new AdministrativeRequest { Id = 1, RequesterUserId = 20, RequiresAmendment = false };
        SetupRepository(new List<AdministrativeRequest> { req });

        var resubmit = new ResubmitRequestDto();

        // Act
        var result = await _service.ResubmitRequestAsync(1, 20, resubmit);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.Code.Should().Be(SystemMessages.MSG09.Code);
    }

    #endregion

    #region Method 38: GetOfficeStaffRequestsAsync (UTCID01 - UTCID03)

    [Fact]
    public async Task UTCID01_GetOfficeStaffRequests_All_ReturnsList()
    {
        // Arrange
        var requests = new List<AdministrativeRequest>
        {
            new() { Id = 1, RequestType = RequestTypes.ClassTransfer, Status = RequestStatus.Submitted },
            new() { Id = 2, RequestType = RequestTypes.Document, Status = RequestStatus.Submitted }
        };
        SetupRepository(requests);

        // Act
        var result = await _service.GetOfficeStaffRequestsAsync();

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Data.Should().HaveCount(2);
    }

    [Fact]
    public async Task UTCID02_GetOfficeStaffRequests_FilterByType_ReturnsFiltered()
    {
        // Arrange
        var requests = new List<AdministrativeRequest>
        {
            new() { Id = 1, RequestType = RequestTypes.ClassTransfer, Status = RequestStatus.Submitted },
            new() { Id = 2, RequestType = RequestTypes.Document, Status = RequestStatus.Submitted }
        };
        SetupRepository(requests);

        // Act
        var result = await _service.GetOfficeStaffRequestsAsync(RequestTypes.ClassTransfer);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Data.Should().HaveCount(1);
    }

    [Fact]
    public async Task UTCID03_GetOfficeStaffRequests_FilterByStatus_ReturnsFiltered()
    {
        // Arrange
        var requests = new List<AdministrativeRequest>
        {
            new() { Id = 1, RequestType = RequestTypes.ClassTransfer, Status = RequestStatus.Submitted },
            new() { Id = 2, RequestType = RequestTypes.ClassTransfer, Status = RequestStatus.Approved }
        };
        SetupRepository(requests);

        // Act
        var result = await _service.GetOfficeStaffRequestsAsync(status: "Submitted");

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Data.Should().HaveCount(1);
    }

    #endregion

    #region Method 39: RequestAmendmentAsync (UTCID01 - UTCID05)

    [Fact]
    public async Task UTCID01_RequestAmendment_Success()
    {
        // Arrange
        var req = new AdministrativeRequest { Id = 1, Status = RequestStatus.Submitted, RequiresAmendment = false };
        SetupRepository(new List<AdministrativeRequest> { req });

        var amendment = new StaffAmendmentDto { AmendmentNotes = "Vui lòng bổ sung sổ hộ khẩu" };

        // Act
        var result = await _service.RequestAmendmentAsync(1, 10, amendment);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Code.Should().Be(SystemMessages.MSG08.Code);
        req.RequiresAmendment.Should().BeTrue();
        req.AmendmentNotes.Should().Be("Vui lòng bổ sung sổ hộ khẩu");
    }

    [Fact]
    public async Task UTCID02_RequestAmendment_MissingNotes_Fails()
    {
        // Arrange
        var amendment = new StaffAmendmentDto { AmendmentNotes = "" };

        // Act
        var result = await _service.RequestAmendmentAsync(1, 10, amendment);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.Code.Should().Be(SystemMessages.MSG01.Code);
    }

    [Fact]
    public async Task UTCID03_RequestAmendment_NotFound_Fails()
    {
        // Arrange
        SetupRepository(new List<AdministrativeRequest>());
        var amendment = new StaffAmendmentDto { AmendmentNotes = "Notes" };

        // Act
        var result = await _service.RequestAmendmentAsync(999, 10, amendment);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
    }

    [Fact]
    public async Task UTCID04_RequestAmendment_InvalidStatus_Fails()
    {
        // Arrange
        var req = new AdministrativeRequest { Id = 1, Status = RequestStatus.Approved, RequiresAmendment = false };
        SetupRepository(new List<AdministrativeRequest> { req });

        var amendment = new StaffAmendmentDto { AmendmentNotes = "Notes" };

        // Act
        var result = await _service.RequestAmendmentAsync(1, 10, amendment);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.Code.Should().Be(SystemMessages.MSG09.Code);
    }

    [Fact]
    public async Task UTCID05_RequestAmendment_AlreadyRequiresAmendment_Fails()
    {
        // Arrange
        var req = new AdministrativeRequest { Id = 1, Status = RequestStatus.Submitted, RequiresAmendment = true };
        SetupRepository(new List<AdministrativeRequest> { req });

        var amendment = new StaffAmendmentDto { AmendmentNotes = "Notes" };

        // Act
        var result = await _service.RequestAmendmentAsync(1, 10, amendment);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.Code.Should().Be(SystemMessages.MSG09.Code);
    }

    #endregion

    #region Method 40: EndorseTransferAsync (UTCID01 - UTCID06)

    [Fact]
    public async Task UTCID01_EndorseTransfer_Success_Approve()
    {
        // Arrange
        var targetClass = new Class { Id = 2, ClassName = "1B", Capacity = 35 };
        var transferDetail = new ClassTransferDetail { ToClassId = 2, ToClass = targetClass };
        var req = new AdministrativeRequest
        {
            Id = 1,
            Status = RequestStatus.Submitted,
            ClassTransferDetail = transferDetail
        };
        SetupRepository(new List<AdministrativeRequest> { req });
        SetupRepository(new List<ClassAssignment>()); // 0 enrolled < 35

        var endorse = new StaffEndorseTransferDto { StaffRecommendation = "Đồng ý chuyển lớp" };

        // Act
        var result = await _service.EndorseTransferAsync(1, 10, endorse);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Code.Should().Be(SystemMessages.MSG08.Code);
        req.Status.Should().Be(RequestStatus.UnderReview);
    }

    [Fact]
    public async Task UTCID02_EndorseTransfer_Success_RejectRecommendation()
    {
        // Arrange
        var targetClass = new Class { Id = 2, ClassName = "1B", Capacity = 35 };
        var transferDetail = new ClassTransferDetail { ToClassId = 2, ToClass = targetClass };
        var req = new AdministrativeRequest
        {
            Id = 1,
            Status = RequestStatus.Submitted,
            ClassTransferDetail = transferDetail
        };
        SetupRepository(new List<AdministrativeRequest> { req });
        SetupRepository(new List<ClassAssignment>());

        var endorse = new StaffEndorseTransferDto { StaffRecommendation = "Không nên chuyển lớp" };

        // Act
        var result = await _service.EndorseTransferAsync(1, 10, endorse);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        req.Status.Should().Be(RequestStatus.UnderReview);
    }

    [Fact]
    public async Task UTCID03_EndorseTransfer_TargetClassFull_Fails()
    {
        // Arrange
        var targetClass = new Class { Id = 2, ClassName = "1B", Capacity = 35 };
        var transferDetail = new ClassTransferDetail { ToClassId = 2, ToClass = targetClass };
        var req = new AdministrativeRequest
        {
            Id = 1,
            Status = RequestStatus.Submitted,
            ClassTransferDetail = transferDetail
        };
        SetupRepository(new List<AdministrativeRequest> { req });

        var fullAssignments = Enumerable.Range(1, 35)
            .Select(i => new ClassAssignment { ClassId = 2, IsActive = true })
            .ToList();
        SetupRepository(fullAssignments);

        var endorse = new StaffEndorseTransferDto { StaffRecommendation = "Đề xuất duyệt" };

        // Act
        var result = await _service.EndorseTransferAsync(1, 10, endorse);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.Code.Should().Be(SystemMessages.MSG15.Code);
    }

    [Fact]
    public async Task UTCID04_EndorseTransfer_NotFound_Fails()
    {
        // Arrange
        SetupRepository(new List<AdministrativeRequest>());
        var endorse = new StaffEndorseTransferDto { StaffRecommendation = "Đề xuất" };

        // Act
        var result = await _service.EndorseTransferAsync(999, 10, endorse);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
    }

    [Fact]
    public async Task UTCID05_EndorseTransfer_AlreadyUnderReview_Fails()
    {
        // Arrange
        var req = new AdministrativeRequest
        {
            Id = 1,
            Status = RequestStatus.UnderReview,
            ClassTransferDetail = new ClassTransferDetail()
        };
        SetupRepository(new List<AdministrativeRequest> { req });
        var endorse = new StaffEndorseTransferDto { StaffRecommendation = "Đề xuất" };

        // Act
        var result = await _service.EndorseTransferAsync(1, 10, endorse);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.Code.Should().Be(SystemMessages.MSG09.Code);
    }

    [Fact]
    public async Task UTCID06_EndorseTransfer_RequiresAmendment_Fails()
    {
        // Arrange
        var req = new AdministrativeRequest
        {
            Id = 1,
            Status = RequestStatus.Submitted,
            RequiresAmendment = true,
            ClassTransferDetail = new ClassTransferDetail()
        };
        SetupRepository(new List<AdministrativeRequest> { req });
        var endorse = new StaffEndorseTransferDto { StaffRecommendation = "Đề xuất" };

        // Act
        var result = await _service.EndorseTransferAsync(1, 10, endorse);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.Code.Should().Be(SystemMessages.MSG09.Code);
    }

    #endregion

    #region Method 41: EndorseDocumentAsync (UTCID01 - UTCID05)

    [Fact]
    public async Task UTCID01_EndorseDocument_Success()
    {
        // Arrange
        var docDetail = new DocumentRequestDetail { DocumentType = "Giấy xác nhận", Purpose = "Visa" };
        var req = new AdministrativeRequest
        {
            Id = 1,
            Status = RequestStatus.Submitted,
            DocumentRequestDetail = docDetail
        };
        SetupRepository(new List<AdministrativeRequest> { req });

        var endorse = new StaffEndorseDocumentDto
        {
            StaffEndorsement = "Hồ sơ đầy đủ hợp lệ",
            DocumentContentDraft = "Dự thảo xác nhận"
        };

        // Act
        var result = await _service.EndorseDocumentAsync(1, 10, endorse);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Code.Should().Be(SystemMessages.MSG08.Code);
        req.Status.Should().Be(RequestStatus.UnderReview);
    }

    [Fact]
    public async Task UTCID02_EndorseDocument_AutoGeneratesDraftContent()
    {
        // Arrange
        var docDetail = new DocumentRequestDetail { DocumentType = "Giấy xác nhận", Purpose = "Visa" };
        var req = new AdministrativeRequest
        {
            Id = 1,
            Status = RequestStatus.Submitted,
            DocumentRequestDetail = docDetail,
            Student = new Student { FullName = "Em A", StudentCode = "HS001" }
        };
        SetupRepository(new List<AdministrativeRequest> { req });

        var endorse = new StaffEndorseDocumentDto
        {
            StaffEndorsement = "Đồng ý",
            DocumentContentDraft = "" // Empty draft
        };

        // Act
        var result = await _service.EndorseDocumentAsync(1, 10, endorse);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        docDetail.DocumentContentDraft.Should().Contain("DỰ THẢO VĂN BẢN XÁC NHẬN");
    }

    [Fact]
    public async Task UTCID03_EndorseDocument_NotFound_Fails()
    {
        // Arrange
        SetupRepository(new List<AdministrativeRequest>());
        var endorse = new StaffEndorseDocumentDto();

        // Act
        var result = await _service.EndorseDocumentAsync(999, 10, endorse);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
    }

    [Fact]
    public async Task UTCID04_EndorseDocument_InvalidStatus_Fails()
    {
        // Arrange
        var req = new AdministrativeRequest
        {
            Id = 1,
            Status = RequestStatus.Approved,
            DocumentRequestDetail = new DocumentRequestDetail()
        };
        SetupRepository(new List<AdministrativeRequest> { req });
        var endorse = new StaffEndorseDocumentDto();

        // Act
        var result = await _service.EndorseDocumentAsync(1, 10, endorse);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.Code.Should().Be(SystemMessages.MSG09.Code);
    }

    [Fact]
    public async Task UTCID05_EndorseDocument_RequiresAmendment_Fails()
    {
        // Arrange
        var req = new AdministrativeRequest
        {
            Id = 1,
            Status = RequestStatus.Submitted,
            RequiresAmendment = true,
            DocumentRequestDetail = new DocumentRequestDetail()
        };
        SetupRepository(new List<AdministrativeRequest> { req });
        var endorse = new StaffEndorseDocumentDto();

        // Act
        var result = await _service.EndorseDocumentAsync(1, 10, endorse);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.Code.Should().Be(SystemMessages.MSG09.Code);
    }

    #endregion

    #region Method 42: ExecuteClassTransferAsync (UTCID01 - UTCID04)

    [Fact]
    public async Task UTCID01_ExecuteClassTransfer_Success()
    {
        // Arrange
        var fromClass = new Class { Id = 1, ClassName = "1A" };
        var toClass = new Class { Id = 2, ClassName = "1B" };
        var transferDetail = new ClassTransferDetail { FromClassId = 1, ToClassId = 2, FromClass = fromClass, ToClass = toClass, IsExecuted = false };
        var req = new AdministrativeRequest
        {
            Id = 1,
            StudentId = 10,
            SchoolBoardApprovedAt = DateTime.UtcNow,
            ClassTransferDetail = transferDetail
        };
        SetupRepository(new List<AdministrativeRequest> { req });

        var oldAssignment = new ClassAssignment { Id = 100, StudentId = 10, ClassId = 1, IsActive = true };
        var assignments = new List<ClassAssignment> { oldAssignment };
        SetupRepository(assignments);

        // Act
        var result = await _service.ExecuteClassTransferAsync(1, 10);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Code.Should().Be(SystemMessages.MSG35.Code);
        oldAssignment.IsActive.Should().BeFalse();
        transferDetail.IsExecuted.Should().BeTrue();
        assignments.Should().Contain(ca => ca.ClassId == 2 && ca.IsActive);
    }

    [Fact]
    public async Task UTCID02_ExecuteClassTransfer_NotFound_Fails()
    {
        // Arrange
        SetupRepository(new List<AdministrativeRequest>());

        // Act
        var result = await _service.ExecuteClassTransferAsync(999, 10);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
    }

    [Fact]
    public async Task UTCID03_ExecuteClassTransfer_NotApprovedByBoard_Fails()
    {
        // Arrange
        var transferDetail = new ClassTransferDetail { FromClassId = 1, ToClassId = 2, IsExecuted = false };
        var req = new AdministrativeRequest
        {
            Id = 1,
            StudentId = 10,
            SchoolBoardApprovedAt = null, // Not yet approved
            ClassTransferDetail = transferDetail
        };
        SetupRepository(new List<AdministrativeRequest> { req });

        // Act
        var result = await _service.ExecuteClassTransferAsync(1, 10);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.Code.Should().Be(SystemMessages.MSG09.Code);
    }

    [Fact]
    public async Task UTCID04_ExecuteClassTransfer_AlreadyExecuted_Fails()
    {
        // Arrange
        var transferDetail = new ClassTransferDetail { FromClassId = 1, ToClassId = 2, IsExecuted = true };
        var req = new AdministrativeRequest
        {
            Id = 1,
            StudentId = 10,
            SchoolBoardApprovedAt = DateTime.UtcNow,
            ClassTransferDetail = transferDetail
        };
        SetupRepository(new List<AdministrativeRequest> { req });

        // Act
        var result = await _service.ExecuteClassTransferAsync(1, 10);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.Code.Should().Be(SystemMessages.MSG09.Code);
    }

    #endregion

    #region Method 43: GetSchoolBoardRequestsAsync (UTCID01 - UTCID02)

    [Fact]
    public async Task UTCID01_GetSchoolBoardRequests_All_ReturnsList()
    {
        // Arrange
        var requests = new List<AdministrativeRequest>
        {
            new() { Id = 1, Status = RequestStatus.UnderReview, RequestType = RequestTypes.ClassTransfer },
            new() { Id = 2, Status = RequestStatus.UnderReview, RequestType = RequestTypes.Document }
        };
        SetupRepository(requests);

        // Act
        var result = await _service.GetSchoolBoardRequestsAsync();

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Data.Should().HaveCount(2);
    }

    [Fact]
    public async Task UTCID02_GetSchoolBoardRequests_FilterByType_ReturnsFiltered()
    {
        // Arrange
        var requests = new List<AdministrativeRequest>
        {
            new() { Id = 1, Status = RequestStatus.UnderReview, RequestType = RequestTypes.ClassTransfer },
            new() { Id = 2, Status = RequestStatus.UnderReview, RequestType = RequestTypes.Document }
        };
        SetupRepository(requests);

        // Act
        var result = await _service.GetSchoolBoardRequestsAsync(RequestTypes.ClassTransfer);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Data.Should().HaveCount(1);
    }

    #endregion

    #region Method 44: ProcessBoardDecisionAsync (UTCID01 - UTCID06)

    [Fact]
    public async Task UTCID01_ProcessBoardDecision_ApproveClassTransfer_Success()
    {
        // Arrange
        var req = new AdministrativeRequest
        {
            Id = 1,
            RequestType = RequestTypes.ClassTransfer,
            Status = RequestStatus.UnderReview
        };
        SetupRepository(new List<AdministrativeRequest> { req });

        var decision = new BoardDecisionDto { IsApproved = true, DecisionNotes = "Đồng ý chuyển lớp" };

        // Act
        var result = await _service.ProcessBoardDecisionAsync(1, 5, decision);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Code.Should().Be(SystemMessages.MSG31.Code);
        req.Status.Should().Be(RequestStatus.Approved);
        req.SchoolBoardApprovedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task UTCID02_ProcessBoardDecision_Reject_Success()
    {
        // Arrange
        var req = new AdministrativeRequest
        {
            Id = 1,
            RequestType = RequestTypes.ClassTransfer,
            Status = RequestStatus.UnderReview
        };
        SetupRepository(new List<AdministrativeRequest> { req });

        var decision = new BoardDecisionDto { IsApproved = false, DecisionNotes = "Lớp đích đã đủ sĩ số" };

        // Act
        var result = await _service.ProcessBoardDecisionAsync(1, 5, decision);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Code.Should().Be(SystemMessages.MSG32.Code);
        req.Status.Should().Be(RequestStatus.Rejected);
    }

    [Fact]
    public async Task UTCID03_ProcessBoardDecision_ApproveDocument_IssuesCertificate()
    {
        // Arrange
        var docDetail = new DocumentRequestDetail { DocumentType = "Giấy xác nhận" };
        var req = new AdministrativeRequest
        {
            Id = 1,
            RequestType = RequestTypes.Document,
            Status = RequestStatus.UnderReview,
            DocumentRequestDetail = docDetail
        };
        SetupRepository(new List<AdministrativeRequest> { req });

        var decision = new BoardDecisionDto { IsApproved = true, DecisionNotes = "Đã duyệt và ký số" };

        // Act
        var result = await _service.ProcessBoardDecisionAsync(1, 5, decision);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        req.Status.Should().Be(RequestStatus.Approved);
        docDetail.CertificateNumber.Should().StartWith("SAWP-");
    }

    [Fact]
    public async Task UTCID04_ProcessBoardDecision_NotFound_Fails()
    {
        // Arrange
        SetupRepository(new List<AdministrativeRequest>());
        var decision = new BoardDecisionDto { IsApproved = true };

        // Act
        var result = await _service.ProcessBoardDecisionAsync(999, 5, decision);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
    }

    [Fact]
    public async Task UTCID05_ProcessBoardDecision_InvalidStatus_Fails()
    {
        // Arrange
        var req = new AdministrativeRequest
        {
            Id = 1,
            Status = RequestStatus.Submitted // Not UnderReview
        };
        SetupRepository(new List<AdministrativeRequest> { req });
        var decision = new BoardDecisionDto { IsApproved = true };

        // Act
        var result = await _service.ProcessBoardDecisionAsync(1, 5, decision);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.Code.Should().Be(SystemMessages.MSG09.Code);
    }

    [Fact]
    public async Task UTCID06_ProcessBoardDecision_RejectWithoutNotes_Fails()
    {
        // Arrange
        var req = new AdministrativeRequest
        {
            Id = 1,
            Status = RequestStatus.UnderReview
        };
        SetupRepository(new List<AdministrativeRequest> { req });
        var decision = new BoardDecisionDto { IsApproved = false, DecisionNotes = "" };

        // Act
        var result = await _service.ProcessBoardDecisionAsync(1, 5, decision);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.Code.Should().Be(SystemMessages.MSG01.Code);
    }

    #endregion

    #region Method 45: ProcessRequestAsync (UTCID01 - UTCID02)

    [Fact]
    public async Task UTCID01_ProcessRequest_Legacy_Success()
    {
        // Arrange
        var req = new AdministrativeRequest { Id = 1, Status = RequestStatus.Submitted };
        SetupRepository(new List<AdministrativeRequest> { req });

        var request = new ProcessRequestDto { NewStatus = RequestStatus.Approved, ReviewerNotes = "Duyệt đơn" };

        // Act
        var result = await _service.ProcessRequestAsync(1, 10, request);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Code.Should().Be(SystemMessages.MSG08.Code);
        req.Status.Should().Be(RequestStatus.Approved);
    }

    [Fact]
    public async Task UTCID02_ProcessRequest_NotFound_Fails()
    {
        // Arrange
        SetupRepository(new List<AdministrativeRequest>());
        var request = new ProcessRequestDto { NewStatus = RequestStatus.Approved };

        // Act
        var result = await _service.ProcessRequestAsync(999, 10, request);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
    }

    #endregion
}

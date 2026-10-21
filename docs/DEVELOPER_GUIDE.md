# SAWP Developer Guide & Architecture Manual

**Project:** School Administrative and Welfare Platform (SAWP) - SEP490 Group 45  
**Live Demo / Reference Repo:** [https://github.com/NguyenNHA1702/SAWP-Demo](https://github.com/NguyenNHA1702/SAWP-Demo)  
**Backend Solution:** `SAWP.Backend/SAWP.sln` (.NET 8.0 LTS Web API)  
**Frontend Application:** `sawp-frontend/` (React + TypeScript + Vite)  

---

## 1. Complete Backend Codebase File Map

The backend follows a streamlined 4-project Clean Architecture where contracts/interfaces are integrated into their respective implementation projects for maintainability and clarity.

```
SAWP.Backend/
│
├── SAWP.sln                                          # Visual Studio / .NET CLI Solution File
│
├── 1. Core/                                          # Domain Layer (Zero external dependencies)
│   ├── Constants/                                    # Standard System Constants & Messages (SRS)
│   │   ├── SystemMessage.cs                          # SystemMessage record descriptor (Code, Type, Context, Content)
│   │   └── SystemMessages.cs                         # Strongly-typed MSG01 to MSG50 constants & dictionary
│   │
│   ├── Entities/                                     # Domain Model Entities (Logical ERD)
│   │   ├── User.cs                                   # User login accounts, credentials, contact info
│   │   ├── Role.cs                                   # System roles (Admin, Teacher, Parent, etc.)
│   │   ├── UserRole.cs                               # User-to-Role associative entity
│   │   ├── StaffProfile.cs                           # School staff identity and homeroom links
│   │   ├── Guardian.cs                               # Parent/Guardian contact & identity records
│   │   ├── Student.cs                                # Central enrolled student record
│   │   ├── StudentGuardian.cs                        # Student-Guardian relationship & primary flag
│   │   ├── SchoolYear.cs                             # Academic year definitions & status
│   │   ├── SchoolCalendarDay.cs                      # School calendar dates (holidays, school days)
│   │   ├── Class.cs                                  # Classes, grade, capacity, homeroom teacher
│   │   ├── ClassAssignment.cs                        # Student enrollment history in classes
│   │   ├── AttendanceRecord.cs                       # Daily student attendance records
│   │   ├── LeaveRequest.cs                           # Leave/absence request details
│   │   ├── LunchServiceDay.cs                        # Daily lunch service availability & cutoff
│   │   ├── LunchRegistration.cs                      # Student meal registrations & active state
│   │   ├── AdministrativeRequest.cs                  # Generic request header (approvals workflow)
│   │   ├── ClassTransferDetail.cs                    # Class-transfer specific details
│   │   ├── DocumentRequestDetail.cs                  # Certificate/document issuance details
│   │   ├── Notification.cs                           # User notifications and read status
│   │   └── AuditLog.cs                               # Append-only security & audit trail
│   │
│   ├── Enums/                                        # Domain Enumerations
│   │   ├── UserRoles.cs                              # Admin, SchoolBoard, OfficeStaff, Teacher, ServiceStaff, Parent
│   │   ├── RequestStatus.cs                          # Submitted, UnderReview, Approved, Rejected, Cancelled
│   │   ├── AttendanceStatus.cs                       # Present, ExcusedAbsence, UnexcusedAbsence, Late
│   │   ├── RelationshipTypes.cs                      # Father, Mother, Guardian, Other
│   │   └── MealType.cs                               # StandardLunch, VegetarianLunch, SpecialDiet
│   │
│   ├── DTOs/                                         # Data Transfer Objects
│   │   ├── Common/
│   │   │   ├── ApiResponse.cs                        # Standardized envelope ApiResponse<T>
│   │   │   └── PaginatedResult.cs                    # Generic PaginatedResult<T>
│   │   ├── Auth/
│   │   │   └── AuthDtos.cs                           # LoginRequestDto, LoginResponseDto, ChangePasswordRequestDto
│   │   ├── Students/
│   │   │   └── StudentDtos.cs                        # CreateStudentDto, StudentProfileDto, ImportStudentRowDto
│   │   ├── Attendance/
│   │   │   └── AttendanceDtos.cs                     # DailyAttendanceEntryDto, LeaveRequestSubmitDto
│   │   ├── Lunch/
│   │   │   └── LunchDtos.cs                          # LunchRegistrationDto, DailyMealCountReportDto
│   │   └── Requests/
│   │       └── RequestDtos.cs                        # ClassTransferRequestDto, DocumentIssuanceRequestDto, ProcessRequestDto
│   │
│   └── Interfaces/                                   # Data Access Contracts
│       ├── IRepository.cs                            # Generic Repository contract (CRUD, Expression querying)
│       ├── IStudentRepository.cs                     # Student specific queries (Profiles, Classes, Codes)
│       ├── IAttendanceRepository.cs                  # Daily class attendance & student history queries
│       ├── ILunchRepository.cs                       # Daily meal counts & registration checks
│       └── IUnitOfWork.cs                            # Unit of Work transaction & repository coordinator
│
├── 2. Services/                                      # Business Logic Layer
│   ├── Interfaces/                                   # Service Contracts
│   │   ├── IAuthService.cs                           # Authentication & password management contract
│   │   ├── IStudentService.cs                        # Student management business logic contract
│   │   ├── IAttendanceService.cs                     # Attendance recording & leave request submission contract
│   │   ├── ILunchService.cs                          # Lunch registration & meal count aggregation contract
│   │   ├── IRequestWorkflowEngine.cs                 # Multi-step administrative approval engine contract
│   │   ├── IAuditService.cs                          # System event & action audit logging contract
│   │   ├── IStudentImporter.cs                       # Excel student file parsing & bulk import contract
│   │   └── IStaffImporter.cs                         # Excel staff file parsing & import contract
│   │
│   └── Implementations/                              # Business Logic Implementations
│       ├── AuthService.cs                            # Implements IAuthService
│       ├── StudentService.cs                         # Implements IStudentService
│       ├── AttendanceService.cs                      # Implements IAttendanceService
│       ├── LunchService.cs                           # Implements ILunchService
│       ├── RequestWorkflowEngine.cs                  # Implements IRequestWorkflowEngine
│       ├── AuditService.cs                           # Implements IAuditService
│       ├── StudentImporter.cs                        # Implements IStudentImporter (ClosedXML)
│       └── StaffImporter.cs                          # Implements IStaffImporter
│
├── 3. Infrastructure/                                # Data Access & External Integrations Layer
│   ├── Data/
│   │   ├── ApplicationDbContext.cs                   # EF Core DbContext with all DbSets
│   │   ├── Configurations/
│   │   │   └── EntityConfigurations.cs               # Fluent API configurations (Keys, Indexes, FKs)
│   │   └── Migrations/
│   │       ├── 20261015040111_InitialCreate.cs       # Initial database migration code
│   │       └── ApplicationDbContextModelSnapshot.cs  # Current EF Core model snapshot
│   │
│   ├── Repositories/                                 # Data Access Implementations
│   │   ├── Repository.cs                             # Implements IRepository<T>
│   │   ├── StudentRepository.cs                      # Implements IStudentRepository
│   │   ├── AttendanceRepository.cs                   # Implements IAttendanceRepository
│   │   ├── LunchRepository.cs                        # Implements ILunchRepository
│   │   └── UnitOfWork.cs                             # Implements IUnitOfWork
│   │
│   └── ExternalServices/                             # External Providers (Contracts + Implementations)
│       ├── IEmailService.cs                          # Email dispatch interface
│       ├── IFileStorageService.cs                    # File upload & document storage interface
│       ├── EmailService.cs                           # Implements IEmailService (MailKit / SMTP)
│       └── FileStorageService.cs                     # Implements IFileStorageService (Local Disk / Cloud)
│
└── 4. API/                                           # Presentation / Host Layer (Web API)
    ├── Controllers/                                  # API HTTP Endpoints (Clean shells)
    │   ├── AuthController.cs                         # Authentication endpoints
    │   ├── StudentsController.cs                     # Student profiles & Excel import endpoints
    │   ├── ClassesController.cs                      # Class & schedule endpoints
    │   ├── AttendanceController.cs                   # Daily attendance & leave submission endpoints
    │   ├── LunchController.cs                        # Lunch registration & kitchen count endpoints
    │   ├── RequestsController.cs                     # Administrative request approval endpoints
    │   ├── NotificationsController.cs                # In-app notifications endpoints
    │   ├── ReportsController.cs                      # Operational summary reports endpoints
    │   └── SystemController.cs                       # System status & audit log endpoints
    │
    ├── Middlewares/                                  # Cross-cutting Middlewares & Filters
    │   ├── ExceptionHandlingMiddleware.cs            # Global centralized exception handling
    │   ├── JwtMiddleware.cs                          # Bearer token verification & UserId extraction
    │   └── DataScopeFilter.cs                        # Role data scope authorization filter
    │
    ├── Program.cs                                    # Application bootstrap & DI registration
    └── appsettings.json                              # Connection strings & runtime configurations
```

---

## 2. Backend Developer Guide (BE)

### 2.1. Project Dependencies (Reference Graph)
```
API  ──> Services ──> Infrastructure ──> Core
 │                           │
 └───────────────────────────┘
```
- `Core`: Pure domain. Do **NOT** reference other projects or external ORMs here.
- `Infrastructure`: References `Core`. Handles SQL Server database, EF Core DbContext, Repositories, and Mail/Storage services.
- `Services`: References `Core` and `Infrastructure`. Contains business logic, calculations, and validations.
- `API`: Composition root. References `Core`, `Infrastructure`, and `Services` to register Dependency Injection.

### 2.2. Pre-built Methods & Function Reference (Collapsible)

This section documents all available, pre-implemented methods across the Unit of Work, Repositories, Business Services, and External Integrations so developers know what functions exist and how to call them.

<details>
<summary><b>📁 1. Unit of Work & Generic Repositories (Data Access)</b></summary>

#### `IRepository<T>` (Base Generic CRUD)
- **File:** `Core/Interfaces/IRepository.cs`
- **Implementation:** `Infrastructure/Repositories/Repository.cs`

| Method | Parameters | Return Type | Description |
| :--- | :--- | :--- | :--- |
| `GetByIdAsync` | `int id`, `CancellationToken cancellationToken = default` | `Task<T?>` | Retrieves an entity by its integer primary key. Returns `null` if not found. |
| `GetAllAsync` | `CancellationToken cancellationToken = default` | `Task<IReadOnlyList<T>>` | Retrieves all entities of type `T` from the database table. |
| `FindAsync` | `Expression<Func<T, bool>> predicate`, `CancellationToken cancellationToken = default` | `Task<IReadOnlyList<T>>` | Finds all records matching a LINQ expression filter (e.g. `s => s.IsActive`). |
| `AddAsync` | `T entity`, `CancellationToken cancellationToken = default` | `Task<T>` | Attaches a new entity to the change tracker to be inserted upon save. |
| `UpdateAsync` | `T entity`, `CancellationToken cancellationToken = default` | `Task` | Marks an existing entity as modified in the change tracker. |
| `DeleteAsync` | `T entity`, `CancellationToken cancellationToken = default` | `Task` | Marks an entity for deletion from the database upon save. |

#### `IStudentRepository`
- **File:** `Core/Interfaces/IStudentRepository.cs`
- **Implementation:** `Infrastructure/Repositories/StudentRepository.cs`

| Method | Parameters | Return Type | Description |
| :--- | :--- | :--- | :--- |
| `GetStudentWithProfileAsync` | `int studentId`, `CancellationToken cancellationToken = default` | `Task<Student?>` | Eager loads student record with guardians, parents' user accounts, and class history. |
| `GetStudentsByClassIdAsync` | `int classId`, `CancellationToken cancellationToken = default` | `Task<IReadOnlyList<Student>>` | Returns all active students currently assigned to a specific class ID. |
| `GetByStudentCodeAsync` | `string studentCode`, `CancellationToken cancellationToken = default` | `Task<Student?>` | Finds an enrolled student by their unique student identification code. |

#### `IAttendanceRepository`
- **File:** `Core/Interfaces/IAttendanceRepository.cs`
- **Implementation:** `Infrastructure/Repositories/AttendanceRepository.cs`

| Method | Parameters | Return Type | Description |
| :--- | :--- | :--- | :--- |
| `GetDailyClassAttendanceAsync` | `int classId`, `DateTime date`, `CancellationToken cancellationToken = default` | `Task<IReadOnlyList<AttendanceRecord>>` | Retrieves roll call records for a whole class on a specific calendar date. |
| `GetStudentAttendanceHistoryAsync` | `int studentId`, `DateTime fromDate`, `DateTime toDate`, `CancellationToken cancellationToken = default` | `Task<IReadOnlyList<AttendanceRecord>>` | Gets historical attendance records for an individual student within a date range. |

#### `ILunchRepository`
- **File:** `Core/Interfaces/ILunchRepository.cs`
- **Implementation:** `Infrastructure/Repositories/LunchRepository.cs`

| Method | Parameters | Return Type | Description |
| :--- | :--- | :--- | :--- |
| `GetDailyRegistrationsAsync` | `DateTime date`, `CancellationToken cancellationToken = default` | `Task<IReadOnlyList<LunchRegistration>>` | Retrieves all active student boarding lunch registrations for a specific service date. |
| `GetStudentRegistrationForDateAsync` | `int studentId`, `DateTime date`, `CancellationToken cancellationToken = default` | `Task<LunchRegistration?>` | Checks whether a student is registered for lunch on a given date. |

#### `IUnitOfWork` (Transaction & Persistence Coordinator)
- **File:** `Core/Interfaces/IUnitOfWork.cs`
- **Implementation:** `Infrastructure/Repositories/UnitOfWork.cs`

| Member / Method | Parameters | Return Type | Description |
| :--- | :--- | :--- | :--- |
| `Students` | *(Property)* | `IStudentRepository` | Accessor for student repository queries. |
| `Attendance` | *(Property)* | `IAttendanceRepository` | Accessor for attendance repository queries. |
| `Lunch` | *(Property)* | `ILunchRepository` | Accessor for lunch registration repository queries. |
| `Repository<T>()` | *(Generic method)* | `IRepository<T>` | Gets or creates a generic repository for any entity type `T`. |
| `SaveChangesAsync` | `CancellationToken cancellationToken = default` | `Task<int>` | Flushes all tracked entity changes to the database. |
| `BeginTransactionAsync` | `CancellationToken cancellationToken = default` | `Task` | Opens an ACID database transaction. |
| `CommitTransactionAsync` | `CancellationToken cancellationToken = default` | `Task` | Commits the open database transaction. Automatically rolls back on error. |
| `RollbackTransactionAsync`| `CancellationToken cancellationToken = default` | `Task` | Manually rolls back all operations inside the current transaction. |

</details>

<details>
<summary><b>📁 2. Business Logic Services (Services Layer)</b></summary>

#### `IAuthService`
- **File:** `Services/Interfaces/IAuthService.cs`
- **Implementation:** `Services/Implementations/AuthService.cs`

| Method | Parameters | Return Type | Description |
| :--- | :--- | :--- | :--- |
| `LoginAsync` | `LoginRequestDto request` (`Username`, `Password`), `CancellationToken cancellationToken = default` | `Task<ApiResponse<LoginResponseDto>>` | Validates credentials, issues JWT bearer token, and returns user roles and session expiry. |
| `ChangePasswordAsync` | `int userId`, `ChangePasswordRequestDto request` (`CurrentPassword`, `NewPassword`), `CancellationToken cancellationToken = default` | `Task<ApiResponse<bool>>` | Verifies the old password and sets the newly hashed password. |

#### `IStudentService`
- **File:** `Services/Interfaces/IStudentService.cs`
- **Implementation:** `Services/Implementations/StudentService.cs`

| Method | Parameters | Return Type | Description |
| :--- | :--- | :--- | :--- |
| `GetStudentProfileAsync` | `int studentId`, `CancellationToken cancellationToken = default` | `Task<ApiResponse<StudentProfileDto>>` | Fetches complete student profile details including class name and enrollment status. |
| `GetStudentsPagedAsync` | `int pageIndex`, `int pageSize`, `int? classId = null`, `CancellationToken cancellationToken = default` | `Task<ApiResponse<PaginatedResult<StudentProfileDto>>>` | Returns a paginated list of students, optionally filtered by class. |
| `CreateStudentAsync` | `CreateStudentDto request` (`StudentCode`, `FullName`, `DateOfBirth`, `Gender`, `InitialClassId`), `CancellationToken cancellationToken = default` | `Task<ApiResponse<StudentProfileDto>>` | Registers a new student into the school and assigns them to an initial class. |

#### `IAttendanceService`
- **File:** `Services/Interfaces/IAttendanceService.cs`
- **Implementation:** `Services/Implementations/AttendanceService.cs`

| Method | Parameters | Return Type | Description |
| :--- | :--- | :--- | :--- |
| `RecordDailyAttendanceAsync` | `int classId`, `DateTime date`, `List<DailyAttendanceEntryDto> entries` (`StudentId`, `Status`, `Remarks`), `int recordedByUserId`, `CancellationToken cancellationToken = default` | `Task<ApiResponse<bool>>` | Performs bulk attendance recording for a classroom on a specific calendar date. |
| `SubmitLeaveRequestAsync` | `int requesterUserId`, `LeaveRequestSubmitDto request` (`StudentId`, `StartDate`, `EndDate`, `Reason`, `DoctorNoteAttachmentUrl`), `CancellationToken cancellationToken = default` | `Task<ApiResponse<bool>>` | Submits a formal absence/leave request on behalf of a student. |

#### `ILunchService`
- **File:** `Services/Interfaces/ILunchService.cs`
- **Implementation:** `Services/Implementations/LunchService.cs`

| Method | Parameters | Return Type | Description |
| :--- | :--- | :--- | :--- |
| `RegisterLunchAsync` | `LunchRegistrationDto request` (`StudentId`, `Date`, `MealType`, `IsActive`), `CancellationToken cancellationToken = default` | `Task<ApiResponse<bool>>` | Registers or cancels a student's daily boarding lunch service. |
| `GetDailyMealCountAsync` | `DateTime date`, `CancellationToken cancellationToken = default` | `Task<ApiResponse<DailyMealCountReportDto>>` | Aggregates kitchen meal counts (Standard, Vegetarian, Special Diet) for a given date. |

#### `IRequestWorkflowEngine`
- **File:** `Services/Interfaces/IRequestWorkflowEngine.cs`
- **Implementation:** `Services/Implementations/RequestWorkflowEngine.cs`

| Method | Parameters | Return Type | Description |
| :--- | :--- | :--- | :--- |
| `SubmitClassTransferRequestAsync` | `int requesterUserId`, `ClassTransferRequestDto request` (`StudentId`, `CurrentClassId`, `TargetClassId`, `TransferReason`), `CancellationToken cancellationToken = default` | `Task<ApiResponse<string>>` | Initiates a class transfer request ticket and returns a unique request tracking number. |
| `SubmitDocumentRequestAsync` | `int requesterUserId`, `DocumentIssuanceRequestDto request` (`StudentId`, `DocumentType`, `NumberOfCopies`, `Purpose`), `CancellationToken cancellationToken = default` | `Task<ApiResponse<string>>` | Submits an official document issuance request (e.g. transcript, enrollment certificate). |
| `ProcessRequestAsync` | `int requestId`, `int reviewerUserId`, `ProcessRequestDto request` (`NewStatus`, `ReviewerNotes`), `CancellationToken cancellationToken = default` | `Task<ApiResponse<bool>>` | Processes an administrative request approval/rejection with reviewer audit remarks. |

#### `IAuditService`
- **File:** `Services/Interfaces/IAuditService.cs`
- **Implementation:** `Services/Implementations/AuditService.cs`

| Method | Parameters | Return Type | Description |
| :--- | :--- | :--- | :--- |
| `LogActionAsync` | `int? userId`, `string action`, `string entityName`, `string? entityId`, `string? oldValues`, `string? newValues`, `string? ipAddress`, `CancellationToken cancellationToken = default` | `Task` | Records an immutable administrative or security audit event in the database. |
| `GetAuditLogsPagedAsync` | `int pageIndex`, `int pageSize`, `CancellationToken cancellationToken = default` | `Task<ApiResponse<PaginatedResult<AuditLog>>>` | Queries paginated audit logs for system administrator inspection. |

#### `IStudentImporter` & `IStaffImporter`
- **File:** `Services/Interfaces/IStudentImporter.cs` & `IStaffImporter.cs`
- **Implementation:** `Services/Implementations/StudentImporter.cs` & `StaffImporter.cs`

| Method | Parameters | Return Type | Description |
| :--- | :--- | :--- | :--- |
| `ParseExcelAsync` | `Stream fileStream`, `CancellationToken cancellationToken = default` | `Task<ApiResponse<List<ImportStudentRowDto>>>` | Reads an uploaded `.xlsx` file using ClosedXML, validates each row, and reports row errors. |
| `ImportStudentsAsync` | `List<ImportStudentRowDto> validRows`, `CancellationToken cancellationToken = default` | `Task<ApiResponse<int>>` | Inserts validated student rows and their guardian links into the database. |
| `ImportStaffAsync` | `Stream fileStream`, `CancellationToken cancellationToken = default` | `Task<ApiResponse<int>>` | Parses and creates school staff profiles from an Excel roster file. |

</details>

<details>
<summary><b>📁 3. External Integration Services (Infrastructure Layer)</b></summary>

#### `IEmailService`
- **File:** `Infrastructure/ExternalServices/IEmailService.cs`
- **Implementation:** `Infrastructure/ExternalServices/EmailService.cs`

| Method | Parameters | Return Type | Description |
| :--- | :--- | :--- | :--- |
| `SendEmailAsync` | `string toEmail`, `string subject`, `string body`, `bool isHtml = true`, `CancellationToken cancellationToken = default` | `Task` | Sends an email notification or OTP to a recipient using MailKit / SMTP protocol. |

#### `IFileStorageService`
- **File:** `Infrastructure/ExternalServices/IFileStorageService.cs`
- **Implementation:** `Infrastructure/ExternalServices/FileStorageService.cs`

| Method | Parameters | Return Type | Description |
| :--- | :--- | :--- | :--- |
| `SaveFileAsync` | `Stream fileStream`, `string fileName`, `string folderName`, `CancellationToken cancellationToken = default` | `Task<string>` | Saves an uploaded document (e.g. medical note) to a dedicated folder and returns the relative path. |
| `GetFileAsync` | `string filePath`, `CancellationToken cancellationToken = default` | `Task<Stream?>` | Retrieves a readable stream for a stored file, or returns `null` if the file does not exist. |
| `DeleteFileAsync` | `string filePath`, `CancellationToken cancellationToken = default` | `Task<bool>` | Removes a stored document file from disk. Returns `true` if successful. |

</details>

### 2.3. EF Core CLI Quick Reference
Always run these commands from the `SAWP.Backend` directory:
- **Build Solution:**
  ```bash
  dotnet build SAWP.sln
  ```
- **Add a new migration:**
  ```bash
  dotnet ef migrations add <MigrationName> --project Infrastructure --startup-project API --output-dir Data/Migrations
  ```
- **Apply migrations to database:**
  ```bash
  dotnet ef database update --project Infrastructure --startup-project API
  ```
- **Remove last unapplied migration:**
  ```bash
  dotnet ef migrations remove --project Infrastructure --startup-project API
  ```
- **List existing migrations:**
  ```bash
  dotnet ef migrations list --project Infrastructure --startup-project API
  ```

---

## 3. Standard System Messages & Error Codes (SRS MSG01 - MSG50)

Per the **SRS Specification (Section 5.3 Message Dictionary)**, all user feedback, toast notifications, confirmation dialogs, inline form errors, and workflow alerts are standardized into 50 codes (**MSG01** through **MSG50**).

Both Backend and Frontend have pre-scaffolded, strongly-typed constants:
- **Backend:** `Core.Constants.SystemMessages` (in `SAWP.Backend/Core/Constants/SystemMessages.cs`)
- **Frontend:** `SYSTEM_MESSAGES` (in `sawp-frontend/src/constants/messages.ts`)

### 3.1. ApiResponse<T> Standard Payload Structure
All API endpoints return the standardized envelope `ApiResponse<T>` containing the optional standard `Code`:
```json
{
  "success": true,
  "code": "MSG08",
  "message": "Lưu thông tin thành công.",
  "data": { ... },
  "errors": null
}
```

### 3.2. Backend Usage in Services & Controllers

#### A. Direct Strong-Typed Constants (Recommended)
```csharp
using Core.Constants;
using Core.DTOs;

// 1. Success with standard message
return ApiResponse<bool>.Ok(true, SystemMessages.MSG08); // Code: "MSG08", Message: "Lưu thông tin thành công."
return ApiResponse<bool>.Ok(true, SystemMessages.MSG18); // Code: "MSG18", Message: "Đã lưu điểm danh."
return ApiResponse<bool>.Ok(true, SystemMessages.MSG25); // Code: "MSG25", Message: "Đăng ký ăn trưa thành công."

// 2. Failure / Validation error with standard message
return ApiResponse<StudentProfileDto>.Fail(SystemMessages.MSG11); // Code: "MSG11", Message: "Không tìm thấy hồ sơ học sinh."
return ApiResponse<bool>.Fail(SystemMessages.MSG01, new List<string> { "StudentCode is required." });
return ApiResponse<bool>.Fail(SystemMessages.MSG27); // Code: "MSG27", Message: "Đã quá thời hạn đăng ký hoặc hủy ăn trưa cho ngày này."
```

#### B. Dynamic Dictionary Lookup by String Key (`GetByCode` or `All[key]`)
When you receive or parse a message code as a string (e.g. from a configuration, external event, or dynamic validation rule):
```csharp
// Safe lookup: returns null if key does not exist (Case-insensitive)
SystemMessage? msg = SystemMessages.GetByCode("MSG01");
if (msg != null)
{
    return ApiResponse<bool>.Fail(msg);
}

// Or direct dictionary access via All:
SystemMessage msg2 = SystemMessages.All["MSG08"];
```

### 3.3. Frontend Usage in React Components

#### A. Dictionary Lookup by String Key (`getSystemMessage` or `SYSTEM_MESSAGES[key]`)
```tsx
import { SYSTEM_MESSAGES, getSystemMessage } from '@/constants/messages';
import { toast } from 'react-hot-toast';

// 1. Safe helper lookup (returns undefined if code is not found)
const msg = getSystemMessage("MSG08");
console.log(msg?.content); // "Lưu thông tin thành công."

// 2. Direct map indexer
const directMsg = SYSTEM_MESSAGES["MSG01"];
```

#### B. Handling API Responses with Toast Notifications
```tsx
// Handling an API response from Backend:
if (response.data.success) {
  // Show standard success toast
  toast.success(response.data.message || SYSTEM_MESSAGES.MSG08.content);
} else {
  // Fall back to dictionary if message text is missing
  const sysMsg = response.data.code ? getSystemMessage(response.data.code) : null;
  toast.error(sysMsg?.content || response.data.message || 'Error occurred');
}
```

### 3.4. Complete System Message Catalog (MSG01 - MSG50)

| Code | Type | Context | Default Vietnamese Message |
| :--- | :--- | :--- | :--- |
| **MSG01** | Inline / Toast (Error) | Required or invalid input | Vui lòng kiểm tra và nhập đầy đủ thông tin hợp lệ. |
| **MSG02** | Toast (Error) | Invalid login/current password | Tên đăng nhập hoặc mật khẩu không chính xác. |
| **MSG03** | Toast (Error) | Inactive/deactivated account | Tài khoản hiện không hoạt động. Vui lòng liên hệ quản trị viên. |
| **MSG04** | Inline (Error) | New password policy or confirmation fails | Mật khẩu mới chưa đáp ứng yêu cầu hoặc xác nhận mật khẩu không khớp. |
| **MSG05** | Toast (Success) | Login successful | Đăng nhập thành công. |
| **MSG06** | Toast (Success) | Logout successful | Đã đăng xuất khỏi hệ thống. |
| **MSG07** | Toast (Error) | Session expired | Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại. |
| **MSG08** | Toast (Success) | Record saved | Lưu thông tin thành công. |
| **MSG09** | Toast (Error) | Operation cannot be completed in current state | Không thể thực hiện thao tác với trạng thái hiện tại. |
| **MSG10** | Toast (Error) | Permission/data-scope denied | Bạn không có quyền truy cập hoặc thực hiện thao tác này. |
| **MSG11** | Toast (Error) | Student not found | Không tìm thấy hồ sơ học sinh. |
| **MSG12** | Inline / Toast (Error) | Duplicate student code | Mã học sinh đã tồn tại trong hệ thống. |
| **MSG13** | Toast (Success) | Guardian link updated | Đã cập nhật liên kết phụ huynh/người giám hộ. |
| **MSG14** | Toast (Error) | No active school year | Chưa có năm học đang hoạt động. |
| **MSG15** | Toast (Error) | Class unavailable/capacity conflict | Lớp được chọn không còn phù hợp hoặc đã đạt giới hạn sĩ số. |
| **MSG16** | Toast (Success) | Class assignment saved | Đã cập nhật phân lớp cho học sinh. |
| **MSG17** | Inline / Toast (Error) | Schedule conflict | Thời khóa biểu bị trùng khung giờ. Vui lòng kiểm tra lại. |
| **MSG18** | Toast (Success) | Attendance saved | Đã lưu điểm danh. |
| **MSG19** | Toast (Error) | Attendance record locked | Bản ghi điểm danh đã bị khóa. Vui lòng liên hệ Giáo vụ nếu cần điều chỉnh. |
| **MSG20** | Toast (Error) | Teacher not authorized for class | Bạn không được phân công phụ trách lớp này. |
| **MSG21** | Toast (Success) | Leave request submitted | Đã gửi đơn xin nghỉ/vắng học. |
| **MSG22** | Notification / Toast (Success) | Leave approved | Đơn xin nghỉ/vắng học đã được chấp thuận. |
| **MSG23** | Notification / Toast (Info) | Leave rejected | Đơn xin nghỉ/vắng học không được chấp thuận. |
| **MSG24** | Inline / Toast (Error) | Invalid/overlapping leave dates | Khoảng thời gian xin nghỉ không hợp lệ hoặc đang trùng với yêu cầu khác. |
| **MSG25** | Toast (Success) | Lunch registration successful | Đăng ký ăn trưa thành công. |
| **MSG26** | Toast (Success) | Lunch cancellation successful | Đã hủy đăng ký ăn trưa. |
| **MSG27** | Toast (Error) | Lunch cutoff passed | Đã quá thời hạn đăng ký hoặc hủy ăn trưa cho ngày này. |
| **MSG28** | Toast (Error) | Lunch service unavailable | Dịch vụ ăn trưa không khả dụng cho ngày đã chọn. |
| **MSG29** | Toast (Info) | Duplicate lunch registration | Học sinh đã có đăng ký ăn trưa cho ngày này. |
| **MSG30** | Toast (Success) | Administrative request submitted | Đã gửi yêu cầu hành chính. |
| **MSG31** | Notification / Toast (Success) | Request approved | Yêu cầu đã được phê duyệt. |
| **MSG32** | Notification / Toast (Info) | Request rejected | Yêu cầu không được phê duyệt. |
| **MSG33** | Notification / Toast (Success) | Request completed | Yêu cầu đã được xử lý hoàn tất. |
| **MSG34** | Toast (Error) | Request cannot be cancelled | Yêu cầu không thể hủy ở trạng thái hiện tại. |
| **MSG35** | Notification / Toast (Success) | Class transfer applied | Đã cập nhật chuyển lớp cho học sinh. |
| **MSG36** | Notification / Toast (Success) | School document issued | Tài liệu/giấy xác nhận đã được xử lý và phát hành. |
| **MSG37** | Toast (Success) | Notifications marked read | Đã đánh dấu thông báo là đã đọc. |
| **MSG38** | Toast (Success) | User account created | Tạo tài khoản người dùng thành công. |
| **MSG39** | Toast (Success) | User account deactivated | Đã vô hiệu hóa tài khoản người dùng. |
| **MSG40** | Toast (Success) | Role/permission updated | Đã cập nhật vai trò và quyền truy cập. |
| **MSG41** | Inline / Toast (Error) | Invalid attachment/import file | Tệp không hợp lệ, không đúng định dạng hoặc vượt quá dung lượng cho phép. |
| **MSG42** | Toast (Error) | Audit-log access denied | Bạn không có quyền xem nhật ký hệ thống. |
| **MSG43** | Empty-state message | No matching data | Không có dữ liệu phù hợp để hiển thị. |
| **MSG44** | Toast (Success) | Configuration saved | Đã lưu cấu hình hệ thống. |
| **MSG45** | Confirmation Dialog | Confirm sensitive action | Bạn có chắc chắn muốn thực hiện thao tác này? |
| **MSG46** | Toast (Success) | Password changed | Đổi mật khẩu thành công. |
| **MSG47** | Toast (Success) | Profile/contact updated | Cập nhật thông tin thành công. |
| **MSG48** | Toast / Summary | Import completed | Nhập dữ liệu hoàn tất. Vui lòng kiểm tra kết quả các bản ghi thành công và lỗi. |
| **MSG49** | Toast / Summary (Error) | Import has no/partial valid rows | Một số dữ liệu không thể nhập. Vui lòng kiểm tra danh sách lỗi. |
| **MSG50** | Toast (Error) | Unexpected system error | Đã xảy ra lỗi hệ thống. Vui lòng thử lại sau. |

---

## 4. Frontend Developer Guide (FE)

### 4.1. Architecture & Design Principles
The frontend in `sawp-frontend/` is built with **React 18 + TypeScript + Vite**. It strictly follows a **Contract-First** approach:
1. **Types (`src/types/`):** TypeScript interfaces mirroring backend DTOs (`common.types.ts`, `auth.types.ts`, `student.types.ts`, etc.).
2. **API Contracts (`src/services/contracts/`):** Explicit interfaces for API interactions (`IAuthApi.ts`, `IStudentApi.ts`).
3. **API Implementations (`src/services/implementations/`):** Axios-based client implementations communicating through `apiClients.ts` with Bearer token interceptors.
4. **Portals (`src/pages/`):** Role-partitioned portals:
   - `ParentPortal/`: Attendance tracking, meal booking, leave submissions.
   - `TeacherPortal/`: Roll call, homeroom timetable, leave approvals.
   - `OfficeStaffPortal/`: Student roster, class distribution, document processing.
   - `ServiceStaffPortal/`: Kitchen meal aggregation dashboard.
   - `SchoolBoardPortal/`: Administrative executive dashboards, transfer approvals.
   - `AdminPortal/`: User roles, access controls, audit logs.

---

## 4. Standard Prompt for AI UI Generation (Playful Geometric Theme)

Frontend developers should use the following standardized prompt when prompting AI coding assistants (such as Antigravity, Claude, ChatGPT, etc.) to generate components and pages for the SAWP project. This guarantees strict visual consistency with the **Playful Geometric Design System** tailored for primary education.

### Copy-Paste AI Generation Prompt:

```markdown
<role>
You are an expert frontend engineer, UI/UX designer, visual design specialist, and typography expert. Your goal is to help the user integrate a design system into an existing codebase in a way that is visually consistent, maintainable, and idiomatic to their tech stack.

Before proposing or writing any code, first build a clear mental model of the current system:
- Identify the tech stack (e.g. React, Next.js, Vue, Tailwind, shadcn/ui, etc.).
- Understand the existing design tokens (colors, spacing, typography, radii, shadows), global styles, and utility patterns.
- Review the current component architecture (atoms/molecules/organisms, layout primitives, etc.) and naming conventions.
- Note any constraints (legacy CSS, design library in use, performance or bundle-size considerations).

Ask the user focused questions to understand the user's goals. Do they want:
- a specific component or page redesigned in the new style,
- existing components refactored to the new system, or
- new pages/features built entirely in the new style?

Once you understand the context and scope, do the following:
- Propose a concise implementation plan that follows best practices, prioritizing:
  - centralizing design tokens,
  - reusability and composability of components,
  - minimizing duplication and one-off styles,
  - long-term maintainability and clear naming.
- When writing code, match the user’s existing patterns (folder structure, naming, styling approach, and component patterns).
- Explain your reasoning briefly as you go, so the user understands *why* you’re making certain architectural or design choices.

Always aim to:
- Preserve or improve accessibility.
- Maintain visual consistency with the provided design system.
- Leave the codebase in a cleaner, more coherent state than you found it.
- Ensure layouts are responsive and usable across devices.
- Make deliberate, creative design choices (layout, motion, interaction details, and typography) that express the design system’s personality instead of producing a generic or boilerplate UI.
</role>

<design-system>
# Playful Geometric Design System

## Design Philosophy

**Playful Geometric** is the antidote to sterile, corporate minimalism. It creates an emotional connection through **optimism, clarity, and tactile fun**.

The core concept is **"Stable Grid, Wild Decoration"**. The content itself (text, forms) lives in clean, readable areas, but the world around it is alive with movement and shape. It references the **Memphis Group** (80s) but cleans it up for modern digital screens—removing the chaos while keeping the energy.

### The Vibe
**Friendly. Tactile. Pop. Energetic.**
It feels like a playground or a well-organized sticker book. It invites clicking. It smiles at you.

### Visual Signatures
- **Primitive Shapes**: Circles, triangles, squares, pill shapes, and squiggles used as background elements, masks, or icons.
- **Hard Shadows**: Elements often have a hard, offset drop shadow (no blur) giving a sticker or cut-out paper feel.
- **Pattern Fills**: Polka dots, grid lines, and diagonal stripes used to fill shapes or backgrounds.
- **Varied Radii**: Mixing fully rounded corners with sharp ones to create "leaf" shapes or asymmetric blobs.

---

## Design Token System

### Colors (Light Mode)
A punchy, high-saturation palette anchored by strong neutrals.

```
background:        #FFFDF5    // Warm Cream/Off-White (Paper feel)
foreground:        #1E293B    // Slate 800 (Softer than black)
muted:             #F1F5F9    // Slate 100
mutedForeground:   #64748B    // Slate 500
accent:            #8B5CF6    // Vivid Violet (Primary Brand)
accentForeground:  #FFFFFF    // White
secondary:         #F472B6    // Hot Pink (Playful pop)
tertiary:          #FBBF24    // Amber/Yellow (Optimism)
quaternary:        #34D399    // Emerald/Mint (Freshness)
border:            #E2E8F0    // Slate 200
input:             #FFFFFF    // White
card:              #FFFFFF    // White
ring:              #8B5CF6    // Violet Focus
```

**Usage Rule**: Use `accent` for primary actions. Use `secondary`, `tertiary`, and `quaternary` rotationally for decorative shapes, icons, or emphasized words to create a "confetti" effect.

### Typography

**Headings**: `"Outfit", system-ui, sans-serif`
- A geometric sans with character. Rounded corners on letters make it friendly.
- **Weights**: Bold (700) or ExtraBold (800).

**Body**: `"Plus Jakarta Sans", system-ui, sans-serif`
- Highly legible, modern, geometric but humanist.
- **Weights**: Regular (400), Medium (500).

**Scale Ratio**: 1.25 (Major Third) - melodic and harmonious.

### Radius & Border

```
radius-sm:   8px
radius-md:   16px
radius-lg:   24px
radius-full: 9999px
border-width: 2px     // Chunky borders by default
```

**Special "Blob" Radius**: `rounded-tl-2xl rounded-tr-2xl rounded-br-2xl rounded-bl-none` (Speech bubble style) or `rounded-t-full rounded-b-none` (Arch).

### Shadows & Effects

**The "Pop" Shadow (Hard Shadow)**:
```
box-shadow: 4px 4px 0px 0px #1E293B;  // Dark hard shadow
box-shadow-hover: 6px 6px 0px 0px #1E293B; // Lift effect
box-shadow-active: 2px 2px 0px 0px #1E293B; // Press effect
```
No blur. Solid offset colors.

### Textures & Patterns
- **Dot Grid**: A background of small dots (`bg-[url(...)]`) in strict formation.
- **Squiggles**: SVG paths used as section dividers or underlining for headings.
- **Confetti**: Small SVG shapes (triangles, circles) absolutely positioned behind main content blocks.

---

## Component Stylings

### Buttons

**Primary Button ("The Candy Button")**:
```
- Bg: accent (#8B5CF6)
- Text: white, font-weight: 700
- Radius: rounded-full (Pill)
- Border: 2px solid #1E293B (Dark border around color)
- Shadow: 4px 4px 0px #1E293B (Hard shadow)
- Hover: translate-x-[-2px] translate-y-[-2px], shadow extends to 6px 6px
- Active: translate-x-[2px] translate-y-[2px], shadow shrinks to 2px 2px
- Icon: ArrowRight, circular background (white) inside button
```

**Secondary Button**:
```
- Bg: transparent
- Text: foreground
- Border: 2px solid #1E293B
- Radius: rounded-full
- Shadow: none
- Hover: bg-tertiary (#FBBF24) - Fills with yellow on hover
```

### Cards

**The "Sticker" Card**:
```
- Bg: white
- Border: 2px solid #1E293B
- Radius: rounded-xl
- Shadow: 8px 8px 0px #E2E8F0 (Soft hard shadow) or #F472B6 (Pink shadow for featured)
- Hover: Rotate -1deg, Scale 1.02 (Wiggle effect)
- Title: Bold Outfit font
- Icon: Floating circle div with centered icon, sitting half-in/half-out of the top border.
```

### Inputs

```
- Bg: white
- Border: 2px solid #CBD5E1
- Radius: rounded-lg
- Text: foreground
- Shadow: 4px 4px 0px transparent (hidden initially)
- Focus: Border accent, Shadow 4px 4px 0px accent (Hard color shadow on focus)
- Label: Bold, uppercase, small tracking-wide.
```

---

## Layout Strategy

### General
- **Container**: `max-w-6xl` (Generous width).
- **Spacing**: `py-24` (96px). Spacious but not empty; filled with patterns.
- **Grid**: 12-column logic, but grouped into big blocks (6/6 or 4/4/4).

### Unique Section Layouts
1.  **Hero**:
    - Text left, Image right.
    - **Decoration**: A massive yellow circle behind the text. A dotted pattern behind the image. The image itself has a "blob" mask (CSS clip-path or border-radius manipulation).
2.  **Features**:
    - Grid of 3.
    - **Decoration**: Each card is connected by a dashed SVG line drawn in the background.
    - Alternating colors for card headers (Violet, Pink, Yellow).
3.  **Pricing**:
    - The middle card is scaled up (1.1) and has a massive yellow star badge "MOST POPULAR" rotated 15deg.

---

## Effects & Animation

**Feel**: Bouncy, Elastic, Fun.

- **Hover**: `transition-all duration-300 ease-[cubic-bezier(0.34,1.56,0.64,1)]` (Overshoot/Bounciness).
- **Entrance**: Elements shouldn't just fade in; they should **pop** in (Scale 0->1 with bounce).
- **Marquee**: Use infinite scrolling text for client logos or keywords.
- **Wiggle**: Keyframe animation `rotate: 0deg -> 3deg -> -3deg -> 0deg` on hover for icons.

---

## Iconography

**Lucide React** settings:
- **Stroke Width**: `2.5px` (Bold/Chunky).
- **Style**: Round line caps, round line joins.
- **Color**: Often white inside a colored circle, or the dark foreground color.
- **Usage**: Enclosed in shapes. Never floating alone. A "Check" icon isn't just a check; it's a check inside a green circle.

---

## Responsive Strategy

- **Mobile**:
  - Stack everything.
  - Reduce "pop" shadows to 2px to save space.
  - Turn horizontal squiggle lines into vertical dividers.
  - Keep buttons big and tappable (min 48px height).
  - Hide complex background floating shapes that might overlap text.

---

## Accessibility & Best Practices

- **Contrast**: The text is slate-800 on off-white/white, which is AAA.
- **Color**: Never rely *only* on color. Use shapes and text labels.
- **Motion**: Respect `prefers-reduced-motion`. Disable the "bounce" and "wiggle" effects if preferred.
- **Focus**: The focus state is high-contrast (thick colored border + hard shadow).
</design-system>
```

---

## 4. Frontend Developer Guide - Workflow 1: Student Profile & Class Placement

### 4.1. Overview & Architecture
The frontend application (`sawp-frontend`) is built with React 19 + TypeScript + Vite, strictly following the **Playful Geometric Design System** ("Stable Grid, Wild Decoration").

```
sawp-frontend/src/
├── components/
│   ├── auth/                     # Authentication modals (ChangePassword)
│   ├── common/                   # Reusable GeometricModal (pop-in, floating icon, pill badges)
│   ├── students/                 # Student Profile modals (Create, DuplicateResolve, ExcelImport, StudentDetail)
│   ├── classes/                  # Class Placement modals (BatchInit, AssignTeacher, AssignStudents, ClassDetail)
│   └── parent/                   # Parent modals (LinkChildModal)
├── pages/
│   ├── Auth/LoginPage.tsx        # Login page with demo role pills
│   ├── Dashboard/DashboardPage.tsx # Overview dashboard with role-based action cards
│   ├── OfficeStaffPortal/
│   │   ├── StudentsPage.tsx      # UC-08 & UC-09 Student master profile CRUD & Excel bulk import
│   │   ├── ClassesPage.tsx       # UC-13 to UC-16 Class management, homeroom & student placement
│   │   └── LinkRequestsPage.tsx  # Step 8 & 9 Parent-child linkage verification queue
│   ├── TeacherPortal/
│   │   └── HomeroomRosterPage.tsx # UC-17 Official homeroom class roster (BR-07, BR-20)
│   └── ParentPortal/
│       └── ChildDashboardPage.tsx # Step 10 Linked children dashboard & placement result
├── services/
│   ├── apiClient.ts              # Axios instance configured to port 5299 with JWT interceptors
│   └── implementations/
│       ├── authApi.ts            # Authentication API client
│       ├── studentApi.ts         # Student profiles, duplicate check/resolve & Excel import
│       ├── classApi.ts           # Academic years, classes, teacher assignment & placements
│       └── guardianApi.ts        # Child linkage requests, review & parent dashboard
├── styles/
│   ├── tokens.css                # Playful Geometric design tokens (HSL tailored, hard shadows)
│   ├── base.css                  # Typography (Outfit, Plus Jakarta Sans), dot grid, squiggles
│   └── components.css            # Candy buttons, sticker cards, data tables, modal popups, pill tabs
└── types/                        # Strongly-typed TypeScript DTOs matching backend C# models
```

### 4.2. Playful Geometric Design System Integration
- **Color Palette:**
  - Background: `#FFFDF5` (Warm Cream / Paper feel with 22px dot-grid pattern).
  - Neutrals: `#1E293B` (Slate 800) for chunky 2px borders, text, and hard shadows.
  - Accent (Primary): `#8B5CF6` (Vivid Violet) for primary candy buttons and active tabs.
  - Secondary: `#F472B6` (Hot Pink) for duplicate alerts, parent badges, and featured shadows.
  - Tertiary: `#FBBF24` (Amber) for school year badges, warning banners, and floating icons.
  - Quaternary: `#34D399` (Mint) for active status chips, verified badges, and capacity bars.
- **Hard Pop Shadows:**
  - Button/Card: `box-shadow: 4px 4px 0px 0px #1E293B;`
  - Featured / Hover: `box-shadow: 6px 6px 0px 0px #1E293B; transform: translate(-2px, -2px);`
  - Active Press: `box-shadow: 2px 2px 0px 0px #1E293B; transform: translate(2px, 2px);`
- **Typography:**
  - Headings: `"Outfit", system-ui, sans-serif` (Weights: 700, 800).
  - Body: `"Plus Jakarta Sans", system-ui, sans-serif` (Weights: 400, 500, 600, 700).

### 4.3. Role & Page Matrix
| Portal / Role | Page Path | Component | Business Capabilities |
|---|---|---|---|
| **Giáo vụ (OfficeStaff)** | `/students` | `StudentsPage.tsx` | - Xem danh sách học sinh toàn trường kèm bộ lọc khối/lớp/trạng thái.<br>- Thêm mới hồ sơ (kiểm tra trùng lặp trên blur).<br>- Tải tệp Excel mẫu và nhập hàng loạt (ClosedXML) có xem trước.<br>- Giải quyết trùng mã (Hợp nhất, Ghi đè, Đổi mã mới).<br>- Kích hoạt hồ sơ học sinh (Active). |
| **Giáo vụ (OfficeStaff)** | `/classes` | `ClassesPage.tsx` | - Chọn và chuyển đổi linh hoạt giữa các năm học.<br>- Khởi tạo nhanh danh sách lớp (1A, 1B, 1C) theo khối và sức chứa.<br>- Phân công Giáo viên chủ nhiệm theo quy tắc BR-16.<br>- Xếp học sinh vào lớp kiểm tra sức chứa thời gian thực (BR-15, BR-18).<br>- Xem danh sách lớp và gỡ học sinh khỏi lớp.<br>- Công bố kết quả xếp lớp toàn trường tới Phụ huynh & GV. |
| **Giáo vụ (OfficeStaff)** | `/link-requests` | `LinkRequestsPage.tsx` | - Hàng đợi duyệt yêu cầu liên kết của phụ huynh.<br>- Nhận diện đối chiếu tự động (Khớp ngày sinh & SĐT phụ huynh).<br>- Phê duyệt hoặc từ chối kèm lý do phản hồi. |
| **Giáo viên (Teacher)** | `/teacher/roster` | `HomeroomRosterPage.tsx` | - Chỉ truy xuất danh sách của lớp mà mình làm chủ nhiệm (BR-07, BR-20).<br>- Hiển thị tỉ lệ Nam/Nữ, SĐT liên hệ của phụ huynh chính và số khẩn cấp.<br>- Hỗ trợ chế độ In danh sách / Xuất báo cáo. |
| **Phụ huynh (Parent)** | `/parent/children` | `ChildDashboardPage.tsx` | - Hiển thị sticker card từng con đã liên kết.<br>- Xem kết quả xếp lớp chính thức khi nhà trường đã công bố.<br>- Bấm nút "Liên kết thêm con" gửi yêu cầu tự động đối chiếu. |

### 4.4. Hướng dẫn kiểm thử giao diện Frontend
1. Mở trình duyệt truy cập: **`http://localhost:5173/login`**.
2. **Đăng nhập bằng tài khoản mẫu:**
   - Bấm nút chip **"Giáo vụ"** (office1 / Password@123) ➜ Vào `/students` kiểm tra CRUD, nhập Excel; vào `/classes` khởi tạo lớp và phân công; vào `/link-requests` duyệt yêu cầu.
   - Bấm nút chip **"Giáo viên"** (teacher1 / Password@123) ➜ Vào menu **"Lớp chủ nhiệm (WF1)"** xem danh sách lớp 1A.
   - Bấm nút chip **"Phụ huynh"** (parent1 / Password@123) ➜ Vào menu **"Hồ sơ các con (WF1)"** xem kết quả xếp lớp của con và gửi yêu cầu liên kết.


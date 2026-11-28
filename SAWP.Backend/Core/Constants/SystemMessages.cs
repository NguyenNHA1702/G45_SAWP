namespace Core.Constants;

/// <summary>
/// Standard System Messages defined in SRS Report 3 (MSG01 to MSG319).
/// </summary>
public static class SystemMessages
{
    /// <summary>[Inline / Toast (Error)] Required or invalid input: Vui lòng kiểm tra và nhập đầy đủ thông tin hợp lệ.</summary>
    public static readonly SystemMessage MSG01 = new("MSG01", "Inline / Toast (Error)", "Required or invalid input", "Vui lòng kiểm tra và nhập đầy đủ thông tin hợp lệ.");
    public static readonly SystemMessage MSG01_RequiredOrInvalidInput = MSG01;

    /// <summary>[Toast (Error)] Invalid login/current password: Email hoặc mật khẩu không chính xác.</summary>
    public static readonly SystemMessage MSG02 = new("MSG02", "Toast (Error)", "Invalid login/current password", "Email hoặc mật khẩu không chính xác.");
    public static readonly SystemMessage MSG02_InvalidLoginCurrentPassword = MSG02;

    /// <summary>[Toast (Error)] Inactive/deactivated account: Tài khoản hiện không hoạt động. Vui lòng liên hệ quản trị viên.</summary>
    public static readonly SystemMessage MSG03 = new("MSG03", "Toast (Error)", "Inactive/deactivated account", "Tài khoản hiện không hoạt động. Vui lòng liên hệ quản trị viên.");
    public static readonly SystemMessage MSG03_InactiveDeactivatedAccount = MSG03;

    /// <summary>[Inline (Error)] New password policy or confirmation fails: Mật khẩu mới chưa đáp ứng yêu cầu hoặc xác nhận mật khẩu không khớp.</summary>
    public static readonly SystemMessage MSG04 = new("MSG04", "Inline (Error)", "New password policy or confirmation fails", "Mật khẩu mới chưa đáp ứng yêu cầu hoặc xác nhận mật khẩu không khớp.");
    public static readonly SystemMessage MSG04_NewPasswordPolicyOrConfirmationFails = MSG04;

    /// <summary>[Toast (Success)] Login successful: Đăng nhập thành công.</summary>
    public static readonly SystemMessage MSG05 = new("MSG05", "Toast (Success)", "Login successful", "Đăng nhập thành công.");
    public static readonly SystemMessage MSG05_LoginSuccessful = MSG05;

    /// <summary>[Toast (Success)] Logout successful: Đã đăng xuất khỏi hệ thống.</summary>
    public static readonly SystemMessage MSG06 = new("MSG06", "Toast (Success)", "Logout successful", "Đã đăng xuất khỏi hệ thống.");
    public static readonly SystemMessage MSG06_LogoutSuccessful = MSG06;

    /// <summary>[Toast (Error)] Session expired: Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại.</summary>
    public static readonly SystemMessage MSG07 = new("MSG07", "Toast (Error)", "Session expired", "Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại.");
    public static readonly SystemMessage MSG07_SessionExpired = MSG07;

    /// <summary>[Toast (Success)] Record saved: Lưu thông tin thành công.</summary>
    public static readonly SystemMessage MSG08 = new("MSG08", "Toast (Success)", "Record saved", "Lưu thông tin thành công.");
    public static readonly SystemMessage MSG08_RecordSaved = MSG08;

    /// <summary>[Toast (Error)] Operation cannot be completed in current state: Không thể thực hiện thao tác với trạng thái hiện tại.</summary>
    public static readonly SystemMessage MSG09 = new("MSG09", "Toast (Error)", "Operation cannot be completed in current state", "Không thể thực hiện thao tác với trạng thái hiện tại.");
    public static readonly SystemMessage MSG09_OperationCannotBeCompletedInCurrentState = MSG09;

    /// <summary>[Toast (Error)] Permission/data-scope denied: Bạn không có quyền truy cập hoặc thực hiện thao tác này.</summary>
    public static readonly SystemMessage MSG10 = new("MSG10", "Toast (Error)", "Permission/data-scope denied", "Bạn không có quyền truy cập hoặc thực hiện thao tác này.");
    public static readonly SystemMessage MSG10_PermissionDataScopeDenied = MSG10;

    /// <summary>[Toast (Error)] Student not found: Không tìm thấy hồ sơ học sinh.</summary>
    public static readonly SystemMessage MSG11 = new("MSG11", "Toast (Error)", "Student not found", "Không tìm thấy hồ sơ học sinh.");
    public static readonly SystemMessage MSG11_StudentNotFound = MSG11;

    /// <summary>[Inline / Toast (Error)] Duplicate student code: Mã học sinh đã tồn tại trong hệ thống.</summary>
    public static readonly SystemMessage MSG12 = new("MSG12", "Inline / Toast (Error)", "Duplicate student code", "Mã học sinh đã tồn tại trong hệ thống.");
    public static readonly SystemMessage MSG12_DuplicateStudentCode = MSG12;

    /// <summary>[Toast (Success)] Guardian link updated: Đã cập nhật liên kết phụ huynh/người giám hộ.</summary>
    public static readonly SystemMessage MSG13 = new("MSG13", "Toast (Success)", "Guardian link updated", "Đã cập nhật liên kết phụ huynh/người giám hộ.");
    public static readonly SystemMessage MSG13_GuardianLinkUpdated = MSG13;

    /// <summary>[Toast (Error)] No active school year: Chưa có năm học đang hoạt động.</summary>
    public static readonly SystemMessage MSG14 = new("MSG14", "Toast (Error)", "No active school year", "Chưa có năm học đang hoạt động.");
    public static readonly SystemMessage MSG14_NoActiveSchoolYear = MSG14;

    /// <summary>[Toast (Error)] Class unavailable/capacity conflict: Lớp được chọn không còn phù hợp hoặc đã đạt giới hạn sĩ số.</summary>
    public static readonly SystemMessage MSG15 = new("MSG15", "Toast (Error)", "Class unavailable/capacity conflict", "Lớp được chọn không còn phù hợp hoặc đã đạt giới hạn sĩ số.");
    public static readonly SystemMessage MSG15_ClassUnavailableCapacityConflict = MSG15;

    /// <summary>[Toast (Success)] Class assignment saved: Đã cập nhật phân lớp cho học sinh.</summary>
    public static readonly SystemMessage MSG16 = new("MSG16", "Toast (Success)", "Class assignment saved", "Đã cập nhật phân lớp cho học sinh.");
    public static readonly SystemMessage MSG16_ClassAssignmentSaved = MSG16;

    /// <summary>[Inline / Toast (Error)] Schedule conflict: Thời khóa biểu bị trùng khung giờ. Vui lòng kiểm tra lại.</summary>
    public static readonly SystemMessage MSG17 = new("MSG17", "Inline / Toast (Error)", "Schedule conflict", "Thời khóa biểu bị trùng khung giờ. Vui lòng kiểm tra lại.");
    public static readonly SystemMessage MSG17_ScheduleConflict = MSG17;

    /// <summary>[Toast (Success)] Attendance saved: Đã lưu điểm danh.</summary>
    public static readonly SystemMessage MSG18 = new("MSG18", "Toast (Success)", "Attendance saved", "Đã lưu điểm danh.");
    public static readonly SystemMessage MSG18_AttendanceSaved = MSG18;

    /// <summary>[Toast (Error)] Attendance record locked: Bản ghi điểm danh đã bị khóa. Vui lòng liên hệ Giáo vụ nếu cần điều chỉnh.</summary>
    public static readonly SystemMessage MSG19 = new("MSG19", "Toast (Error)", "Attendance record locked", "Bản ghi điểm danh đã bị khóa. Vui lòng liên hệ Giáo vụ nếu cần điều chỉnh.");
    public static readonly SystemMessage MSG19_AttendanceRecordLocked = MSG19;

    /// <summary>[Toast (Error)] Teacher not authorized for class: Bạn không được phân công phụ trách lớp này.</summary>
    public static readonly SystemMessage MSG20 = new("MSG20", "Toast (Error)", "Teacher not authorized for class", "Bạn không được phân công phụ trách lớp này.");
    public static readonly SystemMessage MSG20_TeacherNotAuthorizedForClass = MSG20;

    /// <summary>[Toast (Success)] Leave request submitted: Đã gửi đơn xin nghỉ/vắng học.</summary>
    public static readonly SystemMessage MSG21 = new("MSG21", "Toast (Success)", "Leave request submitted", "Đã gửi đơn xin nghỉ/vắng học.");
    public static readonly SystemMessage MSG21_LeaveRequestSubmitted = MSG21;

    /// <summary>[Notification / Toast (Success)] Leave approved: Đơn xin nghỉ/vắng học đã được chấp thuận.</summary>
    public static readonly SystemMessage MSG22 = new("MSG22", "Notification / Toast (Success)", "Leave approved", "Đơn xin nghỉ/vắng học đã được chấp thuận.");
    public static readonly SystemMessage MSG22_LeaveApproved = MSG22;

    /// <summary>[Notification / Toast (Info)] Leave rejected: Đơn xin nghỉ/vắng học không được chấp thuận.</summary>
    public static readonly SystemMessage MSG23 = new("MSG23", "Notification / Toast (Info)", "Leave rejected", "Đơn xin nghỉ/vắng học không được chấp thuận.");
    public static readonly SystemMessage MSG23_LeaveRejected = MSG23;

    /// <summary>[Inline / Toast (Error)] Invalid/overlapping leave dates: Khoảng thời gian xin nghỉ không hợp lệ hoặc đang trùng với yêu cầu khác.</summary>
    public static readonly SystemMessage MSG24 = new("MSG24", "Inline / Toast (Error)", "Invalid/overlapping leave dates", "Khoảng thời gian xin nghỉ không hợp lệ hoặc đang trùng với yêu cầu khác.");
    public static readonly SystemMessage MSG24_InvalidOverlappingLeaveDates = MSG24;

    /// <summary>[Toast (Success)] Lunch registration successful: Đăng ký ăn trưa thành công.</summary>
    public static readonly SystemMessage MSG25 = new("MSG25", "Toast (Success)", "Lunch registration successful", "Đăng ký ăn trưa thành công.");
    public static readonly SystemMessage MSG25_LunchRegistrationSuccessful = MSG25;

    /// <summary>[Toast (Success)] Lunch cancellation successful: Đã hủy đăng ký ăn trưa.</summary>
    public static readonly SystemMessage MSG26 = new("MSG26", "Toast (Success)", "Lunch cancellation successful", "Đã hủy đăng ký ăn trưa.");
    public static readonly SystemMessage MSG26_LunchCancellationSuccessful = MSG26;

    /// <summary>[Toast (Error)] Lunch cutoff passed: Đã quá thời hạn đăng ký hoặc hủy ăn trưa cho ngày này.</summary>
    public static readonly SystemMessage MSG27 = new("MSG27", "Toast (Error)", "Lunch cutoff passed", "Đã quá thời hạn đăng ký hoặc hủy ăn trưa cho ngày này.");
    public static readonly SystemMessage MSG27_LunchCutoffPassed = MSG27;

    /// <summary>[Toast (Error)] Lunch service unavailable: Dịch vụ ăn trưa không khả dụng cho ngày đã chọn.</summary>
    public static readonly SystemMessage MSG28 = new("MSG28", "Toast (Error)", "Lunch service unavailable", "Dịch vụ ăn trưa không khả dụng cho ngày đã chọn.");
    public static readonly SystemMessage MSG28_LunchServiceUnavailable = MSG28;

    /// <summary>[Toast (Info)] Duplicate lunch registration: Học sinh đã có đăng ký ăn trưa cho ngày này.</summary>
    public static readonly SystemMessage MSG29 = new("MSG29", "Toast (Info)", "Duplicate lunch registration", "Học sinh đã có đăng ký ăn trưa cho ngày này.");
    public static readonly SystemMessage MSG29_DuplicateLunchRegistration = MSG29;

    /// <summary>[Toast (Success)] Administrative request submitted: Đã gửi yêu cầu hành chính.</summary>
    public static readonly SystemMessage MSG30 = new("MSG30", "Toast (Success)", "Administrative request submitted", "Đã gửi yêu cầu hành chính.");
    public static readonly SystemMessage MSG30_AdministrativeRequestSubmitted = MSG30;

    /// <summary>[Notification / Toast (Success)] Request approved: Yêu cầu đã được phê duyệt.</summary>
    public static readonly SystemMessage MSG31 = new("MSG31", "Notification / Toast (Success)", "Request approved", "Yêu cầu đã được phê duyệt.");
    public static readonly SystemMessage MSG31_RequestApproved = MSG31;

    /// <summary>[Notification / Toast (Info)] Request rejected: Yêu cầu không được phê duyệt.</summary>
    public static readonly SystemMessage MSG32 = new("MSG32", "Notification / Toast (Info)", "Request rejected", "Yêu cầu không được phê duyệt.");
    public static readonly SystemMessage MSG32_RequestRejected = MSG32;

    /// <summary>[Notification / Toast (Success)] Request completed: Yêu cầu đã được xử lý hoàn tất.</summary>
    public static readonly SystemMessage MSG33 = new("MSG33", "Notification / Toast (Success)", "Request completed", "Yêu cầu đã được xử lý hoàn tất.");
    public static readonly SystemMessage MSG33_RequestCompleted = MSG33;

    /// <summary>[Toast (Error)] Request cannot be cancelled: Yêu cầu không thể hủy ở trạng thái hiện tại.</summary>
    public static readonly SystemMessage MSG34 = new("MSG34", "Toast (Error)", "Request cannot be cancelled", "Yêu cầu không thể hủy ở trạng thái hiện tại.");
    public static readonly SystemMessage MSG34_RequestCannotBeCancelled = MSG34;

    /// <summary>[Notification / Toast (Success)] Class transfer applied: Đã cập nhật chuyển lớp cho học sinh.</summary>
    public static readonly SystemMessage MSG35 = new("MSG35", "Notification / Toast (Success)", "Class transfer applied", "Đã cập nhật chuyển lớp cho học sinh.");
    public static readonly SystemMessage MSG35_ClassTransferApplied = MSG35;

    /// <summary>[Notification / Toast (Success)] School document issued: Tài liệu/giấy xác nhận đã được xử lý và phát hành.</summary>
    public static readonly SystemMessage MSG36 = new("MSG36", "Notification / Toast (Success)", "School document issued", "Tài liệu/giấy xác nhận đã được xử lý và phát hành.");
    public static readonly SystemMessage MSG36_SchoolDocumentIssued = MSG36;

    /// <summary>[Toast (Success)] Notifications marked read: Đã đánh dấu thông báo là đã đọc.</summary>
    public static readonly SystemMessage MSG37 = new("MSG37", "Toast (Success)", "Notifications marked read", "Đã đánh dấu thông báo là đã đọc.");
    public static readonly SystemMessage MSG37_NotificationsMarkedRead = MSG37;

    /// <summary>[Toast (Success)] User account created: Tạo tài khoản người dùng thành công.</summary>
    public static readonly SystemMessage MSG38 = new("MSG38", "Toast (Success)", "User account created", "Tạo tài khoản người dùng thành công.");
    public static readonly SystemMessage MSG38_UserAccountCreated = MSG38;

    /// <summary>[Toast (Success)] User account deactivated: Đã vô hiệu hóa tài khoản người dùng.</summary>
    public static readonly SystemMessage MSG39 = new("MSG39", "Toast (Success)", "User account deactivated", "Đã vô hiệu hóa tài khoản người dùng.");
    public static readonly SystemMessage MSG39_UserAccountDeactivated = MSG39;

    /// <summary>[Toast (Success)] Role/permission updated: Đã cập nhật vai trò và quyền truy cập.</summary>
    public static readonly SystemMessage MSG40 = new("MSG40", "Toast (Success)", "Role/permission updated", "Đã cập nhật vai trò và quyền truy cập.");
    public static readonly SystemMessage MSG40_RolePermissionUpdated = MSG40;

    /// <summary>[Inline / Toast (Error)] Invalid attachment/import file: Tệp không hợp lệ, không đúng định dạng hoặc vượt quá dung lượng cho phép.</summary>
    public static readonly SystemMessage MSG41 = new("MSG41", "Inline / Toast (Error)", "Invalid attachment/import file", "Tệp không hợp lệ, không đúng định dạng hoặc vượt quá dung lượng cho phép.");
    public static readonly SystemMessage MSG41_InvalidAttachmentImportFile = MSG41;

    /// <summary>[Toast (Error)] Audit-log access denied: Bạn không có quyền xem nhật ký hệ thống.</summary>
    public static readonly SystemMessage MSG42 = new("MSG42", "Toast (Error)", "Audit-log access denied", "Bạn không có quyền xem nhật ký hệ thống.");
    public static readonly SystemMessage MSG42_AuditLogAccessDenied = MSG42;

    /// <summary>[Empty-state message] No matching data: Không có dữ liệu phù hợp để hiển thị.</summary>
    public static readonly SystemMessage MSG43 = new("MSG43", "Empty-state message", "No matching data", "Không có dữ liệu phù hợp để hiển thị.");
    public static readonly SystemMessage MSG43_NoMatchingData = MSG43;

    /// <summary>[Toast (Success)] Configuration saved: Đã lưu cấu hình hệ thống.</summary>
    public static readonly SystemMessage MSG44 = new("MSG44", "Toast (Success)", "Configuration saved", "Đã lưu cấu hình hệ thống.");
    public static readonly SystemMessage MSG44_ConfigurationSaved = MSG44;

    /// <summary>[Confirmation Dialog] Confirm sensitive action: Bạn có chắc chắn muốn thực hiện thao tác này?</summary>
    public static readonly SystemMessage MSG45 = new("MSG45", "Confirmation Dialog", "Confirm sensitive action", "Bạn có chắc chắn muốn thực hiện thao tác này?");
    public static readonly SystemMessage MSG45_ConfirmSensitiveAction = MSG45;

    /// <summary>[Toast (Success)] Password changed: Đổi mật khẩu thành công.</summary>
    public static readonly SystemMessage MSG46 = new("MSG46", "Toast (Success)", "Password changed", "Đổi mật khẩu thành công.");
    public static readonly SystemMessage MSG46_PasswordChanged = MSG46;

    /// <summary>[Toast (Success)] Profile/contact updated: Cập nhật thông tin thành công.</summary>
    public static readonly SystemMessage MSG47 = new("MSG47", "Toast (Success)", "Profile/contact updated", "Cập nhật thông tin thành công.");
    public static readonly SystemMessage MSG47_ProfileContactUpdated = MSG47;

    /// <summary>[Toast / Summary] Import completed: Nhập dữ liệu hoàn tất. Vui lòng kiểm tra kết quả các bản ghi thành công và lỗi.</summary>
    public static readonly SystemMessage MSG48 = new("MSG48", "Toast / Summary", "Import completed", "Nhập dữ liệu hoàn tất. Vui lòng kiểm tra kết quả các bản ghi thành công và lỗi.");
    public static readonly SystemMessage MSG48_ImportCompleted = MSG48;

    /// <summary>[Toast / Summary (Error)] Import has no/partial valid rows: Một số dữ liệu không thể nhập. Vui lòng kiểm tra danh sách lỗi.</summary>
    public static readonly SystemMessage MSG49 = new("MSG49", "Toast / Summary (Error)", "Import has no/partial valid rows", "Một số dữ liệu không thể nhập. Vui lòng kiểm tra danh sách lỗi.");
    public static readonly SystemMessage MSG49_ImportHasNoPartialValidRows = MSG49;

    /// <summary>[Toast (Error)] Unexpected system error: Đã xảy ra lỗi hệ thống. Vui lòng thử lại sau.</summary>
    public static readonly SystemMessage MSG50 = new("MSG50", "Toast (Error)", "Unexpected system error", "Đã xảy ra lỗi hệ thống. Vui lòng thử lại sau.");
    public static readonly SystemMessage MSG50_UnexpectedSystemError = MSG50;

    /// <summary>[Toast (Success)] Class post published: Bài viết lớp học đã được đăng tải thành công.</summary>
    public static readonly SystemMessage MSG51 = new("MSG51", "Toast (Success)", "Class post published", "Bài viết lớp học đã được đăng tải thành công.");
    public static readonly SystemMessage MSG51_ClassPostPublished = MSG51;

    /// <summary>[Toast (Success)] Student evaluation submitted: Đã lưu và gửi phiếu nhận xét rèn luyện của học sinh.</summary>
    public static readonly SystemMessage MSG52 = new("MSG52", "Toast (Success)", "Student evaluation submitted", "Đã lưu và gửi phiếu nhận xét rèn luyện của học sinh.");
    public static readonly SystemMessage MSG52_StudentEvaluationSubmitted = MSG52;

    /// <summary>[Toast (Success)] Evaluation acknowledged: Đã xác nhận tiếp nhận thông tin và gửi phản hồi thành công.</summary>
    public static readonly SystemMessage MSG53 = new("MSG53", "Toast (Success)", "Evaluation acknowledged", "Đã xác nhận tiếp nhận thông tin và gửi phản hồi thành công.");
    public static readonly SystemMessage MSG53_EvaluationAcknowledged = MSG53;

    /// <summary>[Toast (Info)] Comments disabled: Bài viết này hiện đã tắt tính năng bình luận.</summary>
    public static readonly SystemMessage MSG54 = new("MSG54", "Toast (Info)", "Comments disabled", "Bài viết này hiện đã tắt tính năng bình luận.");
    public static readonly SystemMessage MSG54_CommentsDisabled = MSG54;

    /// <summary>[Toast (Success)] Contact note saved: Đã lưu ghi chú liên hệ gia đình.</summary>
    public static readonly SystemMessage MSG55 = new("MSG55", "Toast (Success)", "Contact note saved", "Đã lưu ghi chú liên hệ gia đình.");
    public static readonly SystemMessage MSG55_ContactNoteSaved = MSG55;

    /// <summary>[Toast (Info)] Recovery or activation link requested: Nếu địa chỉ email đã được đăng ký, một đường liên kết sẽ được gửi đến hộp thư của bạn. Vui lòng kiểm tra email.</summary>
    public static readonly SystemMessage MSG56 = new("MSG56", "Toast (Info)", "Recovery or activation link requested", "Nếu địa chỉ email đã được đăng ký, một đường liên kết sẽ được gửi đến hộp thư của bạn. Vui lòng kiểm tra email.");
    public static readonly SystemMessage MSG56_LinkRequested = MSG56;

    /// <summary>[Inline / Toast (Error)] Link invalid, expired, or already used: Liên kết không hợp lệ hoặc đã hết hạn. Vui lòng yêu cầu liên kết mới.</summary>
    public static readonly SystemMessage MSG57 = new("MSG57", "Inline / Toast (Error)", "Link invalid, expired, or already used", "Liên kết không hợp lệ hoặc đã hết hạn. Vui lòng yêu cầu liên kết mới.");
    public static readonly SystemMessage MSG57_LinkInvalid = MSG57;

    /// <summary>[Toast (Error)] Too many link requests: Bạn đã yêu cầu quá nhiều lần. Vui lòng thử lại sau ít phút.</summary>
    public static readonly SystemMessage MSG58 = new("MSG58", "Toast (Error)", "Too many link requests", "Bạn đã yêu cầu quá nhiều lần. Vui lòng thử lại sau ít phút.");
    public static readonly SystemMessage MSG58_TooManyLinkRequests = MSG58;

    /// <summary>[Toast (Error)] Activation or recovery email could not be sent: Không thể gửi email lúc này. Vui lòng thử lại sau.</summary>
    public static readonly SystemMessage MSG59 = new("MSG59", "Toast (Error)", "Activation or recovery email could not be sent", "Không thể gửi email lúc này. Vui lòng thử lại sau.");
    public static readonly SystemMessage MSG59_EmailUnavailable = MSG59;

    /// <summary>[Toast (Success)] Account request submitted: Đã gửi yêu cầu tạo tài khoản đến quản trị viên.</summary>
    public static readonly SystemMessage MSG60 = new("MSG60", "Toast (Success)", "Account request submitted", "Đã gửi yêu cầu tạo tài khoản đến quản trị viên.");
    public static readonly SystemMessage MSG60_AccountRequestSubmitted = MSG60;

    /// <summary>[Toast (Success)] Account request reviewed: Đã xử lý yêu cầu tạo tài khoản.</summary>
    public static readonly SystemMessage MSG61 = new("MSG61", "Toast (Success)", "Account request reviewed", "Đã xử lý yêu cầu tạo tài khoản.");
    public static readonly SystemMessage MSG61_AccountRequestReviewed = MSG61;

    /// <summary>[Toast / Summary (Error)] School year not in Draft: Chỉ có thể nhập dữ liệu xếp lớp cho năm học ở trạng thái Bản nháp.</summary>
    public static readonly SystemMessage MSG62 = new("MSG62", "Toast / Summary (Error)", "School year not in Draft", "Chỉ có thể nhập dữ liệu xếp lớp cho năm học ở trạng thái Bản nháp.");
    public static readonly SystemMessage MSG62_SchoolYearNotDraft = MSG62;

    /// <summary>[Toast (Error)] School year cannot be activated: Không thể kích hoạt năm học: năm học phải ở trạng thái Bản nháp và tất cả các lớp phải được đánh dấu Đã rà soát.</summary>
    public static readonly SystemMessage MSG63 = new("MSG63", "Toast (Error)", "School year cannot be activated", "Không thể kích hoạt năm học: năm học phải ở trạng thái Bản nháp và tất cả các lớp phải được đánh dấu Đã rà soát.");
    public static readonly SystemMessage MSG63_SchoolYearCannotBeActivated = MSG63;

    /// <summary>[Inline / Toast (Error)] Role locked (account has data): Không thể thay đổi vai trò của tài khoản đã có dữ liệu liên kết.</summary>
    public static readonly SystemMessage MSG64 = new("MSG64", "Inline / Toast (Error)", "Role locked (account has data)", "Không thể thay đổi vai trò của tài khoản đã có dữ liệu liên kết.");

    /// <summary>[Confirm Dialog] Confirm account enabling or role change: Bạn sắp kích hoạt tài khoản với vai trò "{vai trò}". Vui lòng xác nhận.</summary>
    public static readonly SystemMessage MSG65 = new("MSG65", "Confirm Dialog", "Confirm account enabling or role change", "Bạn sắp kích hoạt tài khoản với vai trò \"{vai trò}\". Vui lòng xác nhận.");

    /// <summary>[Inline / Toast (Error)] Email already used by an enabled account: Không thể kích hoạt tài khoản: địa chỉ email đã được một tài khoản đang hoạt động khác sử dụng.</summary>
    public static readonly SystemMessage MSG66 = new("MSG66", "Inline / Toast (Error)", "Email already used by an enabled account", "Không thể kích hoạt tài khoản: địa chỉ email đã được một tài khoản đang hoạt động khác sử dụng.");

    /// <summary>[Toast (Success)] Announcement published: Đã đăng thông báo của nhà trường.</summary>
    public static readonly SystemMessage MSG67 = new("MSG67", "Toast (Success)", "Announcement published", "Đã đăng thông báo của nhà trường.");

    /// <summary>[Inline / Toast (Error)] Attendance for a future date: Không thể điểm danh cho ngày tương lai.</summary>
    public static readonly SystemMessage MSG68 = new("MSG68", "Inline / Toast (Error)", "Attendance for a future date", "Không thể điểm danh cho ngày tương lai.");

    /// <summary>[Inline / Toast (Error)] Selected date is not a school day: Ngày đã chọn không phải ngày học.</summary>
    public static readonly SystemMessage MSG69 = new("MSG69", "Inline / Toast (Error)", "Selected date is not a school day", "Ngày đã chọn không phải ngày học.");

    /// <summary>[Inline / Toast (Error)] Students not yet marked: Còn {số lượng} học sinh chưa được điểm danh.</summary>
    public static readonly SystemMessage MSG70 = new("MSG70", "Inline / Toast (Error)", "Students not yet marked", "Còn {số lượng} học sinh chưa được điểm danh.");

    /// <summary>[Inline / Toast (Error)] Student not marked on the attendance sheet: Học sinh {họ tên} chưa được điểm danh.</summary>
    public static readonly SystemMessage MSG71 = new("MSG71", "Inline / Toast (Error)", "Student not marked on the attendance sheet", "Học sinh {họ tên} chưa được điểm danh.");

    /// <summary>[Toast (Error)] Today sheet already submitted: Bảng điểm danh hôm nay đã được giáo viên nộp. Vui lòng dùng chức năng điều chỉnh từng bản ghi kèm lý do.</summary>
    public static readonly SystemMessage MSG72 = new("MSG72", "Toast (Error)", "Today sheet already submitted", "Bảng điểm danh hôm nay đã được giáo viên nộp. Vui lòng dùng chức năng điều chỉnh từng bản ghi kèm lý do.");

    /// <summary>[Inline / Toast (Error)] Excused absence without approved request needs a note: Học sinh {mã học sinh}: vắng có phép chưa có đơn duyệt trên hệ thống cần nhập ghi chú tối thiểu 5 ký tự (ví dụ: Xin phép ngoài hệ thống).</summary>
    public static readonly SystemMessage MSG73 = new("MSG73", "Inline / Toast (Error)", "Excused absence without approved request needs a note", "Học sinh {mã học sinh}: vắng có phép chưa có đơn duyệt trên hệ thống cần nhập ghi chú tối thiểu 5 ký tự (ví dụ: Xin phép ngoài hệ thống).");

    /// <summary>[Toast (Error)] Unexcused absence conflicts with approved leave: Học sinh {mã học sinh}: đã có đơn xin nghỉ được duyệt, không thể đánh dấu Vắng không phép.</summary>
    public static readonly SystemMessage MSG74 = new("MSG74", "Toast (Error)", "Unexcused absence conflicts with approved leave", "Học sinh {mã học sinh}: đã có đơn xin nghỉ được duyệt, không thể đánh dấu Vắng không phép.");

    /// <summary>[Inline (Warning)] Warning: student attended but meal was cancelled by leave: {họ tên}: có đơn nghỉ đã hủy suất ăn nhưng thực tế đi học.</summary>
    public static readonly SystemMessage MSG75 = new("MSG75", "Inline (Warning)", "Warning: student attended but meal was cancelled by leave", "{họ tên}: có đơn nghỉ đã hủy suất ăn nhưng thực tế đi học.");

    /// <summary>[Inline (Warning)] Warning: kitchen cutoff passed, meal cannot be restored: {họ tên}: Bếp đã chốt suất lúc {giờ chốt}, không khôi phục được suất ăn.</summary>
    public static readonly SystemMessage MSG76 = new("MSG76", "Inline (Warning)", "Warning: kitchen cutoff passed, meal cannot be restored", "{họ tên}: Bếp đã chốt suất lúc {giờ chốt}, không khôi phục được suất ăn.");

    /// <summary>[Inline / Toast (Error)] Past-day attendance requires a reason: Bổ sung điểm danh ngày quá khứ bắt buộc nhập lý do từ 5 đến 500 ký tự.</summary>
    public static readonly SystemMessage MSG77 = new("MSG77", "Inline / Toast (Error)", "Past-day attendance requires a reason", "Bổ sung điểm danh ngày quá khứ bắt buộc nhập lý do từ 5 đến 500 ký tự.");

    /// <summary>[Inline / Toast (Error)] Correction reason required: Lý do điều chỉnh bắt buộc từ 5 đến 500 ký tự.</summary>
    public static readonly SystemMessage MSG78 = new("MSG78", "Inline / Toast (Error)", "Correction reason required", "Lý do điều chỉnh bắt buộc từ 5 đến 500 ký tự.");

    /// <summary>[Toast (Error)] Past-day correction to excused absence not allowed: Điều chỉnh điểm danh ngày quá khứ chỉ dùng để sửa lỗi nhập liệu, không cho phép chuyển sang Vắng có phép.</summary>
    public static readonly SystemMessage MSG79 = new("MSG79", "Toast (Error)", "Past-day correction to excused absence not allowed", "Điều chỉnh điểm danh ngày quá khứ chỉ dùng để sửa lỗi nhập liệu, không cho phép chuyển sang Vắng có phép.");

    /// <summary>[Toast (Error)] Correction: unexcused absence conflicts with approved leave: Học sinh đã có đơn xin nghỉ được duyệt, không thể đánh dấu Vắng không phép.</summary>
    public static readonly SystemMessage MSG80 = new("MSG80", "Toast (Error)", "Correction: unexcused absence conflicts with approved leave", "Học sinh đã có đơn xin nghỉ được duyệt, không thể đánh dấu Vắng không phép.");

    /// <summary>[Toast (Info)] Correction contains no change: Không có thay đổi nào để cập nhật.</summary>
    public static readonly SystemMessage MSG81 = new("MSG81", "Toast (Info)", "Correction contains no change", "Không có thay đổi nào để cập nhật.");

    /// <summary>[Toast (Success)] Attendance record corrected: Đã điều chỉnh bản ghi điểm danh thành công.</summary>
    public static readonly SystemMessage MSG82 = new("MSG82", "Toast (Success)", "Attendance record corrected", "Đã điều chỉnh bản ghi điểm danh thành công.");

    /// <summary>[Inline / Toast (Error)] Re-attendance request reason required: Vui lòng nhập lý do yêu cầu điểm danh lại (tối thiểu 5 ký tự).</summary>
    public static readonly SystemMessage MSG83 = new("MSG83", "Inline / Toast (Error)", "Re-attendance request reason required", "Vui lòng nhập lý do yêu cầu điểm danh lại (tối thiểu 5 ký tự).");

    /// <summary>[Toast (Success)] Re-attendance request sent: Đã gửi yêu cầu điểm danh lại tới Phòng Giáo vụ.</summary>
    public static readonly SystemMessage MSG84 = new("MSG84", "Toast (Success)", "Re-attendance request sent", "Đã gửi yêu cầu điểm danh lại tới Phòng Giáo vụ.");

    /// <summary>[Notification (In-app)] Office staff notified of a re-attendance request: Tiêu đề: GV yêu cầu điểm danh lại lớp {tên lớp} / Nội dung: {họ tên giáo viên} yêu cầu Giáo vụ hỗ trợ điểm danh lại {buổi} ngày {dd/MM/yyyy} cho lớp {tên lớp}. Lý do: {lý do}</summary>
    public static readonly SystemMessage MSG85 = new("MSG85", "Notification (In-app)", "Office staff notified of a re-attendance request", "Tiêu đề: GV yêu cầu điểm danh lại lớp {tên lớp} / Nội dung: {họ tên giáo viên} yêu cầu Giáo vụ hỗ trợ điểm danh lại {buổi} ngày {dd/MM/yyyy} cho lớp {tên lớp}. Lý do: {lý do}");

    /// <summary>[Empty-state message] No class student on the selected day: Lớp hiện chưa có học sinh nào được xếp vào ngày đã chọn.</summary>
    public static readonly SystemMessage MSG86 = new("MSG86", "Empty-state message", "No class student on the selected day", "Lớp hiện chưa có học sinh nào được xếp vào ngày đã chọn.");

    /// <summary>[Toast (Error)] Temporary password must be changed before continuing: Vui lòng đổi mật khẩu tạm thời trước khi tiếp tục.</summary>
    public static readonly SystemMessage MSG87 = new("MSG87", "Toast (Error)", "Temporary password must be changed before continuing", "Vui lòng đổi mật khẩu tạm thời trước khi tiếp tục.");

    /// <summary>[Inline / Toast (Error)] Email already used by another account (profile update): Email đã được sử dụng bởi một tài khoản khác.</summary>
    public static readonly SystemMessage MSG88 = new("MSG88", "Inline / Toast (Error)", "Email already used by another account (profile update)", "Email đã được sử dụng bởi một tài khoản khác.");

    /// <summary>[Inline / Toast (Error)] New password equals current password: Mật khẩu mới phải khác mật khẩu hiện tại.</summary>
    public static readonly SystemMessage MSG89 = new("MSG89", "Inline / Toast (Error)", "New password equals current password", "Mật khẩu mới phải khác mật khẩu hiện tại.");

    /// <summary>[Inline / Toast (Error)] New password shorter than 8 characters: Mật khẩu mới phải có tối thiểu 8 ký tự.</summary>
    public static readonly SystemMessage MSG90 = new("MSG90", "Inline / Toast (Error)", "New password shorter than 8 characters", "Mật khẩu mới phải có tối thiểu 8 ký tự.");

    /// <summary>[Email] Password recovery email: Tiêu đề: SAWP - Đặt lại mật khẩu / Nội dung: Xin chào {họ tên}, chúng tôi nhận được yêu cầu đặt lại mật khẩu cho tài khoản của bạn. Vui lòng mở đường liên kết để đặt mật khẩu mới. Đường liên kết chỉ dùng được một lần và hết hạn sau {số phút} phút. Nếu bạn không yêu cầu, hãy bỏ qua email này; mật khẩu của bạn không thay đổi.</summary>
    public static readonly SystemMessage MSG91 = new("MSG91", "Email", "Password recovery email", "Tiêu đề: SAWP - Đặt lại mật khẩu / Nội dung: Xin chào {họ tên}, chúng tôi nhận được yêu cầu đặt lại mật khẩu cho tài khoản của bạn. Vui lòng mở đường liên kết để đặt mật khẩu mới. Đường liên kết chỉ dùng được một lần và hết hạn sau {số phút} phút. Nếu bạn không yêu cầu, hãy bỏ qua email này; mật khẩu của bạn không thay đổi.");

    /// <summary>[Email] Account activation email: Tiêu đề: SAWP - Kích hoạt tài khoản / Nội dung: Xin chào {họ tên}, tài khoản {tên đăng nhập} đã được tạo cho bạn trên hệ thống SAWP. Vui lòng mở đường liên kết để đặt mật khẩu và kích hoạt tài khoản. Đường liên kết chỉ dùng được một lần và hết hạn sau {số giờ} giờ. Nếu đã hết hạn, hãy chọn "Quên mật khẩu" trên trang đăng nhập để nhận liên kết mới.</summary>
    public static readonly SystemMessage MSG92 = new("MSG92", "Email", "Account activation email", "Tiêu đề: SAWP - Kích hoạt tài khoản / Nội dung: Xin chào {họ tên}, tài khoản {tên đăng nhập} đã được tạo cho bạn trên hệ thống SAWP. Vui lòng mở đường liên kết để đặt mật khẩu và kích hoạt tài khoản. Đường liên kết chỉ dùng được một lần và hết hạn sau {số giờ} giờ. Nếu đã hết hạn, hãy chọn \"Quên mật khẩu\" trên trang đăng nhập để nhận liên kết mới.");

    /// <summary>[Toast (Success)] Student record created: Tạo hồ sơ học sinh thành công!</summary>
    public static readonly SystemMessage MSG93 = new("MSG93", "Toast (Success)", "Student record created", "Tạo hồ sơ học sinh thành công!");

    /// <summary>[Toast (Success)] Student record activated: Đã kích hoạt hồ sơ học sinh {họ tên}!</summary>
    public static readonly SystemMessage MSG94 = new("MSG94", "Toast (Success)", "Student record activated", "Đã kích hoạt hồ sơ học sinh {họ tên}!");

    /// <summary>[Toast (Info)] Duplicate student code detected during creation: Mã học sinh đã tồn tại trong hệ thống. Đang chuyển sang màn hình giải quyết trùng lặp.</summary>
    public static readonly SystemMessage MSG95 = new("MSG95", "Toast (Info)", "Duplicate student code detected during creation", "Mã học sinh đã tồn tại trong hệ thống. Đang chuyển sang màn hình giải quyết trùng lặp.");

    /// <summary>[Toast (Success)] Duplicate student record resolved: Xử lý trùng lặp hồ sơ thành công!</summary>
    public static readonly SystemMessage MSG96 = new("MSG96", "Toast (Success)", "Duplicate student record resolved", "Xử lý trùng lặp hồ sơ thành công!");

    /// <summary>[Inline / Toast (Error)] Student code format invalid: Mã học sinh chỉ được chứa chữ cái, chữ số, dấu gạch ngang (-) và gạch dưới (_) từ 3 đến 30 ký tự.</summary>
    public static readonly SystemMessage MSG97 = new("MSG97", "Inline / Toast (Error)", "Student code format invalid", "Mã học sinh chỉ được chứa chữ cái, chữ số, dấu gạch ngang (-) và gạch dưới (_) từ 3 đến 30 ký tự.");

    /// <summary>[Inline / Toast (Error)] Student age outside primary-school range: Độ tuổi học sinh tiểu học không hợp lệ ({tuổi} tuổi). Tuổi hợp lệ phải từ 5 đến 15 tuổi.</summary>
    public static readonly SystemMessage MSG98 = new("MSG98", "Inline / Toast (Error)", "Student age outside primary-school range", "Độ tuổi học sinh tiểu học không hợp lệ ({tuổi} tuổi). Tuổi hợp lệ phải từ 5 đến 15 tuổi.");

    /// <summary>[Inline / Toast (Error)] Student age does not match the grade: Độ tuổi của học sinh ({tuổi} tuổi) không phù hợp với Khối {khối} (yêu cầu từ {tuổi tối thiểu} đến {tuổi tối đa} tuổi).</summary>
    public static readonly SystemMessage MSG99 = new("MSG99", "Inline / Toast (Error)", "Student age does not match the grade", "Độ tuổi của học sinh ({tuổi} tuổi) không phù hợp với Khối {khối} (yêu cầu từ {tuổi tối thiểu} đến {tuổi tối đa} tuổi).");

    /// <summary>[Toast (Error)] Selected class is full (student create/update): Lớp {tên lớp} đã đủ sĩ số ({số hiện có}/{sĩ số tối đa}). Vui lòng chọn lớp khác hoặc chọn 'Chưa xếp lớp'.</summary>
    public static readonly SystemMessage MSG100 = new("MSG100", "Toast (Error)", "Selected class is full (student create/update)", "Lớp {tên lớp} đã đủ sĩ số ({số hiện có}/{sĩ số tối đa}). Vui lòng chọn lớp khác hoặc chọn 'Chưa xếp lớp'.");

    /// <summary>[Inline / Toast (Error)] Student code already exists (shows owner): Mã học sinh '{mã học sinh}' đã tồn tại trong hệ thống (Học sinh: {họ tên}).</summary>
    public static readonly SystemMessage MSG101 = new("MSG101", "Inline / Toast (Error)", "Student code already exists (shows owner)", "Mã học sinh '{mã học sinh}' đã tồn tại trong hệ thống (Học sinh: {họ tên}).");

    /// <summary>[Toast (Success)] Student record updated: Đã cập nhật hồ sơ học sinh thành công!</summary>
    public static readonly SystemMessage MSG102 = new("MSG102", "Toast (Success)", "Student record updated", "Đã cập nhật hồ sơ học sinh thành công!");

    /// <summary>[Inline / Toast (Error)] Student full name length invalid (2-100 chars): Họ và tên học sinh phải có độ dài từ 2 đến 100 ký tự.</summary>
    public static readonly SystemMessage MSG103 = new("MSG103", "Inline / Toast (Error)", "Student full name length invalid (2-100 chars)", "Họ và tên học sinh phải có độ dài từ 2 đến 100 ký tự.");

    /// <summary>[Inline / Toast (Error)] Date of birth is in the future: Ngày sinh không hợp lệ (không thể là ngày trong tương lai).</summary>
    public static readonly SystemMessage MSG104 = new("MSG104", "Inline / Toast (Error)", "Date of birth is in the future", "Ngày sinh không hợp lệ (không thể là ngày trong tương lai).");

    /// <summary>[Toast (Success)] Excel template downloaded: Đã tải tệp mẫu Excel thành công!</summary>
    public static readonly SystemMessage MSG105 = new("MSG105", "Toast (Success)", "Excel template downloaded", "Đã tải tệp mẫu Excel thành công!");

    /// <summary>[Toast (Success)] Excel file parsed: Đã phân tích {số lượng} dòng dữ liệu từ Excel!</summary>
    public static readonly SystemMessage MSG106 = new("MSG106", "Toast (Success)", "Excel file parsed", "Đã phân tích {số lượng} dòng dữ liệu từ Excel!");

    /// <summary>[Confirmation Dialog] Confirm student import: Xác nhận nhập {số lượng} học sinh hợp lệ</summary>
    public static readonly SystemMessage MSG107 = new("MSG107", "Confirmation Dialog", "Confirm student import", "Xác nhận nhập {số lượng} học sinh hợp lệ");

    /// <summary>[Toast (Success)] Students imported successfully: Nhập thành công {số lượng} học sinh mới vào hệ thống!</summary>
    public static readonly SystemMessage MSG108 = new("MSG108", "Toast (Success)", "Students imported successfully", "Nhập thành công {số lượng} học sinh mới vào hệ thống!");

    /// <summary>[Inline (Warning)] Guardian match: same ID card, different email: Hồ sơ người giám hộ có cùng CCCD nhưng email khác.</summary>
    public static readonly SystemMessage MSG109 = new("MSG109", "Inline (Warning)", "Guardian match: same ID card, different email", "Hồ sơ người giám hộ có cùng CCCD nhưng email khác.");

    /// <summary>[Inline (Warning)] Guardian match: same email, different ID card: Hồ sơ người giám hộ có cùng email nhưng CCCD khác.</summary>
    public static readonly SystemMessage MSG110 = new("MSG110", "Inline (Warning)", "Guardian match: same email, different ID card", "Hồ sơ người giám hộ có cùng email nhưng CCCD khác.");

    /// <summary>[Inline (Warning)] Guardian match: existing profile reused despite different name: Dùng hồ sơ có sẵn "{họ tên}" (họ tên trong dữ liệu khác).</summary>
    public static readonly SystemMessage MSG111 = new("MSG111", "Inline (Warning)", "Guardian match: existing profile reused despite different name", "Dùng hồ sơ có sẵn \"{họ tên}\" (họ tên trong dữ liệu khác).");

    /// <summary>[Toast (Error)] Guardian match: several profiles share ID card or email: Có nhiều hồ sơ người giám hộ trùng CCCD hoặc email; cần kiểm tra lại dữ liệu.</summary>
    public static readonly SystemMessage MSG112 = new("MSG112", "Toast (Error)", "Guardian match: several profiles share ID card or email", "Có nhiều hồ sơ người giám hộ trùng CCCD hoặc email; cần kiểm tra lại dữ liệu.");

    /// <summary>[Toast (Error)] Guardian match: ID card and email belong to different profiles: CCCD và email thuộc hai hồ sơ người giám hộ khác nhau.</summary>
    public static readonly SystemMessage MSG113 = new("MSG113", "Toast (Error)", "Guardian match: ID card and email belong to different profiles", "CCCD và email thuộc hai hồ sơ người giám hộ khác nhau.");

    /// <summary>[Inline / Toast (Error)] Excel file contains no worksheet: Tệp Excel không chứa trang tính (sheet) nào.</summary>
    public static readonly SystemMessage MSG114 = new("MSG114", "Inline / Toast (Error)", "Excel file contains no worksheet", "Tệp Excel không chứa trang tính (sheet) nào.");

    /// <summary>[Inline / Toast (Error)] Excel file contains no valid data row: Tệp Excel không có dòng dữ liệu hợp lệ nào.</summary>
    public static readonly SystemMessage MSG115 = new("MSG115", "Inline / Toast (Error)", "Excel file contains no valid data row", "Tệp Excel không có dòng dữ liệu hợp lệ nào.");

    /// <summary>[Inline / Toast (Error)] Only .xlsx files are supported: Vui lòng chọn tệp Excel định dạng .xlsx!</summary>
    public static readonly SystemMessage MSG116 = new("MSG116", "Inline / Toast (Error)", "Only .xlsx files are supported", "Vui lòng chọn tệp Excel định dạng .xlsx!");

    /// <summary>[Inline / Toast (Error)] No valid record to import: Không có bản ghi hợp lệ nào để nhập.</summary>
    public static readonly SystemMessage MSG117 = new("MSG117", "Inline / Toast (Error)", "No valid record to import", "Không có bản ghi hợp lệ nào để nhập.");

    /// <summary>[Confirmation Dialog] Confirm unlinking a guardian: {nội dung MSG45} / (Gỡ liên kết {họ tên người giám hộ} khỏi học sinh {họ tên học sinh})</summary>
    public static readonly SystemMessage MSG118 = new("MSG118", "Confirmation Dialog", "Confirm unlinking a guardian", "{nội dung MSG45} / (Gỡ liên kết {họ tên người giám hộ} khỏi học sinh {họ tên học sinh})");

    /// <summary>[Toast (Error)] Guardian already linked to the student: Người giám hộ này đã được liên kết với học sinh.</summary>
    public static readonly SystemMessage MSG119 = new("MSG119", "Toast (Error)", "Guardian already linked to the student", "Người giám hộ này đã được liên kết với học sinh.");

    /// <summary>[Toast (Error)] Active student needs a primary guardian (update): Học sinh đang học cần có một người giám hộ chính. Hãy chọn người giám hộ chính khác trước.</summary>
    public static readonly SystemMessage MSG120 = new("MSG120", "Toast (Error)", "Active student needs a primary guardian (update)", "Học sinh đang học cần có một người giám hộ chính. Hãy chọn người giám hộ chính khác trước.");

    /// <summary>[Toast (Error)] Primary guardian of an active student cannot be unlinked: Không thể gỡ người giám hộ chính của học sinh đang học. Hãy chọn người giám hộ chính khác trước.</summary>
    public static readonly SystemMessage MSG121 = new("MSG121", "Toast (Error)", "Primary guardian of an active student cannot be unlinked", "Không thể gỡ người giám hộ chính của học sinh đang học. Hãy chọn người giám hộ chính khác trước.");

    /// <summary>[Inline / Toast (Error)] Guardian phone number invalid: Số điện thoại phải gồm đúng 10 chữ số (bắt đầu bằng số 0, ví dụ: 0912345678).</summary>
    public static readonly SystemMessage MSG122 = new("MSG122", "Inline / Toast (Error)", "Guardian phone number invalid", "Số điện thoại phải gồm đúng 10 chữ số (bắt đầu bằng số 0, ví dụ: 0912345678).");

    /// <summary>[Inline / Toast (Error)] Guardian email invalid: Email người giám hộ không hợp lệ.</summary>
    public static readonly SystemMessage MSG123 = new("MSG123", "Inline / Toast (Error)", "Guardian email invalid", "Email người giám hộ không hợp lệ.");

    /// <summary>[Inline / Toast (Error)] Guardian ID card number invalid: Số CCCD/CMND phải gồm 9 hoặc 12 chữ số.</summary>
    public static readonly SystemMessage MSG124 = new("MSG124", "Inline / Toast (Error)", "Guardian ID card number invalid", "Số CCCD/CMND phải gồm 9 hoặc 12 chữ số.");

    /// <summary>[Notification (In-app)] New account request sent to the reviewer: Tiêu đề: Yêu cầu cấp tài khoản phụ huynh mới / Nội dung: {người gửi} đã gửi yêu cầu "{tiêu đề}" ({số lượng} người giám hộ).</summary>
    public static readonly SystemMessage MSG125 = new("MSG125", "Notification (In-app)", "New account request sent to the reviewer", "Tiêu đề: Yêu cầu cấp tài khoản phụ huynh mới / Nội dung: {người gửi} đã gửi yêu cầu \"{tiêu đề}\" ({số lượng} người giám hộ).");

    /// <summary>[Inline / Toast (Error)] Close rows: closing note missing: Vui lòng nhập ghi chú khi đóng dòng.</summary>
    public static readonly SystemMessage MSG126 = new("MSG126", "Inline / Toast (Error)", "Close rows: closing note missing", "Vui lòng nhập ghi chú khi đóng dòng.");

    /// <summary>[Toast (Error)] Resend activation: account is not pending activation: Chỉ gửi lại liên kết cho tài khoản đang chờ kích hoạt.</summary>
    public static readonly SystemMessage MSG127 = new("MSG127", "Toast (Error)", "Resend activation: account is not pending activation", "Chỉ gửi lại liên kết cho tài khoản đang chờ kích hoạt.");

    /// <summary>[Inline / Toast (Error)] Account request: only drafts can be edited: Chỉ sửa được yêu cầu đang ở trạng thái nháp.</summary>
    public static readonly SystemMessage MSG128 = new("MSG128", "Inline / Toast (Error)", "Account request: only drafts can be edited", "Chỉ sửa được yêu cầu đang ở trạng thái nháp.");

    /// <summary>[Inline / Toast (Error)] Account request: title missing or too long: Tiêu đề yêu cầu là bắt buộc (tối đa 200 ký tự).</summary>
    public static readonly SystemMessage MSG129 = new("MSG129", "Inline / Toast (Error)", "Account request: title missing or too long", "Tiêu đề yêu cầu là bắt buộc (tối đa 200 ký tự).");

    /// <summary>[Inline / Toast (Error)] Account request: reason not selected: Vui lòng chọn lý do của yêu cầu.</summary>
    public static readonly SystemMessage MSG130 = new("MSG130", "Inline / Toast (Error)", "Account request: reason not selected", "Vui lòng chọn lý do của yêu cầu.");

    /// <summary>[Inline / Toast (Error)] Account request has no guardian: Yêu cầu cần ít nhất một người giám hộ.</summary>
    public static readonly SystemMessage MSG131 = new("MSG131", "Inline / Toast (Error)", "Account request has no guardian", "Yêu cầu cần ít nhất một người giám hộ.");

    /// <summary>[Toast (Error)] Guardian already has an account: Người giám hộ đã có tài khoản.</summary>
    public static readonly SystemMessage MSG132 = new("MSG132", "Toast (Error)", "Guardian already has an account", "Người giám hộ đã có tài khoản.");

    /// <summary>[Toast (Error)] Guardian profile has no email (BR-64): Hồ sơ người giám hộ chưa có email (BR-64).</summary>
    public static readonly SystemMessage MSG133 = new("MSG133", "Toast (Error)", "Guardian profile has no email (BR-64)", "Hồ sơ người giám hộ chưa có email (BR-64).");

    /// <summary>[Toast (Error)] Guardian email is invalid: Email không hợp lệ.</summary>
    public static readonly SystemMessage MSG134 = new("MSG134", "Toast (Error)", "Guardian email is invalid", "Email không hợp lệ.");

    /// <summary>[Toast (Error)] Guardian email belongs to a staff account (BR-68): Email thuộc một tài khoản nhân sự; tài khoản phụ huynh cần một email khác. Hãy sửa email trong hồ sơ người giám hộ rồi gửi lại (BR-68).</summary>
    public static readonly SystemMessage MSG135 = new("MSG135", "Toast (Error)", "Guardian email belongs to a staff account (BR-68)", "Email thuộc một tài khoản nhân sự; tài khoản phụ huynh cần một email khác. Hãy sửa email trong hồ sơ người giám hộ rồi gửi lại (BR-68).");

    /// <summary>[Toast (Error)] Guardian email already used by another account: Email đã được dùng cho một tài khoản khác.</summary>
    public static readonly SystemMessage MSG136 = new("MSG136", "Toast (Error)", "Guardian email already used by another account", "Email đã được dùng cho một tài khoản khác.");

    /// <summary>[Toast (Error)] Guardian is already in an unprocessed request: Người giám hộ đang nằm trong một yêu cầu chưa xử lý xong.</summary>
    public static readonly SystemMessage MSG137 = new("MSG137", "Toast (Error)", "Guardian is already in an unprocessed request", "Người giám hộ đang nằm trong một yêu cầu chưa xử lý xong.");

    /// <summary>[Toast (Error)] Same email appears twice in one request: Email này xuất hiện hai lần trong yêu cầu.</summary>
    public static readonly SystemMessage MSG138 = new("MSG138", "Toast (Error)", "Same email appears twice in one request", "Email này xuất hiện hai lần trong yêu cầu.");

    /// <summary>[Toast (Error)] Row excluded from request because of a validation error: {họ tên}: {lý do} – không đưa vào yêu cầu.</summary>
    public static readonly SystemMessage MSG139 = new("MSG139", "Toast (Error)", "Row excluded from request because of a validation error", "{họ tên}: {lý do} – không đưa vào yêu cầu.");

    /// <summary>[Inline / Toast (Error)] Calendar day invalid: Ngày cấu hình lịch học không hợp lệ.</summary>
    public static readonly SystemMessage MSG140 = new("MSG140", "Inline / Toast (Error)", "Calendar day invalid", "Ngày cấu hình lịch học không hợp lệ.");

    /// <summary>[Inline / Toast (Error)] Calendar note too long: Ghi chú không được vượt quá 250 ký tự.</summary>
    public static readonly SystemMessage MSG141 = new("MSG141", "Inline / Toast (Error)", "Calendar note too long", "Ghi chú không được vượt quá 250 ký tự.");

    /// <summary>[Toast (Error)] Selected school year not found: Không tìm thấy năm học được chọn.</summary>
    public static readonly SystemMessage MSG142 = new("MSG142", "Toast (Error)", "Selected school year not found", "Không tìm thấy năm học được chọn.");

    /// <summary>[Toast (Error)] Calendar date outside the school year: Ngày {ngày} nằm ngoài phạm vi năm học {năm học} ({ngày bắt đầu} – {ngày kết thúc}).</summary>
    public static readonly SystemMessage MSG143 = new("MSG143", "Toast (Error)", "Calendar date outside the school year", "Ngày {ngày} nằm ngoài phạm vi năm học {năm học} ({ngày bắt đầu} – {ngày kết thúc}).");

    /// <summary>[Inline / Toast (Error)] Lunch configuration date required: Ngày cấu hình bán trú là bắt buộc.</summary>
    public static readonly SystemMessage MSG144 = new("MSG144", "Inline / Toast (Error)", "Lunch configuration date required", "Ngày cấu hình bán trú là bắt buộc.");

    /// <summary>[Inline / Toast (Error)] Calendar range not fully selected: Vui lòng chọn đầy đủ Từ ngày và Đến ngày.</summary>
    public static readonly SystemMessage MSG145 = new("MSG145", "Inline / Toast (Error)", "Calendar range not fully selected", "Vui lòng chọn đầy đủ Từ ngày và Đến ngày.");

    /// <summary>[Inline / Toast (Error)] Calendar end date before start date: Đến ngày phải lớn hơn hoặc bằng Từ ngày.</summary>
    public static readonly SystemMessage MSG146 = new("MSG146", "Inline / Toast (Error)", "Calendar end date before start date", "Đến ngày phải lớn hơn hoặc bằng Từ ngày.");

    /// <summary>[Inline / Toast (Error)] Calendar bulk update limited to 90 days: Chỉ hỗ trợ cập nhật tối đa 90 ngày mỗi lần.</summary>
    public static readonly SystemMessage MSG147 = new("MSG147", "Inline / Toast (Error)", "Calendar bulk update limited to 90 days", "Chỉ hỗ trợ cập nhật tối đa 90 ngày mỗi lần.");

    /// <summary>[Notification (In-app)] Homeroom teacher notified of a new leave request: Tiêu đề: Đơn xin nghỉ học mới / Nội dung: Phụ huynh học sinh đã gửi đơn xin nghỉ từ {từ ngày} đến {đến ngày} ({số lượng} ngày học). Mã đơn: {mã đơn}</summary>
    public static readonly SystemMessage MSG148 = new("MSG148", "Notification (In-app)", "Homeroom teacher notified of a new leave request", "Tiêu đề: Đơn xin nghỉ học mới / Nội dung: Phụ huynh học sinh đã gửi đơn xin nghỉ từ {từ ngày} đến {đến ngày} ({số lượng} ngày học). Mã đơn: {mã đơn}");

    /// <summary>[Notification (In-app)] School Board notified of a leave request from a class without homeroom teacher: Tiêu đề: Đơn xin nghỉ học mới (lớp chưa có GVCN) / Nội dung: Lớp {tên lớp} hiện chưa phân công GVCN. {nội dung đơn}</summary>
    public static readonly SystemMessage MSG149 = new("MSG149", "Notification (In-app)", "School Board notified of a leave request from a class without homeroom teacher", "Tiêu đề: Đơn xin nghỉ học mới (lớp chưa có GVCN) / Nội dung: Lớp {tên lớp} hiện chưa phân công GVCN. {nội dung đơn}");

    /// <summary>[Toast (Success)] Leave request submitted (to homeroom teacher): Đã gửi đơn xin nghỉ thành công! GVCN sẽ sớm xem xét.</summary>
    public static readonly SystemMessage MSG150 = new("MSG150", "Toast (Success)", "Leave request submitted (to homeroom teacher)", "Đã gửi đơn xin nghỉ thành công! GVCN sẽ sớm xem xét.");

    /// <summary>[Toast (Success)] Leave request updated and resubmitted: Đã cập nhật và gửi lại đơn xin nghỉ học thành công.</summary>
    public static readonly SystemMessage MSG151 = new("MSG151", "Toast (Success)", "Leave request updated and resubmitted", "Đã cập nhật và gửi lại đơn xin nghỉ học thành công.");

    /// <summary>[Notification (In-app)] Teacher notified that the parent updated a leave request: Tiêu đề: Phụ huynh đã cập nhật đơn xin nghỉ học / Nội dung: Đơn xin nghỉ {mã đơn} của học sinh {họ tên} đã được phụ huynh bổ sung/chỉnh sửa ({từ ngày} - {đến ngày}, {số lượng} ngày học).</summary>
    public static readonly SystemMessage MSG152 = new("MSG152", "Notification (In-app)", "Teacher notified that the parent updated a leave request", "Tiêu đề: Phụ huynh đã cập nhật đơn xin nghỉ học / Nội dung: Đơn xin nghỉ {mã đơn} của học sinh {họ tên} đã được phụ huynh bổ sung/chỉnh sửa ({từ ngày} - {đến ngày}, {số lượng} ngày học).");

    /// <summary>[Toast (Success)] Leave request cancelled by the parent: Đã hủy đơn xin nghỉ học thành công.</summary>
    public static readonly SystemMessage MSG153 = new("MSG153", "Toast (Success)", "Leave request cancelled by the parent", "Đã hủy đơn xin nghỉ học thành công.");

    /// <summary>[Toast (Success)] Leave request partially withdrawn: Đã thu hồi các ngày chưa qua giờ chốt của đơn nghỉ và khôi phục suất ăn liên quan.</summary>
    public static readonly SystemMessage MSG154 = new("MSG154", "Toast (Success)", "Leave request partially withdrawn", "Đã thu hồi các ngày chưa qua giờ chốt của đơn nghỉ và khôi phục suất ăn liên quan.");

    /// <summary>[Toast (Success)] Approved leave request withdrawn: Đã thu hồi đơn xin nghỉ đã duyệt và khôi phục suất ăn (nếu có) thành công.</summary>
    public static readonly SystemMessage MSG155 = new("MSG155", "Toast (Success)", "Approved leave request withdrawn", "Đã thu hồi đơn xin nghỉ đã duyệt và khôi phục suất ăn (nếu có) thành công.");

    /// <summary>[Notification (In-app)] Teacher notified that the parent withdrew an approved leave request: Tiêu đề: Phụ huynh đã thu hồi đơn nghỉ đã duyệt / Nội dung: Phụ huynh học sinh {họ tên} đã thu hồi đơn nghỉ {mã đơn} cho ngày ({danh sách ngày}) trước giờ chốt. Lý do: {lý do}</summary>
    public static readonly SystemMessage MSG156 = new("MSG156", "Notification (In-app)", "Teacher notified that the parent withdrew an approved leave request", "Tiêu đề: Phụ huynh đã thu hồi đơn nghỉ đã duyệt / Nội dung: Phụ huynh học sinh {họ tên} đã thu hồi đơn nghỉ {mã đơn} cho ngày ({danh sách ngày}) trước giờ chốt. Lý do: {lý do}");

    /// <summary>[Inline / Toast (Error)] Leave date in the past: Không thể xin nghỉ học cho ngày trong quá khứ.</summary>
    public static readonly SystemMessage MSG157 = new("MSG157", "Inline / Toast (Error)", "Leave date in the past", "Không thể xin nghỉ học cho ngày trong quá khứ.");

    /// <summary>[Inline / Toast (Error)] Leave start date after end date: Ngày bắt đầu nghỉ không được lớn hơn ngày kết thúc.</summary>
    public static readonly SystemMessage MSG158 = new("MSG158", "Inline / Toast (Error)", "Leave start date after end date", "Ngày bắt đầu nghỉ không được lớn hơn ngày kết thúc.");

    /// <summary>[Inline / Toast (Error)] Leave dates not selected: Vui lòng chọn ngày bắt đầu và ngày kết thúc nghỉ.</summary>
    public static readonly SystemMessage MSG159 = new("MSG159", "Inline / Toast (Error)", "Leave dates not selected", "Vui lòng chọn ngày bắt đầu và ngày kết thúc nghỉ.");

    /// <summary>[Inline / Toast (Error)] Leave exceeds the end of the school year: Thời gian xin nghỉ không được vượt quá ngày kết thúc năm học hiện tại ({ngày kết thúc năm học}).</summary>
    public static readonly SystemMessage MSG160 = new("MSG160", "Inline / Toast (Error)", "Leave exceeds the end of the school year", "Thời gian xin nghỉ không được vượt quá ngày kết thúc năm học hiện tại ({ngày kết thúc năm học}).");

    /// <summary>[Inline / Toast (Error)] Leave start too far in advance: Không thể đặt ngày bắt đầu xin nghỉ quá {số ngày} ngày kể từ hôm nay.</summary>
    public static readonly SystemMessage MSG161 = new("MSG161", "Inline / Toast (Error)", "Leave start too far in advance", "Không thể đặt ngày bắt đầu xin nghỉ quá {số ngày} ngày kể từ hôm nay.");

    /// <summary>[Inline / Toast (Error)] Leave calendar span too long: Khoảng thời gian xin nghỉ không được vượt quá {số ngày} ngày lịch.</summary>
    public static readonly SystemMessage MSG162 = new("MSG162", "Inline / Toast (Error)", "Leave calendar span too long", "Khoảng thời gian xin nghỉ không được vượt quá {số ngày} ngày lịch.");

    /// <summary>[Inline / Toast (Error)] Leave range contains no school day: Khoảng thời gian xin nghỉ chỉ gồm ngày cuối tuần hoặc ngày nghỉ lễ. Vui lòng chọn ngày học.</summary>
    public static readonly SystemMessage MSG163 = new("MSG163", "Inline / Toast (Error)", "Leave range contains no school day", "Khoảng thời gian xin nghỉ chỉ gồm ngày cuối tuần hoặc ngày nghỉ lễ. Vui lòng chọn ngày học.");

    /// <summary>[Inline / Toast (Error)] Leave school days exceed limit: Tổng số ngày học xin nghỉ không được vượt quá {số ngày} ngày học.</summary>
    public static readonly SystemMessage MSG164 = new("MSG164", "Inline / Toast (Error)", "Leave school days exceed limit", "Tổng số ngày học xin nghỉ không được vượt quá {số ngày} ngày học.");

    /// <summary>[Inline / Toast (Error)] Keep-meal option only for morning leave: Tùy chọn giữ suất ăn bán trú chỉ áp dụng cho đơn nghỉ buổi sáng (trưa học sinh vào trường ăn để học buổi chiều).</summary>
    public static readonly SystemMessage MSG165 = new("MSG165", "Inline / Toast (Error)", "Keep-meal option only for morning leave", "Tùy chọn giữ suất ăn bán trú chỉ áp dụng cho đơn nghỉ buổi sáng (trưa học sinh vào trường ăn để học buổi chiều).");

    /// <summary>[Inline / Toast (Error)] Keep-meal option without lunch registration: Học sinh chưa đăng ký suất ăn bán trú trong các ngày xin nghỉ nên không thể chọn giữ suất ăn.</summary>
    public static readonly SystemMessage MSG166 = new("MSG166", "Inline / Toast (Error)", "Keep-meal option without lunch registration", "Học sinh chưa đăng ký suất ăn bán trú trong các ngày xin nghỉ nên không thể chọn giữ suất ăn.");

    /// <summary>[Inline / Toast (Error)] Leave reason missing: Vui lòng nhập lý do xin nghỉ học.</summary>
    public static readonly SystemMessage MSG167 = new("MSG167", "Inline / Toast (Error)", "Leave reason missing", "Vui lòng nhập lý do xin nghỉ học.");

    /// <summary>[Inline / Toast (Error)] Leave reason length invalid: Lý do xin nghỉ học phải từ 5 đến 500 ký tự.</summary>
    public static readonly SystemMessage MSG168 = new("MSG168", "Inline / Toast (Error)", "Leave reason length invalid", "Lý do xin nghỉ học phải từ 5 đến 500 ký tự.");

    /// <summary>[Inline / Toast (Error)] Too many leave attachments: Chỉ được đính kèm tối đa 5 tệp minh chứng cho mỗi đơn xin nghỉ.</summary>
    public static readonly SystemMessage MSG169 = new("MSG169", "Inline / Toast (Error)", "Too many leave attachments", "Chỉ được đính kèm tối đa 5 tệp minh chứng cho mỗi đơn xin nghỉ.");

    /// <summary>[Inline / Toast (Error)] Attachment path invalid: Đường dẫn tệp đính kèm không hợp lệ. Vui lòng tải tệp lên qua hệ thống (chỉ hỗ trợ PDF, JPG, PNG).</summary>
    public static readonly SystemMessage MSG170 = new("MSG170", "Inline / Toast (Error)", "Attachment path invalid", "Đường dẫn tệp đính kèm không hợp lệ. Vui lòng tải tệp lên qua hệ thống (chỉ hỗ trợ PDF, JPG, PNG).");

    /// <summary>[Inline / Toast (Error)] Student not selected for the leave request: Vui lòng chọn học sinh.</summary>
    public static readonly SystemMessage MSG171 = new("MSG171", "Inline / Toast (Error)", "Student not selected for the leave request", "Vui lòng chọn học sinh.");

    /// <summary>[Toast (Error)] Overlapping leave request exists: Học sinh đã có đơn xin nghỉ trùng khoảng thời gian này đang chờ duyệt hoặc đã duyệt.</summary>
    public static readonly SystemMessage MSG172 = new("MSG172", "Toast (Error)", "Overlapping leave request exists", "Học sinh đã có đơn xin nghỉ trùng khoảng thời gian này đang chờ duyệt hoặc đã duyệt.");

    /// <summary>[Toast (Error)] Leave request cannot be cancelled in this status: Chỉ có thể hủy đơn khi đơn đang ở trạng thái Chờ duyệt hoặc Yêu cầu bổ sung thông tin.</summary>
    public static readonly SystemMessage MSG173 = new("MSG173", "Toast (Error)", "Leave request cannot be cancelled in this status", "Chỉ có thể hủy đơn khi đơn đang ở trạng thái Chờ duyệt hoặc Yêu cầu bổ sung thông tin.");

    /// <summary>[Toast (Error)] Withdrawal impossible: all days past cutoff or locked: Tất cả các ngày trong đơn đã qua giờ chốt hoặc đã bị khóa, không thể thu hồi đơn đã duyệt.</summary>
    public static readonly SystemMessage MSG174 = new("MSG174", "Toast (Error)", "Withdrawal impossible: all days past cutoff or locked", "Tất cả các ngày trong đơn đã qua giờ chốt hoặc đã bị khóa, không thể thu hồi đơn đã duyệt.");

    /// <summary>[Inline / Toast (Error)] Attendance report range too long: Khoảng thời gian không hợp lệ (tối đa 366 ngày và Từ ngày phải trước Đến ngày).</summary>
    public static readonly SystemMessage MSG175 = new("MSG175", "Inline / Toast (Error)", "Attendance report range too long", "Khoảng thời gian không hợp lệ (tối đa 366 ngày và Từ ngày phải trước Đến ngày).");

    /// <summary>[Inline / Toast (Error)] Matrix range too long: Khoảng thời gian xem ma trận tối đa 31 ngày và Từ ngày phải trước Đến ngày.</summary>
    public static readonly SystemMessage MSG176 = new("MSG176", "Inline / Toast (Error)", "Matrix range too long", "Khoảng thời gian xem ma trận tối đa 31 ngày và Từ ngày phải trước Đến ngày.");

    /// <summary>[Inline / Toast (Error)] From-date after to-date: Từ ngày phải trước hoặc bằng Đến ngày.</summary>
    public static readonly SystemMessage MSG177 = new("MSG177", "Inline / Toast (Error)", "From-date after to-date", "Từ ngày phải trước hoặc bằng Đến ngày.");

    /// <summary>[Empty-state message] No attendance data in range: Không có dữ liệu chuyên cần trong khoảng thời gian đã chọn.</summary>
    public static readonly SystemMessage MSG178 = new("MSG178", "Empty-state message", "No attendance data in range", "Không có dữ liệu chuyên cần trong khoảng thời gian đã chọn.");

    /// <summary>[Notification (In-app)] Parent confirmation of lunch registration: Tiêu đề: Xác nhận đăng ký bán trú / Nội dung: Đã đăng ký thành công {số lượng} ngày ăn bán trú ({danh sách ngày}) cho học sinh {họ tên}.</summary>
    public static readonly SystemMessage MSG179 = new("MSG179", "Notification (In-app)", "Parent confirmation of lunch registration", "Tiêu đề: Xác nhận đăng ký bán trú / Nội dung: Đã đăng ký thành công {số lượng} ngày ăn bán trú ({danh sách ngày}) cho học sinh {họ tên}.");

    /// <summary>[Toast (Success)] Meal type and allergy note updated: Đã cập nhật chế độ ăn và ghi chú dị ứng thành công.</summary>
    public static readonly SystemMessage MSG180 = new("MSG180", "Toast (Success)", "Meal type and allergy note updated", "Đã cập nhật chế độ ăn và ghi chú dị ứng thành công.");

    /// <summary>[Toast (Error)] Lunch registration deadline passed: Đã quá thời hạn đăng ký ({giờ chốt}) cho ngày {ngày}.</summary>
    public static readonly SystemMessage MSG181 = new("MSG181", "Toast (Error)", "Lunch registration deadline passed", "Đã quá thời hạn đăng ký ({giờ chốt}) cho ngày {ngày}.");

    /// <summary>[Toast (Error)] Student has approved leave on selected lunch date: Học sinh đã có đơn xin nghỉ được phê duyệt cho ngày {ngày}. Vui lòng thu hồi đơn xin nghỉ trước khi đăng ký lại suất ăn.</summary>
    public static readonly SystemMessage MSG182 = new("MSG182", "Toast (Error)", "Student has approved leave on selected lunch date", "Học sinh đã có đơn xin nghỉ được phê duyệt cho ngày {ngày}. Vui lòng thu hồi đơn xin nghỉ trước khi đăng ký lại suất ăn.");

    /// <summary>[Toast (Error)] Approved leave blocks lunch registration (ParentLunchPage): Ngày này học sinh đang có đơn xin nghỉ đã được phê duyệt. Vui lòng thu hồi đơn xin nghỉ trước khi đăng ký lại suất ăn.</summary>
    public static readonly SystemMessage MSG183 = new("MSG183", "Toast (Error)", "Approved leave blocks lunch registration (ParentLunchPage)", "Ngày này học sinh đang có đơn xin nghỉ đã được phê duyệt. Vui lòng thu hồi đơn xin nghỉ trước khi đăng ký lại suất ăn.");

    /// <summary>[Toast (Error)] Day without lunch service: Ngày {ngày} không phục vụ ăn trưa.</summary>
    public static readonly SystemMessage MSG184 = new("MSG184", "Toast (Error)", "Day without lunch service", "Ngày {ngày} không phục vụ ăn trưa.");

    /// <summary>[Inline / Toast (Error)] Special diet requires a description: Khi chọn chế độ ăn đặc biệt, vui lòng nhập mô tả dị ứng / kiêng khem cụ thể để nhà bếp chuẩn bị.</summary>
    public static readonly SystemMessage MSG185 = new("MSG185", "Inline / Toast (Error)", "Special diet requires a description", "Khi chọn chế độ ăn đặc biệt, vui lòng nhập mô tả dị ứng / kiêng khem cụ thể để nhà bếp chuẩn bị.");

    /// <summary>[Notification (In-app)] Parent confirmation of lunch cancellation: Tiêu đề: Đã hủy đăng ký suất ăn bán trú / Nội dung: Đã hủy suất ăn bán trú ngày {ngày} của học sinh {họ tên} trước giờ chốt ({giờ chốt}).</summary>
    public static readonly SystemMessage MSG186 = new("MSG186", "Notification (In-app)", "Parent confirmation of lunch cancellation", "Tiêu đề: Đã hủy đăng ký suất ăn bán trú / Nội dung: Đã hủy suất ăn bán trú ngày {ngày} của học sinh {họ tên} trước giờ chốt ({giờ chốt}).");

    /// <summary>[Toast (Error)] No valid lunch registration for the day: Không tìm thấy đăng ký ăn trưa còn hiệu lực cho ngày đã chọn.</summary>
    public static readonly SystemMessage MSG187 = new("MSG187", "Toast (Error)", "No valid lunch registration for the day", "Không tìm thấy đăng ký ăn trưa còn hiệu lực cho ngày đã chọn.");

    /// <summary>[Empty-state message] No boarding data: Không có dữ liệu bán trú phù hợp.</summary>
    public static readonly SystemMessage MSG188 = new("MSG188", "Empty-state message", "No boarding data", "Không có dữ liệu bán trú phù hợp.");

    /// <summary>[Notification (In-app)] Parent notified that a lunch was cancelled because school does not serve: Tiêu đề: Thông báo hủy suất ăn do đóng ngày phục vụ / Nội dung: Suất ăn bán trú ngày {ngày} của học sinh {họ tên} đã được tự động hủy do nhà trường không phục vụ bán trú ngày này.</summary>
    public static readonly SystemMessage MSG189 = new("MSG189", "Notification (In-app)", "Parent notified that a lunch was cancelled because school does not serve", "Tiêu đề: Thông báo hủy suất ăn do đóng ngày phục vụ / Nội dung: Suất ăn bán trú ngày {ngày} của học sinh {họ tên} đã được tự động hủy do nhà trường không phục vụ bán trú ngày này.");

    /// <summary>[Toast (Success)] Lunch day configuration saved with auto-cancellations: Đã cập nhật cấu hình ngày phục vụ và tự động hủy {số lượng} suất ăn đã đăng ký.</summary>
    public static readonly SystemMessage MSG190 = new("MSG190", "Toast (Success)", "Lunch day configuration saved with auto-cancellations", "Đã cập nhật cấu hình ngày phục vụ và tự động hủy {số lượng} suất ăn đã đăng ký.");

    /// <summary>[Toast (Success)] Lunch configuration saved for several days: Đã cập nhật cấu hình cho {số lượng} ngày trong tuần!</summary>
    public static readonly SystemMessage MSG191 = new("MSG191", "Toast (Success)", "Lunch configuration saved for several days", "Đã cập nhật cấu hình cho {số lượng} ngày trong tuần!");

    /// <summary>[Toast (Success)] Meal list locked and sent to the kitchen: Đã chốt danh sách suất ăn gửi nhà bếp thành công (mốc {giờ chốt})</summary>
    public static readonly SystemMessage MSG192 = new("MSG192", "Toast (Success)", "Meal list locked and sent to the kitchen", "Đã chốt danh sách suất ăn gửi nhà bếp thành công (mốc {giờ chốt})");

    /// <summary>[Inline / Toast (Error)] Unlock reason required: Bắt buộc phải nhập lý do mở khóa ngày bán trú.</summary>
    public static readonly SystemMessage MSG193 = new("MSG193", "Inline / Toast (Error)", "Unlock reason required", "Bắt buộc phải nhập lý do mở khóa ngày bán trú.");

    /// <summary>[Toast (Success)] Lunch day unlocked: Đã mở khóa ngày bán trú {ngày} thành công.</summary>
    public static readonly SystemMessage MSG194 = new("MSG194", "Toast (Success)", "Lunch day unlocked", "Đã mở khóa ngày bán trú {ngày} thành công.");

    /// <summary>[Inline / Toast (Error)] Lunch cutoff time out of range: Giờ chốt suất ăn phải có định dạng HH:mm trong khoảng 06:00 đến 11:00 (trước giờ ăn trưa).</summary>
    public static readonly SystemMessage MSG195 = new("MSG195", "Inline / Toast (Error)", "Lunch cutoff time out of range", "Giờ chốt suất ăn phải có định dạng HH:mm trong khoảng 06:00 đến 11:00 (trước giờ ăn trưa).");

    /// <summary>[Toast (Error)] Lunch day already closed (locked): Ngày ({ngày}) đã chốt sổ bán trú. Vui lòng mở khóa trước khi thay đổi cấu hình.</summary>
    public static readonly SystemMessage MSG196 = new("MSG196", "Toast (Error)", "Lunch day already closed (locked)", "Ngày ({ngày}) đã chốt sổ bán trú. Vui lòng mở khóa trước khi thay đổi cấu hình.");

    /// <summary>[Toast (Error)] Day can only be locked if today or past: Chỉ được chốt/khóa suất ăn cho ngày hôm nay hoặc ngày đã qua.</summary>
    public static readonly SystemMessage MSG197 = new("MSG197", "Toast (Error)", "Day can only be locked if today or past", "Chỉ được chốt/khóa suất ăn cho ngày hôm nay hoặc ngày đã qua.");

    /// <summary>[Toast (Success)] Daily boarding operation list exported to CSV: Đã xuất danh sách vận hành bán trú (.csv) thành công.</summary>
    public static readonly SystemMessage MSG198 = new("MSG198", "Toast (Success)", "Daily boarding operation list exported to CSV", "Đã xuất danh sách vận hành bán trú (.csv) thành công.");

    /// <summary>[Inline / Toast (Error)] Restoration reason required: Bắt buộc phải nhập lý do khi khôi phục suất ăn sau giờ chốt.</summary>
    public static readonly SystemMessage MSG199 = new("MSG199", "Inline / Toast (Error)", "Restoration reason required", "Bắt buộc phải nhập lý do khi khôi phục suất ăn sau giờ chốt.");

    /// <summary>[Notification (In-app)] Alert: meal restored after cutoff: Tiêu đề: Cảnh báo: Khôi phục suất ăn sau giờ chốt / Nội dung: Suất ăn ngày {ngày} của học sinh {họ tên} đã được khôi phục ngoại lệ sau giờ chốt ({giờ chốt}). Lý do: {lý do}</summary>
    public static readonly SystemMessage MSG200 = new("MSG200", "Notification (In-app)", "Alert: meal restored after cutoff", "Tiêu đề: Cảnh báo: Khôi phục suất ăn sau giờ chốt / Nội dung: Suất ăn ngày {ngày} của học sinh {họ tên} đã được khôi phục ngoại lệ sau giờ chốt ({giờ chốt}). Lý do: {lý do}");

    /// <summary>[Toast (Success)] Meal restored: Đã khôi phục suất ăn</summary>
    public static readonly SystemMessage MSG201 = new("MSG201", "Toast (Success)", "Meal restored", "Đã khôi phục suất ăn");

    /// <summary>[Toast (Error)] Meal restoration after cutoff not permitted: Đã qua giờ chốt ({giờ chốt}) hoặc ngày đã khóa. Chỉ Nhân viên bán trú / Quản trị viên mới được phép khôi phục ngoại lệ.</summary>
    public static readonly SystemMessage MSG202 = new("MSG202", "Toast (Error)", "Meal restoration after cutoff not permitted", "Đã qua giờ chốt ({giờ chốt}) hoặc ngày đã khóa. Chỉ Nhân viên bán trú / Quản trị viên mới được phép khôi phục ngoại lệ.");

    /// <summary>[Toast (Error)] No cancelled registration to restore: Học sinh chưa có bản ghi đăng ký bán trú bị hủy cho ngày này</summary>
    public static readonly SystemMessage MSG203 = new("MSG203", "Toast (Error)", "No cancelled registration to restore", "Học sinh chưa có bản ghi đăng ký bán trú bị hủy cho ngày này");

    /// <summary>[Toast (Success)] Class meal summary exported to CSV: Đã xuất bảng tổng hợp suất ăn theo lớp (.csv) thành công.</summary>
    public static readonly SystemMessage MSG204 = new("MSG204", "Toast (Success)", "Class meal summary exported to CSV", "Đã xuất bảng tổng hợp suất ăn theo lớp (.csv) thành công.");

    /// <summary>[Toast (Success)] Boarding statistics report exported to CSV: Đã xuất báo cáo thống kê bán trú (.csv)!</summary>
    public static readonly SystemMessage MSG205 = new("MSG205", "Toast (Success)", "Boarding statistics report exported to CSV", "Đã xuất báo cáo thống kê bán trú (.csv)!");

    /// <summary>[Inline / Toast (Error)] Lunch report range invalid: Khoảng thời gian báo cáo không hợp lệ (tối đa 92 ngày).</summary>
    public static readonly SystemMessage MSG206 = new("MSG206", "Inline / Toast (Error)", "Lunch report range invalid", "Khoảng thời gian báo cáo không hợp lệ (tối đa 92 ngày).");

    /// <summary>[Inline / Toast (Error)] Report period exceeds 92 days (headcount page): Khoảng thời gian báo cáo tối đa 92 ngày.</summary>
    public static readonly SystemMessage MSG207 = new("MSG207", "Inline / Toast (Error)", "Report period exceeds 92 days (headcount page)", "Khoảng thời gian báo cáo tối đa 92 ngày.");

    /// <summary>[Notification (In-app)] Office staff notified of a class transfer request: Tiêu đề: Yêu cầu chuyển lớp mới / Nội dung: Phụ huynh học sinh {họ tên} vừa nộp đơn xin chuyển từ lớp {lớp hiện tại} sang lớp {lớp muốn chuyển}.</summary>
    public static readonly SystemMessage MSG208 = new("MSG208", "Notification (In-app)", "Office staff notified of a class transfer request", "Tiêu đề: Yêu cầu chuyển lớp mới / Nội dung: Phụ huynh học sinh {họ tên} vừa nộp đơn xin chuyển từ lớp {lớp hiện tại} sang lớp {lớp muốn chuyển}.");

    /// <summary>[Toast (Success)] Class transfer request submitted: Đã gửi đơn xin chuyển lớp thành công!</summary>
    public static readonly SystemMessage MSG209 = new("MSG209", "Toast (Success)", "Class transfer request submitted", "Đã gửi đơn xin chuyển lớp thành công!");

    /// <summary>[Toast (Success)] Request resubmitted by the parent: Đã nộp lại hồ sơ thành công! Giáo vụ sẽ kiểm tra lại.</summary>
    public static readonly SystemMessage MSG210 = new("MSG210", "Toast (Success)", "Request resubmitted by the parent", "Đã nộp lại hồ sơ thành công! Giáo vụ sẽ kiểm tra lại.");

    /// <summary>[Notification (In-app)] Office staff notified that the parent resubmitted a request: Tiêu đề: Phụ huynh đã cập nhật &amp; nộp lại hồ sơ / Nội dung: Đơn {mã đơn} của học sinh {họ tên} đã được phụ huynh bổ sung hồ sơ. Vui lòng kiểm tra lại.</summary>
    public static readonly SystemMessage MSG211 = new("MSG211", "Notification (In-app)", "Office staff notified that the parent resubmitted a request", "Tiêu đề: Phụ huynh đã cập nhật & nộp lại hồ sơ / Nội dung: Đơn {mã đơn} của học sinh {họ tên} đã được phụ huynh bổ sung hồ sơ. Vui lòng kiểm tra lại.");

    /// <summary>[Toast (Error)] Student has no class, transfer impossible: Học sinh hiện chưa được xếp vào lớp nào nên không thể xin chuyển lớp.</summary>
    public static readonly SystemMessage MSG212 = new("MSG212", "Toast (Error)", "Student has no class, transfer impossible", "Học sinh hiện chưa được xếp vào lớp nào nên không thể xin chuyển lớp.");

    /// <summary>[Toast (Error)] Target class equals the current class: Lớp muốn chuyển tới trùng với lớp hiện tại của học sinh.</summary>
    public static readonly SystemMessage MSG213 = new("MSG213", "Toast (Error)", "Target class equals the current class", "Lớp muốn chuyển tới trùng với lớp hiện tại của học sinh.");

    /// <summary>[Toast (Error)] Target class is full (transfer request): Lớp {tên lớp} đã đủ sĩ số ({số hiện có}/{sĩ số tối đa}). Không thể nộp đơn xin chuyển tới lớp này.</summary>
    public static readonly SystemMessage MSG214 = new("MSG214", "Toast (Error)", "Target class is full (transfer request)", "Lớp {tên lớp} đã đủ sĩ số ({số hiện có}/{sĩ số tối đa}). Không thể nộp đơn xin chuyển tới lớp này.");

    /// <summary>[Toast (Error)] Transfer only allowed within the same grade: Chỉ được xin chuyển lớp trong cùng khối (Khối {khối}). Lớp đích '{tên lớp}' thuộc Khối {khối đích}.</summary>
    public static readonly SystemMessage MSG215 = new("MSG215", "Toast (Error)", "Transfer only allowed within the same grade", "Chỉ được xin chuyển lớp trong cùng khối (Khối {khối}). Lớp đích '{tên lớp}' thuộc Khối {khối đích}.");

    /// <summary>[Toast (Error)] Student already has a transfer request in progress (UC-36 E1): Học sinh hiện đang có một đơn xin chuyển lớp đang trong quá trình xử lý (UC-36 E1).</summary>
    public static readonly SystemMessage MSG216 = new("MSG216", "Toast (Error)", "Student already has a transfer request in progress (UC-36 E1)", "Học sinh hiện đang có một đơn xin chuyển lớp đang trong quá trình xử lý (UC-36 E1).");

    /// <summary>[Inline / Toast (Error)] Transfer effective date in the past: Ngày mong muốn chuyển lớp không được là ngày trong quá khứ.</summary>
    public static readonly SystemMessage MSG217 = new("MSG217", "Inline / Toast (Error)", "Transfer effective date in the past", "Ngày mong muốn chuyển lớp không được là ngày trong quá khứ.");

    /// <summary>[Inline / Toast (Error)] File exceeds 10MB: Kích thước tệp không được vượt quá 10MB (BR-50).</summary>
    public static readonly SystemMessage MSG218 = new("MSG218", "Inline / Toast (Error)", "File exceeds 10MB", "Kích thước tệp không được vượt quá 10MB (BR-50).");

    /// <summary>[Notification (In-app)] School Board notified of a class transfer proposal: Tiêu đề: Đề xuất chuyển lớp chờ phê duyệt / Nội dung: Giáo vụ đã thẩm định và đề xuất chuyển lớp cho học sinh {họ tên} (Đơn {mã đơn}).</summary>
    public static readonly SystemMessage MSG219 = new("MSG219", "Notification (In-app)", "School Board notified of a class transfer proposal", "Tiêu đề: Đề xuất chuyển lớp chờ phê duyệt / Nội dung: Giáo vụ đã thẩm định và đề xuất chuyển lớp cho học sinh {họ tên} (Đơn {mã đơn}).");

    /// <summary>[Toast (Success)] Transfer proposal submitted to the School Board: Đã gửi đề xuất chuyển lớp lên Ban Giám Hiệu phê duyệt!</summary>
    public static readonly SystemMessage MSG220 = new("MSG220", "Toast (Success)", "Transfer proposal submitted to the School Board", "Đã gửi đề xuất chuyển lớp lên Ban Giám Hiệu phê duyệt!");

    /// <summary>[Inline / Toast (Error)] Amendment request content required: Vui lòng nhập nội dung yêu cầu bổ sung (tối thiểu 5 ký tự).</summary>
    public static readonly SystemMessage MSG221 = new("MSG221", "Inline / Toast (Error)", "Amendment request content required", "Vui lòng nhập nội dung yêu cầu bổ sung (tối thiểu 5 ký tự).");

    /// <summary>[Toast (Success)] Amendment request sent to parent (transfer modal): Đã gửi yêu cầu bổ sung hồ sơ tới phụ huynh!</summary>
    public static readonly SystemMessage MSG222 = new("MSG222", "Toast (Success)", "Amendment request sent to parent (transfer modal)", "Đã gửi yêu cầu bổ sung hồ sơ tới phụ huynh!");

    /// <summary>[Notification (In-app)] Parent notified that a request needs more information: Tiêu đề: Yêu cầu bổ sung thông tin đơn {mã đơn} / Nội dung: Giáo vụ đã gửi yêu cầu bổ sung hồ sơ cho học sinh {họ tên}: {nội dung yêu cầu}</summary>
    public static readonly SystemMessage MSG223 = new("MSG223", "Notification (In-app)", "Parent notified that a request needs more information", "Tiêu đề: Yêu cầu bổ sung thông tin đơn {mã đơn} / Nội dung: Giáo vụ đã gửi yêu cầu bổ sung hồ sơ cho học sinh {họ tên}: {nội dung yêu cầu}");

    /// <summary>[Inline / Toast (Error)] Rejection reason required (min 5 characters): Vui lòng nhập lý do từ chối đơn (tối thiểu 5 ký tự).</summary>
    public static readonly SystemMessage MSG224 = new("MSG224", "Inline / Toast (Error)", "Rejection reason required (min 5 characters)", "Vui lòng nhập lý do từ chối đơn (tối thiểu 5 ký tự).");

    /// <summary>[Toast (Success)] Request rejected by office staff: Đã từ chối đơn thành công!</summary>
    public static readonly SystemMessage MSG225 = new("MSG225", "Toast (Success)", "Request rejected by office staff", "Đã từ chối đơn thành công!");

    /// <summary>[Notification (In-app)] Parent notified that office staff rejected a request: Tiêu đề: Yêu cầu hành chính bị từ chối tiếp nhận / Nội dung: Đơn {mã đơn} ({loại đơn}) cho học sinh {họ tên} không được Giáo vụ tiếp nhận. Lý do: {lý do}</summary>
    public static readonly SystemMessage MSG226 = new("MSG226", "Notification (In-app)", "Parent notified that office staff rejected a request", "Tiêu đề: Yêu cầu hành chính bị từ chối tiếp nhận / Nội dung: Đơn {mã đơn} ({loại đơn}) cho học sinh {họ tên} không được Giáo vụ tiếp nhận. Lý do: {lý do}");

    /// <summary>[Toast (Error)] Target class became full before endorsement: Lớp {tên lớp} đã đủ sĩ số ({số hiện có}/{sĩ số tối đa}).</summary>
    public static readonly SystemMessage MSG227 = new("MSG227", "Toast (Error)", "Target class became full before endorsement", "Lớp {tên lớp} đã đủ sĩ số ({số hiện có}/{sĩ số tối đa}).");

    /// <summary>[Inline / Toast (Error)] Effective date of class transfer not selected: Vui lòng chọn ngày có hiệu lực chuyển lớp (không được để trống).</summary>
    public static readonly SystemMessage MSG228 = new("MSG228", "Inline / Toast (Error)", "Effective date of class transfer not selected", "Vui lòng chọn ngày có hiệu lực chuyển lớp (không được để trống).");

    /// <summary>[Inline / Toast (Error)] Effective date of class transfer in the past: Ngày có hiệu lực chuyển lớp không được là ngày trong quá khứ.</summary>
    public static readonly SystemMessage MSG229 = new("MSG229", "Inline / Toast (Error)", "Effective date of class transfer in the past", "Ngày có hiệu lực chuyển lớp không được là ngày trong quá khứ.");

    /// <summary>[Toast (Error)] Request waits for parent amendment, cannot be submitted to the Board: Hồ sơ đang ở trạng thái chờ phụ huynh bổ sung thông tin/giấy tờ. Giáo vụ chỉ có thể trình Ban Giám Hiệu sau khi phụ huynh đã hoàn tất bổ sung.</summary>
    public static readonly SystemMessage MSG230 = new("MSG230", "Toast (Error)", "Request waits for parent amendment, cannot be submitted to the Board", "Hồ sơ đang ở trạng thái chờ phụ huynh bổ sung thông tin/giấy tờ. Giáo vụ chỉ có thể trình Ban Giám Hiệu sau khi phụ huynh đã hoàn tất bổ sung.");

    /// <summary>[Toast (Error)] Transfer request awaiting parent amendment (modal): Đơn đang chờ phụ huynh bổ sung hồ sơ. Chưa thể trình Ban Giám Hiệu.</summary>
    public static readonly SystemMessage MSG231 = new("MSG231", "Toast (Error)", "Transfer request awaiting parent amendment (modal)", "Đơn đang chờ phụ huynh bổ sung hồ sơ. Chưa thể trình Ban Giám Hiệu.");

    /// <summary>[Notification (In-app)] Parent notified that the Board approved a class transfer: Tiêu đề: BGH đã duyệt đơn chuyển lớp / Nội dung: Đơn chuyển lớp {mã đơn} của học sinh {họ tên} đã được Ban Giám Hiệu phê duyệt. Giáo vụ sẽ thực hiện điều chuyển lớp trong thời gian sớm nhất.</summary>
    public static readonly SystemMessage MSG232 = new("MSG232", "Notification (In-app)", "Parent notified that the Board approved a class transfer", "Tiêu đề: BGH đã duyệt đơn chuyển lớp / Nội dung: Đơn chuyển lớp {mã đơn} của học sinh {họ tên} đã được Ban Giám Hiệu phê duyệt. Giáo vụ sẽ thực hiện điều chuyển lớp trong thời gian sớm nhất.");

    /// <summary>[Notification (In-app)] Office staff notified that the Board approved a class transfer: Tiêu đề: BGH đã duyệt đơn chuyển lớp / Nội dung: Đơn chuyển lớp {mã đơn} của học sinh {họ tên} đã được BGH duyệt. Vui lòng thực hiện thao tác chuyển lớp.</summary>
    public static readonly SystemMessage MSG233 = new("MSG233", "Notification (In-app)", "Office staff notified that the Board approved a class transfer", "Tiêu đề: BGH đã duyệt đơn chuyển lớp / Nội dung: Đơn chuyển lớp {mã đơn} của học sinh {họ tên} đã được BGH duyệt. Vui lòng thực hiện thao tác chuyển lớp.");

    /// <summary>[Notification (In-app)] Parent notified that the Board rejected a request: Tiêu đề: Yêu cầu hành chính không được phê duyệt / Nội dung: Yêu cầu {mã đơn} ({loại đơn}) cho học sinh {họ tên} không được BGH phê duyệt. Lý do: {lý do}</summary>
    public static readonly SystemMessage MSG234 = new("MSG234", "Notification (In-app)", "Parent notified that the Board rejected a request", "Tiêu đề: Yêu cầu hành chính không được phê duyệt / Nội dung: Yêu cầu {mã đơn} ({loại đơn}) cho học sinh {họ tên} không được BGH phê duyệt. Lý do: {lý do}");

    /// <summary>[Toast (Success)] Board approved class transfer request: Đã duyệt đơn chuyển lớp! Hồ sơ đã được chuyển tới Giáo vụ để thực thi phân lớp.</summary>
    public static readonly SystemMessage MSG235 = new("MSG235", "Toast (Success)", "Board approved class transfer request", "Đã duyệt đơn chuyển lớp! Hồ sơ đã được chuyển tới Giáo vụ để thực thi phân lớp.");

    /// <summary>[Toast (Success)] Request rejected by the Board and logged: Đã từ chối đơn yêu cầu và ghi vết nhật ký kiểm toán (Audit Log).</summary>
    public static readonly SystemMessage MSG236 = new("MSG236", "Toast (Success)", "Request rejected by the Board and logged", "Đã từ chối đơn yêu cầu và ghi vết nhật ký kiểm toán (Audit Log).");

    /// <summary>[Inline / Toast (Error)] Signed document file required before approval: Vui lòng tải lên tệp văn bản đã gắn chữ ký số / đóng dấu (PDF hoặc Ảnh) trước khi phê duyệt phát hành.</summary>
    public static readonly SystemMessage MSG237 = new("MSG237", "Inline / Toast (Error)", "Signed document file required before approval", "Vui lòng tải lên tệp văn bản đã gắn chữ ký số / đóng dấu (PDF hoặc Ảnh) trước khi phê duyệt phát hành.");

    /// <summary>[Toast (Success)] Board approved and issued official document: Đã phê duyệt và phát hành văn bản chính thức!</summary>
    public static readonly SystemMessage MSG238 = new("MSG238", "Toast (Success)", "Board approved and issued official document", "Đã phê duyệt và phát hành văn bản chính thức!");

    /// <summary>[Inline / Toast (Error)] Board return note required: Vui lòng nhập nội dung yêu cầu Giáo vụ chỉnh sửa / gửi lại (tối thiểu 5 ký tự).</summary>
    public static readonly SystemMessage MSG239 = new("MSG239", "Inline / Toast (Error)", "Board return note required", "Vui lòng nhập nội dung yêu cầu Giáo vụ chỉnh sửa / gửi lại (tối thiểu 5 ký tự).");

    /// <summary>[Toast (Success)] Request returned to office staff by the Board: Đã trả hồ sơ về cho bộ phận Giáo vụ chỉnh sửa và trình lại!</summary>
    public static readonly SystemMessage MSG240 = new("MSG240", "Toast (Success)", "Request returned to office staff by the Board", "Đã trả hồ sơ về cho bộ phận Giáo vụ chỉnh sửa và trình lại!");

    /// <summary>[Inline / Toast (Error)] Board rejection reason required: Vui lòng nhập lý do từ chối yêu cầu (tối thiểu 5 ký tự).</summary>
    public static readonly SystemMessage MSG241 = new("MSG241", "Inline / Toast (Error)", "Board rejection reason required", "Vui lòng nhập lý do từ chối yêu cầu (tối thiểu 5 ký tự).");

    /// <summary>[Inline / Toast (Error)] Board rejection reason missing (Board decision modal): Bắt buộc phải nhập lý do từ chối (tối thiểu 5 ký tự) để thông báo tới phụ huynh.</summary>
    public static readonly SystemMessage MSG242 = new("MSG242", "Inline / Toast (Error)", "Board rejection reason missing (Board decision modal)", "Bắt buộc phải nhập lý do từ chối (tối thiểu 5 ký tự) để thông báo tới phụ huynh.");

    /// <summary>[Toast (Error)] Request not processed by the Board yet or already processed: Yêu cầu chưa được Giáo vụ thẩm định hoặc đã được xử lý trước đó.</summary>
    public static readonly SystemMessage MSG243 = new("MSG243", "Toast (Error)", "Request not processed by the Board yet or already processed", "Yêu cầu chưa được Giáo vụ thẩm định hoặc đã được xử lý trước đó.");

    /// <summary>[Confirmation Dialog] Confirm class transfer execution: Xác nhận thực thi chuyển lớp cho học sinh {họ tên} sang lớp {tên lớp}? Hệ thống sẽ tự động gán lớp mới và hoàn tất hồ sơ.</summary>
    public static readonly SystemMessage MSG244 = new("MSG244", "Confirmation Dialog", "Confirm class transfer execution", "Xác nhận thực thi chuyển lớp cho học sinh {họ tên} sang lớp {tên lớp}? Hệ thống sẽ tự động gán lớp mới và hoàn tất hồ sơ.");

    /// <summary>[Confirmation Dialog] Confirm early class transfer before the effective date: LƯU Ý NGÀY HIỆU LỰC: Đơn này có ngày hiệu lực mong muốn là ngày {ngày} (Chưa đến hạn). Bạn có chắc chắn muốn thực thi điều chuyển SỚM ngay hôm nay không?</summary>
    public static readonly SystemMessage MSG245 = new("MSG245", "Confirmation Dialog", "Confirm early class transfer before the effective date", "LƯU Ý NGÀY HIỆU LỰC: Đơn này có ngày hiệu lực mong muốn là ngày {ngày} (Chưa đến hạn). Bạn có chắc chắn muốn thực thi điều chuyển SỚM ngay hôm nay không?");

    /// <summary>[Notification (In-app)] Parent notified that the class transfer was executed: Tiêu đề: Đã cập nhật chuyển lớp thành công / Nội dung: Học sinh {họ tên} đã được chuyển sang lớp {tên lớp} thành công. Vui lòng kiểm tra thời khóa biểu mới của con.</summary>
    public static readonly SystemMessage MSG246 = new("MSG246", "Notification (In-app)", "Parent notified that the class transfer was executed", "Tiêu đề: Đã cập nhật chuyển lớp thành công / Nội dung: Học sinh {họ tên} đã được chuyển sang lớp {tên lớp} thành công. Vui lòng kiểm tra thời khóa biểu mới của con.");

    /// <summary>[Toast (Success)] Class transfer executed: Đã áp dụng chuyển lớp thành công cho học sinh!</summary>
    public static readonly SystemMessage MSG247 = new("MSG247", "Toast (Success)", "Class transfer executed", "Đã áp dụng chuyển lớp thành công cho học sinh!");

    /// <summary>[Toast (Error)] Target class full at execution time: Lớp {tên lớp} đã đủ sĩ số ({số hiện có}/{sĩ số tối đa}). Không thể thực thi chuyển lớp.</summary>
    public static readonly SystemMessage MSG248 = new("MSG248", "Toast (Error)", "Target class full at execution time", "Lớp {tên lớp} đã đủ sĩ số ({số hiện có}/{sĩ số tối đa}). Không thể thực thi chuyển lớp.");

    /// <summary>[Toast (Error)] Class transfer not yet approved by the Board: Đơn chuyển lớp chưa được Ban Giám Hiệu phê duyệt.</summary>
    public static readonly SystemMessage MSG249 = new("MSG249", "Toast (Error)", "Class transfer not yet approved by the Board", "Đơn chuyển lớp chưa được Ban Giám Hiệu phê duyệt.");

    /// <summary>[Toast (Error)] Class transfer already executed: Đơn chuyển lớp này đã được thực thi trước đó.</summary>
    public static readonly SystemMessage MSG250 = new("MSG250", "Toast (Error)", "Class transfer already executed", "Đơn chuyển lớp này đã được thực thi trước đó.");

    /// <summary>[Notification (In-app)] Office staff notified of a document request: Tiêu đề: Yêu cầu cấp giấy tờ / chứng nhận mới / Nội dung: Phụ huynh học sinh {họ tên} vừa nộp đơn xin cấp {loại giấy tờ} ({số lượng} bản).</summary>
    public static readonly SystemMessage MSG251 = new("MSG251", "Notification (In-app)", "Office staff notified of a document request", "Tiêu đề: Yêu cầu cấp giấy tờ / chứng nhận mới / Nội dung: Phụ huynh học sinh {họ tên} vừa nộp đơn xin cấp {loại giấy tờ} ({số lượng} bản).");

    /// <summary>[Toast (Success)] Document request submitted: Đã gửi đơn xin cấp giấy tờ thành công!</summary>
    public static readonly SystemMessage MSG252 = new("MSG252", "Toast (Success)", "Document request submitted", "Đã gửi đơn xin cấp giấy tờ thành công!");

    /// <summary>[Toast (Success)] Other request submitted: Đã gửi đơn thành công!</summary>
    public static readonly SystemMessage MSG253 = new("MSG253", "Toast (Success)", "Other request submitted", "Đã gửi đơn thành công!");

    /// <summary>[Notification (In-app)] Office staff notified of another type of request: Tiêu đề: Yêu cầu / Kiến nghị khác mới / Nội dung: Phụ huynh học sinh {họ tên} vừa nộp đơn mới: {tiêu đề đơn}.</summary>
    public static readonly SystemMessage MSG254 = new("MSG254", "Notification (In-app)", "Office staff notified of another type of request", "Tiêu đề: Yêu cầu / Kiến nghị khác mới / Nội dung: Phụ huynh học sinh {họ tên} vừa nộp đơn mới: {tiêu đề đơn}.");

    /// <summary>[Inline / Toast (Error)] Document request: student, type or purpose missing: Vui lòng chọn học sinh, loại giấy tờ và mục đích xin cấp.</summary>
    public static readonly SystemMessage MSG255 = new("MSG255", "Inline / Toast (Error)", "Document request: student, type or purpose missing", "Vui lòng chọn học sinh, loại giấy tờ và mục đích xin cấp.");

    /// <summary>[Inline / Toast (Error)] Document purpose too short: Vui lòng nhập mục đích xin cấp giấy tờ (tối thiểu 5 ký tự).</summary>
    public static readonly SystemMessage MSG256 = new("MSG256", "Inline / Toast (Error)", "Document purpose too short", "Vui lòng nhập mục đích xin cấp giấy tờ (tối thiểu 5 ký tự).");

    /// <summary>[Inline / Toast (Error)] Document purpose length invalid: Mục đích xin cấp giấy tờ phải có độ dài từ 5 đến 1000 ký tự.</summary>
    public static readonly SystemMessage MSG257 = new("MSG257", "Inline / Toast (Error)", "Document purpose length invalid", "Mục đích xin cấp giấy tờ phải có độ dài từ 5 đến 1000 ký tự.");

    /// <summary>[Inline / Toast (Error)] Number of copies out of range: Số lượng bản sao phải từ 1 đến 10 bản (không chấp nhận số âm hoặc vượt quá 10 bản).</summary>
    public static readonly SystemMessage MSG258 = new("MSG258", "Inline / Toast (Error)", "Number of copies out of range", "Số lượng bản sao phải từ 1 đến 10 bản (không chấp nhận số âm hoặc vượt quá 10 bản).");

    /// <summary>[Inline / Toast (Error)] Document type invalid: Loại giấy tờ không hợp lệ (tối đa 150 ký tự).</summary>
    public static readonly SystemMessage MSG259 = new("MSG259", "Inline / Toast (Error)", "Document type invalid", "Loại giấy tờ không hợp lệ (tối đa 150 ký tự).");

    /// <summary>[Toast (Error)] Same document request already in progress: Học sinh hiện đang có đơn xin cấp '{loại giấy tờ}' đang trong quá trình xử lý. Quý phụ huynh vui lòng chờ kết quả đơn trước đó.</summary>
    public static readonly SystemMessage MSG260 = new("MSG260", "Toast (Error)", "Same document request already in progress", "Học sinh hiện đang có đơn xin cấp '{loại giấy tờ}' đang trong quá trình xử lý. Quý phụ huynh vui lòng chờ kết quả đơn trước đó.");

    /// <summary>[Inline / Toast (Error)] Document file required before submitting to the Board: Vui lòng tải lên tệp văn bản (PDF hoặc Ảnh) để Ban Giám Hiệu xem xét và ký số.</summary>
    public static readonly SystemMessage MSG261 = new("MSG261", "Inline / Toast (Error)", "Document file required before submitting to the Board", "Vui lòng tải lên tệp văn bản (PDF hoặc Ảnh) để Ban Giám Hiệu xem xét và ký số.");

    /// <summary>[Inline / Toast (Error)] Staff note required for document endorsement: Vui lòng nhập ghi chú / ý kiến thẩm định của Giáo vụ (tối thiểu 5 ký tự).</summary>
    public static readonly SystemMessage MSG262 = new("MSG262", "Inline / Toast (Error)", "Staff note required for document endorsement", "Vui lòng nhập ghi chú / ý kiến thẩm định của Giáo vụ (tối thiểu 5 ký tự).");

    /// <summary>[Toast (Success)] Document and attachment submitted to the School Board: Đã gửi hồ sơ và tệp đính kèm lên Ban Giám Hiệu phê duyệt!</summary>
    public static readonly SystemMessage MSG263 = new("MSG263", "Toast (Success)", "Document and attachment submitted to the School Board", "Đã gửi hồ sơ và tệp đính kèm lên Ban Giám Hiệu phê duyệt!");

    /// <summary>[Notification (In-app)] School Board notified of a document or other request awaiting approval: Tiêu đề: Hồ sơ xin cấp giấy tờ chờ phê duyệt / Nội dung: Giáo vụ đã thẩm định và trình hồ sơ '{loại giấy tờ}' cho học sinh {họ tên} (Đơn {mã đơn}).</summary>
    public static readonly SystemMessage MSG264 = new("MSG264", "Notification (In-app)", "School Board notified of a document or other request awaiting approval", "Tiêu đề: Hồ sơ xin cấp giấy tờ chờ phê duyệt / Nội dung: Giáo vụ đã thẩm định và trình hồ sơ '{loại giấy tờ}' cho học sinh {họ tên} (Đơn {mã đơn}).");

    /// <summary>[Toast (Success)] Amendment request sent to the parent: Đã gửi yêu cầu bổ sung thông tin tới phụ huynh!</summary>
    public static readonly SystemMessage MSG265 = new("MSG265", "Toast (Success)", "Amendment request sent to the parent", "Đã gửi yêu cầu bổ sung thông tin tới phụ huynh!");

    /// <summary>[Toast (Error)] Document request awaiting parent amendment (modal): Hồ sơ đang chờ phụ huynh bổ sung. Chưa thể trình Ban Giám Hiệu.</summary>
    public static readonly SystemMessage MSG266 = new("MSG266", "Toast (Error)", "Document request awaiting parent amendment (modal)", "Hồ sơ đang chờ phụ huynh bổ sung. Chưa thể trình Ban Giám Hiệu.");

    /// <summary>[Notification (In-app)] Parent notified that a document was issued by office staff: Tiêu đề: Giấy xác nhận đã được phát hành / Nội dung: Văn bản {loại giấy tờ} cho học sinh {họ tên} đã được Giáo vụ xử lý và phát hành chính thức (Số: {số văn bản}).</summary>
    public static readonly SystemMessage MSG267 = new("MSG267", "Notification (In-app)", "Parent notified that a document was issued by office staff", "Tiêu đề: Giấy xác nhận đã được phát hành / Nội dung: Văn bản {loại giấy tờ} cho học sinh {họ tên} đã được Giáo vụ xử lý và phát hành chính thức (Số: {số văn bản}).");

    /// <summary>[Toast (Success)] Document issued directly by office staff: Đã phát hành văn bản trực tiếp thành công!</summary>
    public static readonly SystemMessage MSG268 = new("MSG268", "Toast (Success)", "Document issued directly by office staff", "Đã phát hành văn bản trực tiếp thành công!");

    /// <summary>[Toast (Success)] Official document downloaded: Đã tải văn bản chính thức thành công!</summary>
    public static readonly SystemMessage MSG269 = new("MSG269", "Toast (Success)", "Official document downloaded", "Đã tải văn bản chính thức thành công!");

    /// <summary>[Toast (Success)] Request answered and completed: Đã phản hồi &amp; hoàn tất đơn thành công!</summary>
    public static readonly SystemMessage MSG270 = new("MSG270", "Toast (Success)", "Request answered and completed", "Đã phản hồi & hoàn tất đơn thành công!");

    /// <summary>[Notification (In-app)] Parent notified that another request was resolved: Tiêu đề: Đơn đề xuất / kiến nghị đã được giải quyết / Nội dung: Đơn "{loại giấy tờ}" cho học sinh {họ tên} đã được Giáo vụ xử lý và giải quyết (Mã: {số văn bản}).</summary>
    public static readonly SystemMessage MSG271 = new("MSG271", "Notification (In-app)", "Parent notified that another request was resolved", "Tiêu đề: Đơn đề xuất / kiến nghị đã được giải quyết / Nội dung: Đơn \"{loại giấy tờ}\" cho học sinh {họ tên} đã được Giáo vụ xử lý và giải quyết (Mã: {số văn bản}).");

    /// <summary>[Toast (Error)] Document type reserved for the School Board: Văn bản loại '{loại giấy tờ}' thuộc thẩm quyền quyết định của Ban Giám Hiệu (chính sách tài chính / chuyên môn khảo thí / chuyển trường). Giáo vụ vui lòng chọn tab 'Trình Ban Giám Hiệu xem xét'.</summary>
    public static readonly SystemMessage MSG272 = new("MSG272", "Toast (Error)", "Document type reserved for the School Board", "Văn bản loại '{loại giấy tờ}' thuộc thẩm quyền quyết định của Ban Giám Hiệu (chính sách tài chính / chuyên môn khảo thí / chuyển trường). Giáo vụ vui lòng chọn tab 'Trình Ban Giám Hiệu xem xét'.");

    /// <summary>[Toast (Error)] Direct issue blocked for Board-authority document (UI): Loại văn bản này thuộc thẩm quyền của Ban Giám Hiệu. Giáo vụ không thể phát hành trực tiếp.</summary>
    public static readonly SystemMessage MSG273 = new("MSG273", "Toast (Error)", "Direct issue blocked for Board-authority document (UI)", "Loại văn bản này thuộc thẩm quyền của Ban Giám Hiệu. Giáo vụ không thể phát hành trực tiếp.");

    /// <summary>[Inline / Toast (Error)] Issue date in the future: Ngày cấp / phản hồi văn bản không được là ngày trong tương lai.</summary>
    public static readonly SystemMessage MSG274 = new("MSG274", "Inline / Toast (Error)", "Issue date in the future", "Ngày cấp / phản hồi văn bản không được là ngày trong tương lai.");

    /// <summary>[Inline / Toast (Error)] Issue date before the submission date: Ngày cấp / phản hồi văn bản không thể trước ngày phụ huynh nộp đơn.</summary>
    public static readonly SystemMessage MSG275 = new("MSG275", "Inline / Toast (Error)", "Issue date before the submission date", "Ngày cấp / phản hồi văn bản không thể trước ngày phụ huynh nộp đơn.");

    /// <summary>[Inline / Toast (Error)] Stamped document file required for direct issue: Vui lòng tải lên tệp văn bản đã xác nhận/đóng dấu (PDF hoặc Ảnh) để phát hành cho Phụ huynh.</summary>
    public static readonly SystemMessage MSG276 = new("MSG276", "Inline / Toast (Error)", "Stamped document file required for direct issue", "Vui lòng tải lên tệp văn bản đã xác nhận/đóng dấu (PDF hoặc Ảnh) để phát hành cho Phụ huynh.");

    /// <summary>[Toast (Error)] Administrative request not found: Không tìm thấy yêu cầu.</summary>
    public static readonly SystemMessage MSG277 = new("MSG277", "Toast (Error)", "Administrative request not found", "Không tìm thấy yêu cầu.");

    /// <summary>[Confirmation Dialog] Confirm cancelling an administrative request: Bạn có chắc chắn muốn hủy đơn "{mã đơn}" không?</summary>
    public static readonly SystemMessage MSG278 = new("MSG278", "Confirmation Dialog", "Confirm cancelling an administrative request", "Bạn có chắc chắn muốn hủy đơn \"{mã đơn}\" không?");

    /// <summary>[Notification (In-app)] Office staff notified that the parent cancelled a request: Tiêu đề: Phụ huynh đã hủy đơn / Nội dung: Phụ huynh đã hủy đơn {mã đơn} của học sinh {họ tên}.</summary>
    public static readonly SystemMessage MSG279 = new("MSG279", "Notification (In-app)", "Office staff notified that the parent cancelled a request", "Tiêu đề: Phụ huynh đã hủy đơn / Nội dung: Phụ huynh đã hủy đơn {mã đơn} của học sinh {họ tên}.");

    /// <summary>[Toast (Success)] Request cancelled by the parent: Đã hủy đơn thành công!</summary>
    public static readonly SystemMessage MSG280 = new("MSG280", "Toast (Success)", "Request cancelled by the parent", "Đã hủy đơn thành công!");

    /// <summary>[Toast (Error)] Notification not found: Không tìm thấy thông báo.</summary>
    public static readonly SystemMessage MSG281 = new("MSG281", "Toast (Error)", "Notification not found", "Không tìm thấy thông báo.");

    /// <summary>[Empty-state message] No open class in the grade: Chưa có lớp nào mở trong khối này.</summary>
    public static readonly SystemMessage MSG282 = new("MSG282", "Empty-state message", "No open class in the grade", "Chưa có lớp nào mở trong khối này.");

    /// <summary>[Inline / Toast (Error)] Matrix view limited to 31 days: Chế độ ma trận theo ngày hỗ trợ tối đa 31 ngày. Vui lòng thu hẹp khoảng thời gian.</summary>
    public static readonly SystemMessage MSG283 = new("MSG283", "Inline / Toast (Error)", "Matrix view limited to 31 days", "Chế độ ma trận theo ngày hỗ trợ tối đa 31 ngày. Vui lòng thu hẹp khoảng thời gian.");

    /// <summary>[Empty-state message] No request matches the filter: Không tìm thấy hồ sơ nào phù hợp</summary>
    public static readonly SystemMessage MSG284 = new("MSG284", "Empty-state message", "No request matches the filter", "Không tìm thấy hồ sơ nào phù hợp");

    /// <summary>[Toast (Error)] Lunch configuration partially saved: Đã lưu {số ngày thành công}/{tổng số ngày} ngày. {lỗi đầu tiên}</summary>
    public static readonly SystemMessage MSG285 = new("MSG285", "Toast (Error)", "Lunch configuration partially saved", "Đã lưu {số ngày thành công}/{tổng số ngày} ngày. {lỗi đầu tiên}");

    /// <summary>[Toast (Error)] Admin password reset does not meet password policy: Mật khẩu mới chưa đáp ứng yêu cầu bảo mật (tối thiểu 8 ký tự, gồm hoa, thường, số, ký tự đặc biệt).</summary>
    public static readonly SystemMessage MSG286 = new("MSG286", "Toast (Error)", "Admin password reset does not meet password policy", "Mật khẩu mới chưa đáp ứng yêu cầu bảo mật (tối thiểu 8 ký tự, gồm hoa, thường, số, ký tự đặc biệt).");

    /// <summary>[Notification (In-app)] Account request has been processed: Tiêu đề: Yêu cầu cấp tài khoản đã được xử lý / Nội dung: Yêu cầu "{tiêu đề}": {số lượng} tài khoản đã tạo, {số lượng} bị từ chối, {số lượng} lỗi.</summary>
    public static readonly SystemMessage MSG287 = new("MSG287", "Notification (In-app)", "Account request has been processed", "Tiêu đề: Yêu cầu cấp tài khoản đã được xử lý / Nội dung: Yêu cầu \"{tiêu đề}\": {số lượng} tài khoản đã tạo, {số lượng} bị từ chối, {số lượng} lỗi.");

    /// <summary>[Inline / Toast (Error)] Account request review: no row selected: Vui lòng chọn các dòng cần duyệt hoặc từ chối.</summary>
    public static readonly SystemMessage MSG288 = new("MSG288", "Inline / Toast (Error)", "Account request review: no row selected", "Vui lòng chọn các dòng cần duyệt hoặc từ chối.");

    /// <summary>[Inline / Toast (Error)] Account request rejection: reason missing: Vui lòng nhập lý do từ chối.</summary>
    public static readonly SystemMessage MSG289 = new("MSG289", "Inline / Toast (Error)", "Account request rejection: reason missing", "Vui lòng nhập lý do từ chối.");

    /// <summary>[Toast (Error)] Row could not be approved: {họ tên}: {lý do} – chưa được duyệt.</summary>
    public static readonly SystemMessage MSG290 = new("MSG290", "Toast (Error)", "Row could not be approved", "{họ tên}: {lý do} – chưa được duyệt.");

    /// <summary>[Toast (Error)] Account could not be created for a row: Không tạo được tài khoản (có thể email vừa được dùng cho tài khoản khác).</summary>
    public static readonly SystemMessage MSG291 = new("MSG291", "Toast (Error)", "Account could not be created for a row", "Không tạo được tài khoản (có thể email vừa được dùng cho tài khoản khác).");

    /// <summary>[Toast (Error)] Activation email could not be sent for a row: Không gửi được email kích hoạt; Giáo vụ có thể gửi lại liên kết.</summary>
    public static readonly SystemMessage MSG292 = new("MSG292", "Toast (Error)", "Activation email could not be sent for a row", "Không gửi được email kích hoạt; Giáo vụ có thể gửi lại liên kết.");

    /// <summary>[Notification (In-app)] Parents notified of a new class post: Tiêu đề: Thông báo mới từ lớp {tên lớp} / Nội dung: GVCN vừa đăng bài: "{tiêu đề bài viết}"</summary>
    public static readonly SystemMessage MSG293 = new("MSG293", "Notification (In-app)", "Parents notified of a new class post", "Tiêu đề: Thông báo mới từ lớp {tên lớp} / Nội dung: GVCN vừa đăng bài: \"{tiêu đề bài viết}\"");

    /// <summary>[Toast (Success)] Class post updated: Cập nhật bài viết thành công.</summary>
    public static readonly SystemMessage MSG294 = new("MSG294", "Toast (Success)", "Class post updated", "Cập nhật bài viết thành công.");

    /// <summary>[Toast (Success)] Post pin status updated: Đã cập nhật trạng thái ghim.</summary>
    public static readonly SystemMessage MSG295 = new("MSG295", "Toast (Success)", "Post pin status updated", "Đã cập nhật trạng thái ghim.");

    /// <summary>[Toast (Success)] Comments enabled for a post: Đã mở bình luận cho bài viết.</summary>
    public static readonly SystemMessage MSG296 = new("MSG296", "Toast (Success)", "Comments enabled for a post", "Đã mở bình luận cho bài viết.");

    /// <summary>[Toast (Success)] Comments disabled for a post (by teacher): Đã khóa bình luận cho bài viết.</summary>
    public static readonly SystemMessage MSG297 = new("MSG297", "Toast (Success)", "Comments disabled for a post (by teacher)", "Đã khóa bình luận cho bài viết.");

    /// <summary>[Toast (Success)] Comment deleted: Đã xóa bình luận.</summary>
    public static readonly SystemMessage MSG298 = new("MSG298", "Toast (Success)", "Comment deleted", "Đã xóa bình luận.");

    /// <summary>[Confirmation Dialog] Confirm deleting a class post: Bạn có chắc chắn muốn xóa bài viết này?</summary>
    public static readonly SystemMessage MSG299 = new("MSG299", "Confirmation Dialog", "Confirm deleting a class post", "Bạn có chắc chắn muốn xóa bài viết này?");

    /// <summary>[Toast (Success)] Class post deleted: Đã xóa bài viết.</summary>
    public static readonly SystemMessage MSG300 = new("MSG300", "Toast (Success)", "Class post deleted", "Đã xóa bài viết.");

    /// <summary>[Inline / Toast (Error)] Post title or content blank: Tiêu đề và nội dung bài viết không được để trống.</summary>
    public static readonly SystemMessage MSG301 = new("MSG301", "Inline / Toast (Error)", "Post title or content blank", "Tiêu đề và nội dung bài viết không được để trống.");

    /// <summary>[Inline / Toast (Error)] Post title length invalid: Tiêu đề bài viết phải từ 5 đến 200 ký tự.</summary>
    public static readonly SystemMessage MSG302 = new("MSG302", "Inline / Toast (Error)", "Post title length invalid", "Tiêu đề bài viết phải từ 5 đến 200 ký tự.");

    /// <summary>[Inline / Toast (Error)] Post content length invalid: Nội dung bài viết phải từ 10 đến 5000 ký tự.</summary>
    public static readonly SystemMessage MSG303 = new("MSG303", "Inline / Toast (Error)", "Post content length invalid", "Nội dung bài viết phải từ 10 đến 5000 ký tự.");

    /// <summary>[Inline / Toast (Error)] Post link invalid or unsafe: Liên kết đính kèm không hợp lệ hoặc không an toàn.</summary>
    public static readonly SystemMessage MSG304 = new("MSG304", "Inline / Toast (Error)", "Post link invalid or unsafe", "Liên kết đính kèm không hợp lệ hoặc không an toàn.");

    /// <summary>[Notification (In-app)] Teacher/parent notified of a new comment: Tiêu đề: Bình luận mới từ {vai trò} / Nội dung: {họ tên} vừa bình luận bài "{tiêu đề bài viết}": "{nội dung bình luận}"</summary>
    public static readonly SystemMessage MSG305 = new("MSG305", "Notification (In-app)", "Teacher/parent notified of a new comment", "Tiêu đề: Bình luận mới từ {vai trò} / Nội dung: {họ tên} vừa bình luận bài \"{tiêu đề bài viết}\": \"{nội dung bình luận}\"");

    /// <summary>[Toast (Success)] Comment posted: Đã gửi bình luận</summary>
    public static readonly SystemMessage MSG306 = new("MSG306", "Toast (Success)", "Comment posted", "Đã gửi bình luận");

    /// <summary>[Inline / Toast (Error)] Comment empty: Vui lòng nhập nội dung bình luận.</summary>
    public static readonly SystemMessage MSG307 = new("MSG307", "Inline / Toast (Error)", "Comment empty", "Vui lòng nhập nội dung bình luận.");

    /// <summary>[Inline / Toast (Error)] Comment too long: Bình luận không được vượt quá 1000 ký tự.</summary>
    public static readonly SystemMessage MSG308 = new("MSG308", "Inline / Toast (Error)", "Comment too long", "Bình luận không được vượt quá 1000 ký tự.");

    /// <summary>[Notification (In-app)] Parent notified of a new weekly evaluation: Tiêu đề: Phiếu nhận xét tuần {tuần} của em {họ tên} / Nội dung: GVCN đã gửi nhận xét nề nếp tuần {tuần}. Vui lòng xem và ký xác nhận.</summary>
    public static readonly SystemMessage MSG309 = new("MSG309", "Notification (In-app)", "Parent notified of a new weekly evaluation", "Tiêu đề: Phiếu nhận xét tuần {tuần} của em {họ tên} / Nội dung: GVCN đã gửi nhận xét nề nếp tuần {tuần}. Vui lòng xem và ký xác nhận.");

    /// <summary>[Notification (In-app)] Parent notified that the evaluation was updated: Tiêu đề: Cập nhật phiếu nhận xét tuần {tuần} của em {họ tên} / Nội dung: GVCN đã cập nhật nhận xét nề nếp tuần {tuần}. Vui lòng xem lại và ký xác nhận.</summary>
    public static readonly SystemMessage MSG310 = new("MSG310", "Notification (In-app)", "Parent notified that the evaluation was updated", "Tiêu đề: Cập nhật phiếu nhận xét tuần {tuần} của em {họ tên} / Nội dung: GVCN đã cập nhật nhận xét nề nếp tuần {tuần}. Vui lòng xem lại và ký xác nhận.");

    /// <summary>[Inline / Toast (Error)] Conduct rating not selected: Vui lòng chọn xếp loại nề nếp.</summary>
    public static readonly SystemMessage MSG311 = new("MSG311", "Inline / Toast (Error)", "Conduct rating not selected", "Vui lòng chọn xếp loại nề nếp.");

    /// <summary>[Inline / Toast (Error)] Conduct rating invalid: Xếp loại nề nếp không hợp lệ (chỉ chấp nhận Excellent, Good, NeedsAttention).</summary>
    public static readonly SystemMessage MSG312 = new("MSG312", "Inline / Toast (Error)", "Conduct rating invalid", "Xếp loại nề nếp không hợp lệ (chỉ chấp nhận Excellent, Good, NeedsAttention).");

    /// <summary>[Inline / Toast (Error)] Teacher comment length invalid: Lời nhận xét của giáo viên phải từ 10 đến 1500 ký tự.</summary>
    public static readonly SystemMessage MSG313 = new("MSG313", "Inline / Toast (Error)", "Teacher comment length invalid", "Lời nhận xét của giáo viên phải từ 10 đến 1500 ký tự.");

    /// <summary>[Inline / Toast (Error)] Private note too long: Dặn dò riêng tối đa 500 ký tự.</summary>
    public static readonly SystemMessage MSG314 = new("MSG314", "Inline / Toast (Error)", "Private note too long", "Dặn dò riêng tối đa 500 ký tự.");

    /// <summary>[Inline / Toast (Error)] Evaluation week outside the school year: Tuần học phải nằm trong phạm vi năm học (từ tuần 1 đến tuần {số tuần}).</summary>
    public static readonly SystemMessage MSG315 = new("MSG315", "Inline / Toast (Error)", "Evaluation week outside the school year", "Tuần học phải nằm trong phạm vi năm học (từ tuần 1 đến tuần {số tuần}).");

    /// <summary>[Inline / Toast (Error)] Evaluation for a future week: Không thể nhận xét cho tuần tương lai (hiện tại là tuần {tuần hiện tại}).</summary>
    public static readonly SystemMessage MSG316 = new("MSG316", "Inline / Toast (Error)", "Evaluation for a future week", "Không thể nhận xét cho tuần tương lai (hiện tại là tuần {tuần hiện tại}).");

    /// <summary>[Notification (In-app)] Teacher notified that the parent acknowledged an evaluation: Tiêu đề: Phụ huynh đã ký nhận phiếu nhận xét / Nội dung: Phụ huynh em {họ tên} đã xác nhận phiếu tuần {tuần}. Phản hồi: "{nội dung phản hồi}"</summary>
    public static readonly SystemMessage MSG317 = new("MSG317", "Notification (In-app)", "Teacher notified that the parent acknowledged an evaluation", "Tiêu đề: Phụ huynh đã ký nhận phiếu nhận xét / Nội dung: Phụ huynh em {họ tên} đã xác nhận phiếu tuần {tuần}. Phản hồi: \"{nội dung phản hồi}\"");

    /// <summary>[Toast (Success)] Evaluation acknowledged by parent: Đã xác nhận phiếu nhận xét thành công.</summary>
    public static readonly SystemMessage MSG318 = new("MSG318", "Toast (Success)", "Evaluation acknowledged by parent", "Đã xác nhận phiếu nhận xét thành công.");

    /// <summary>[Toast (Success)] Contact note marked superseded: Đã đánh dấu ghi chú liên hệ không còn hiệu lực.</summary>
    public static readonly SystemMessage MSG319 = new("MSG319", "Toast (Success)", "Contact note marked superseded", "Đã đánh dấu ghi chú liên hệ không còn hiệu lực.");
    public static readonly SystemMessage MSG319_ContactNoteSuperseded = MSG319;

    /// <summary>Map of all system messages by code</summary>
    public static readonly IReadOnlyDictionary<string, SystemMessage> All = new Dictionary<string, SystemMessage>(System.StringComparer.OrdinalIgnoreCase)
    {
        { "MSG01", MSG01 },
        { "MSG02", MSG02 },
        { "MSG03", MSG03 },
        { "MSG04", MSG04 },
        { "MSG05", MSG05 },
        { "MSG06", MSG06 },
        { "MSG07", MSG07 },
        { "MSG08", MSG08 },
        { "MSG09", MSG09 },
        { "MSG10", MSG10 },
        { "MSG11", MSG11 },
        { "MSG12", MSG12 },
        { "MSG13", MSG13 },
        { "MSG14", MSG14 },
        { "MSG15", MSG15 },
        { "MSG16", MSG16 },
        { "MSG17", MSG17 },
        { "MSG18", MSG18 },
        { "MSG19", MSG19 },
        { "MSG20", MSG20 },
        { "MSG21", MSG21 },
        { "MSG22", MSG22 },
        { "MSG23", MSG23 },
        { "MSG24", MSG24 },
        { "MSG25", MSG25 },
        { "MSG26", MSG26 },
        { "MSG27", MSG27 },
        { "MSG28", MSG28 },
        { "MSG29", MSG29 },
        { "MSG30", MSG30 },
        { "MSG31", MSG31 },
        { "MSG32", MSG32 },
        { "MSG33", MSG33 },
        { "MSG34", MSG34 },
        { "MSG35", MSG35 },
        { "MSG36", MSG36 },
        { "MSG37", MSG37 },
        { "MSG38", MSG38 },
        { "MSG39", MSG39 },
        { "MSG40", MSG40 },
        { "MSG41", MSG41 },
        { "MSG42", MSG42 },
        { "MSG43", MSG43 },
        { "MSG44", MSG44 },
        { "MSG45", MSG45 },
        { "MSG46", MSG46 },
        { "MSG47", MSG47 },
        { "MSG48", MSG48 },
        { "MSG49", MSG49 },
        { "MSG50", MSG50 },
        { "MSG51", MSG51 },
        { "MSG52", MSG52 },
        { "MSG53", MSG53 },
        { "MSG54", MSG54 },
        { "MSG55", MSG55 },
        { "MSG56", MSG56 },
        { "MSG57", MSG57 },
        { "MSG58", MSG58 },
        { "MSG59", MSG59 },
        { "MSG60", MSG60 },
        { "MSG61", MSG61 },
        { "MSG62", MSG62 },
        { "MSG63", MSG63 },
        { "MSG64", MSG64 },
        { "MSG65", MSG65 },
        { "MSG66", MSG66 },
        { "MSG67", MSG67 },
        { "MSG68", MSG68 },
        { "MSG69", MSG69 },
        { "MSG70", MSG70 },
        { "MSG71", MSG71 },
        { "MSG72", MSG72 },
        { "MSG73", MSG73 },
        { "MSG74", MSG74 },
        { "MSG75", MSG75 },
        { "MSG76", MSG76 },
        { "MSG77", MSG77 },
        { "MSG78", MSG78 },
        { "MSG79", MSG79 },
        { "MSG80", MSG80 },
        { "MSG81", MSG81 },
        { "MSG82", MSG82 },
        { "MSG83", MSG83 },
        { "MSG84", MSG84 },
        { "MSG85", MSG85 },
        { "MSG86", MSG86 },
        { "MSG87", MSG87 },
        { "MSG88", MSG88 },
        { "MSG89", MSG89 },
        { "MSG90", MSG90 },
        { "MSG91", MSG91 },
        { "MSG92", MSG92 },
        { "MSG93", MSG93 },
        { "MSG94", MSG94 },
        { "MSG95", MSG95 },
        { "MSG96", MSG96 },
        { "MSG97", MSG97 },
        { "MSG98", MSG98 },
        { "MSG99", MSG99 },
        { "MSG100", MSG100 },
        { "MSG101", MSG101 },
        { "MSG102", MSG102 },
        { "MSG103", MSG103 },
        { "MSG104", MSG104 },
        { "MSG105", MSG105 },
        { "MSG106", MSG106 },
        { "MSG107", MSG107 },
        { "MSG108", MSG108 },
        { "MSG109", MSG109 },
        { "MSG110", MSG110 },
        { "MSG111", MSG111 },
        { "MSG112", MSG112 },
        { "MSG113", MSG113 },
        { "MSG114", MSG114 },
        { "MSG115", MSG115 },
        { "MSG116", MSG116 },
        { "MSG117", MSG117 },
        { "MSG118", MSG118 },
        { "MSG119", MSG119 },
        { "MSG120", MSG120 },
        { "MSG121", MSG121 },
        { "MSG122", MSG122 },
        { "MSG123", MSG123 },
        { "MSG124", MSG124 },
        { "MSG125", MSG125 },
        { "MSG126", MSG126 },
        { "MSG127", MSG127 },
        { "MSG128", MSG128 },
        { "MSG129", MSG129 },
        { "MSG130", MSG130 },
        { "MSG131", MSG131 },
        { "MSG132", MSG132 },
        { "MSG133", MSG133 },
        { "MSG134", MSG134 },
        { "MSG135", MSG135 },
        { "MSG136", MSG136 },
        { "MSG137", MSG137 },
        { "MSG138", MSG138 },
        { "MSG139", MSG139 },
        { "MSG140", MSG140 },
        { "MSG141", MSG141 },
        { "MSG142", MSG142 },
        { "MSG143", MSG143 },
        { "MSG144", MSG144 },
        { "MSG145", MSG145 },
        { "MSG146", MSG146 },
        { "MSG147", MSG147 },
        { "MSG148", MSG148 },
        { "MSG149", MSG149 },
        { "MSG150", MSG150 },
        { "MSG151", MSG151 },
        { "MSG152", MSG152 },
        { "MSG153", MSG153 },
        { "MSG154", MSG154 },
        { "MSG155", MSG155 },
        { "MSG156", MSG156 },
        { "MSG157", MSG157 },
        { "MSG158", MSG158 },
        { "MSG159", MSG159 },
        { "MSG160", MSG160 },
        { "MSG161", MSG161 },
        { "MSG162", MSG162 },
        { "MSG163", MSG163 },
        { "MSG164", MSG164 },
        { "MSG165", MSG165 },
        { "MSG166", MSG166 },
        { "MSG167", MSG167 },
        { "MSG168", MSG168 },
        { "MSG169", MSG169 },
        { "MSG170", MSG170 },
        { "MSG171", MSG171 },
        { "MSG172", MSG172 },
        { "MSG173", MSG173 },
        { "MSG174", MSG174 },
        { "MSG175", MSG175 },
        { "MSG176", MSG176 },
        { "MSG177", MSG177 },
        { "MSG178", MSG178 },
        { "MSG179", MSG179 },
        { "MSG180", MSG180 },
        { "MSG181", MSG181 },
        { "MSG182", MSG182 },
        { "MSG183", MSG183 },
        { "MSG184", MSG184 },
        { "MSG185", MSG185 },
        { "MSG186", MSG186 },
        { "MSG187", MSG187 },
        { "MSG188", MSG188 },
        { "MSG189", MSG189 },
        { "MSG190", MSG190 },
        { "MSG191", MSG191 },
        { "MSG192", MSG192 },
        { "MSG193", MSG193 },
        { "MSG194", MSG194 },
        { "MSG195", MSG195 },
        { "MSG196", MSG196 },
        { "MSG197", MSG197 },
        { "MSG198", MSG198 },
        { "MSG199", MSG199 },
        { "MSG200", MSG200 },
        { "MSG201", MSG201 },
        { "MSG202", MSG202 },
        { "MSG203", MSG203 },
        { "MSG204", MSG204 },
        { "MSG205", MSG205 },
        { "MSG206", MSG206 },
        { "MSG207", MSG207 },
        { "MSG208", MSG208 },
        { "MSG209", MSG209 },
        { "MSG210", MSG210 },
        { "MSG211", MSG211 },
        { "MSG212", MSG212 },
        { "MSG213", MSG213 },
        { "MSG214", MSG214 },
        { "MSG215", MSG215 },
        { "MSG216", MSG216 },
        { "MSG217", MSG217 },
        { "MSG218", MSG218 },
        { "MSG219", MSG219 },
        { "MSG220", MSG220 },
        { "MSG221", MSG221 },
        { "MSG222", MSG222 },
        { "MSG223", MSG223 },
        { "MSG224", MSG224 },
        { "MSG225", MSG225 },
        { "MSG226", MSG226 },
        { "MSG227", MSG227 },
        { "MSG228", MSG228 },
        { "MSG229", MSG229 },
        { "MSG230", MSG230 },
        { "MSG231", MSG231 },
        { "MSG232", MSG232 },
        { "MSG233", MSG233 },
        { "MSG234", MSG234 },
        { "MSG235", MSG235 },
        { "MSG236", MSG236 },
        { "MSG237", MSG237 },
        { "MSG238", MSG238 },
        { "MSG239", MSG239 },
        { "MSG240", MSG240 },
        { "MSG241", MSG241 },
        { "MSG242", MSG242 },
        { "MSG243", MSG243 },
        { "MSG244", MSG244 },
        { "MSG245", MSG245 },
        { "MSG246", MSG246 },
        { "MSG247", MSG247 },
        { "MSG248", MSG248 },
        { "MSG249", MSG249 },
        { "MSG250", MSG250 },
        { "MSG251", MSG251 },
        { "MSG252", MSG252 },
        { "MSG253", MSG253 },
        { "MSG254", MSG254 },
        { "MSG255", MSG255 },
        { "MSG256", MSG256 },
        { "MSG257", MSG257 },
        { "MSG258", MSG258 },
        { "MSG259", MSG259 },
        { "MSG260", MSG260 },
        { "MSG261", MSG261 },
        { "MSG262", MSG262 },
        { "MSG263", MSG263 },
        { "MSG264", MSG264 },
        { "MSG265", MSG265 },
        { "MSG266", MSG266 },
        { "MSG267", MSG267 },
        { "MSG268", MSG268 },
        { "MSG269", MSG269 },
        { "MSG270", MSG270 },
        { "MSG271", MSG271 },
        { "MSG272", MSG272 },
        { "MSG273", MSG273 },
        { "MSG274", MSG274 },
        { "MSG275", MSG275 },
        { "MSG276", MSG276 },
        { "MSG277", MSG277 },
        { "MSG278", MSG278 },
        { "MSG279", MSG279 },
        { "MSG280", MSG280 },
        { "MSG281", MSG281 },
        { "MSG282", MSG282 },
        { "MSG283", MSG283 },
        { "MSG284", MSG284 },
        { "MSG285", MSG285 },
        { "MSG286", MSG286 },
        { "MSG287", MSG287 },
        { "MSG288", MSG288 },
        { "MSG289", MSG289 },
        { "MSG290", MSG290 },
        { "MSG291", MSG291 },
        { "MSG292", MSG292 },
        { "MSG293", MSG293 },
        { "MSG294", MSG294 },
        { "MSG295", MSG295 },
        { "MSG296", MSG296 },
        { "MSG297", MSG297 },
        { "MSG298", MSG298 },
        { "MSG299", MSG299 },
        { "MSG300", MSG300 },
        { "MSG301", MSG301 },
        { "MSG302", MSG302 },
        { "MSG303", MSG303 },
        { "MSG304", MSG304 },
        { "MSG305", MSG305 },
        { "MSG306", MSG306 },
        { "MSG307", MSG307 },
        { "MSG308", MSG308 },
        { "MSG309", MSG309 },
        { "MSG310", MSG310 },
        { "MSG311", MSG311 },
        { "MSG312", MSG312 },
        { "MSG313", MSG313 },
        { "MSG314", MSG314 },
        { "MSG315", MSG315 },
        { "MSG316", MSG316 },
        { "MSG317", MSG317 },
        { "MSG318", MSG318 },
        { "MSG319", MSG319 },
    };

    /// <summary>Lookup a message by code (e.g. MSG01)</summary>
    public static SystemMessage? GetByCode(string code) => All.TryGetValue(code, out var msg) ? msg : null;
}

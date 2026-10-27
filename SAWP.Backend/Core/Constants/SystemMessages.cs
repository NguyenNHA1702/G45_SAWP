namespace Core.Constants;

/// <summary>
/// Standard System Messages defined in SRS (MSG01 to MSG50).
/// </summary>
public static class SystemMessages
{
    /// <summary>[Inline / Toast (Error)] Required or invalid input: Vui lòng kiểm tra và nhập đầy đủ thông tin hợp lệ.</summary>
    public static readonly SystemMessage MSG01 = new("MSG01", "Inline / Toast (Error)", "Required or invalid input", "Vui lòng kiểm tra và nhập đầy đủ thông tin hợp lệ.");
    public static readonly SystemMessage MSG01_RequiredOrInvalidInput = MSG01;

    /// <summary>[Toast (Error)] Invalid login/current password: Tên đăng nhập hoặc mật khẩu không chính xác.</summary>
    public static readonly SystemMessage MSG02 = new("MSG02", "Toast (Error)", "Invalid login/current password", "Tên đăng nhập hoặc mật khẩu không chính xác.");
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
    };

    /// <summary>Lookup a message by code (e.g. MSG01)</summary>
    public static SystemMessage? GetByCode(string code) => All.TryGetValue(code, out var msg) ? msg : null;
}


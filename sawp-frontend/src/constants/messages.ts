// Standard System Messages defined in SRS (MSG01 to MSG50)

export interface SystemMessage {
  code: string;
  messageType: string;
  context: string;
  content: string;
}

export const SYSTEM_MESSAGES = {
  MSG01: {
    code: "MSG01",
    messageType: "Inline / Toast (Error)",
    context: "Required or invalid input",
    content: "Vui lòng kiểm tra và nhập đầy đủ thông tin hợp lệ.",
  },
  MSG02: {
    code: "MSG02",
    messageType: "Toast (Error)",
    context: "Invalid login/current password",
    content: "Tên đăng nhập hoặc mật khẩu không chính xác.",
  },
  MSG03: {
    code: "MSG03",
    messageType: "Toast (Error)",
    context: "Inactive/deactivated account",
    content: "Tài khoản hiện không hoạt động. Vui lòng liên hệ quản trị viên.",
  },
  MSG04: {
    code: "MSG04",
    messageType: "Inline (Error)",
    context: "New password policy or confirmation fails",
    content: "Mật khẩu mới chưa đáp ứng yêu cầu hoặc xác nhận mật khẩu không khớp.",
  },
  MSG05: {
    code: "MSG05",
    messageType: "Toast (Success)",
    context: "Login successful",
    content: "Đăng nhập thành công.",
  },
  MSG06: {
    code: "MSG06",
    messageType: "Toast (Success)",
    context: "Logout successful",
    content: "Đã đăng xuất khỏi hệ thống.",
  },
  MSG07: {
    code: "MSG07",
    messageType: "Toast (Error)",
    context: "Session expired",
    content: "Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại.",
  },
  MSG08: {
    code: "MSG08",
    messageType: "Toast (Success)",
    context: "Record saved",
    content: "Lưu thông tin thành công.",
  },
  MSG09: {
    code: "MSG09",
    messageType: "Toast (Error)",
    context: "Operation cannot be completed in current state",
    content: "Không thể thực hiện thao tác với trạng thái hiện tại.",
  },
  MSG10: {
    code: "MSG10",
    messageType: "Toast (Error)",
    context: "Permission/data-scope denied",
    content: "Bạn không có quyền truy cập hoặc thực hiện thao tác này.",
  },
  MSG11: {
    code: "MSG11",
    messageType: "Toast (Error)",
    context: "Student not found",
    content: "Không tìm thấy hồ sơ học sinh.",
  },
  MSG12: {
    code: "MSG12",
    messageType: "Inline / Toast (Error)",
    context: "Duplicate student code",
    content: "Mã học sinh đã tồn tại trong hệ thống.",
  },
  MSG13: {
    code: "MSG13",
    messageType: "Toast (Success)",
    context: "Guardian link updated",
    content: "Đã cập nhật liên kết phụ huynh/người giám hộ.",
  },
  MSG14: {
    code: "MSG14",
    messageType: "Toast (Error)",
    context: "No active school year",
    content: "Chưa có năm học đang hoạt động.",
  },
  MSG15: {
    code: "MSG15",
    messageType: "Toast (Error)",
    context: "Class unavailable/capacity conflict",
    content: "Lớp được chọn không còn phù hợp hoặc đã đạt giới hạn sĩ số.",
  },
  MSG16: {
    code: "MSG16",
    messageType: "Toast (Success)",
    context: "Class assignment saved",
    content: "Đã cập nhật phân lớp cho học sinh.",
  },
  MSG17: {
    code: "MSG17",
    messageType: "Inline / Toast (Error)",
    context: "Schedule conflict",
    content: "Thời khóa biểu bị trùng khung giờ. Vui lòng kiểm tra lại.",
  },
  MSG18: {
    code: "MSG18",
    messageType: "Toast (Success)",
    context: "Attendance saved",
    content: "Đã lưu điểm danh.",
  },
  MSG19: {
    code: "MSG19",
    messageType: "Toast (Error)",
    context: "Attendance record locked",
    content: "Bản ghi điểm danh đã bị khóa. Vui lòng liên hệ Giáo vụ nếu cần điều chỉnh.",
  },
  MSG20: {
    code: "MSG20",
    messageType: "Toast (Error)",
    context: "Teacher not authorized for class",
    content: "Bạn không được phân công phụ trách lớp này.",
  },
  MSG21: {
    code: "MSG21",
    messageType: "Toast (Success)",
    context: "Leave request submitted",
    content: "Đã gửi đơn xin nghỉ/vắng học.",
  },
  MSG22: {
    code: "MSG22",
    messageType: "Notification / Toast (Success)",
    context: "Leave approved",
    content: "Đơn xin nghỉ/vắng học đã được chấp thuận.",
  },
  MSG23: {
    code: "MSG23",
    messageType: "Notification / Toast (Info)",
    context: "Leave rejected",
    content: "Đơn xin nghỉ/vắng học không được chấp thuận.",
  },
  MSG24: {
    code: "MSG24",
    messageType: "Inline / Toast (Error)",
    context: "Invalid/overlapping leave dates",
    content: "Khoảng thời gian xin nghỉ không hợp lệ hoặc đang trùng với yêu cầu khác.",
  },
  MSG25: {
    code: "MSG25",
    messageType: "Toast (Success)",
    context: "Lunch registration successful",
    content: "Đăng ký ăn trưa thành công.",
  },
  MSG26: {
    code: "MSG26",
    messageType: "Toast (Success)",
    context: "Lunch cancellation successful",
    content: "Đã hủy đăng ký ăn trưa.",
  },
  MSG27: {
    code: "MSG27",
    messageType: "Toast (Error)",
    context: "Lunch cutoff passed",
    content: "Đã quá thời hạn đăng ký hoặc hủy ăn trưa cho ngày này.",
  },
  MSG28: {
    code: "MSG28",
    messageType: "Toast (Error)",
    context: "Lunch service unavailable",
    content: "Dịch vụ ăn trưa không khả dụng cho ngày đã chọn.",
  },
  MSG29: {
    code: "MSG29",
    messageType: "Toast (Info)",
    context: "Duplicate lunch registration",
    content: "Học sinh đã có đăng ký ăn trưa cho ngày này.",
  },
  MSG30: {
    code: "MSG30",
    messageType: "Toast (Success)",
    context: "Administrative request submitted",
    content: "Đã gửi yêu cầu hành chính.",
  },
  MSG31: {
    code: "MSG31",
    messageType: "Notification / Toast (Success)",
    context: "Request approved",
    content: "Yêu cầu đã được phê duyệt.",
  },
  MSG32: {
    code: "MSG32",
    messageType: "Notification / Toast (Info)",
    context: "Request rejected",
    content: "Yêu cầu không được phê duyệt.",
  },
  MSG33: {
    code: "MSG33",
    messageType: "Notification / Toast (Success)",
    context: "Request completed",
    content: "Yêu cầu đã được xử lý hoàn tất.",
  },
  MSG34: {
    code: "MSG34",
    messageType: "Toast (Error)",
    context: "Request cannot be cancelled",
    content: "Yêu cầu không thể hủy ở trạng thái hiện tại.",
  },
  MSG35: {
    code: "MSG35",
    messageType: "Notification / Toast (Success)",
    context: "Class transfer applied",
    content: "Đã cập nhật chuyển lớp cho học sinh.",
  },
  MSG36: {
    code: "MSG36",
    messageType: "Notification / Toast (Success)",
    context: "School document issued",
    content: "Tài liệu/giấy xác nhận đã được xử lý và phát hành.",
  },
  MSG37: {
    code: "MSG37",
    messageType: "Toast (Success)",
    context: "Notifications marked read",
    content: "Đã đánh dấu thông báo là đã đọc.",
  },
  MSG38: {
    code: "MSG38",
    messageType: "Toast (Success)",
    context: "User account created",
    content: "Tạo tài khoản người dùng thành công.",
  },
  MSG39: {
    code: "MSG39",
    messageType: "Toast (Success)",
    context: "User account deactivated",
    content: "Đã vô hiệu hóa tài khoản người dùng.",
  },
  MSG40: {
    code: "MSG40",
    messageType: "Toast (Success)",
    context: "Role/permission updated",
    content: "Đã cập nhật vai trò và quyền truy cập.",
  },
  MSG41: {
    code: "MSG41",
    messageType: "Inline / Toast (Error)",
    context: "Invalid attachment/import file",
    content: "Tệp không hợp lệ, không đúng định dạng hoặc vượt quá dung lượng cho phép.",
  },
  MSG42: {
    code: "MSG42",
    messageType: "Toast (Error)",
    context: "Audit-log access denied",
    content: "Bạn không có quyền xem nhật ký hệ thống.",
  },
  MSG43: {
    code: "MSG43",
    messageType: "Empty-state message",
    context: "No matching data",
    content: "Không có dữ liệu phù hợp để hiển thị.",
  },
  MSG44: {
    code: "MSG44",
    messageType: "Toast (Success)",
    context: "Configuration saved",
    content: "Đã lưu cấu hình hệ thống.",
  },
  MSG45: {
    code: "MSG45",
    messageType: "Confirmation Dialog",
    context: "Confirm sensitive action",
    content: "Bạn có chắc chắn muốn thực hiện thao tác này?",
  },
  MSG46: {
    code: "MSG46",
    messageType: "Toast (Success)",
    context: "Password changed",
    content: "Đổi mật khẩu thành công.",
  },
  MSG47: {
    code: "MSG47",
    messageType: "Toast (Success)",
    context: "Profile/contact updated",
    content: "Cập nhật thông tin thành công.",
  },
  MSG48: {
    code: "MSG48",
    messageType: "Toast / Summary",
    context: "Import completed",
    content: "Nhập dữ liệu hoàn tất. Vui lòng kiểm tra kết quả các bản ghi thành công và lỗi.",
  },
  MSG49: {
    code: "MSG49",
    messageType: "Toast / Summary (Error)",
    context: "Import has no/partial valid rows",
    content: "Một số dữ liệu không thể nhập. Vui lòng kiểm tra danh sách lỗi.",
  },
  MSG50: {
    code: "MSG50",
    messageType: "Toast (Error)",
    context: "Unexpected system error",
    content: "Đã xảy ra lỗi hệ thống. Vui lòng thử lại sau.",
  },
} as const;

export type SystemMessageCode = keyof typeof SYSTEM_MESSAGES;

export const getSystemMessage = (code: string): SystemMessage | undefined => {
  return (SYSTEM_MESSAGES as Record<string, SystemMessage>)[code];
};

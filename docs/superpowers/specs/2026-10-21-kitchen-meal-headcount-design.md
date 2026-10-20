# Design Spec: Màn hình Vận hành Bán trú & Bếp ăn (Services Staff Live Meal Operations)

## 1. Mục tiêu và Bối cảnh
- **Bối cảnh**: Trong Workflow 1.2.3 (Quy trình xin nghỉ học và đồng bộ điểm danh - bán trú), mắt xích quan trọng nhất đối với tài chính và phục vụ trường học là bộ phận Bán trú & Bếp ăn (Tân Lập A: WFL-09 & WFL-10).
- **Mục tiêu**:
  - Cung cấp cho Nhân viên Bán trú (Services Staff / Bếp) màn hình giám sát số lượng suất ăn theo thời gian thực (Live Board).
  - Tự động trừ các suất ăn của học sinh có đơn xin nghỉ đã được GVCN duyệt trước mốc thời gian chốt (08h15 sáng) mà không yêu cầu phụ huynh/kế toán phải thao tác thủ công nhiều lần.
  - Cung cấp tính năng Chốt suất ăn hàng ngày lúc 08h15, xuất Phiếu bàn giao Nhà bếp, và ghi nhận xử lý các trường hợp phát sinh ngoại lệ (học sinh đi muộn hoặc học sinh có đơn nghỉ nhưng thực tế vẫn đi học).

---

## 2. Kiến trúc & Dữ liệu (Backend)

### 2.1. DTOs
- `DailyMealBoardDto`:
  - `Date`: DateTime (Ngày khảo sát).
  - `IsLocked`: bool (Đã chốt suất ăn lúc 08h15 chưa).
  - `LockedAt`: DateTime? (Thời điểm chốt).
  - `CutoffTime`: string ("08:15").
  - `TotalInitialRegistered`: int (Tổng suất ăn đăng ký tháng đầu ngày).
  - `TotalAutoCancelled`: int (Tổng suất tự động hủy trước 08h15 do đơn nghỉ học).
  - `TotalActiveToCook`: int (Tổng suất thực tế bếp cần chuẩn bị).
  - `TotalAnomalies`: int (Số suất phát sinh bất thường / học sinh đi học lại).
  - `ClassBreakdowns`: List<ClassMealSummaryDto>
    - `ClassId`: int
    - `ClassName`: string
    - `InitialCount`: int
    - `CancelledCount`: int
    - `ActualCount`: int
  - `MealChangeLogs`: List<MealAuditLogDto>
    - `StudentId`: int
    - `StudentCode`: string
    - `StudentName`: string
    - `ClassName`: string
    - `ChangeType`: string ("AutoCancelled" | "Restored" | "LateAdded")
    - `Reason`: string
    - `Timestamp`: DateTime
    - `IsBeforeCutoff`: bool

- `LockMealDayRequestDto`:
  - `Date`: DateTime
  - `Notes`: string?

### 2.2. Service Interfaces (`ILunchService`)
- Thêm các methods:
  - `Task<ApiResponse<DailyMealBoardDto>> GetDailyMealBoardAsync(DateTime date, CancellationToken cancellationToken = default);`
  - `Task<ApiResponse<bool>> LockDailyMealAsync(DateTime date, string? notes, CancellationToken cancellationToken = default);`

### 2.3. Controller Endpoints (`LunchController`)
- `GET /api/lunch/live-board?date=YYYY-MM-DD`: Lấy dữ liệu bảng vận hành bán trú trực tiếp.
- `POST /api/lunch/lock-day`: Chốt số lượng suất ăn trong ngày gửi nhà bếp.
- `POST /api/lunch/cancel-for-leave`: Endpoint hủy suất nếu cần kích hoạt độc lập.
- `POST /api/lunch/restore-meal`: Endpoint khôi phục suất nếu học sinh đi học lại.

---

## 3. Giao diện Người dùng (Frontend)

### 3.1. Trang `MealHeadcountPage.tsx` (Route `/service-staff/meal-headcount`)
- **Header**: Tiêu đề trang, bộ chọn ngày (Date Picker), nhãn trạng thái Ngày (🟢 Đang cập nhật trực tiếp / 🔒 Đã chốt số liệu).
- **Bộ 4 thẻ chỉ số KPI (Live Metric Cards)**:
  1. 🥗 **Tổng suất đăng ký gốc**: Tổng số học sinh có đăng ký bán trú trong ngày.
  2. ❌ **Suất hủy tự động (trước 08h15)**: Số suất đã giảm từ các đơn nghỉ học được duyệt.
  3. 🍲 **Suất thực tế cần nấu**: Số lượng khay ăn nhà bếp cần chuẩn bị.
  4. ⚠️ **Phát sinh / Bất thường**: Số học sinh có đơn xin nghỉ nhưng GVCN điểm danh "Có mặt".
- **Khu vực Thao tác Nghiệp vụ**:
  - Nút **"Chốt số liệu gửi Nhà bếp (08h15)"**: Bấm để chốt số lượng ngày, hiển thị xác nhận và chuyển trạng thái sang Đã chốt.
  - Nút **"Xuất phiếu bàn giao Bếp"**: Mở modal / in định dạng A4 sạch đẹp để ký nhận thực phẩm và giao khay cơm.
- **Bảng Chi tiết Phân bổ theo Lớp (Class Distribution Table)**:
  - Cột: Lớp, Khối, Đăng ký ban đầu, Đã hủy, Thực ăn, Tỷ lệ ăn.
- **Nhật ký Biến động Suất ăn Thời gian thực (Live Audit Log Table)**:
  - Cột: Thời gian, Học sinh, Lớp, Loại thay đổi (badge màu), Lý do / Nguồn gốc, Tình trạng chốt.

### 3.2. Cập nhật Navigation (`MainLayout.tsx`)
- Đảm bảo Role `ServiceStaff` hoặc giáo viên/admin có thể truy cập trang `/service-staff/meal-headcount` từ Sidebar.

---

## 4. Xử lý Lỗi & Kiểm thử (Verification Plan)
- **Kiểm thử Luồng Đồng bộ Toàn diện**:
  1. Mở trang Bán trú: Quan sát số suất ban đầu của bé Trần Đức Anh (Lớp 1A) là 1 suất.
  2. Phụ huynh nộp đơn xin nghỉ học cho Trần Đức Anh.
  3. GVCN duyệt đơn xin nghỉ trước 08h15.
  4. F5 hoặc tải lại trang Bán trú: Số suất hủy tự động tăng thêm 1, số suất thực tế cần nấu giảm 1, dòng nhật ký hiển thị "Trần Đức Anh - Hủy do đơn nghỉ học".
  5. Bấm nút "Chốt số liệu gửi Nhà bếp": Bảng chuyển sang trạng thái Đã chốt thành công.

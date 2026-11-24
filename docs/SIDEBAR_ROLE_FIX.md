# Khắc phục lỗi lẫn lộn Role trên Sidebar (`MainLayout.tsx` & `App.tsx`)

## 1. Các lỗi đang tồn tại

### 1.1. `Admin` bị hiển thị toàn bộ menu của tất cả các role khác (Nghiêm trọng)
- **Vị trí:**
  - `sawp-frontend/src/layouts/MainLayout.tsx` (dòng 325–329, hàm `hasRoleMatch`)
  - `sawp-frontend/src/App.tsx` (dòng 85–87, component `RequireRole`)
- **Nguyên nhân:**
  ```ts
  const hasRoleMatch = (rolesList?: string[]) =>
    !rolesList ||
    rolesList.some((r) =>
      userRoles.some((ur) => typeof ur === 'string' && (ur.toLowerCase() === r.toLowerCase() || ur.toLowerCase() === 'admin'))
    );
  ```
  Điều kiện `|| ur.toLowerCase() === 'admin'` khiến tài khoản `Admin` luôn khớp mọi `roles` trong `NAV_ITEMS`. Kết quả là khi đăng nhập bằng `Admin`, sidebar hiện cả menu của `Parent` (*Theo dõi con & Bán trú*, *Gửi đơn & Giấy tờ*), `Teacher` (*Lớp chủ nhiệm*, *Bán trú lớp học*, *Sổ liên lạc lớp*), `ServiceStaff` (*Báo cáo suất ăn Bếp*), `OfficeStaff` và `SchoolBoard`.
- **Hệ quả:** Giao diện sidebar của `Admin` bị rối, lẫn lộn nghiệp vụ, và bấm vào các trang của `Parent`/`Teacher` sẽ thiếu dữ liệu ngữ cảnh (ví dụ `ParentChildContext` không có con).

---

### 1.2. Lệch quyền giữa Sidebar (`MainLayout.tsx`), Route Guard (`App.tsx`) và Backend
1. **Mục `Lịch năm học & Học bù` (`office-calendar` -> `/admin/config`):**
   - `MainLayout.tsx` (dòng 125–130): cho `roles: ['OfficeStaff']`.
   - `App.tsx` (dòng 306–312): `<RequireRole roles={['Admin']}>` -> **chặn `OfficeStaff`**, báo *"Bạn không có quyền truy cập trang này."* và đẩy về `/dashboard`.
   - `AdminController.cs` (dòng 109–129): `PUT /api/admin/calendar/day` cho phép `Admin,OfficeStaff`, nhưng `GET/PUT /api/admin/lunch-cutoff` chỉ cho phép `Admin`.
   - **Cần sửa:** Cho phép `OfficeStaff` vào `/admin/config` (hoặc tách route `/office/calendar`), đồng thời trong `SystemConfigPage.tsx` ẩn tab `lunch` (Cấu hình bán trú / giờ chốt suất) đối với `OfficeStaff`.

2. **Mục `Sổ liên lạc lớp` (`teacher-contact-book` -> `/teacher/contact-book`):**
   - `MainLayout.tsx` (dòng 172): `roles: ['Teacher']`.
   - `App.tsx` (dòng 164): `roles={['Teacher', 'SchoolBoard', 'Admin']}`.
   - `ContactBookController.cs` (dòng 42): `[Authorize(Roles = "Teacher,OfficeStaff,Admin")]`.
   - **Cần sửa:** Thống nhất `Teacher` là role chính trên sidebar và route guard (hoặc đồng bộ 3 nơi nếu cho `OfficeStaff` xem/đăng).

3. **Nhóm `Đơn từ & Hành chính` (`administrative-requests-group`) trộn 3 role và nhét nhầm `Nhật ký hệ thống`:**
   - `MainLayout.tsx` (dòng 218–252) gộp `Parent` (*Gửi đơn & Giấy tờ*), `OfficeStaff` (*Xử lý đơn hành chính*, *Nhật ký hệ thống*) và `SchoolBoard` (*Phê duyệt đơn cấp trường*, *Nhật ký hệ thống*) vào chung một nhóm.
   - `Nhật ký hệ thống` (`/system/audit-logs`) là chức năng giám sát hệ thống, nhưng với `OfficeStaff`/`SchoolBoard` lại nằm trong nhóm *Đơn từ & Hành chính*, còn với `Admin` lại nằm trong *Quản trị hệ thống*.

4. **Nhóm `Điểm danh & Lớp học` (`teacher-group`) dùng chung cho `Teacher`, `OfficeStaff`, `SchoolBoard`:**
   - Với `OfficeStaff` và `SchoolBoard`, các mục chủ nhiệm (`Lớp chủ nhiệm`, `Bán trú lớp học`, `Sổ liên lạc lớp`) bị ẩn, nhưng `Điểm danh & Tình trạng chuyên cần` và `Duyệt đơn xin nghỉ` vẫn nằm dưới `teacher-group`. Nên tách rõ nhóm theo vai trò để tránh nhầm lẫn khi bảo trì.

5. **Footer chỉ hiển thị `userRoles[0]`:**
   - `MainLayout.tsx` (dòng 525): `{userRoles[0] ?? 'User'}`. Nếu tài khoản có nhiều role, sidebar chỉ hiện role đầu tiên, dễ gây hiểu nhầm khi kiểm thử phân quyền.

---

## 2. Ma trận Sidebar chuẩn theo từng Role

Mỗi role chỉ nên thấy đúng nhóm chức năng thuộc nghiệp vụ của mình (trừ `Tổng quan` `/dashboard` và `Hồ sơ cá nhân` `/profile` dùng chung):

| Role | Các nhóm & mục trên Sidebar |
| :--- | :--- |
| **Admin** | **Quản trị hệ thống**: Quản lý tài khoản (`/admin/users`), Duyệt yêu cầu cấp tài khoản (`/admin/account-requests`), Cấu hình hệ thống (`/admin/config`), Nhật ký hệ thống (`/system/audit-logs`) |
| **SchoolBoard** | **Ban giám hiệu**: Lớp học & Xếp lớp (`/classes`), Phê duyệt đơn cấp trường (`/board/requests`), Duyệt đơn xin nghỉ (`/teacher/leave-requests`), Theo dõi chuyên cần (`/teacher/attendance`), Báo cáo suất ăn Bếp (`/service/headcount`), Nhật ký hệ thống (`/system/audit-logs`) |
| **OfficeStaff** | **Giáo vụ & Văn phòng**: Hồ sơ học sinh (`/students`), Lớp học & Xếp lớp (`/classes`), Yêu cầu cấp tài khoản (`/office/account-requests`), Xử lý đơn hành chính (`/office/requests`), Lịch năm học & Học bù (`/admin/config`), Điểm danh & Chuyên cần (`/teacher/attendance`), Báo cáo suất ăn Bếp (`/service/headcount`), Nhật ký hệ thống (`/system/audit-logs`) |
| **Teacher** | **Giáo viên chủ nhiệm**: Lớp chủ nhiệm (`/teacher/roster`), Điểm danh & Chuyên cần (`/teacher/attendance`), Duyệt đơn xin nghỉ (`/teacher/leave-requests`), Bán trú lớp học (`/teacher/boarding`), Sổ liên lạc lớp (`/teacher/contact-book`) |
| **ServiceStaff** | **Bếp ăn bán trú**: Tổng hợp & Chốt suất (`/service/headcount?tab=summary`), Danh sách nhận suất (`/service/headcount?tab=students`), Nhật ký biến động (`/service/headcount?tab=logs`), Báo cáo thống kê (`/service/headcount?tab=report`) |
| **Parent** | **Cổng phụ huynh**: Hồ sơ các con (`/parent/children`), Sổ liên lạc điện tử (`/parent/contact-book`), Đăng ký bán trú (`/parent/lunch`), Đơn xin nghỉ học (`/parent/leave-requests`), Chuyên cần của con (`/parent/attendance`), Gửi đơn & Giấy tờ (`/parent/requests`) |

---

## 3. Các bước sửa code chi tiết

### Bước 1: Sửa `hasRoleMatch` trong `sawp-frontend/src/layouts/MainLayout.tsx`
Bỏ đặc quyền tự động `|| ur.toLowerCase() === 'admin'` để `Admin` không bị dính menu của các role khác:

```ts
const hasRoleMatch = (rolesList?: string[]) =>
  !rolesList ||
  rolesList.some((r) =>
    userRoles.some((ur) => typeof ur === 'string' && ur.toLowerCase() === r.toLowerCase())
  );
```

### Bước 2: Tách `NAV_ITEMS` theo đúng nghiệp vụ từng Role trong `MainLayout.tsx`
- Tách nhóm **Cổng phụ huynh** (`roles: ['Parent']`) chứa đầy đủ 6 mục của phụ huynh (đưa `Gửi đơn & Giấy tờ` `/parent/requests` vào đây thay vì để chung với `OfficeStaff`/`SchoolBoard`).
- Tách nhóm **Giáo viên chủ nhiệm** (`roles: ['Teacher']`) cho riêng `Teacher`.
- Tách nhóm **Giáo vụ & Hành chính** (`roles: ['OfficeStaff']`) và **Ban giám hiệu** (`roles: ['SchoolBoard']`), hoặc dùng `roles` tường minh trên từng item/group sao cho không bị rò rỉ giữa `Parent`, `Teacher`, `OfficeStaff`, `SchoolBoard`, `ServiceStaff`, `Admin`.
- Đưa **Nhật ký hệ thống** (`/system/audit-logs`) vào nhóm quản trị/giám sát của `Admin`, `SchoolBoard`, `OfficeStaff` thay vì đặt trong nhóm *Đơn từ & Hành chính*.
- Ở footer sidebar (dòng 523–527), hiển thị tất cả role của user:
  ```tsx
  <div className="user-role-badge" style={{ display: 'flex', flexWrap: 'wrap', gap: 4 }}>
    {(userRoles.length > 0 ? userRoles : ['User']).map((role) => (
      <span key={role} className="badge badge--mint" style={{ fontSize: '0.7rem', padding: '1px 6px' }}>
        {role}
      </span>
    ))}
  </div>
  ```

### Bước 3: Đồng bộ `RequireRole` trong `sawp-frontend/src/App.tsx`
1. **Bỏ `|| ur.toLowerCase() === 'admin'` trong `RequireRole` (dòng 85–87)** nếu muốn `Admin` không truy cập nhầm trang nghiệp vụ của `Parent`/`Teacher`:
   ```ts
   const hasRole = roles.some((r) =>
     userRoles.some((ur) => typeof ur === 'string' && ur.toLowerCase() === r.toLowerCase())
   );
   ```
   *(Lưu ý: giữ `'Admin'` trong mảng `roles={[...]}` của các route mà `Admin` thực sự được phép mở để debug/quản trị như `/system/audit-logs`, `/admin/*`, v.v., và bỏ `'Admin'` khỏi `/parent/*`, `/teacher/roster`, `/teacher/boarding`).*
2. **Mở route `/admin/config` cho `OfficeStaff` (dòng 306–312):**
   ```tsx
   <Route
     path="/admin/config"
     element={
       <RequireRole roles={['Admin', 'OfficeStaff']}>
         <SystemConfigPage />
       </RequireRole>
     }
   />
   ```
3. **Đồng bộ route `/teacher/contact-book` (dòng 161–168):**
   Chỉ giữ các role khớp với sidebar và backend (ví dụ `roles={['Teacher']}` hoặc `roles={['Teacher', 'OfficeStaff']}` tùy yêu cầu).

### Bước 4: Giới hạn tab trong `sawp-frontend/src/pages/AdminPortal/SystemConfigPage.tsx`
Vì `AdminController.cs` chỉ cho phép `Admin` gọi `GET/PUT /api/admin/lunch-cutoff`, cần kiểm tra role `Admin` trong `SystemConfigPage.tsx` để:
- `OfficeStaff` chỉ thấy và thao tác trên tab **Lịch năm học & Học bù** (`calendar`).
- `Admin` thấy cả 2 tab (`calendar` và `lunch`).

---

## 4. Checklist kiểm thử sau khi sửa

1. Đăng nhập lần lượt 6 tài khoản đơn role (`Admin`, `SchoolBoard`, `OfficeStaff`, `Teacher`, `ServiceStaff`, `Parent`):
   - `Admin`: chỉ thấy *Tổng quan* + *Quản trị hệ thống* (không còn thấy menu *Phụ huynh*, *Giáo viên*, *Bếp*).
   - `OfficeStaff`: bấm vào *Lịch năm học & Học bù* (`/admin/config`) mở được trang cấu hình lịch mà không bị đá về `/dashboard` và không bị lỗi `403` từ API `lunch-cutoff`.
   - `Parent`: thấy đầy đủ 6 mục trong *Theo dõi con & Bán trú* (bao gồm *Gửi đơn & Giấy tờ*).
   - `Teacher`: chỉ thấy các mục của giáo viên chủ nhiệm.
   - `ServiceStaff`: chỉ thấy *Tổng quan* + *Báo cáo suất ăn Bếp*.
   - `SchoolBoard`: chỉ thấy các mục giám sát/phê duyệt cấp trường.
2. Đăng nhập tài khoản **đa role** (ví dụ `Teacher` + `Parent`):
   - Footer hiển thị đủ các badge role.
   - Sidebar hiển thị đúng hợp nhất của cả 2 role mà không bị thừa menu của role khác.

# Kitchen Meal Headcount Operations Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Implement real-time Kitchen Meal Headcount Operations Dashboard linking WFL 1.2.3 leave approvals to daily kitchen headcount, 08h15 cutoff locking, and meal distribution reports.

**Architecture:** ASP.NET Core 8 Backend (DTOs, Service, Controller) + React Vite Frontend (API client, KPI cards, class breakdown table, live audit log, print handover sheet).

**Tech Stack:** C#, ASP.NET Core 8, EF Core, SQL Server LocalDB, React 18, TypeScript, Vite, Lucide Icons, Vanilla CSS design system tokens.

## Global Constraints
- Target Framework: .NET 8 for SAWP.Backend.
- Database: SQL Server LocalDB `(localdb)\mssqllocaldb`.
- Frontend: Vite + React 18, Vanilla CSS design tokens (no tailwind).
- Cutoff time rule: 08:15 AM (per-day cutoff for meal cancellation/restoration).

---

### Task 1: Backend Lunch DTOs and Data Models

**Files:**
- Modify: `SAWP.Backend/Core/Entities/LunchServiceDay.cs`
- Modify: `SAWP.Backend/Core/DTOs/Lunch/LunchDtos.cs`

**Interfaces:**
- Produces: `DailyMealBoardDto`, `ClassMealSummaryDto`, `MealAuditLogDto`, `LockMealDayRequestDto`

- [ ] **Step 1: Update `LunchServiceDay.cs` with lock fields**
```csharp
namespace Core.Entities;

public class LunchServiceDay
{
    public int Id { get; set; }
    public DateTime Date { get; set; }
    public bool IsServing { get; set; } = true;
    public TimeSpan CutoffTime { get; set; } = new TimeSpan(8, 15, 0);
    public bool IsLocked { get; set; } = false;
    public DateTime? LockedAt { get; set; }
    public string? Remarks { get; set; }
}
```

- [ ] **Step 2: Update `LunchDtos.cs` with comprehensive board DTOs**
```csharp
using Core.Enums;

namespace Core.DTOs;

public class LunchRegistrationDto
{
    public int StudentId { get; set; }
    public DateTime Date { get; set; }
    public MealType MealType { get; set; }
    public bool IsActive { get; set; }
}

public class DailyMealCountReportDto
{
    public DateTime Date { get; set; }
    public int TotalStandardMeals { get; set; }
    public int TotalVegetarianMeals { get; set; }
    public int TotalSpecialDietMeals { get; set; }
    public int GrandTotal { get; set; }
}

public class ClassMealSummaryDto
{
    public int ClassId { get; set; }
    public string ClassName { get; set; } = string.Empty;
    public int Grade { get; set; }
    public int InitialRegistered { get; set; }
    public int CancelledCount { get; set; }
    public int ActualCount { get; set; }
}

public class MealAuditLogDto
{
    public int StudentId { get; set; }
    public string StudentCode { get; set; } = string.Empty;
    public string StudentName { get; set; } = string.Empty;
    public string ClassName { get; set; } = string.Empty;
    public string ChangeType { get; set; } = string.Empty; // "AutoCancelled" | "Restored" | "LateAdded"
    public string Reason { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public bool IsBeforeCutoff { get; set; }
}

public class DailyMealBoardDto
{
    public DateTime Date { get; set; }
    public bool IsLocked { get; set; }
    public DateTime? LockedAt { get; set; }
    public string CutoffTime { get; set; } = "08:15";
    public int TotalInitialRegistered { get; set; }
    public int TotalAutoCancelled { get; set; }
    public int TotalActiveToCook { get; set; }
    public int TotalAnomalies { get; set; }
    public List<ClassMealSummaryDto> ClassBreakdowns { get; set; } = new();
    public List<MealAuditLogDto> MealChangeLogs { get; set; } = new();
}

public class LockMealDayRequestDto
{
    public DateTime Date { get; set; }
    public string? Notes { get; set; }
}
```

- [ ] **Step 3: Build backend to verify types compile**
Run: `dotnet build SAWP.Backend`
Expected: Build succeeded with 0 errors.

- [ ] **Step 4: Commit Task 1**
```bash
git add SAWP.Backend/Core/Entities/LunchServiceDay.cs SAWP.Backend/Core/DTOs/Lunch/LunchDtos.cs
git commit -m "feat(backend): add DailyMealBoard DTOs and LunchServiceDay lock properties"
```

---

### Task 2: Backend LunchService & LunchController Implementation

**Files:**
- Modify: `SAWP.Backend/Services/Interfaces/ILunchService.cs`
- Modify: `SAWP.Backend/Services/Implementations/LunchService.cs`
- Modify: `SAWP.Backend/API/Controllers/LunchController.cs`

**Interfaces:**
- Consumes: `DailyMealBoardDto`, `LockMealDayRequestDto`, `IUnitOfWork`
- Produces: `GET /api/lunch/live-board`, `POST /api/lunch/lock-day`, `POST /api/lunch/cancel-for-leave`, `POST /api/lunch/restore-meal`

- [ ] **Step 1: Add interface methods in `ILunchService.cs`**
```csharp
Task<ApiResponse<DailyMealBoardDto>> GetDailyMealBoardAsync(DateTime date, CancellationToken cancellationToken = default);
Task<ApiResponse<bool>> LockDailyMealAsync(LockMealDayRequestDto request, CancellationToken cancellationToken = default);
```

- [ ] **Step 2: Implement logic in `LunchService.cs`**
Query `LunchRegistration`, join with `Student`, `ClassAssignment`, and `Class`. Compute initial count, cancelled count (where `!IsActive`), and actual count. Populate `ClassBreakdowns` and parse `Remarks` into `MealAuditLogDto`. Support `LockDailyMealAsync` via `LunchServiceDay`.

- [ ] **Step 3: Implement endpoints in `LunchController.cs`**
```csharp
[HttpGet("live-board")]
public async Task<IActionResult> GetLiveBoard([FromQuery] DateTime? date, CancellationToken cancellationToken)
{
    var targetDate = date ?? DateTime.Today;
    var result = await _lunchService.GetDailyMealBoardAsync(targetDate, cancellationToken);
    return Ok(result);
}

[HttpPost("lock-day")]
public async Task<IActionResult> LockDay([FromBody] LockMealDayRequestDto request, CancellationToken cancellationToken)
{
    var result = await _lunchService.LockDailyMealAsync(request, cancellationToken);
    return Ok(result);
}
```

- [ ] **Step 4: Build backend and verify endpoints**
Run: `dotnet build SAWP.Backend`
Expected: Build succeeded with 0 errors.

- [ ] **Step 5: Commit Task 2**
```bash
git add SAWP.Backend/Services/Interfaces/ILunchService.cs SAWP.Backend/Services/Implementations/LunchService.cs SAWP.Backend/API/Controllers/LunchController.cs
git commit -m "feat(backend): implement live meal board and lock-day API endpoints"
```

---

### Task 3: Frontend Lunch API Service

**Files:**
- Create: `sawp-frontend/src/services/implementations/lunchApi.ts`

**Interfaces:**
- Produces: `lunchApi.getDailyMealBoard(date: string)`, `lunchApi.lockDailyMeal(date: string, notes?: string)`, `lunchApi.restoreMeal(studentId: number, date: string, reason: string)`

- [ ] **Step 1: Create `lunchApi.ts`**
Export TypeScript interfaces matching backend DTOs and Axios API calls.

- [ ] **Step 2: Verify frontend builds**
Run: `cd sawp-frontend && npm run build`
Expected: Vite build succeeds with 0 errors.

- [ ] **Step 3: Commit Task 3**
```bash
git add sawp-frontend/src/services/implementations/lunchApi.ts
git commit -m "feat(frontend): add lunchApi service for meal headcount operations"
```

---

### Task 4: Frontend Meal Headcount Page (`MealHeadcountPage.tsx`)

**Files:**
- Modify: `sawp-frontend/src/pages/ServiceStaffPortal/MealHeadcountPage.tsx`

**Features:**
- Header with Date Picker & Live / Locked Status Badge.
- 4 Live KPI Metric Cards:
  1. Tổng đăng ký gốc (Blue / Primary)
  2. Suất hủy trước 08h15 (Red / Amber)
  3. Suất thực tế cần nấu (Green / Emerald)
  4. Suất phát sinh bất thường (Purple / Cyan)
- Two action buttons:
  - "Chốt số liệu gửi Nhà Bếp (Mốc 08h15)" with confirm modal.
  - "In Phiếu Giao Nhận Suất Ăn" triggering clean print view / dialog.
- Class Breakdown Table with columns: Lớp, Khối, Đăng ký, Đã hủy, Thực ăn, Tỷ lệ chuẩn bị.
- Real-time Audit Log Table showing every student with cancelled/restored meals, timestamp, and leave reason.

- [ ] **Step 1: Write `MealHeadcountPage.tsx`**
- [ ] **Step 2: Build frontend to verify component compiles**
Run: `cd sawp-frontend && npm run build`
Expected: Build succeeded with 0 errors.

- [ ] **Step 3: Commit Task 4**
```bash
git add sawp-frontend/src/pages/ServiceStaffPortal/MealHeadcountPage.tsx
git commit -m "feat(frontend): complete MealHeadcountPage with live KPI board, class breakdown and audit logs"
```

---

### Task 5: Routing, Sidebar Navigation & Verification

**Files:**
- Modify: `sawp-frontend/src/layouts/MainLayout.tsx`

- [ ] **Step 1: Ensure Bán trú & Bếp ăn link is accessible in sidebar**
Verify `/service-staff/meal-headcount` has a clear, prominent menu item under "Bán trú & Bếp ăn".
- [ ] **Step 2: Verify complete build & run end-to-end integration test**
Test flow:
1. Verify `HS001` (Trần Đức Anh) has a lunch registration.
2. Approve leave request for `HS001`.
3. Check `MealHeadcountPage`: `HS001` is shown in Auto-Cancelled count and Audit log.
4. Click "Chốt số liệu gửi Nhà bếp": status changes to locked.
- [ ] **Step 3: Commit Task 5**
```bash
git add sawp-frontend/src/layouts/MainLayout.tsx
git commit -m "feat: link MealHeadcountPage in sidebar navigation"
```

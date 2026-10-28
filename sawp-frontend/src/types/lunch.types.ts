export type MealType = 'StandardLunch' | 'VegetarianLunch' | 'SpecialDiet' | 1 | 2 | 3;

export type LunchCancellationSource = 'Parent' | 'Leave' | 'Attendance' | 'Staff' | 1 | 2 | 3 | 4;

export interface ClassHeadcount {
  classId: number;
  className: string;
  meals: number;
  deductedByAttendance: number;
  attendanceSubmitted: boolean;
}

export interface MealHeadcount {
  date: string;
  isServiceDay: boolean;
  isFinalized: boolean;
  finalizeTime: string; // giờ chốt suất ăn, vd "08:15:00"
  totalMeals: number;
  standardLunch: number;
  vegetarianLunch: number;
  specialDiet: number;
  deductedByAttendance: number;
  deductedByLeave: number;
  classes: ClassHeadcount[];
}

export interface ClassMealSummary {
  classId: number;
  className: string;
  grade: number;
  initialRegistered: number;
  cancelledCount: number;
  actualCount: number;
}

export interface MealAuditLog {
  studentId: number;
  studentCode: string;
  studentName: string;
  className: string;
  changeType: 'AutoCancelled' | 'ParentCancelled' | 'Restored' | 'LateAdded' | string;
  reason: string;
  timestamp: string;
  isBeforeCutoff: boolean;
}

export interface StudentMealOperationItem {
  registrationId: number;
  studentId: number;
  studentCode: string;
  studentName: string;
  classId: number;
  className: string;
  grade: number;
  mealType: MealType;
  isActive: boolean;
  remarks?: string | null;
  cancellationSource?: LunchCancellationSource | null;
  cancelledAt?: string | null;
  cancellationReason?: string | null;
}

export interface DailyMealBoard {
  date: string;
  isServing: boolean;
  isLocked: boolean;
  lockedAt?: string;
  cutoffTime: string;
  serviceRemarks?: string | null;
  canManage: boolean;
  totalInitialRegistered: number;
  totalAutoCancelled: number;
  totalActiveToCook: number;
  totalAnomalies: number;
  classBreakdowns: ClassMealSummary[];
  mealChangeLogs: MealAuditLog[];
  studentMeals: StudentMealOperationItem[];
}

export interface LockMealDayPayload {
  date: string;
  notes?: string;
}

export interface RestoreMealPayload {
  studentId: number;
  date: string;
  reason: string;
}

// ---- UC-32: Cấu hình ngày phục vụ, hạn đăng ký/hủy và ghi chú/thực đơn ----
export interface LunchServiceDayConfig {
  date: string;
  isServing: boolean;
  cutoffTime: string;
  isLocked: boolean;
  lockedAt?: string | null;
  remarks?: string | null;
}

export interface UpsertLunchServiceDaysPayload {
  dates: string[];
  isServing: boolean;
  cutoffTime: string;
  remarks?: string;
}

// ---- UC-31: Phụ huynh xem lịch và trạng thái đăng ký của con ----
export interface StudentLunchDay {
  date: string;
  isServing: boolean;
  cutoffTime: string;
  isLocked: boolean;
  isCutoffPassed: boolean;
  serviceRemarks?: string | null;
  registrationId?: number | null;
  isRegistered: boolean;
  mealType?: MealType | null;
  studentRemarks?: string | null;
  cancellationSource?: LunchCancellationSource | null;
  cancelledAt?: string | null;
  cancellationReason?: string | null;
}

export interface StudentLunchSchedule {
  studentId: number;
  studentCode: string;
  studentName: string;
  className: string;
  month: string;
  totalServingDays: number;
  totalRegisteredDays: number;
  totalCancelledDays: number;
  days: StudentLunchDay[];
}

// ---- UC-29: Phụ huynh đăng ký suất ăn cho 1 hoặc nhiều ngày phục vụ ----
export interface BatchLunchRegistrationPayload {
  studentId: number;
  dates: string[];
  mealType: number; // 1: StandardLunch, 2: VegetarianLunch, 3: SpecialDiet
  remarks?: string;
}

export interface BatchLunchRegistrationResult {
  registeredCount: number;
  registeredDates: string[];
  skippedDuplicateDates: string[];
}

// ---- UC-30: Phụ huynh hủy đăng ký còn hiệu lực ----
export interface CancelLunchRegistrationPayload {
  studentId: number;
  date: string;
  reason?: string;
}

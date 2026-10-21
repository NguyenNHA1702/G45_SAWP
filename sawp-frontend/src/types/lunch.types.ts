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
  finalizeTime: string; // "10:30:00"
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
  changeType: 'AutoCancelled' | 'Restored' | 'LateAdded' | string;
  reason: string;
  timestamp: string;
  isBeforeCutoff: boolean;
}

export interface DailyMealBoard {
  date: string;
  isLocked: boolean;
  lockedAt?: string;
  cutoffTime: string;
  totalInitialRegistered: number;
  totalAutoCancelled: number;
  totalActiveToCook: number;
  totalAnomalies: number;
  classBreakdowns: ClassMealSummary[];
  mealChangeLogs: MealAuditLog[];
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

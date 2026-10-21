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

import { useCallback, useEffect, useMemo, useState } from 'react';
import { attendanceApi } from '../services/implementations/attendanceApi';
import { ApiError, asApiError } from '../utils/apiError';
import type { AttendanceSession, AttendanceSheet, AttendanceStatus, AttendanceSummary } from '../types/attendance.types';

export interface DraftRow {
  status: AttendanceStatus | null;
  remarks: string;
}
export type DraftMap = Record<number, DraftRow>;

const buildDraft = (sheet: AttendanceSheet): DraftMap => {
  const map: DraftMap = {};
  for (const e of sheet.entries) map[e.studentId] = { status: e.status, remarks: e.remarks ?? '' };
  return map;
};

export function useAttendanceSheet(classId: number | null, date: string, session: AttendanceSession) {
  const [sheet, setSheet] = useState<AttendanceSheet | null>(null);
  const [draft, setDraft] = useState<DraftMap>({});
  const [loading, setLoading] = useState(false);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<ApiError | null>(null);
  const [reloadKey, setReloadKey] = useState(0);

  // 1. Load bảng điểm danh mỗi khi đổi lớp / ngày / buổi / reload
  useEffect(() => {
    if (classId === null) {
      setSheet(null);
      setDraft({});
      return;
    }
    let cancelled = false;
    setLoading(true);
    setError(null);
    attendanceApi
      .getSheet(classId, date, session)
      .then((s) => {
        if (cancelled) return;
        setSheet(s);
        setDraft(buildDraft(s));
      })
      .catch((e: unknown) => {
        if (cancelled) return;
        setSheet(null);
        setError(asApiError(e));
      })
      .finally(() => {
        if (!cancelled) setLoading(false);
      });
    return () => {
      cancelled = true;
    };
  }, [classId, date, session, reloadKey]);

  // 2. Thao tác sửa draft
  const setStatus = useCallback((studentId: number, status: AttendanceStatus) => {
    setDraft((prev) => ({ ...prev, [studentId]: { ...prev[studentId], status } }));
  }, []);

  const setRemarks = useCallback((studentId: number, remarks: string) => {
    setDraft((prev) => ({ ...prev, [studentId]: { ...prev[studentId], remarks } }));
  }, []);

  /** Điền nhanh: HS chưa chọn -> trạng thái gợi ý (Có mặt, hoặc Vắng có phép nếu có đơn đã duyệt) */
  const fillSuggested = useCallback(() => {
    if (!sheet) return;
    setDraft((prev) => {
      const next = { ...prev };
      for (const e of sheet.entries) {
        if (next[e.studentId]?.status == null) next[e.studentId] = { ...next[e.studentId], status: e.suggestedStatus };
      }
      return next;
    });
  }, [sheet]);

  const reset = useCallback(() => {
    if (sheet) setDraft(buildDraft(sheet));
  }, [sheet]);

  // 3. Giá trị dẫn xuất
  const summary = useMemo<AttendanceSummary>(() => {
    const s: AttendanceSummary = { total: 0, present: 0, late: 0, excusedAbsence: 0, unexcusedAbsence: 0, notMarked: 0 };
    for (const row of Object.values(draft)) {
      s.total++;
      switch (row.status) {
        case 'Present': s.present++; break;
        case 'Late': s.late++; break;
        case 'ExcusedAbsence': s.excusedAbsence++; break;
        case 'UnexcusedAbsence': s.unexcusedAbsence++; break;
        default: s.notMarked++;
      }
    }
    return s;
  }, [draft]);

  const isDirty = useMemo(
    () =>
      !!sheet &&
      sheet.entries.some((e) => {
        const d = draft[e.studentId];
        return !!d && (d.status !== e.status || d.remarks !== (e.remarks ?? ''));
      }),
    [sheet, draft],
  );

  /** Ảnh hưởng của lần lưu này – hiển thị trong hộp xác nhận */
  const impact = useMemo(() => {
    let newlyUnexcused = 0;
    let lunchToDeduct = 0;
    if (sheet) {
      for (const e of sheet.entries) {
        const d = draft[e.studentId];
        if (d?.status === 'UnexcusedAbsence' && e.status !== 'UnexcusedAbsence') {
          newlyUnexcused++;
          if (e.hasLunch) lunchToDeduct++;
        }
      }
    }
    return { newlyUnexcused, lunchToDeduct };
  }, [sheet, draft]);

  // 4. Submit
  const submit = useCallback(async () => {
    if (!sheet || classId === null) throw new ApiError('Chưa chọn lớp.', 'MSG01');
    const entries = sheet.entries.map((e) => {
      const d = draft[e.studentId];
      if (!d?.status) throw new ApiError(`Học sinh ${e.fullName} chưa được điểm danh.`, 'MSG01');
      return { studentId: e.studentId, status: d.status, remarks: d.remarks.trim() || null };
    });
    setSaving(true);
    try {
      const res = await attendanceApi.submitSheet(classId, { date, session, entries });
      setReloadKey((k) => k + 1);
      return res;
    } finally {
      setSaving(false);
    }
  }, [sheet, classId, date, session, draft]);

  const reload = useCallback(() => setReloadKey((k) => k + 1), []);

  return { sheet, draft, loading, saving, error, summary, isDirty, impact, setStatus, setRemarks, fillSuggested, reset, submit, reload };
}

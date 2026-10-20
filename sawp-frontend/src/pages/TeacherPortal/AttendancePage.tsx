import { useEffect, useState } from 'react';
import { Clock, CheckCircle2, AlertTriangle, Save, RefreshCw, UserCheck, Check, AlertCircle } from 'lucide-react';
import toast from 'react-hot-toast';
import { attendanceApi, type ClassDailySheet, type AttendanceRow } from '../../services/implementations/attendanceApi';
import { apiClient } from '../../services/apiClient';

interface ClassOption {
  id: number;
  className: string;
}

export default function AttendancePage() {
  const [classes, setClasses] = useState<ClassOption[]>([]);
  const [selectedClassId, setSelectedClassId] = useState<number>(1);
  const [date, setDate] = useState<string>(new Date().toISOString().split('T')[0]);
  const [session, setSession] = useState<number>(1); // 1: Sáng, 2: Chiều

  const [sheet, setSheet] = useState<ClassDailySheet | null>(null);
  const [students, setStudents] = useState<AttendanceRow[]>([]);
  const [loading, setLoading] = useState(false);
  const [saving, setSaving] = useState(false);

  // 1. Fetch available classes
  useEffect(() => {
    const fetchClasses = async () => {
      try {
        const res = await apiClient.get('/classes').catch(() => null);
        if (res?.data?.data && Array.isArray(res.data.data)) {
          const mapped = res.data.data.map((c: { id: number; className?: string; name?: string }) => ({
            id: c.id,
            className: c.className || c.name || `Lớp ${c.id}`,
          }));
          setClasses(mapped);
          if (mapped.length > 0) setSelectedClassId(mapped[0].id);
        } else {
          // Default mock classes for primary school
          setClasses([
            { id: 1, className: 'Lớp 1A' },
            { id: 2, className: 'Lớp 1B' },
          ]);
        }
      } catch {
        setClasses([
          { id: 1, className: 'Lớp 1A' },
          { id: 2, className: 'Lớp 1B' },
        ]);
      }
    };
    fetchClasses();
  }, []);

  // 2. Fetch sheet for selected class, date, session
  const fetchSheet = async () => {
    if (!selectedClassId) return;
    try {
      setLoading(true);
      const res = await attendanceApi.getClassDailySheet(selectedClassId, date, session);
      if (res.success && res.data) {
        setSheet(res.data);
        setStudents(res.data.students || []);
      }
    } catch {
      toast.error('Không thể tải bảng điểm danh lớp.');
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    fetchSheet();
  }, [selectedClassId, date, session]);

  const handleStatusChange = (studentId: number, newStatus: number) => {
    setStudents((prev) =>
      prev.map((s) => (s.studentId === studentId ? { ...s, currentStatus: newStatus } : s))
    );
  };

  const handleRemarksChange = (studentId: number, remarks: string) => {
    setStudents((prev) =>
      prev.map((s) => (s.studentId === studentId ? { ...s, remarks } : s))
    );
  };

  const handleSave = async () => {
    try {
      setSaving(true);
      const res = await attendanceApi.saveAttendance({
        classId: selectedClassId,
        date,
        session,
        entries: students.map((s) => ({
          studentId: s.studentId,
          status: s.currentStatus,
          remarks: s.remarks,
        })),
      });

      if (res.success) {
        toast.success('Đã lưu điểm danh lớp thành công!');
        if (res.data.mealAlerts && res.data.mealAlerts.length > 0) {
          res.data.mealAlerts.forEach((alert) => {
            toast(alert, { icon: '⚠️', duration: 7000 });
          });
        }
        fetchSheet();
      } else {
        toast.error(res.message);
      }
    } catch (err: unknown) {
      const errorMsg = (err as { response?: { data?: { message?: string } } })?.response?.data?.message || 'Lỗi khi lưu điểm danh.';
      toast.error(errorMsg);
    } finally {
      setSaving(false);
    }
  };

  // Stats calculation
  const total = students.length;
  const present = students.filter((s) => s.currentStatus === 1).length;
  const excused = students.filter((s) => s.currentStatus === 2).length;
  const unexcused = students.filter((s) => s.currentStatus === 3).length;
  const late = students.filter((s) => s.currentStatus === 4).length;

  return (
    <div style={{ padding: '24px', maxWidth: '1200px', margin: '0 auto' }}>
      {/* Header */}
      <div style={{ marginBottom: '24px' }}>
        <h1 style={{ fontSize: '24px', fontWeight: 800, color: '#0F172A', margin: 0 }}>
          Điểm danh lớp học
        </h1>
        <p style={{ color: '#64748B', marginTop: '4px', fontSize: '14px' }}>
          Đơn nghỉ được duyệt sẽ <strong>tự động điền gợi ý</strong>. GVCN đối chiếu thực tế tại lớp học và bấm <em>Lưu điểm danh</em> để chốt số liệu chính thức.
        </p>
      </div>

      {/* Control Card */}
      <div
        style={{
          backgroundColor: '#FFFFFF',
          borderRadius: '16px',
          border: '1px solid #E2E8F0',
          padding: '20px',
          boxShadow: '0 2px 8px rgba(0,0,0,0.04)',
          marginBottom: '20px',
        }}
      >
        <div style={{ display: 'flex', gap: '16px', flexWrap: 'wrap', alignItems: 'flex-end' }}>
          {/* Class Select */}
          <div style={{ minWidth: '180px' }}>
            <label style={{ display: 'block', fontSize: '13px', fontWeight: 700, color: '#475569', marginBottom: '6px' }}>
              Lớp học
            </label>
            <select
              value={selectedClassId}
              onChange={(e) => setSelectedClassId(Number(e.target.value))}
              style={{
                width: '100%',
                padding: '9px 12px',
                borderRadius: '8px',
                border: '1px solid #CBD5E1',
                fontSize: '14px',
                fontWeight: 600,
              }}
            >
              {classes.map((c) => (
                <option key={c.id} value={c.id}>
                  {c.className}
                </option>
              ))}
            </select>
          </div>

          {/* Date input */}
          <div>
            <label style={{ display: 'block', fontSize: '13px', fontWeight: 700, color: '#475569', marginBottom: '6px' }}>
              Ngày học
            </label>
            <input
              type="date"
              value={date}
              onChange={(e) => setDate(e.target.value)}
              style={{
                padding: '8px 12px',
                borderRadius: '8px',
                border: '1px solid #CBD5E1',
                fontSize: '14px',
                fontWeight: 600,
              }}
            />
          </div>

          {/* Session toggle */}
          <div>
            <label style={{ display: 'block', fontSize: '13px', fontWeight: 700, color: '#475569', marginBottom: '6px' }}>
              Buổi học
            </label>
            <div style={{ display: 'flex', gap: '6px' }}>
              <button
                type="button"
                onClick={() => setSession(1)}
                style={{
                  padding: '8px 16px',
                  borderRadius: '8px',
                  border: session === 1 ? '2px solid #2563EB' : '1px solid #CBD5E1',
                  backgroundColor: session === 1 ? '#EFF6FF' : '#FFFFFF',
                  color: session === 1 ? '#1D4ED8' : '#475569',
                  fontWeight: 700,
                  fontSize: '13px',
                  cursor: 'pointer',
                }}
              >
                Buổi sáng (07h15)
              </button>
              <button
                type="button"
                onClick={() => setSession(2)}
                style={{
                  padding: '8px 16px',
                  borderRadius: '8px',
                  border: session === 2 ? '2px solid #2563EB' : '1px solid #CBD5E1',
                  backgroundColor: session === 2 ? '#EFF6FF' : '#FFFFFF',
                  color: session === 2 ? '#1D4ED8' : '#475569',
                  fontWeight: 700,
                  fontSize: '13px',
                  cursor: 'pointer',
                }}
              >
                Buổi chiều (13h45)
              </button>
            </div>
          </div>

          {/* Reload button */}
          <button
            onClick={fetchSheet}
            style={{
              padding: '9px 14px',
              borderRadius: '8px',
              border: '1px solid #CBD5E1',
              background: '#F8FAFC',
              cursor: 'pointer',
              color: '#475569',
              display: 'flex',
              alignItems: 'center',
              gap: '6px',
              fontWeight: 600,
              fontSize: '13px',
            }}
          >
            <RefreshCw size={14} /> Tải lại
          </button>

          {/* Cutoff note */}
          <div style={{ marginLeft: 'auto', display: 'flex', alignItems: 'center', gap: '8px' }}>
            <span
              style={{
                backgroundColor: '#FEF3C7',
                color: '#92400E',
                padding: '6px 12px',
                borderRadius: '8px',
                fontSize: '12px',
                fontWeight: 700,
                display: 'flex',
                alignItems: 'center',
                gap: '6px',
              }}
            >
              <Clock size={14} /> Giờ chốt suất ăn: 08:15 AM
            </span>
          </div>
        </div>
      </div>

      {/* Status & Stats bar */}
      <div
        style={{
          display: 'grid',
          gridTemplateColumns: 'repeat(auto-fit, minmax(140px, 1fr))',
          gap: '12px',
          marginBottom: '20px',
        }}
      >
        <div style={{ backgroundColor: '#F8FAFC', padding: '12px 16px', borderRadius: '12px', border: '1px solid #E2E8F0' }}>
          <div style={{ fontSize: '12px', color: '#64748B', fontWeight: 600 }}>Sĩ số lớp</div>
          <div style={{ fontSize: '22px', fontWeight: 800, color: '#0F172A' }}>{total}</div>
        </div>
        <div style={{ backgroundColor: '#ECFDF5', padding: '12px 16px', borderRadius: '12px', border: '1px solid #A7F3D0' }}>
          <div style={{ fontSize: '12px', color: '#047857', fontWeight: 600 }}>Có mặt</div>
          <div style={{ fontSize: '22px', fontWeight: 800, color: '#065F46' }}>{present}</div>
        </div>
        <div style={{ backgroundColor: '#EFF6FF', padding: '12px 16px', borderRadius: '12px', border: '1px solid #BFDBFE' }}>
          <div style={{ fontSize: '12px', color: '#1D4ED8', fontWeight: 600 }}>Vắng có phép</div>
          <div style={{ fontSize: '22px', fontWeight: 800, color: '#1E40AF' }}>{excused}</div>
        </div>
        <div style={{ backgroundColor: '#FEF2F2', padding: '12px 16px', borderRadius: '12px', border: '1px solid #FECACA' }}>
          <div style={{ fontSize: '12px', color: '#B91C1C', fontWeight: 600 }}>Vắng không phép</div>
          <div style={{ fontSize: '22px', fontWeight: 800, color: '#991B1B' }}>{unexcused}</div>
        </div>
        <div style={{ backgroundColor: '#FFFBEB', padding: '12px 16px', borderRadius: '12px', border: '1px solid #FDE68A' }}>
          <div style={{ fontSize: '12px', color: '#B45309', fontWeight: 600 }}>Đi muộn</div>
          <div style={{ fontSize: '22px', fontWeight: 800, color: '#92400E' }}>{late}</div>
        </div>
      </div>

      {/* Save state notification */}
      {sheet && (
        <div
          style={{
            backgroundColor: sheet.isSaved ? '#F0FDF4' : '#FFFBEB',
            border: sheet.isSaved ? '1px solid #BBF7D0' : '1px solid #FDE68A',
            borderRadius: '10px',
            padding: '12px 16px',
            marginBottom: '16px',
            display: 'flex',
            justifyContent: 'space-between',
            alignItems: 'center',
          }}
        >
          <div style={{ display: 'flex', alignItems: 'center', gap: '8px', fontSize: '13px' }}>
            {sheet.isSaved ? (
              <>
                <CheckCircle2 size={16} color="#16A34A" />
                <span style={{ color: '#15803D', fontWeight: 600 }}>
                  Đã lưu điểm danh chính thức bởi <strong>{sheet.savedByName || 'GVCN'}</strong> lúc{' '}
                  {sheet.savedAt ? new Date(sheet.savedAt).toLocaleTimeString('vi-VN') : ''}
                </span>
              </>
            ) : (
              <>
                <AlertCircle size={16} color="#D97706" />
                <span style={{ color: '#B45309', fontWeight: 600 }}>
                  Chưa lưu điểm danh (Dữ liệu hiện tại là nháp/gợi ý). Nhớ bấm <em>"Lưu điểm danh"</em> trước 07h35!
                </span>
              </>
            )}
          </div>
          <button
            onClick={handleSave}
            disabled={saving}
            style={{
              display: 'flex',
              alignItems: 'center',
              gap: '6px',
              backgroundColor: '#2563EB',
              color: '#FFFFFF',
              border: 'none',
              padding: '8px 18px',
              borderRadius: '8px',
              fontWeight: 700,
              fontSize: '13px',
              cursor: 'pointer',
              boxShadow: '0 2px 6px rgba(37, 99, 235, 0.25)',
              opacity: saving ? 0.7 : 1,
            }}
          >
            <Save size={14} /> {saving ? 'Đang lưu...' : 'Lưu điểm danh'}
          </button>
        </div>
      )}

      {/* Roster Table */}
      {loading ? (
        <div style={{ textAlign: 'center', padding: '40px', color: '#64748B' }}>Đang tải danh sách học sinh...</div>
      ) : students.length === 0 ? (
        <div style={{ textAlign: 'center', padding: '40px', backgroundColor: '#F8FAFC', borderRadius: '12px' }}>
          <UserCheck size={36} color="#94A3B8" />
          <p style={{ color: '#64748B', marginTop: '8px' }}>Chưa có học sinh nào trong lớp này.</p>
        </div>
      ) : (
        <div
          style={{
            backgroundColor: '#FFFFFF',
            borderRadius: '16px',
            border: '1px solid #E2E8F0',
            overflow: 'hidden',
            boxShadow: '0 2px 8px rgba(0,0,0,0.04)',
          }}
        >
          <table style={{ width: '100%', borderCollapse: 'collapse', textAlign: 'left', fontSize: '14px' }}>
            <thead>
              <tr style={{ backgroundColor: '#F8FAFC', borderBottom: '2px solid #E2E8F0', color: '#475569', fontWeight: 700 }}>
                <th style={{ padding: '14px 16px', width: '50px' }}>STT</th>
                <th style={{ padding: '14px 16px', width: '120px' }}>Mã HS</th>
                <th style={{ padding: '14px 16px' }}>Họ và tên</th>
                <th style={{ padding: '14px 16px', width: '380px' }}>Trạng thái điểm danh</th>
                <th style={{ padding: '14px 16px' }}>Ghi chú / Thông tin đơn nghỉ</th>
              </tr>
            </thead>
            <tbody>
              {students.map((s, idx) => (
                <tr
                  key={s.studentId}
                  style={{
                    borderBottom: '1px solid #F1F5F9',
                    backgroundColor: s.hasApprovedLeave && s.currentStatus === 1 ? '#FEF2F2' : 'transparent',
                  }}
                >
                  <td style={{ padding: '14px 16px', color: '#64748B' }}>{idx + 1}</td>
                  <td style={{ padding: '14px 16px', fontFamily: 'monospace', fontWeight: 600 }}>{s.studentCode}</td>
                  <td style={{ padding: '14px 16px' }}>
                    <div style={{ fontWeight: 700, color: '#0F172A' }}>{s.fullName}</div>
                    <div style={{ fontSize: '12px', color: '#64748B' }}>{s.gender}</div>
                  </td>
                  <td style={{ padding: '14px 16px' }}>
                    <div style={{ display: 'flex', gap: '4px' }}>
                      {[
                        { val: 1, label: 'Có mặt', color: '#10B981', bg: '#D1FAE5' },
                        { val: 2, label: 'Vắng phép', color: '#3B82F6', bg: '#DBEAFE' },
                        { val: 3, label: 'Vắng KP', color: '#EF4444', bg: '#FEE2E2' },
                        { val: 4, label: 'Đi muộn', color: '#F59E0B', bg: '#FEF3C7' },
                      ].map((btn) => (
                        <button
                          key={btn.val}
                          type="button"
                          onClick={() => handleStatusChange(s.studentId, btn.val)}
                          style={{
                            padding: '6px 12px',
                            borderRadius: '6px',
                            border: s.currentStatus === btn.val ? `2px solid ${btn.color}` : '1px solid #CBD5E1',
                            backgroundColor: s.currentStatus === btn.val ? btn.bg : '#FFFFFF',
                            color: s.currentStatus === btn.val ? '#0F172A' : '#64748B',
                            fontWeight: s.currentStatus === btn.val ? 700 : 500,
                            fontSize: '12px',
                            cursor: 'pointer',
                          }}
                        >
                          {s.currentStatus === btn.val && <Check size={12} style={{ display: 'inline', marginRight: '2px' }} />}
                          {btn.label}
                        </button>
                      ))}
                    </div>
                  </td>
                  <td style={{ padding: '14px 16px' }}>
                    {s.hasApprovedLeave && (
                      <div
                        style={{
                          display: 'inline-flex',
                          alignItems: 'center',
                          gap: '6px',
                          backgroundColor: '#EFF6FF',
                          color: '#1D4ED8',
                          padding: '4px 8px',
                          borderRadius: '6px',
                          fontSize: '12px',
                          fontWeight: 600,
                          marginBottom: '4px',
                        }}
                      >
                        <CheckCircle2 size={12} /> Có đơn nghỉ duyệt: {s.leaveReason}
                      </div>
                    )}
                    {s.hasApprovedLeave && s.currentStatus === 1 && (
                      <div
                        style={{
                          color: '#B91C1C',
                          fontSize: '12px',
                          fontWeight: 600,
                          display: 'flex',
                          alignItems: 'center',
                          gap: '4px',
                          marginTop: '2px',
                        }}
                      >
                        <AlertTriangle size={12} /> Bất thường: Đơn duyệt nhưng học sinh đi học!
                      </div>
                    )}
                    <input
                      type="text"
                      placeholder="Ghi chú thêm..."
                      value={s.remarks || ''}
                      onChange={(e) => handleRemarksChange(s.studentId, e.target.value)}
                      style={{
                        width: '100%',
                        padding: '4px 8px',
                        borderRadius: '6px',
                        border: '1px solid #E2E8F0',
                        fontSize: '12px',
                        marginTop: '2px',
                      }}
                    />
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  );
}

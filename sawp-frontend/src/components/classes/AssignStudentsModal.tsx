import React, { useCallback, useEffect, useState } from 'react';
import { AlertCircle, Check, UserPlus, Users } from 'lucide-react';
import toast from 'react-hot-toast';
import { GeometricModal } from '../common/GeometricModal';
import { PaginationBar } from '../common/PaginationBar';
import { classApi } from '../../services/implementations/classApi';
import { formatDDMMYYYY } from '../../utils/date';
import type { ClassSummaryDto, RosterStudentItemDto } from '../../types/class.types';

interface AssignStudentsModalProps {
  open: boolean;
  onClose: () => void;
  targetClass: ClassSummaryDto | null;
  onAssigned: () => void;
}

export const AssignStudentsModal: React.FC<AssignStudentsModalProps> = ({
  open,
  onClose,
  targetClass,
  onAssigned,
}) => {
  const [eligibleStudents, setEligibleStudents] = useState<RosterStudentItemDto[]>([]);
  const [totalCount, setTotalCount] = useState(0);
  const [pageIndex, setPageIndex] = useState(1);
  const pageSize = 25;
  const [selectedIds, setSelectedIds] = useState<number[]>([]);
  const [loading, setLoading] = useState(false);
  const [submitting, setSubmitting] = useState(false);
  const [search, setSearch] = useState('');
  const [debouncedSearch, setDebouncedSearch] = useState('');

  useEffect(() => {
    const timer = window.setTimeout(() => {
      setDebouncedSearch(search);
      setPageIndex(1);
    }, 300);
    return () => window.clearTimeout(timer);
  }, [search]);

  useEffect(() => {
    if (open && targetClass) {
      setSelectedIds([]);
      setSearch('');
      setDebouncedSearch('');
      setPageIndex(1);
    }
  }, [open, targetClass]);

  const loadEligible = useCallback(async () => {
    if (!open || !targetClass) return;
    setLoading(true);
    try {
      const res = await classApi.getEligibleStudentsPaged(targetClass.id, debouncedSearch, pageIndex, pageSize);
      if (res.success && res.data) {
        setEligibleStudents(res.data.items);
        setTotalCount(res.data.totalCount);
      }
    } catch {
      toast.error('Lỗi khi tải danh sách học sinh chưa phân lớp.');
    } finally {
      setLoading(false);
    }
  }, [open, targetClass, debouncedSearch, pageIndex]);

  useEffect(() => {
    void loadEligible();
  }, [loadEligible]);

  const handleToggle = (id: number) => {
    setSelectedIds((prev) =>
      prev.includes(id) ? prev.filter((item) => item !== id) : [...prev, id]
    );
  };

  const handleSelectAllOnPage = () => {
    const pageIds = eligibleStudents.map((s) => s.studentId);
    const allPageSelected = pageIds.length > 0 && pageIds.every((id) => selectedIds.includes(id));
    if (allPageSelected) {
      setSelectedIds((prev) => prev.filter((id) => !pageIds.includes(id)));
    } else {
      setSelectedIds((prev) => Array.from(new Set([...prev, ...pageIds])));
    }
  };

  const handleAssign = async () => {
    if (!targetClass || selectedIds.length === 0) return;

    const currentEnrolled = targetClass.enrolledCount ?? targetClass.enrolledStudentsCount ?? 0;
    const remainingSpots = targetClass.availableCapacity ?? (targetClass.capacity - currentEnrolled);
    if (currentEnrolled + selectedIds.length > targetClass.capacity) {
      toast.error(
        `Số lượng chọn (${selectedIds.length}) vượt quá sức chứa còn lại (${remainingSpots} chỗ)!`
      );
      return;
    }

    setSubmitting(true);
    try {
      const res = await classApi.assignStudents(targetClass.id, selectedIds);
      if (res.success) {
        toast.success(`Đã xếp ${selectedIds.length} học sinh vào lớp ${targetClass.className}!`);
        onAssigned();
        onClose();
      } else {
        toast.error(res.message || 'Xếp lớp thất bại.');
      }
    } catch (err: any) {
      toast.error(err.response?.data?.message || 'Lỗi khi xếp học sinh vào lớp.');
    } finally {
      setSubmitting(false);
    }
  };

  if (!targetClass) return null;

  const allPageSelected =
    eligibleStudents.length > 0 && eligibleStudents.every((s) => selectedIds.includes(s.studentId));
  const currentEnrolled = targetClass.enrolledCount ?? targetClass.enrolledStudentsCount ?? 0;
  const availableSpots = targetClass.availableCapacity ?? (targetClass.capacity - currentEnrolled);

  return (
    <GeometricModal
      open={open}
      onClose={onClose}
      title={`Xếp học sinh vào Lớp ${targetClass.className}`}
      badgeText={`Sức chứa còn lại: ${availableSpots}/${targetClass.capacity}`}
      badgeColor={availableSpots > 0 ? 'mint' : 'pink'}
      icon={<UserPlus size={24} color="#1E293B" strokeWidth={2.5} />}
      iconBg="var(--color-tertiary)"
      maxWidth="lg"
    >
      <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
        {/* Capacity Warning Banner */}
        <div
          className={`banner ${
            selectedIds.length > availableSpots ? 'banner--danger' : 'banner--info'
          }`}
          style={{ fontSize: 'var(--fs-sm)' }}
        >
          {selectedIds.length > availableSpots ? (
            <>
              <AlertCircle size={18} color="var(--color-secondary)" />
              <div>
                <strong>Cảnh báo:</strong> Đã chọn {selectedIds.length} học sinh, vượt quá sức
                chứa còn lại ({availableSpots} chỗ). Vui lòng bỏ bớt học sinh trước khi lưu.
              </div>
            </>
          ) : (
            <div>
              Hiện lớp đã có <strong>{currentEnrolled}</strong> học sinh. Bạn có thể
              chọn tối đa thêm <strong>{availableSpots}</strong> học sinh vào lớp này.
            </div>
          )}
        </div>

        {/* Search & Select All */}
        <div style={{ display: 'flex', gap: 12, alignItems: 'center', flexWrap: 'wrap' }}>
          <input
            type="text"
            className="input"
            style={{ flex: 1, minWidth: 200 }}
            placeholder="Tìm theo mã học sinh hoặc họ tên..."
            value={search}
            onChange={(e) => setSearch(e.target.value)}
          />
          <button
            type="button"
            className="btn btn--secondary btn--sm"
            onClick={handleSelectAllOnPage}
            disabled={eligibleStudents.length === 0}
          >
            {allPageSelected
              ? 'Bỏ chọn trang này'
              : `Chọn trang này (${eligibleStudents.length})`}
          </button>
          <span className="badge badge--violet">
            Đã chọn: <strong>{selectedIds.length}</strong>
          </span>
        </div>

        {/* Student list */}
        {loading ? (
          <div style={{ textAlign: 'center', padding: 24 }}>Đang tải danh sách học sinh chưa phân lớp...</div>
        ) : eligibleStudents.length === 0 ? (
          <div className="card empty-state" style={{ padding: 24, textAlign: 'center' }}>
            <Users size={36} color="var(--color-muted-fg)" style={{ margin: '0 auto 8px' }} />
            <p style={{ margin: 0, color: 'var(--color-muted-fg)' }}>
              Không có học sinh nào chưa được phân lớp (hoặc không tìm thấy kết quả phù hợp).
            </p>
          </div>
        ) : (
          <>
            <div className="data-table-wrapper" style={{ maxHeight: 320 }}>
              <table className="data-table">
                <thead>
                  <tr>
                    <th style={{ width: 40 }}>Chọn</th>
                    <th>Mã HS</th>
                    <th>Họ và tên</th>
                    <th>Ngày sinh</th>
                    <th>Giới tính</th>
                    <th>Phụ huynh</th>
                  </tr>
                </thead>
                <tbody>
                  {eligibleStudents.map((s) => {
                    const isChecked = selectedIds.includes(s.studentId);
                    return (
                      <tr
                        key={s.studentId}
                        onClick={() => handleToggle(s.studentId)}
                        style={{
                          cursor: 'pointer',
                          backgroundColor: isChecked ? 'var(--color-accent-soft)' : undefined,
                        }}
                      >
                        <td onClick={(e) => e.stopPropagation()}>
                          <input
                            type="checkbox"
                            checked={isChecked}
                            onChange={() => handleToggle(s.studentId)}
                          />
                        </td>
                        <td style={{ fontWeight: 700 }}>{s.studentCode}</td>
                        <td>{s.fullName}</td>
                        <td>{formatDDMMYYYY(s.dateOfBirth)}</td>
                        <td>{s.gender}</td>
                        <td>{s.primaryGuardianName || '-'}</td>
                      </tr>
                    );
                  })}
                </tbody>
              </table>
            </div>
            <PaginationBar
              pageIndex={pageIndex}
              pageSize={pageSize}
              totalCount={totalCount}
              onPageChange={setPageIndex}
              itemLabel="học sinh chưa phân lớp"
            />
          </>
        )}

        {/* Action Buttons */}
        <div style={{ display: 'flex', justifyContent: 'flex-end', gap: 10, marginTop: 10 }}>
          <button type="button" className="btn btn--secondary" onClick={onClose} disabled={submitting}>
            Hủy
          </button>
          <button
            type="button"
            className="btn btn--primary"
            onClick={handleAssign}
            disabled={
              selectedIds.length === 0 ||
              selectedIds.length > availableSpots ||
              submitting
            }
          >
            {submitting ? 'Đang lưu...' : `Xác nhận xếp ${selectedIds.length} học sinh`}
            <Check size={18} />
          </button>
        </div>
      </div>
    </GeometricModal>
  );
};

import { Lock } from 'lucide-react';
import type { SheetLockReason } from '../../types/attendance.types';
import { SYSTEM_MESSAGES } from '../../constants/messages';

const TEXT: Record<SheetLockReason, string> = {
  PastDay: SYSTEM_MESSAGES.MSG19.content,
  NotAssigned: SYSTEM_MESSAGES.MSG20.content,
  NonSchoolDay: 'Ngày đã chọn không phải ngày học.',
  Future: 'Chưa thể điểm danh cho ngày trong tương lai.',
  EmptyRoster: 'Lớp hiện chưa có học sinh nào được xếp vào ngày đã chọn.',
  AlreadySubmitted: 'Bảng điểm danh hôm nay đã được giáo viên nộp.',
};

export function LockedBanner({ reason }: { reason: SheetLockReason | null }) {
  if (!reason || reason === 'EmptyRoster') return null;
  return (
    <div className="banner banner--warning" role="status">
      <Lock size={20} aria-hidden="true" />
      <span>{TEXT[reason]} Bảng điểm danh đang ở chế độ chỉ xem.</span>
    </div>
  );
}

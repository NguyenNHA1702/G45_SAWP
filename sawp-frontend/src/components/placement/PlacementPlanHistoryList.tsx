import React from 'react';
import {
  AlertTriangle,
  CheckCircle2,
  Clock,
  ExternalLink,
  History,
  Plus,
  RotateCcw,
  Send,
  Sparkles,
} from 'lucide-react';
import type { PlacementPlanSummaryDto } from '../../types/placement.types';

interface PlacementPlanHistoryListProps {
  plans: PlacementPlanSummaryDto[];
  loading: boolean;
  userRoles: string[];
  onSelectPlan: (planId: number) => void;
  onOpenGenerateModal: () => void;
}

export const PlacementPlanHistoryList: React.FC<PlacementPlanHistoryListProps> = ({
  plans,
  loading,
  userRoles,
  onSelectPlan,
  onOpenGenerateModal,
}) => {
  const isOfficeStaff = userRoles.includes('OfficeStaff') || userRoles.includes('Admin');

  const getStatusBadge = (status: string) => {
    switch (status) {
      case 'Draft':
        return {
          label: 'Bản nháp',
          bg: '#FEF08A',
          color: '#854D0E',
          border: '#CA8A04',
          icon: Clock,
        };
      case 'Submitted':
        return {
          label: 'Chờ BGH duyệt',
          bg: '#BAE6FD',
          color: '#0369A1',
          border: '#0284C7',
          icon: Send,
        };
      case 'Approved':
        return {
          label: 'Đã phê duyệt',
          bg: '#DCFCE7',
          color: '#15803D',
          border: '#16A34A',
          icon: CheckCircle2,
        };
      case 'Rejected':
        return {
          label: 'Bị trả lại',
          bg: '#FEE2E2',
          color: '#B91C1C',
          border: '#EF4444',
          icon: RotateCcw,
        };
      default:
        return {
          label: status,
          bg: '#F1F5F9',
          color: '#475569',
          border: '#94A3B8',
          icon: Clock,
        };
    }
  };

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 20 }}>
      {/* Header Bar */}
      <div
        style={{
          display: 'flex',
          flexWrap: 'wrap',
          justifyContent: 'space-between',
          alignItems: 'center',
          gap: 16,
          backgroundColor: '#FFFFFF',
          padding: '20px 24px',
          borderRadius: 20,
          border: '3px solid #1E293B',
          boxShadow: '4px 4px 0 #1E293B',
        }}
      >
        <div style={{ display: 'flex', alignItems: 'center', gap: 12 }}>
          <div
            style={{
              width: 44,
              height: 44,
              borderRadius: 14,
              backgroundColor: '#FEF08A',
              border: '2px solid #1E293B',
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'center',
            }}
          >
            <History size={24} color="#1E293B" />
          </div>
          <div>
            <h2 style={{ fontSize: '1.25rem', fontWeight: 900, color: '#1E293B', margin: 0 }}>
              Lịch sử các phương án xếp lớp đầu năm
            </h2>
            <p style={{ fontSize: '0.85rem', color: '#64748B', margin: 0 }}>
              Quản lý các phiên bản phương án xếp lớp, trạng thái duyệt và phản hồi của Ban Giám hiệu
            </p>
          </div>
        </div>

        {isOfficeStaff && (
          <button
            onClick={onOpenGenerateModal}
            style={{
              display: 'flex',
              alignItems: 'center',
              gap: 8,
              padding: '10px 20px',
              borderRadius: 14,
              border: '2px solid #1E293B',
              backgroundColor: '#FEF08A',
              fontWeight: 800,
              color: '#1E293B',
              boxShadow: '3px 3px 0 0 #1E293B',
              cursor: 'pointer',
            }}
          >
            <Sparkles size={18} />
            Lập phương án mới
          </button>
        )}
      </div>

      {/* List of plans */}
      {loading ? (
        <div style={{ textAlign: 'center', padding: 40, color: '#64748B', fontWeight: 700 }}>
          Đang tải danh sách phương án xếp lớp...
        </div>
      ) : plans.length === 0 ? (
        <div
          style={{
            backgroundColor: '#FFFFFF',
            border: '2px dashed #94A3B8',
            borderRadius: 20,
            padding: 48,
            textAlign: 'center',
          }}
        >
          <Sparkles size={36} color="#94A3B8" style={{ margin: '0 auto 12px auto' }} />
          <h3 style={{ fontSize: '1.1rem', fontWeight: 800, color: '#334155', margin: 0 }}>
            Chưa có phương án xếp lớp nào được lập
          </h3>
          <p style={{ fontSize: '0.875rem', color: '#64748B', maxWidth: 440, margin: '8px auto 20px auto' }}>
            Giáo vụ có thể nhấn nút "Lập phương án mới" để hệ thống tự động sinh phương án nháp cân bằng sĩ số, giới tính và hòa nhập.
          </p>
          {isOfficeStaff && (
            <button
              onClick={onOpenGenerateModal}
              style={{
                display: 'inline-flex',
                alignItems: 'center',
                gap: 8,
                padding: '10px 22px',
                borderRadius: 14,
                border: '2px solid #1E293B',
                backgroundColor: '#FEF08A',
                fontWeight: 800,
                color: '#1E293B',
                boxShadow: '3px 3px 0 0 #1E293B',
                cursor: 'pointer',
              }}
            >
              <Plus size={18} /> Bắt đầu lập phương án đầu tiên
            </button>
          )}
        </div>
      ) : (
        <div style={{ display: 'flex', flexDirection: 'column', gap: 14 }}>
          {plans.map((p) => {
            const statusConfig = getStatusBadge(p.status);
            const StatusIcon = statusConfig.icon;

            return (
              <div
                key={p.id}
                onClick={() => onSelectPlan(p.id)}
                style={{
                  backgroundColor: '#FFFFFF',
                  border: '2.5px solid #1E293B',
                  borderRadius: 20,
                  padding: '18px 22px',
                  boxShadow: '4px 4px 0 #1E293B',
                  display: 'flex',
                  flexWrap: 'wrap',
                  justifyContent: 'space-between',
                  alignItems: 'center',
                  gap: 16,
                  cursor: 'pointer',
                  transition: 'transform 0.15s ease',
                }}
                onMouseEnter={(e) => (e.currentTarget.style.transform = 'translate(-2px, -2px)')}
                onMouseLeave={(e) => (e.currentTarget.style.transform = 'translate(0, 0)')}
              >
                <div style={{ display: 'flex', alignItems: 'center', gap: 14 }}>
                  <div
                    style={{
                      width: 48,
                      height: 48,
                      borderRadius: 16,
                      backgroundColor: statusConfig.bg,
                      border: `2px solid ${statusConfig.border}`,
                      display: 'flex',
                      alignItems: 'center',
                      justifyContent: 'center',
                      fontWeight: 900,
                      color: statusConfig.color,
                      fontSize: '1.1rem',
                      flexShrink: 0,
                    }}
                  >
                    v{p.version}
                  </div>

                  <div>
                    <div style={{ display: 'flex', alignItems: 'center', gap: 10, flexWrap: 'wrap' }}>
                      <span style={{ fontSize: '1.1rem', fontWeight: 900, color: '#0F172A' }}>
                        {p.planName}
                      </span>
                      <span
                        style={{
                          fontSize: '0.75rem',
                          fontWeight: 800,
                          padding: '3px 10px',
                          borderRadius: 20,
                          border: `1.5px solid ${statusConfig.border}`,
                          backgroundColor: statusConfig.bg,
                          color: statusConfig.color,
                          display: 'inline-flex',
                          alignItems: 'center',
                          gap: 4,
                        }}
                      >
                        <StatusIcon size={13} /> {statusConfig.label}
                      </span>
                      {p.hasWarnings && (
                        <span
                          style={{
                            fontSize: '0.72rem',
                            fontWeight: 800,
                            padding: '2px 8px',
                            borderRadius: 12,
                            backgroundColor: p.violationCount > 0 ? '#FEE2E2' : '#FEF3C7',
                            color: p.violationCount > 0 ? '#B91C1C' : '#B45309',
                            border: `1px solid ${p.violationCount > 0 ? '#EF4444' : '#F59E0B'}`,
                            display: 'inline-flex',
                            alignItems: 'center',
                            gap: 3,
                          }}
                        >
                          <AlertTriangle size={12} /> Có tiêu chí cảnh báo
                        </span>
                      )}
                    </div>

                    <div style={{ fontSize: '0.85rem', color: '#64748B', display: 'flex', gap: 16, marginTop: 4, flexWrap: 'wrap' }}>
                      <span>Khối: <strong>{p.grade}</strong></span>
                      <span>Quy mô: <strong>{p.totalStudents} học sinh / {p.targetClassCount} lớp</strong></span>
                      <span>Người tạo: <strong>{p.createdByName}</strong></span>
                      <span>Ngày tạo: {new Date(p.createdAt).toLocaleDateString('vi-VN')}</span>
                    </div>

                    {p.reviewNotes && (
                      <div
                        style={{
                          fontSize: '0.8rem',
                          color: p.status === 'Approved' ? '#15803D' : '#B91C1C',
                          fontWeight: 600,
                          marginTop: 6,
                        }}
                      >
                        💬 Ý kiến BGH: "{p.reviewNotes}" ({p.reviewedByName})
                      </div>
                    )}
                  </div>
                </div>

                <div style={{ display: 'flex', alignItems: 'center', gap: 8, color: '#0284C7', fontWeight: 800, fontSize: '0.9rem' }}>
                  <span>Xem chi tiết & xếp lớp</span>
                  <ExternalLink size={16} />
                </div>
              </div>
            );
          })}
        </div>
      )}
    </div>
  );
};

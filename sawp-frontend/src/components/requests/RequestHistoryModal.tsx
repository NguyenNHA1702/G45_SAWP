import React, { useEffect, useState } from 'react';
import { X, Clock, CheckCircle2, AlertCircle, FileText } from 'lucide-react';
import { administrativeRequestApi } from '../../services/implementations/administrativeRequestApi';
import { formatDDMMYYYYHHMM } from '../../utils/date';
import type { RequestHistoryItem } from '../../types/request.types';

interface RequestHistoryModalProps {
  isOpen: boolean;
  onClose: () => void;
  requestId: number;
  requestNumber: string;
}

export const RequestHistoryModal: React.FC<RequestHistoryModalProps> = ({
  isOpen,
  onClose,
  requestId,
  requestNumber,
}) => {
  const [history, setHistory] = useState<RequestHistoryItem[]>([]);
  const [loading, setLoading] = useState(false);

  useEffect(() => {
    if (!isOpen || !requestId) return;
    let isMounted = true;
    setLoading(true);

    administrativeRequestApi.getRequestHistory(requestId)
      .then((res) => {
        if (isMounted && res.success && Array.isArray(res.data)) {
          setHistory(res.data);
        }
      })
      .catch(() => {
        if (isMounted) setHistory([]);
      })
      .finally(() => {
        if (isMounted) setLoading(false);
      });

    return () => {
      isMounted = false;
    };
  }, [isOpen, requestId]);

  if (!isOpen) return null;

  return (
    <div
      style={{
        position: 'fixed',
        inset: 0,
        backgroundColor: 'rgba(15, 23, 42, 0.65)',
        backdropFilter: 'blur(4px)',
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'center',
        zIndex: 9999,
        padding: 16,
      }}
    >
      <div
        style={{
          backgroundColor: '#FFFFFF',
          borderRadius: 20,
          border: '3px solid #1E293B',
          boxShadow: '8px 8px 0 #1E293B',
          maxWidth: 620,
          width: '100%',
          maxHeight: '90vh',
          display: 'flex',
          flexDirection: 'column',
          overflow: 'hidden',
        }}
      >
        {/* Header */}
        <div
          style={{
            padding: '16px 20px',
            borderBottom: '2px solid #E2E8F0',
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'space-between',
            backgroundColor: '#F8FAFC',
          }}
        >
          <div style={{ display: 'flex', alignItems: 'center', gap: 10 }}>
            <span
              style={{
                display: 'inline-flex',
                alignItems: 'center',
                justifyContent: 'center',
                width: 36,
                height: 36,
                borderRadius: 10,
                backgroundColor: '#EEF2FF',
                border: '2px solid #1E293B',
                color: '#4F46E5',
              }}
            >
              <Clock size={20} />
            </span>
            <div>
              <h3 style={{ margin: 0, fontSize: 16, fontWeight: 800, color: '#1E293B' }}>
                Lịch sử tiến trình xử lý
              </h3>
              <p style={{ margin: 0, fontSize: 13, color: '#64748B' }}>
                Mã đơn: <strong style={{ color: '#4F46E5' }}>{requestNumber}</strong>
              </p>
            </div>
          </div>
          <button
            type="button"
            onClick={onClose}
            style={{
              border: '2px solid #1E293B',
              backgroundColor: '#FFFFFF',
              borderRadius: 8,
              width: 32,
              height: 32,
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'center',
              cursor: 'pointer',
              boxShadow: '2px 2px 0 #1E293B',
            }}
          >
            <X size={16} />
          </button>
        </div>

        {/* Content */}
        <div style={{ padding: 20, overflowY: 'auto', flex: 1 }}>
          {loading ? (
            <div style={{ textAlign: 'center', padding: '40px 0', color: '#64748B' }}>
              <Clock size={32} className="animate-spin" style={{ margin: '0 auto 12px' }} />
              <p style={{ margin: 0 }}>Đang tải lịch sử tiến trình...</p>
            </div>
          ) : history.length === 0 ? (
            <div style={{ textAlign: 'center', padding: '40px 0', color: '#64748B' }}>
              <FileText size={36} style={{ margin: '0 auto 12px', opacity: 0.5 }} />
              <p style={{ margin: 0 }}>Chưa có bản ghi lịch sử cho đơn này.</p>
            </div>
          ) : (
            <div style={{ display: 'flex', flexDirection: 'column', gap: 16, position: 'relative' }}>
              <div
                style={{
                  position: 'absolute',
                  left: 19,
                  top: 14,
                  bottom: 14,
                  width: 2,
                  backgroundColor: '#E2E8F0',
                  zIndex: 0,
                }}
              />
              {history.map((item, index) => {
                const isFinal = index === history.length - 1;
                const isRejected = item.status === 'Rejected' || item.status === 'Cancelled';
                const isCompleted = item.status === 'Completed' || item.status === 'Approved';
                const isReturned = item.status === 'ReturnedToStaff' || item.status === 'NeedMoreInfo';

                let dotColor = '#4F46E5';
                let dotBg = '#EEF2FF';
                if (isRejected) {
                  dotColor = '#EF4444';
                  dotBg = '#FEE2E2';
                } else if (isReturned) {
                  dotColor = '#EA580C';
                  dotBg = '#FFF7ED';
                } else if (isCompleted) {
                  dotColor = '#10B981';
                  dotBg = '#D1FAE5';
                }

                return (
                  <div
                    key={index}
                    style={{
                      display: 'flex',
                      alignItems: 'flex-start',
                      gap: 14,
                      position: 'relative',
                      zIndex: 1,
                    }}
                  >
                    <div
                      style={{
                        width: 38,
                        height: 38,
                        borderRadius: '50%',
                        backgroundColor: dotBg,
                        border: `2px solid ${dotColor}`,
                        display: 'flex',
                        alignItems: 'center',
                        justifyContent: 'center',
                        color: dotColor,
                        flexShrink: 0,
                        boxShadow: '0 2px 4px rgba(0,0,0,0.05)',
                      }}
                    >
                      {isCompleted ? (
                        <CheckCircle2 size={20} />
                      ) : isRejected || isReturned ? (
                        <AlertCircle size={20} />
                      ) : (
                        <Clock size={18} />
                      )}
                    </div>
                    <div
                      style={{
                        flex: 1,
                        backgroundColor: isFinal ? '#F8FAFC' : '#FFFFFF',
                        border: '2px solid #E2E8F0',
                        borderRadius: 12,
                        padding: '12px 14px',
                        boxShadow: isFinal ? '2px 2px 0 #1E293B' : 'none',
                      }}
                    >
                      <div
                        style={{
                          display: 'flex',
                          alignItems: 'center',
                          justifyContent: 'space-between',
                          marginBottom: 4,
                          flexWrap: 'wrap',
                          gap: 6,
                        }}
                      >
                        <strong style={{ fontSize: 14, color: '#1E293B' }}>{item.action}</strong>
                        <span style={{ fontSize: 12, color: '#64748B', fontWeight: 600 }}>
                          {formatDDMMYYYYHHMM(item.timestamp)}
                        </span>
                      </div>
                      <p
                        style={{
                          margin: 0,
                          fontSize: 13,
                          color: '#475569',
                          lineHeight: 1.5,
                          whiteSpace: 'pre-wrap',
                        }}
                      >
                        {item.description}
                      </p>
                      {item.performedBy && (
                        <div
                          style={{
                            marginTop: 6,
                            fontSize: 12,
                            color: '#94A3B8',
                            display: 'flex',
                            alignItems: 'center',
                            gap: 4,
                          }}
                        >
                          <span>Thực hiện:</span>
                          <strong style={{ color: '#64748B' }}>{item.performedBy}</strong>
                          {item.performedByRole && (
                            <span
                              style={{
                                marginLeft: 4,
                                backgroundColor: '#EEF2FF',
                                color: '#4338CA',
                                borderRadius: 999,
                                padding: '1px 8px',
                                fontSize: 11,
                                fontWeight: 700,
                              }}
                            >
                              {item.performedByRole}
                            </span>
                          )}
                        </div>
                      )}
                    </div>
                  </div>
                );
              })}
            </div>
          )}
        </div>

        {/* Footer */}
        <div
          style={{
            padding: '12px 20px',
            borderTop: '2px solid #E2E8F0',
            display: 'flex',
            justifyContent: 'flex-end',
            backgroundColor: '#F8FAFC',
          }}
        >
          <button
            type="button"
            onClick={onClose}
            style={{
              padding: '8px 20px',
              backgroundColor: '#1E293B',
              color: '#FFFFFF',
              border: '2px solid #1E293B',
              borderRadius: 10,
              fontSize: 14,
              fontWeight: 700,
              cursor: 'pointer',
              boxShadow: '2px 2px 0 #000000',
            }}
          >
            Đóng
          </button>
        </div>
      </div>
    </div>
  );
};

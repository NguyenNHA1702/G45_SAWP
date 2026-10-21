import React, { useEffect } from 'react';
import { X } from 'lucide-react';

interface GeometricModalProps {
  open: boolean;
  onClose: () => void;
  title: string;
  badgeText?: string;
  badgeColor?: 'mint' | 'amber' | 'violet' | 'pink';
  icon?: React.ReactNode;
  iconBg?: string;
  children: React.ReactNode;
  maxWidth?: 'sm' | 'md' | 'lg' | 'xl';
}

export const GeometricModal: React.FC<GeometricModalProps> = ({
  open,
  onClose,
  title,
  badgeText,
  badgeColor = 'violet',
  icon,
  iconBg = 'var(--color-tertiary)',
  children,
  maxWidth = 'md',
}) => {
  useEffect(() => {
    const handleKeyDown = (e: KeyboardEvent) => {
      if (e.key === 'Escape' && open) onClose();
    };
    window.addEventListener('keydown', handleKeyDown);
    return () => window.removeEventListener('keydown', handleKeyDown);
  }, [open, onClose]);

  if (!open) return null;

  const maxWidthPx = {
    sm: 440,
    md: 580,
    lg: 860,
    xl: 1040,
  }[maxWidth];

  return (
    <div
      className="modal-overlay"
      onClick={(e) => {
        if (e.target === e.currentTarget) onClose();
      }}
      role="dialog"
      aria-modal="true"
    >
      <div
        className="modal-box"
        style={{
          maxWidth: maxWidthPx,
          position: 'relative',
          paddingTop: icon ? 36 : undefined,
        }}
      >
        {/* Floating Circle Icon */}
        {icon && (
          <div
            style={{
              position: 'absolute',
              top: -24,
              left: 28,
              width: 50,
              height: 50,
              borderRadius: '50%',
              background: iconBg,
              border: 'var(--border)',
              boxShadow: 'var(--shadow-pop-active)',
              display: 'grid',
              placeItems: 'center',
            }}
          >
            {icon}
          </div>
        )}

        <div className="modal-header">
          <div>
            {badgeText && (
              <span className={`badge badge--${badgeColor}`} style={{ marginBottom: 6 }}>
                {badgeText}
              </span>
            )}
            <h2 className="modal-title">{title}</h2>
          </div>
          <button
            type="button"
            className="modal-close-btn"
            onClick={onClose}
            aria-label="Đóng hộp thoại"
          >
            <X size={22} strokeWidth={2.5} />
          </button>
        </div>

        <div className="modal-content-body">{children}</div>
      </div>
    </div>
  );
};

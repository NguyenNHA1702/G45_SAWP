import React, { useEffect } from 'react';
import { createPortal } from 'react-dom';
import { X } from 'lucide-react';

interface GeometricModalProps {
  open: boolean;
  onClose: () => void;
  title: string;
  subtitle?: string;
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
  subtitle,
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

  return createPortal(
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
          overflow: 'visible',
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
              border: '2px solid var(--color-fg)',
              boxShadow: '3px 3px 0 0 var(--color-fg)',
              display: 'grid',
              placeItems: 'center',
              zIndex: 10,
            }}
          >
            {icon}
          </div>
        )}

        <div className="modal-header">
          <div>
            {badgeText && (
              <span
                className={`badge badge--${badgeColor}`}
                style={{
                  marginBottom: 6,
                  border: '1.5px solid var(--color-fg)',
                  fontWeight: 800,
                  padding: '3px 10px',
                }}
              >
                {badgeText}
              </span>
            )}
            <h2 className="modal-title" style={{ fontFamily: 'var(--font-heading)', fontWeight: 800, margin: '2px 0 0' }}>
              {title}
            </h2>
            {subtitle && (
              <p style={{ margin: '4px 0 0', fontSize: 'var(--fs-xs)', color: 'var(--color-muted-fg)' }}>
                {subtitle}
              </p>
            )}
          </div>
          <button
            type="button"
            className="modal-close-btn"
            onClick={onClose}
            aria-label="Đóng hộp thoại"
            style={{
              width: 36,
              height: 36,
              borderRadius: '50%',
              border: '2px solid var(--color-fg)',
              boxShadow: '2px 2px 0 0 var(--color-fg)',
              background: 'var(--color-card)',
              display: 'grid',
              placeItems: 'center',
              cursor: 'pointer',
              transition: 'all 0.15s ease',
              flexShrink: 0,
            }}
          >
            <X size={18} strokeWidth={2.5} />
          </button>
        </div>

        <div className="modal-content-body">{children}</div>
      </div>
    </div>,
    document.body
  );
};

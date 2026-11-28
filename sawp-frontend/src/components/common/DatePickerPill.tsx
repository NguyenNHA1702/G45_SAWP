import React, { useRef } from 'react';
import { Calendar } from 'lucide-react';
import { formatDateDDMMYYYY } from '../../constants/lunch';

interface DatePickerPillProps {
  value: string;
  onChange: (value: string) => void;
  label?: string;
  min?: string;
  max?: string;
  size?: 'sm' | 'md';
  background?: string;
  ariaLabel?: string;
}

export const DatePickerPill: React.FC<DatePickerPillProps> = ({
  value,
  onChange,
  label,
  min,
  max,
  size = 'md',
  background = '#FFFFFF',
  ariaLabel,
}) => {
  const inputRef = useRef<HTMLInputElement>(null);

  const openPicker = () => {
    const el = inputRef.current;
    if (!el) return;
    try {
      if (typeof el.showPicker === 'function') {
        el.showPicker();
        return;
      }
    } catch {
      // Fallback for browsers that disallow or do not support showPicker()
    }
    el.focus();
  };

  const isSmall = size === 'sm';

  return (
    <div
      onClick={openPicker}
      style={{
        position: 'relative',
        display: 'inline-flex',
        alignItems: 'center',
        gap: isSmall ? 6 : 8,
        background,
        border: '2px solid var(--color-fg)',
        borderRadius: 'var(--radius-full)',
        boxShadow: isSmall ? '2px 2px 0 0 var(--color-fg)' : '3px 3px 0 0 var(--color-fg)',
        padding: isSmall ? '4px 12px' : '6px 14px 6px 16px',
        height: isSmall ? 36 : 42,
        boxSizing: 'border-box',
        cursor: 'pointer',
        userSelect: 'none',
      }}
    >
      {label && (
        <span style={{ fontSize: isSmall ? 'var(--fs-xs)' : 'var(--fs-sm)', fontWeight: 800, color: 'var(--color-muted-fg)' }}>
          {label}
        </span>
      )}
      <Calendar size={isSmall ? 14 : 16} color="var(--color-accent)" style={{ flexShrink: 0 }} />
      <span
        style={{
          fontWeight: 800,
          fontSize: isSmall ? 'var(--fs-xs)' : 'var(--fs-sm)',
          color: 'var(--color-fg)',
          letterSpacing: '0.01em',
          whiteSpace: 'nowrap',
        }}
      >
        {value ? formatDateDDMMYYYY(value) : 'dd/mm/yyyy'}
      </span>
      <input
        ref={inputRef}
        type="date"
        value={value}
        min={min}
        max={max}
        aria-label={ariaLabel || label || 'Chọn ngày'}
        onClick={(e) => {
          e.stopPropagation();
          openPicker();
        }}
        onChange={(e) => {
          if (e.target.value) {
            onChange(e.target.value);
          }
        }}
        style={{
          position: 'absolute',
          inset: 0,
          width: '100%',
          height: '100%',
          opacity: 0,
          cursor: 'pointer',
          margin: 0,
          padding: 0,
          border: 'none',
        }}
      />
    </div>
  );
};

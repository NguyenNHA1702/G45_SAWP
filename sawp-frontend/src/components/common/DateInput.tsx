import { useEffect, useRef, useState } from 'react';
import { Calendar } from 'lucide-react';
import { formatDateDDMMYYYY, parseDDMMYYYYToIso } from '../../utils/date';

interface DateInputProps {
  id?: string;
  value: string; // yyyy-MM-dd
  onChange: (isoDate: string) => void;
  min?: string;
  max?: string;
  disabled?: boolean;
  className?: string;
  style?: React.CSSProperties;
  inputStyle?: React.CSSProperties;
  placeholder?: string;
  ariaLabel?: string;
}

/**
 * Date input hiển thị và nhập theo định dạng dd-mm-yyyy,
 * kèm nút lịch mở bộ chọn ngày chuẩn bất kể locale trình duyệt.
 */
export function DateInput({
  id,
  value,
  onChange,
  min,
  max,
  disabled,
  className = 'input',
  style,
  inputStyle,
  placeholder = 'dd-mm-yyyy',
  ariaLabel,
}: DateInputProps) {
  const [text, setText] = useState<string>(() => formatDateDDMMYYYY(value));
  const pickerRef = useRef<HTMLInputElement>(null);

  useEffect(() => {
    setText(formatDateDDMMYYYY(value));
  }, [value]);

  const handleTextChange = (raw: string) => {
    setText(raw);
    const parsed = parseDDMMYYYYToIso(raw);
    if (parsed) {
      if (min && parsed < min) return;
      if (max && parsed > max) return;
      onChange(parsed);
    }
  };

  const handleBlur = () => {
    const parsed = parseDDMMYYYYToIso(text);
    if (parsed) {
      if (min && parsed < min) {
        setText(formatDateDDMMYYYY(value));
        return;
      }
      if (max && parsed > max) {
        setText(formatDateDDMMYYYY(value));
        return;
      }
      onChange(parsed);
      setText(formatDateDDMMYYYY(parsed));
    } else {
      setText(formatDateDDMMYYYY(value));
    }
  };

  return (
    <div style={{ position: 'relative', display: 'flex', alignItems: 'center', ...style }}>
      <input
        id={id}
        type="text"
        inputMode="numeric"
        className={className}
        style={{ width: '100%', paddingRight: 34, ...inputStyle }}
        value={text}
        disabled={disabled}
        placeholder={placeholder}
        aria-label={ariaLabel}
        maxLength={10}
        onChange={(e) => handleTextChange(e.target.value)}
        onBlur={handleBlur}
      />
      <div
        style={{
          position: 'absolute',
          right: 10,
          top: '50%',
          transform: 'translateY(-50%)',
          width: 22,
          height: 22,
          display: 'grid',
          placeItems: 'center',
          color: 'var(--color-muted-fg)',
        }}
      >
        <Calendar size={16} aria-hidden="true" />
        <input
          ref={pickerRef}
          type="date"
          tabIndex={-1}
          aria-hidden="true"
          value={value}
          min={min}
          max={max}
          disabled={disabled}
          onChange={(e) => {
            if (e.target.value) {
              onChange(e.target.value);
              setText(formatDateDDMMYYYY(e.target.value));
            }
          }}
          onClick={() => {
            try {
              pickerRef.current?.showPicker?.();
            } catch {
              // ignore when browser handles native click directly
            }
          }}
          style={{
            position: 'absolute',
            inset: 0,
            opacity: 0,
            cursor: disabled ? 'not-allowed' : 'pointer',
            width: '100%',
            height: '100%',
          }}
        />
      </div>
    </div>
  );
}

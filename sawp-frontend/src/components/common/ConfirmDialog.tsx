import { useEffect, useRef } from 'react';
import type { ReactNode } from 'react';

interface Props {
  open: boolean;
  title: string;
  children: ReactNode;
  confirmText?: string;
  cancelText?: string;
  busy?: boolean;
  onConfirm: () => void;
  onCancel: () => void;
}

/** Hộp xác nhận dùng thẻ <dialog> gốc của trình duyệt (có sẵn focus trap + Esc) – MSG45 */
export function ConfirmDialog({
  open,
  title,
  children,
  confirmText = 'Xác nhận',
  cancelText = 'Hủy',
  busy = false,
  onConfirm,
  onCancel,
}: Props) {
  const ref = useRef<HTMLDialogElement>(null);

  useEffect(() => {
    const d = ref.current;
    if (!d) return;
    if (open && !d.open) d.showModal();
    if (!open && d.open) d.close();
  }, [open]);

  return (
    <dialog
      ref={ref}
      className="dialog"
      aria-labelledby="confirm-title"
      onCancel={(e) => {
        e.preventDefault();
        onCancel();
      }}
    >
      <h2 id="confirm-title" className="dialog__title">
        {title}
      </h2>
      <div className="dialog__body">{children}</div>
      <div className="dialog__actions">
        <button id="confirm-cancel" type="button" className="btn btn--secondary" onClick={onCancel}>
          {cancelText}
        </button>
        <button id="confirm-ok" type="button" className="btn btn--primary" onClick={onConfirm} disabled={busy}>
          {confirmText}
        </button>
      </div>
    </dialog>
  );
}

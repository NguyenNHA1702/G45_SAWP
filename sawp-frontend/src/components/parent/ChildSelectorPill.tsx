import { useParentChild } from '../../contexts/ParentChildContext';
import './ChildSelectorPill.css';

export function ChildSelectorPill() {
  const { children, activeChild, selectChild, loading } = useParentChild();

  if (loading && !activeChild) {
    return <span className="child-selector__label" style={{ padding: '4px 10px', display: 'inline-flex', alignItems: 'center' }}>⏳ Đang tải...</span>;
  }

  if (children.length === 0) {
    return (
      <span className="badge badge--pink" style={{ padding: '4px 8px', fontSize: 12 }}>
        👶 Đang kết nối con...
      </span>
    );
  }

  return (
    <div className="child-selector" title="Chọn học sinh theo dõi">
      <span className="child-selector__icon" aria-hidden="true">👶</span>
      <span className="child-selector__label">Bé:</span>
      <select
        id="child-selector-dropdown"
        className="child-selector__select"
        value={activeChild?.studentId ?? ''}
        onChange={(e) => selectChild(Number(e.target.value))}
      >
        {children.map((child) => (
          <option key={child.studentId} value={child.studentId}>
            {child.fullName} — Lớp {child.currentClassName}
          </option>
        ))}
      </select>
    </div>
  );
}

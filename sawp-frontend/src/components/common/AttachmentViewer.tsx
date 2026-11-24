import React from 'react';
import { FileText, Image as ImageIcon, Paperclip, ExternalLink, Download } from 'lucide-react';
import { extractAttachmentFileName, openProtectedFile } from '../../utils/fileViewer';

interface AttachmentViewerProps {
  url?: string | null;
  compact?: boolean;
  label?: string;
}

export const AttachmentViewer: React.FC<AttachmentViewerProps> = ({
  url,
  compact = false,
  label = 'Tệp đính kèm',
}) => {
  if (!url || !url.trim()) return null;

  // Split multiple attachment URLs separated by ';' or ','
  const urls = url.split(/[;,]/).map((u) => u.trim()).filter(Boolean);
  if (urls.length === 0) return null;

  return (
    <div style={{ marginTop: 8 }}>
      <div style={{ display: 'flex', alignItems: 'center', gap: 6, marginBottom: compact ? 4 : 6 }}>
        <Paperclip size={14} color="#64748B" />
        <span style={{ fontSize: 12, fontWeight: 700, color: '#475569' }}>
          {label} ({urls.length}):
        </span>
      </div>

      <div style={{ display: 'flex', flexWrap: 'wrap', gap: 8 }}>
        {urls.map((fileUrl, index) => {
          const isImg = fileUrl.match(/\.(jpeg|jpg|png)($|\?|&)/i);
          const isPdf = fileUrl.match(/\.pdf($|\?|&)/i);
          const rawName = extractAttachmentFileName(fileUrl, `Tệp ${index + 1}`);
          const displayName = urls.length > 1 ? `Tệp ${index + 1} (${isImg ? 'Ảnh' : isPdf ? 'PDF' : rawName})` : rawName;

          return (
            <div
              key={index}
              style={{
                display: 'inline-flex',
                alignItems: 'center',
                gap: 6,
                padding: '4px 8px',
                borderRadius: 8,
                backgroundColor: '#EFF6FF',
                border: '1.5px solid #BFDBFE',
                color: '#1D4ED8',
                fontSize: 12,
                fontWeight: 600,
              }}
            >
              <button
                type="button"
                onClick={() => openProtectedFile(fileUrl, false, rawName)}
                style={{
                  display: 'inline-flex',
                  alignItems: 'center',
                  gap: 6,
                  border: 'none',
                  background: 'none',
                  padding: 0,
                  color: '#1D4ED8',
                  fontSize: 12,
                  fontWeight: 600,
                  cursor: 'pointer',
                }}
                title={`Nhấn để mở xem: ${rawName}`}
              >
                {isImg ? (
                  <ImageIcon size={14} color="#16A34A" />
                ) : (
                  <FileText size={14} color="#2563EB" />
                )}
                <span style={{ maxWidth: 200, overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>
                  {displayName}
                </span>
                <ExternalLink size={12} color="#64748B" />
              </button>

              <button
                type="button"
                onClick={() => openProtectedFile(fileUrl, true, rawName)}
                style={{
                  display: 'inline-flex',
                  alignItems: 'center',
                  gap: 3,
                  border: '1px solid #93C5FD',
                  borderRadius: 6,
                  backgroundColor: '#FFFFFF',
                  padding: '2px 6px',
                  color: '#1E40AF',
                  fontSize: 11,
                  fontWeight: 700,
                  cursor: 'pointer',
                }}
                title={`Tải về máy: ${rawName}`}
              >
                <Download size={11} /> Tải về
              </button>
            </div>
          );
        })}
      </div>
    </div>
  );
};

import React from 'react';
import { FileText, Image as ImageIcon, Paperclip, ExternalLink } from 'lucide-react';

interface AttachmentViewerProps {
  url?: string | null;
  compact?: boolean;
}

export const AttachmentViewer: React.FC<AttachmentViewerProps> = ({ url, compact = false }) => {
  if (!url || !url.trim()) return null;

  // Split multiple attachment URLs separated by ';' or ','
  const urls = url.split(/[;,]/).map((u) => u.trim()).filter(Boolean);
  if (urls.length === 0) return null;

  return (
    <div style={{ marginTop: 8 }}>
      <div style={{ display: 'flex', alignItems: 'center', gap: 6, marginBottom: compact ? 4 : 6 }}>
        <Paperclip size={14} color="#64748B" />
        <span style={{ fontSize: 12, fontWeight: 700, color: '#475569' }}>
          Tệp minh chứng đính kèm ({urls.length}):
        </span>
      </div>

      <div style={{ display: 'flex', flexWrap: 'wrap', gap: 8 }}>
        {urls.map((fileUrl, index) => {
          const isImg = fileUrl.match(/\.(jpeg|jpg|png)($|\?)/i);
          const isPdf = fileUrl.match(/\.pdf($|\?)/i);
          const rawName = fileUrl.split('/').pop()?.split('?')[0] || `Tệp ${index + 1}`;
          const displayName = urls.length > 1 ? `Minh chứng ${index + 1} (${isImg ? 'Ảnh' : isPdf ? 'PDF' : 'Tệp'})` : rawName;

          return (
            <a
              key={index}
              href={fileUrl}
              target="_blank"
              rel="noopener noreferrer"
              style={{
                display: 'inline-flex',
                alignItems: 'center',
                gap: 6,
                padding: '5px 10px',
                borderRadius: 8,
                backgroundColor: '#EFF6FF',
                border: '1.5px solid #BFDBFE',
                color: '#1D4ED8',
                fontSize: 12,
                fontWeight: 600,
                textDecoration: 'none',
                transition: 'all 0.15s ease',
              }}
              title={`Nhấn để mở xem: ${rawName}`}
            >
              {isImg ? (
                <ImageIcon size={14} color="#16A34A" />
              ) : (
                <FileText size={14} color="#2563EB" />
              )}
              <span style={{ maxWidth: 220, overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>
                {displayName}
              </span>
              <ExternalLink size={12} color="#64748B" />
            </a>
          );
        })}
      </div>
    </div>
  );
};

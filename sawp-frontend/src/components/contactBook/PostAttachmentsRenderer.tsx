import { ExternalLink, FileSpreadsheet, FileText, Play } from 'lucide-react';
import type { PostAttachmentDto } from '../../types/contactBook.types';

export function getYouTubeVideoId(url: string): string | null {
  if (!url) return null;
  const match = url.match(
    /(?:youtu\.be\/|youtube\.com\/(?:embed\/|v\/|watch\?v=|watch\?.+&v=|shorts\/))([\w-]{11})/
  );
  return match ? match[1] : null;
}

interface PostAttachmentsRendererProps {
  attachments?: PostAttachmentDto[];
}

export function PostAttachmentsRenderer({ attachments }: PostAttachmentsRendererProps) {
  if (!attachments || attachments.length === 0) return null;

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 12, marginBottom: 16 }}>
      {attachments.map((att, idx) => {
        const ytId = getYouTubeVideoId(att.fileUrl);
        const isVideo = att.fileType === 'Video' || !!ytId;

        // 1. YouTube Video Embed
        if (isVideo && ytId) {
          return (
            <div
              key={att.id || `yt-${idx}`}
              style={{
                borderRadius: 14,
                overflow: 'hidden',
                border: '2px solid var(--color-border)',
                background: '#0F172A',
                maxWidth: 520,
                width: '100%',
              }}
            >
              <div style={{ position: 'relative', width: '100%', aspectRatio: '16/9' }}>
                <iframe
                  src={`https://www.youtube.com/embed/${ytId}`}
                  title="Video YouTube"
                  style={{
                    position: 'absolute',
                    top: 0,
                    left: 0,
                    width: '100%',
                    height: '100%',
                    border: 0,
                  }}
                  allow="accelerometer; autoplay; clipboard-write; encrypted-media; gyroscope; picture-in-picture"
                  allowFullScreen
                />
              </div>
              <div
                style={{
                  padding: '8px 12px',
                  display: 'flex',
                  alignItems: 'center',
                  justifyContent: 'space-between',
                  background: '#1E293B',
                  color: '#F8FAFC',
                  fontSize: '0.8rem',
                }}
              >
                <span style={{ display: 'flex', alignItems: 'center', gap: 6, fontWeight: 600 }}>
                  <Play size={14} color="#EF4444" fill="#EF4444" /> Video YouTube
                </span>
                <a
                  href={att.fileUrl}
                  target="_blank"
                  rel="noreferrer"
                  style={{ color: '#93C5FD', display: 'flex', alignItems: 'center', gap: 4, textDecoration: 'none' }}
                >
                  Mở tab mới <ExternalLink size={12} />
                </a>
              </div>
            </div>
          );
        }

        // 2. Image
        const isImage =
          att.fileType === 'Image' ||
          /\.(jpg|jpeg|png|webp|gif|bmp|svg)(\?.*)?$/i.test(att.fileUrl);

        if (isImage) {
          return (
            <div
              key={att.id || `img-${idx}`}
              style={{
                maxWidth: 480,
                borderRadius: 12,
                overflow: 'hidden',
                border: '1.5px solid var(--color-border)',
                background: '#F8FAFC',
              }}
            >
              <img
                src={att.fileUrl}
                alt={att.fileName || 'Ảnh đính kèm'}
                style={{
                  width: '100%',
                  maxHeight: 320,
                  objectFit: 'contain',
                  display: 'block',
                  cursor: 'pointer',
                  background: '#F1F5F9',
                }}
                onClick={() => window.open(att.fileUrl, '_blank')}
                onError={(e) => {
                  const target = e.target as HTMLElement;
                  target.style.display = 'none';
                  const parent = target.parentElement;
                  if (parent) {
                    parent.innerHTML = `
                      <div style="padding: 12px; display: flex; alignItems: center; gap: 8px;">
                        <span>🔗</span>
                        <a href="${att.fileUrl}" target="_blank" rel="noreferrer" style="font-weight: 600; color: #2563EB; word-break: break-all;">
                          ${att.fileName || att.fileUrl}
                        </a>
                      </div>
                    `;
                  }
                }}
              />
            </div>
          );
        }

        // 3. Document or External Link
        const ext = (att.fileName || '').split('.').pop()?.toLowerCase();
        let icon = <FileText size={20} color="#2563EB" />;
        let fileLabel = 'Tài liệu';

        if (ext === 'pdf') {
          icon = <FileText size={20} color="#DC2626" />;
          fileLabel = 'PDF';
        } else if (ext === 'doc' || ext === 'docx') {
          icon = <FileText size={20} color="#2563EB" />;
          fileLabel = 'Word';
        } else if (ext === 'xls' || ext === 'xlsx') {
          icon = <FileSpreadsheet size={20} color="#16A34A" />;
          fileLabel = 'Excel';
        } else {
          icon = <ExternalLink size={20} color="#7C3AED" />;
          fileLabel = 'Liên kết';
        }

        return (
          <div
            key={att.id || `doc-${idx}`}
            style={{
              display: 'inline-flex',
              alignItems: 'center',
              gap: 12,
              padding: '10px 16px',
              borderRadius: 12,
              background: '#F8FAFC',
              border: '1.5px solid var(--color-border)',
              maxWidth: 480,
            }}
          >
            <div style={{ flexShrink: 0 }}>{icon}</div>
            <div style={{ flex: 1, minWidth: 0 }}>
              <div
                style={{
                  fontSize: '0.72rem',
                  fontWeight: 700,
                  color: 'var(--color-muted-fg)',
                  textTransform: 'uppercase',
                }}
              >
                {fileLabel}
              </div>
              <a
                href={att.fileUrl}
                target="_blank"
                rel="noreferrer"
                download={att.fileName}
                style={{
                  fontWeight: 600,
                  fontSize: '0.85rem',
                  color: '#1E293B',
                  textDecoration: 'none',
                  display: 'block',
                  overflow: 'hidden',
                  textOverflow: 'ellipsis',
                  whiteSpace: 'nowrap',
                }}
                title={att.fileName}
              >
                {att.fileName}
              </a>
            </div>
            <a
              href={att.fileUrl}
              target="_blank"
              rel="noreferrer"
              download={att.fileName}
              className="btn btn--outline btn--sm"
              style={{ flexShrink: 0, padding: '4px 10px', fontSize: '0.75rem' }}
            >
              Mở / Tải
            </a>
          </div>
        );
      })}
    </div>
  );
}

import { useState, useRef } from 'react';
import { UploadCloud, CheckCircle2, FileText, Image, Trash2, Link as LinkIcon, Loader2 } from 'lucide-react';
import toast from 'react-hot-toast';
import { administrativeRequestApi } from '../../services/implementations/administrativeRequestApi';

interface DirectFileUploaderProps {
  value: string;
  onChange: (url: string) => void;
  label?: string;
  hint?: string;
}

export function DirectFileUploader({
  value,
  onChange,
  label = 'Tệp / Ảnh minh chứng đính kèm:',
  hint = 'Hỗ trợ tải trực tiếp hình ảnh (.jpg, .png) hoặc tài liệu (.pdf, .doc, .docx). Dung lượng tối đa 15MB.',
}: DirectFileUploaderProps) {
  const [uploading, setUploading] = useState(false);
  const [showManualUrl, setShowManualUrl] = useState(false);
  const [uploadedFileName, setUploadedFileName] = useState<string>('');
  const fileInputRef = useRef<HTMLInputElement>(null);

  const handleFileChange = async (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    if (!file) return;

    if (file.size > 15 * 1024 * 1024) {
      toast.error('Kích thước tệp không được vượt quá 15MB.');
      return;
    }

    try {
      setUploading(true);
      const res = await administrativeRequestApi.uploadAttachment(file);
      if (res.success && res.data?.url) {
        onChange(res.data.url);
        setUploadedFileName(file.name);
        toast.success(`Đã tải lên tệp ${file.name} thành công!`);
      } else {
        toast.error(res.message || 'Không thể tải lên tệp.');
      }
    } catch {
      toast.error('Đã xảy ra lỗi khi tải lên tệp tin.');
    } finally {
      setUploading(false);
      if (fileInputRef.current) {
        fileInputRef.current.value = '';
      }
    }
  };

  const handleClear = () => {
    onChange('');
    setUploadedFileName('');
  };

  const isImage = value.match(/\.(jpeg|jpg|gif|png|webp)($|\?)/i);

  return (
    <div>
      <label style={{ display: 'block', fontWeight: 700, fontSize: 14, marginBottom: 6 }}>
        {label}
      </label>

      {/* Hidden file input */}
      <input
        ref={fileInputRef}
        type="file"
        style={{ display: 'none' }}
        accept="image/*,.pdf,.doc,.docx"
        onChange={handleFileChange}
      />

      {/* Box Upload */}
      {!value ? (
        <div
          style={{
            border: '2px dashed #94A3B8',
            borderRadius: 12,
            padding: '16px 20px',
            backgroundColor: '#F8FAFC',
            textAlign: 'center',
            cursor: uploading ? 'wait' : 'pointer',
            transition: 'all 0.15s ease',
          }}
          onClick={() => !uploading && fileInputRef.current?.click()}
        >
          {uploading ? (
            <div style={{ display: 'flex', flexDirection: 'column', alignItems: 'center', gap: 8 }}>
              <Loader2 size={28} className="animate-spin" color="#4F46E5" />
              <div style={{ fontSize: 13, fontWeight: 700, color: '#4F46E5' }}>
                Đang tải tệp lên hệ thống, vui lòng chờ...
              </div>
            </div>
          ) : (
            <div style={{ display: 'flex', flexDirection: 'column', alignItems: 'center', gap: 6 }}>
              <UploadCloud size={30} color="#4F46E5" />
              <div style={{ fontSize: 14, fontWeight: 700, color: '#1E293B' }}>
                Nhấn vào đây để tải ảnh hoặc tệp từ máy tính / điện thoại
              </div>
              <div style={{ fontSize: 12, color: '#64748B' }}>
                {hint}
              </div>
            </div>
          )}
        </div>
      ) : (
        /* Box đã có tệp */
        <div
          style={{
            border: '2px solid #1E293B',
            borderRadius: 12,
            padding: '12px 16px',
            backgroundColor: '#F0FDF4',
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'space-between',
            gap: 12,
            flexWrap: 'wrap',
          }}
        >
          <div style={{ display: 'flex', alignItems: 'center', gap: 10, minWidth: 200, flex: '1 1 auto' }}>
            {isImage ? (
              <Image size={24} color="#16A34A" />
            ) : (
              <FileText size={24} color="#16A34A" />
            )}
            <div>
              <div style={{ fontSize: 13, fontWeight: 800, color: '#14532D', display: 'flex', alignItems: 'center', gap: 6 }}>
                <CheckCircle2 size={16} color="#16A34A" />
                {uploadedFileName || 'Tệp minh chứng đã sẵn sàng'}
              </div>
              <a
                href={value}
                target="_blank"
                rel="noopener noreferrer"
                style={{ fontSize: 12, color: '#2563EB', textDecoration: 'underline', marginTop: 2, display: 'inline-block' }}
              >
                Mở xem tệp đã đính kèm
              </a>
            </div>
          </div>

          <div style={{ display: 'flex', gap: 8, alignItems: 'center' }}>
            <button
              type="button"
              className="btn btn--outline btn--sm"
              onClick={() => fileInputRef.current?.click()}
              style={{ fontSize: 12, padding: '5px 10px' }}
            >
              Chọn tệp khác
            </button>
            <button
              type="button"
              className="btn btn--danger btn--sm"
              onClick={handleClear}
              style={{ fontSize: 12, padding: '5px 10px', display: 'inline-flex', alignItems: 'center', gap: 4 }}
              title="Xóa tệp đính kèm"
            >
              <Trash2 size={14} /> Xóa
            </button>
          </div>
        </div>
      )}

      {/* Nút toggle nhập URL thủ công (fallback) */}
      <div style={{ marginTop: 6, display: 'flex', justifyContent: 'flex-end' }}>
        <button
          type="button"
          onClick={() => setShowManualUrl(!showManualUrl)}
          style={{
            background: 'none',
            border: 'none',
            color: '#64748B',
            fontSize: 12,
            fontWeight: 600,
            cursor: 'pointer',
            display: 'inline-flex',
            alignItems: 'center',
            gap: 4,
            textDecoration: 'underline',
          }}
        >
          <LinkIcon size={12} />
          {showManualUrl ? 'Ẩn ô dán link trực tiếp' : 'Hoặc dán đường dẫn link bên ngoài (Drive, Cloud...)'}
        </button>
      </div>

      {showManualUrl && (
        <div style={{ marginTop: 8 }}>
          <input
            type="text"
            className="input"
            style={{ width: '100%', borderRadius: 8, padding: '8px 12px', fontSize: 13 }}
            placeholder="https://..."
            value={value}
            onChange={(e) => {
              onChange(e.target.value);
              setUploadedFileName('');
            }}
          />
        </div>
      )}
    </div>
  );
}

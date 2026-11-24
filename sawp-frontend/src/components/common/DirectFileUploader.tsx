import { useState, useRef, useEffect } from 'react';
import { UploadCloud, FileText, Image, Trash2, Loader2 } from 'lucide-react';
import toast from 'react-hot-toast';
import { administrativeRequestApi } from '../../services/implementations/administrativeRequestApi';
import { extractAttachmentFileName, openProtectedFile } from '../../utils/fileViewer';

interface DirectFileUploaderProps {
  value: string;
  onChange: (url: string) => void;
  label?: string;
  hint?: string;
}

export function DirectFileUploader({
  value,
  onChange,
  label = 'Tệp minh chứng đính kèm (nếu có):',
  hint = 'Định dạng hợp lệ: PDF, JPG, PNG (tối đa 5 tệp, dung lượng 10MB/tệp).',
}: DirectFileUploaderProps) {
  const MAX_FILES = 5;
  const [uploading, setUploading] = useState(false);
  const [isDragging, setIsDragging] = useState(false);
  const fileInputRef = useRef<HTMLInputElement>(null);

  // File list parsed from semicolon-separated value
  const fileList = value ? value.split(';').filter(Boolean) : [];

  const uploadSingleFile = async (file: File): Promise<string | null> => {
    if (file.size > 10 * 1024 * 1024) {
      toast.error(`Tệp ${file.name} vượt quá dung lượng tối đa 10MB.`);
      return null;
    }

    const ext = file.name.split('.').pop()?.toLowerCase();
    const allowed = ['pdf', 'jpg', 'jpeg', 'png'];
    if (!ext || !allowed.includes(ext)) {
      toast.error(`Tệp ${file.name} không đúng định dạng. Chỉ chấp nhận tệp PDF, JPG, PNG.`);
      return null;
    }

    const res = await administrativeRequestApi.uploadAttachment(file);
    if (res.success && res.data?.url) {
      return res.data.url;
    } else {
      toast.error(res.message || `Không thể tải tệp ${file.name}`);
      return null;
    }
  };

  const handleFiles = async (files: FileList | File[]) => {
    if (!files || files.length === 0) return;

    if (fileList.length + files.length > MAX_FILES) {
      toast.error(`Chỉ được đính kèm tối đa ${MAX_FILES} tệp minh chứng. Hiện bạn đã có ${fileList.length} tệp.`);
      return;
    }

    try {
      setUploading(true);
      const newUrls: string[] = [];

      for (let i = 0; i < files.length; i++) {
        const file = files[i];
        const uploadedUrl = await uploadSingleFile(file);
        if (uploadedUrl) {
          newUrls.push(uploadedUrl);
        }
      }

      if (newUrls.length > 0) {
        const combined = [...fileList, ...newUrls].join(';');
        onChange(combined);
        toast.success(`Đã tải lên ${newUrls.length} tệp thành công!`);
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

  const handleFileChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    if (e.target.files) {
      handleFiles(e.target.files);
    }
  };

  // Clipboard paste support (Ctrl+V)
  useEffect(() => {
    const handlePaste = (e: ClipboardEvent) => {
      const items = e.clipboardData?.items;
      if (!items) return;

      const pastedFiles: File[] = [];
      for (let i = 0; i < items.length; i++) {
        if (items[i].type.startsWith('image/')) {
          const file = items[i].getAsFile();
          if (file) pastedFiles.push(file);
        }
      }

      if (pastedFiles.length > 0) {
        e.preventDefault();
        toast.loading('Đang tải ảnh từ bộ nhớ tạm...', { duration: 1500 });
        handleFiles(pastedFiles);
      }
    };

    window.addEventListener('paste', handlePaste);
    return () => window.removeEventListener('paste', handlePaste);
  }, [fileList]);

  const handleRemove = (indexToRemove: number) => {
    const updated = fileList.filter((_, idx) => idx !== indexToRemove);
    onChange(updated.join(';'));
  };

  return (
    <div>
      <label style={{ display: 'block', fontWeight: 700, fontSize: 13, marginBottom: 6 }}>
        {label} <span style={{ fontWeight: 400, color: '#64748B', fontSize: 12 }}>({hint})</span>
      </label>

      {/* Hidden file input */}
      <input
        ref={fileInputRef}
        type="file"
        multiple
        style={{ display: 'none' }}
        accept=".pdf,.jpg,.jpeg,.png,image/png,image/jpeg,application/pdf"
        onChange={handleFileChange}
      />

      {/* Upload Zone */}
      <div
        style={{
          border: isDragging ? '2px dashed #4F46E5' : '2px dashed #94A3B8',
          borderRadius: 12,
          padding: '18px 20px',
          backgroundColor: isDragging ? '#EEF2FF' : '#F8FAFC',
          textAlign: 'center',
          cursor: uploading ? 'wait' : 'pointer',
          transition: 'all 0.15s ease',
        }}
        onClick={() => !uploading && fileInputRef.current?.click()}
        onDragOver={(e) => {
          e.preventDefault();
          e.stopPropagation();
          setIsDragging(true);
        }}
        onDragEnter={(e) => {
          e.preventDefault();
          e.stopPropagation();
          setIsDragging(true);
        }}
        onDragLeave={(e) => {
          e.preventDefault();
          e.stopPropagation();
          setIsDragging(false);
        }}
        onDrop={(e) => {
          e.preventDefault();
          e.stopPropagation();
          setIsDragging(false);
          if (e.dataTransfer.files && e.dataTransfer.files.length > 0) {
            handleFiles(e.dataTransfer.files);
          }
        }}
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
            <UploadCloud size={32} color={isDragging ? '#4338CA' : '#4F46E5'} />
            <div style={{ fontSize: 14, fontWeight: 700, color: '#1E293B' }}>
              Kéo thả tệp vào đây hoặc Bấm để chọn tệp từ máy tính
            </div>
            <div style={{ fontSize: 12, color: '#64748B' }}>
              Định dạng hợp lệ: <strong>PDF, JPG, PNG</strong> (Tối đa 10MB/tệp). Hỗ trợ chọn hoặc kéo thả nhiều tệp.
            </div>
          </div>
        )}
      </div>

      {/* Render list of attached files */}
      {fileList.length > 0 && (
        <div style={{ marginTop: 12, display: 'flex', flexDirection: 'column', gap: 8 }}>
          <div style={{ fontSize: 12, fontWeight: 700, color: '#475569' }}>
            Danh sách tệp đã đính kèm ({fileList.length}):
          </div>
          {fileList.map((url, idx) => {
            const isImg = url.match(/\.(jpeg|jpg|png)($|\?|&)/i);
            const fileName = extractAttachmentFileName(url, `Tệp minh chứng ${idx + 1}`);

            return (
              <div
                key={idx}
                style={{
                  border: '1.5px solid #CBD5E1',
                  borderRadius: 10,
                  padding: '8px 12px',
                  backgroundColor: '#FFFFFF',
                  display: 'flex',
                  alignItems: 'center',
                  justifyContent: 'space-between',
                  gap: 10,
                }}
              >
                <div style={{ display: 'flex', alignItems: 'center', gap: 8, overflow: 'hidden' }}>
                  {isImg ? (
                    <Image size={20} color="#16A34A" style={{ flexShrink: 0 }} />
                  ) : (
                    <FileText size={20} color="#2563EB" style={{ flexShrink: 0 }} />
                  )}
                  <button
                    type="button"
                    onClick={() => openProtectedFile(url, false, fileName)}
                    style={{
                      border: 'none',
                      background: 'none',
                      padding: 0,
                      cursor: 'pointer',
                      fontSize: 13,
                      color: '#2563EB',
                      textDecoration: 'underline',
                      overflow: 'hidden',
                      textOverflow: 'ellipsis',
                      whiteSpace: 'nowrap',
                      textAlign: 'left',
                    }}
                    title={`Mở xem: ${fileName}`}
                  >
                    {fileName}
                  </button>
                </div>
                <button
                  type="button"
                  onClick={() => handleRemove(idx)}
                  style={{
                    border: 'none',
                    background: 'none',
                    color: '#EF4444',
                    cursor: 'pointer',
                    padding: 4,
                    display: 'flex',
                    alignItems: 'center',
                  }}
                  title="Xóa tệp này"
                >
                  <Trash2 size={16} />
                </button>
              </div>
            );
          })}
        </div>
      )}
    </div>
  );
}

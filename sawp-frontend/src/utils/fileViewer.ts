import toast from 'react-hot-toast';
import { apiClient } from '../services/apiClient';

function inferMimeType(url: string, headerContentType?: string): string {
  if (headerContentType && !headerContentType.includes('application/octet-stream')) {
    return headerContentType;
  }
  const lower = url.toLowerCase();
  if (lower.includes('.pdf')) return 'application/pdf';
  if (lower.includes('.png')) return 'image/png';
  if (lower.includes('.jpg') || lower.includes('.jpeg')) return 'image/jpeg';
  return 'application/octet-stream';
}

export function extractAttachmentFileName(fileUrl: string, fallback = 'Tai_lieu_dinh_kem'): string {
  try {
    const qIndex = fileUrl.indexOf('path=');
    if (qIndex >= 0) {
      const encodedPath = fileUrl.slice(qIndex + 5).split('&')[0];
      const decoded = decodeURIComponent(encodedPath);
      const base = decoded.split('/').pop() || fallback;
      // Remove GUID prefix if present (36 chars + '_')
      return base.length > 37 && base[36] === '_' ? base.slice(37) : base;
    }
    const clean = fileUrl.split('/').pop()?.split('?')[0];
    return clean || fallback;
  } catch {
    return fallback;
  }
}

/**
 * Opens or downloads a file URL.
 * Internal `/api/...` endpoints require the JWT Authorization header, so we fetch them via `apiClient` as a Blob
 * instead of relying on `<a href>` (which omits Authorization and fails with 401).
 */
export async function openProtectedFile(fileUrl: string, forceDownload = false, customFileName?: string): Promise<void> {
  if (!fileUrl || !fileUrl.trim()) return;
  const trimmed = fileUrl.trim();

  if (trimmed.startsWith('http://') || trimmed.startsWith('https://')) {
    window.open(trimmed, '_blank', 'noopener,noreferrer');
    return;
  }

  try {
    // apiClient already has baseURL ending with /api
    const relativeEndpoint = trimmed.startsWith('/api/')
      ? trimmed.slice(4)
      : trimmed.startsWith('/api')
        ? trimmed.slice(4)
        : trimmed;

    const res = await apiClient.get(relativeEndpoint, { responseType: 'blob' });
    const headerType = typeof res.headers?.['content-type'] === 'string' ? res.headers['content-type'] : undefined;
    const mimeType = inferMimeType(trimmed, headerType);
    const blob = new Blob([res.data], { type: mimeType });
    const blobUrl = window.URL.createObjectURL(blob);

    if (forceDownload) {
      const link = document.createElement('a');
      link.href = blobUrl;
      link.setAttribute('download', customFileName || extractAttachmentFileName(trimmed));
      document.body.appendChild(link);
      link.click();
      link.remove();
      setTimeout(() => window.URL.revokeObjectURL(blobUrl), 10000);
    } else {
      const opened = window.open(blobUrl, '_blank', 'noopener,noreferrer');
      if (!opened) {
        // Fallback download if browser popup blocker prevented opening new tab
        const link = document.createElement('a');
        link.href = blobUrl;
        link.setAttribute('download', customFileName || extractAttachmentFileName(trimmed));
        document.body.appendChild(link);
        link.click();
        link.remove();
      }
      setTimeout(() => window.URL.revokeObjectURL(blobUrl), 60000);
    }
  } catch (err: any) {
    const status = err?.response?.status;
    if (status === 404) {
      toast.error('Không tìm thấy tệp văn bản trên máy chủ.');
    } else if (status === 401 || status === 403) {
      toast.error('Bạn không có quyền xem hoặc tải tệp này.');
    } else {
      toast.error('Không thể mở tệp văn bản. Vui lòng thử lại sau.');
    }
  }
}

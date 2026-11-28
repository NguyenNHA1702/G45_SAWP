import { useEffect, useMemo, useState } from 'react';
import {
  Award,
  Check,
  CheckCircle2,
  Heart,
  MessageCircle,
  Newspaper,
  Pencil,
  Pin,
  Plus,
  RefreshCw,
  Send,
  Sparkles,
  Trash2,
  Users,
} from 'lucide-react';
import toast from 'react-hot-toast';
import { contactBookApi } from '../../services/implementations/contactBookApi';
import { classApi } from '../../services/implementations/classApi';
import { GeometricModal } from '../../components/common/GeometricModal';
import { PostAttachmentsRenderer } from '../../components/contactBook/PostAttachmentsRenderer';
import type {
  ClassPostDto,
  StudentEvaluationRosterItemDto,
  CreateStudentEvaluationDto,
  PostAttachmentInputDto,
} from '../../types/contactBook.types';
import type { ClassRosterDto } from '../../types/class.types';
import { getSystemMessage } from '../../constants/messages';
import { formatViFullDateTime } from '../../utils/date';

type TabKey = 'feed' | 'evaluations';

const AVAILABLE_BADGES = [
  '🌟 Phát biểu sôi nổi',
  '🤝 Giúp đỡ bạn bè',
  '🍱 Tự giác ăn hết suất',
  '⏰ Đi học đúng giờ',
  '📚 Chăm đọc sách',
  '🧹 Giữ vệ sinh chung',
  '🎨 Sáng tạo trong học tập',
  '💪 Nỗ lực tiến bộ',
];

interface SchoolWeekOption {
  weekNumber: number;
  label: string;
  isFuture: boolean;
}

function computeSchoolWeeks(startDateStr?: string, endDateStr?: string): {
  weeks: SchoolWeekOption[];
  currentWeek: number;
} {
  const start = startDateStr ? new Date(startDateStr) : new Date(2026, 8, 1);
  const end = endDateStr ? new Date(endDateStr) : new Date(2027, 5, 30);
  const startDay = new Date(start.getFullYear(), start.getMonth(), start.getDate());
  const endDay = new Date(end.getFullYear(), end.getMonth(), end.getDate());

  const diffToMonday = (startDay.getDay() + 6) % 7;
  const week1Monday = new Date(startDay);
  week1Monday.setDate(startDay.getDate() - diffToMonday);

  const msPerDay = 86400000;
  const totalDays = Math.max(7, Math.ceil((endDay.getTime() - week1Monday.getTime()) / msPerDay) + 1);
  const totalWeeks = Math.min(52, Math.max(1, Math.ceil(totalDays / 7)));

  const now = new Date();
  const today = new Date(now.getFullYear(), now.getMonth(), now.getDate());
  const daysSinceStart = Math.floor((today.getTime() - week1Monday.getTime()) / msPerDay);
  const currentWeek = Math.min(totalWeeks, Math.max(1, Math.floor(daysSinceStart / 7) + 1));

  const fmt = (d: Date) =>
    `${String(d.getDate()).padStart(2, '0')}/${String(d.getMonth() + 1).padStart(2, '0')}`;

  const weeks: SchoolWeekOption[] = [];
  for (let w = 1; w <= totalWeeks; w++) {
    const wStart = new Date(week1Monday);
    wStart.setDate(week1Monday.getDate() + (w - 1) * 7);
    const wEnd = new Date(wStart);
    wEnd.setDate(wStart.getDate() + 6);
    weeks.push({
      weekNumber: w,
      label: `Tuần ${w} (${fmt(wStart)} – ${fmt(wEnd)})`,
      isFuture: w > currentWeek,
    });
  }
  return { weeks, currentWeek };
}

export default function TeacherContactBookPage() {
  const [homeroom, setHomeroom] = useState<ClassRosterDto | null>(null);
  const [loadingHomeroom, setLoadingHomeroom] = useState(false);
  const [activeTab, setActiveTab] = useState<TabKey>('feed');

  // Tab 1: Posts
  const [posts, setPosts] = useState<ClassPostDto[]>([]);
  const [loadingPosts, setLoadingPosts] = useState(false);
  const [showCreateModal, setShowCreateModal] = useState(false);
  const [editingPost, setEditingPost] = useState<ClassPostDto | null>(null);
  const [postTitle, setPostTitle] = useState('');
  const [postContent, setPostContent] = useState('');
  const [postCategory] = useState('General');
  const [postIsPinned, setPostIsPinned] = useState(false);
  const [postAllowComments, setPostAllowComments] = useState(true);
  const [postImageUrl, setPostImageUrl] = useState('');
  const [postAttachments, setPostAttachments] = useState<PostAttachmentInputDto[]>([]);
  const [uploadingFiles, setUploadingFiles] = useState(false);
  const [submittingPost, setSubmittingPost] = useState(false);

  // Tab 2: Evaluations
  const { weeks: schoolWeeks, currentWeek } = useMemo(
    () => computeSchoolWeeks(homeroom?.schoolYearStartDate, homeroom?.schoolYearEndDate),
    [homeroom?.schoolYearStartDate, homeroom?.schoolYearEndDate]
  );
  const [weekNumber, setWeekNumber] = useState<number>(() => computeSchoolWeeks().currentWeek);
  const [roster, setRoster] = useState<StudentEvaluationRosterItemDto[]>([]);
  const [loadingRoster, setLoadingRoster] = useState(false);
  const [selectedStudent, setSelectedStudent] = useState<StudentEvaluationRosterItemDto | null>(null);
  const [evalRating, setEvalRating] = useState('Good');
  const [evalBadges, setEvalBadges] = useState<string[]>([]);
  const [evalNote, setEvalNote] = useState('');
  const [evalRemind, setEvalRemind] = useState('');
  const [submittingEval, setSubmittingEval] = useState(false);

  useEffect(() => {
    loadHomeroom();
  }, []);

  const loadHomeroom = async () => {
    setLoadingHomeroom(true);
    try {
      const res = await classApi.getMyHomeroomClass();
      if (res.success && res.data) {
        setHomeroom(res.data);
        const { currentWeek: cw } = computeSchoolWeeks(res.data.schoolYearStartDate, res.data.schoolYearEndDate);
        setWeekNumber(cw);
        loadPosts(res.data.classId);
        loadRoster(res.data.classId, cw);
      }
    } catch {
      toast.error('Lỗi khi tải thông tin lớp chủ nhiệm.');
    } finally {
      setLoadingHomeroom(false);
    }
  };

  const loadPosts = async (classId: number) => {
    setLoadingPosts(true);
    try {
      const res = await contactBookApi.getClassPosts(classId);
      if (res.success && res.data) {
        setPosts(res.data);
      }
    } catch {
      // silent
    } finally {
      setLoadingPosts(false);
    }
  };

  const loadRoster = async (classId: number, week: number) => {
    setLoadingRoster(true);
    try {
      const res = await contactBookApi.getClassEvaluationsRoster(classId, week);
      if (res.success && res.data) {
        setRoster(res.data);
      }
    } catch {
      // silent
    } finally {
      setLoadingRoster(false);
    }
  };

  const handleFileChange = async (e: React.ChangeEvent<HTMLInputElement>) => {
    const files = e.target.files;
    if (!files || files.length === 0) return;

    if (postAttachments.length + files.length > 10) {
      toast.error('Mỗi bài viết chỉ được đính kèm tối đa 10 tệp.');
      e.target.value = '';
      return;
    }

    const allowedExtensions = ['.jpg', '.jpeg', '.png', '.pdf'];
    const newAttachments: PostAttachmentInputDto[] = [];

    for (let i = 0; i < files.length; i++) {
      const file = files[i];
      if (file.name.length > 150) {
        toast.error('Tên tệp không được vượt quá 150 ký tự.');
        e.target.value = '';
        return;
      }
      const ext = '.' + file.name.split('.').pop()?.toLowerCase();

      // BR-50: Giới hạn kích thước tệp đính kèm <= 5MB
      if (file.size > 5 * 1024 * 1024) {
        toast.error(getSystemMessage('MSG41')?.content || 'Tệp không hợp lệ, không đúng định dạng hoặc vượt quá dung lượng cho phép.');
        e.target.value = '';
        return;
      }

      // BR-50: Chặn các định dạng tệp nguy hại hoặc không hỗ trợ (ví dụ: .exe)
      if (!allowedExtensions.includes(ext)) {
        toast.error(getSystemMessage('MSG41')?.content || 'Tệp không hợp lệ, không đúng định dạng hoặc vượt quá dung lượng cho phép.');
        e.target.value = '';
        return;
      }
    }

    setUploadingFiles(true);
    try {
      for (let i = 0; i < files.length; i++) {
        const file = files[i];
        const res = await contactBookApi.uploadAttachment(file);
        if (res.success && res.data) {
          newAttachments.push({
            fileUrl: res.data.url,
            fileName: res.data.fileName,
            fileType: res.data.fileType,
            fileSizeBytes: res.data.fileSize,
          });
        } else {
          toast.error(res.message || 'Lỗi khi tải tệp lên');
        }
      }
      setPostAttachments((prev) => [...prev, ...newAttachments]);
    } catch {
      toast.error('Lỗi khi tải tệp lên');
    } finally {
      setUploadingFiles(false);
      e.target.value = '';
    }
  };

  const openCreatePostModal = () => {
    setEditingPost(null);
    setPostTitle('');
    setPostContent('');
    setPostIsPinned(false);
    setPostAllowComments(true);
    setPostImageUrl('');
    setPostAttachments([]);
    setShowCreateModal(true);
  };

  const openEditPostModal = (post: ClassPostDto) => {
    setEditingPost(post);
    setPostTitle(post.title);
    setPostContent(post.content);
    setPostIsPinned(post.isPinned);
    setPostAllowComments(post.allowComments);
    setPostImageUrl('');
    setPostAttachments([]);
    setShowCreateModal(true);
  };

  const handleCreatePost = async () => {
    const trimmedTitle = postTitle.trim();
    const trimmedContent = postContent.trim();
    if (trimmedTitle.length < 5 || trimmedTitle.length > 200) {
      toast.error('Tiêu đề bài viết phải từ 5 đến 200 ký tự.');
      return;
    }
    if (trimmedContent.length < 10 || trimmedContent.length > 5000) {
      toast.error('Nội dung bài viết phải từ 10 đến 5000 ký tự.');
      return;
    }
    if (!homeroom) return;

    setSubmittingPost(true);
    try {
      const res = editingPost
        ? await contactBookApi.updateClassPost(editingPost.id, {
            title: trimmedTitle,
            content: trimmedContent,
            category: editingPost.category || postCategory,
            isPinned: postIsPinned,
            allowComments: postAllowComments,
          })
        : await contactBookApi.createClassPost({
            classId: homeroom.classId,
            title: trimmedTitle,
            content: trimmedContent,
            category: postCategory,
            isPinned: postIsPinned,
            allowComments: postAllowComments,
            attachmentUrls: postImageUrl.trim() ? [postImageUrl.trim()] : undefined,
            attachments: postAttachments.length > 0 ? postAttachments : undefined,
          });

      if (res.success) {
        toast.success(
          editingPost
            ? 'Cập nhật bài viết thành công.'
            : getSystemMessage('MSG51')?.content || 'Đăng bài viết lên bảng tin lớp thành công.'
        );
        setShowCreateModal(false);
        setEditingPost(null);
        setPostTitle('');
        setPostContent('');
        setPostImageUrl('');
        setPostAttachments([]);
        loadPosts(homeroom.classId);
      } else {
        toast.error(res.errors?.[0] || res.message || 'Không thể lưu bài viết.');
      }
    } catch (err: any) {
      toast.error(err?.response?.data?.errors?.[0] || err?.response?.data?.message || 'Không thể lưu bài viết lên bảng tin.');
    } finally {
      setSubmittingPost(false);
    }
  };

  const handleTogglePin = async (postId: number) => {
    try {
      await contactBookApi.togglePinPost(postId);
      toast.success('Đã cập nhật trạng thái ghim.');
      if (homeroom) loadPosts(homeroom.classId);
    } catch {
      toast.error('Lỗi khi thao tác ghim.');
    }
  };

  const handleToggleCommentsLock = async (postId: number) => {
    try {
      const res = await contactBookApi.toggleCommentsLock(postId);
      toast.success(res.data ? 'Đã mở bình luận cho bài viết.' : 'Đã khóa bình luận cho bài viết.');
      if (homeroom) loadPosts(homeroom.classId);
    } catch {
      toast.error('Lỗi khi cập nhật trạng thái bình luận.');
    }
  };

  const handleDeletePost = async (postId: number) => {
    if (!window.confirm(getSystemMessage('MSG45')?.content || 'Bạn có chắc chắn muốn xóa bài viết này?')) {
      return;
    }
    try {
      await contactBookApi.deleteClassPost(postId);
      toast.success('Đã xóa bài viết.');
      if (homeroom) loadPosts(homeroom.classId);
    } catch {
      toast.error('Lỗi khi xóa bài viết.');
    }
  };

  const handleDeleteComment = async (commentId: number) => {
    if (!window.confirm(getSystemMessage('MSG45')?.content || 'Bạn có chắc chắn muốn thực hiện thao tác này?')) {
      return;
    }
    try {
      await contactBookApi.deleteComment(commentId);
      toast.success('Đã xóa bình luận.');
      if (homeroom) loadPosts(homeroom.classId);
    } catch {
      toast.error('Lỗi khi xóa bình luận.');
    }
  };

  const openEvaluationModal = (item: StudentEvaluationRosterItemDto) => {
    setSelectedStudent(item);
    if (item.evaluation) {
      setEvalRating(item.evaluation.rating || 'Good');
      setEvalBadges(item.evaluation.badges || []);
      setEvalNote(item.evaluation.teacherNote || '');
      setEvalRemind(item.evaluation.privateRemind || '');
    } else {
      setEvalRating('Good');
      setEvalBadges(['🌟 Phát biểu sôi nổi', '🤝 Giúp đỡ bạn bè']);
      setEvalNote('Em chăm ngoan, hoàn thành tốt nhiệm vụ học tập trong tuần.');
      setEvalRemind('');
    }
  };

  const toggleBadge = (badge: string) => {
    setEvalBadges((prev) =>
      prev.includes(badge) ? prev.filter((b) => b !== badge) : [...prev, badge]
    );
  };

  const handleSaveEvaluation = async () => {
    if (!selectedStudent || !homeroom) return;
    const trimmedNote = evalNote.trim();
    if (!evalRating) {
      toast.error('Vui lòng chọn xếp loại nề nếp.');
      return;
    }
    if (trimmedNote.length < 10 || trimmedNote.length > 1500) {
      toast.error('Lời nhận xét của giáo viên phải từ 10 đến 1500 ký tự.');
      return;
    }
    if (evalRemind.trim().length > 500) {
      toast.error('Dặn dò riêng tối đa 500 ký tự.');
      return;
    }
    if (weekNumber > currentWeek) {
      toast.error(`Không thể nhận xét cho tuần tương lai (hiện tại là tuần ${currentWeek}).`);
      return;
    }

    setSubmittingEval(true);
    try {
      const payload: CreateStudentEvaluationDto = {
        id: selectedStudent.evaluation?.id,
        studentId: selectedStudent.studentId,
        classId: homeroom.classId,
        weekNumber,
        evaluationDate: new Date().toISOString(),
        rating: evalRating,
        badges: evalBadges,
        teacherNote: evalNote.trim(),
        privateRemind: evalRemind.trim() || undefined,
      };

      const res = await contactBookApi.createOrUpdateEvaluation(payload);
      if (res.success) {
        toast.success(getSystemMessage('MSG52')?.content || 'Đã lưu và gửi phiếu đánh giá rèn luyện của học sinh.');
        setSelectedStudent(null);
        loadRoster(homeroom.classId, weekNumber);
      } else {
        toast.error(res.errors?.[0] || res.message || 'Không thể lưu nhận xét học sinh.');
      }
    } catch (err: any) {
      toast.error(err?.response?.data?.errors?.[0] || err?.response?.data?.message || 'Không thể lưu nhận xét học sinh.');
    } finally {
      setSubmittingEval(false);
    }
  };

  if (loadingHomeroom) {
    return (
      <div className="container" style={{ textAlign: 'center', padding: '60px 0' }}>
        <RefreshCw size={36} className="spin" color="var(--color-accent)" style={{ margin: '0 auto 16px' }} />
        <div style={{ fontWeight: 700 }}>Đang tải dữ liệu lớp chủ nhiệm...</div>
      </div>
    );
  }

  if (!homeroom) {
    return (
      <div className="container">
        <div className="card" style={{ textAlign: 'center', padding: 48, marginTop: 32 }}>
          <Users size={48} color="var(--color-muted-fg)" style={{ margin: '0 auto 16px' }} />
          <h3>Bạn chưa được phân công làm Giáo viên chủ nhiệm</h3>
          <p style={{ color: 'var(--color-muted-fg)' }}>
            Chức năng quản lý Sổ liên lạc lớp học chỉ dành cho Giáo viên chủ nhiệm của lớp trong năm học hiện tại (BR-57).
          </p>
        </div>
      </div>
    );
  }

  return (
    <div className="container pop-in">
      {/* Decorative Memphis shapes */}
      <span className="decor decor--circle" style={{ top: -20, right: 30 }} aria-hidden="true" />
      <span className="decor decor--triangle" style={{ bottom: 20, left: 10 }} aria-hidden="true" />

      {/* Header */}
      <header className="page-header" style={{ marginBottom: 20 }}>
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', flexWrap: 'wrap', gap: 16 }}>
          <div>
            <span className="badge badge--pink" style={{ marginBottom: 8 }}>
              <Sparkles size={14} /> Quản lý Sổ liên lạc điện tử (GVCN)
            </span>
            <h1 style={{ margin: '4px 0 8px' }}>
              Lớp chủ nhiệm <span className="squiggle">{homeroom.className}</span>
            </h1>
            <p style={{ color: 'var(--color-muted-fg)', margin: 0 }}>
              Sĩ số: <strong>{homeroom.totalEnrolled ?? homeroom.totalStudents ?? homeroom.students?.length ?? 0} học sinh</strong> · Năm học: <strong>{homeroom.academicYear || homeroom.schoolYearName || '—'}</strong>
            </p>
          </div>

          {activeTab === 'feed' && (
            <button
              type="button"
              className="btn btn--primary"
              onClick={openCreatePostModal}
            >
              <Plus size={18} /> Đăng bài viết mới
            </button>
          )}
        </div>
      </header>

      {/* Navigation Tabs */}
      <div style={{ display: 'flex', gap: 12, marginBottom: 24, borderBottom: '2px solid var(--color-border)', paddingBottom: 8 }}>
        <button
          type="button"
          className={`btn ${activeTab === 'feed' ? 'btn--primary' : 'btn--outline'}`}
          onClick={() => setActiveTab('feed')}
        >
          <Newspaper size={16} /> Bảng tin lớp ({posts.length})
        </button>
        <button
          type="button"
          className={`btn ${activeTab === 'evaluations' ? 'btn--primary' : 'btn--outline'}`}
          onClick={() => setActiveTab('evaluations')}
        >
          <Award size={16} /> Sổ nhận xét học sinh (Tuần {weekNumber})
        </button>
      </div>

      {/* TAB 1: BẢNG TIN LỚP */}
      {activeTab === 'feed' && (
        <div>
          {loadingPosts ? (
            <div className="card" style={{ textAlign: 'center', padding: 48 }}>
              <RefreshCw size={28} className="spin" color="var(--color-accent)" style={{ margin: '0 auto 12px' }} />
              <div>Đang tải bài viết...</div>
            </div>
          ) : posts.length === 0 ? (
            <div className="card" style={{ textAlign: 'center', padding: 48 }}>
              <Newspaper size={48} color="var(--color-muted-fg)" style={{ margin: '0 auto 16px' }} />
              <h3>Lớp chưa có bài đăng nào</h3>
              <p style={{ color: 'var(--color-muted-fg)', maxWidth: 460, margin: '0 auto 16px' }}>
                Hãy đăng thông báo dặn dò chuẩn bị đồ dùng học tập hoặc ảnh hoạt động để phụ huynh nắm bắt thông tin.
              </p>
              <button
                type="button"
                className="btn btn--primary btn--sm"
                onClick={openCreatePostModal}
              >
                <Plus size={16} /> Đăng bài viết đầu tiên
              </button>
            </div>
          ) : (
            <div style={{ display: 'flex', flexDirection: 'column', gap: 20 }}>
              {posts.map((post) => (
                <div key={post.id} className="card">
                  <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', marginBottom: 12 }}>
                    <div>
                      <div style={{ display: 'flex', alignItems: 'center', gap: 8, marginBottom: 4 }}>
                        <span className="badge badge--yellow">{post.category}</span>
                        {post.isPinned && (
                          <span
                            style={{
                              display: 'inline-flex',
                              alignItems: 'center',
                              gap: 4,
                              background: '#FEF08A',
                              color: '#854D0E',
                              padding: '2px 8px',
                              borderRadius: 999,
                              fontSize: '0.75rem',
                              fontWeight: 700,
                            }}
                          >
                            <Pin size={12} /> Đã ghim
                          </span>
                        )}
                      </div>
                      <h3 style={{ margin: '4px 0 2px' }}>{post.title}</h3>
                      <div style={{ fontSize: '0.8rem', color: 'var(--color-muted-fg)' }}>
                        Đăng lúc: {formatViFullDateTime(post.createdAt)}
                      </div>
                    </div>

                    <div style={{ display: 'flex', gap: 8, flexWrap: 'wrap' }}>
                      <button
                        type="button"
                        className="btn btn--outline btn--sm"
                        onClick={() => openEditPostModal(post)}
                        title="Chỉnh sửa bài viết"
                      >
                        <Pencil size={14} /> Sửa
                      </button>
                      <button
                        type="button"
                        className="btn btn--outline btn--sm"
                        onClick={() => handleTogglePin(post.id)}
                        title={post.isPinned ? 'Gỡ ghim' : 'Ghim lên đầu'}
                      >
                        <Pin size={14} /> {post.isPinned ? 'Gỡ ghim' : 'Ghim'}
                      </button>
                      <button
                        type="button"
                        className="btn btn--outline btn--sm"
                        onClick={() => handleToggleCommentsLock(post.id)}
                        title={post.allowComments ? 'Khóa bình luận' : 'Mở bình luận'}
                      >
                        {post.allowComments ? 'Khóa BL' : 'Mở BL'}
                      </button>
                      <button
                        type="button"
                        className="btn btn--outline btn--sm"
                        style={{ color: '#DC2626', borderColor: '#FCA5A5' }}
                        onClick={() => handleDeletePost(post.id)}
                        title="Xóa bài viết"
                      >
                        <Trash2 size={14} />
                      </button>
                    </div>
                  </div>

                  <p style={{ whiteSpace: 'pre-line', lineHeight: 1.6, color: '#334155', margin: '0 0 16px' }}>
                    {post.content}
                  </p>

                  {/* Attachments */}
                  <PostAttachmentsRenderer attachments={post.attachments} />

                  {/* Reaction & Comments bar */}
                  <div style={{ display: 'flex', alignItems: 'center', gap: 16, paddingTop: 10, borderTop: '1px solid var(--color-border)' }}>
                    <span style={{ fontSize: '0.85rem', color: 'var(--color-muted-fg)', display: 'flex', alignItems: 'center', gap: 4 }}>
                      <Heart size={15} color="#DC2626" fill="#DC2626" /> {post.reactionCount} lượt thả tim
                    </span>
                    <span style={{ fontSize: '0.85rem', color: 'var(--color-muted-fg)', display: 'flex', alignItems: 'center', gap: 4 }}>
                      <MessageCircle size={15} /> {post.comments.length} bình luận
                    </span>
                    {!post.allowComments && (
                      <span style={{ fontSize: '0.75rem', color: '#B45309', fontWeight: 600 }}>
                        (Đã tắt bình luận)
                      </span>
                    )}
                  </div>

                  {/* Comments list with delete action */}
                  {post.comments.length > 0 && (
                    <div style={{ marginTop: 12, display: 'flex', flexDirection: 'column', gap: 8 }}>
                      {post.comments.map((c) => (
                        <div
                          key={c.id}
                          style={{
                            background: '#F8FAFC',
                            padding: '8px 12px',
                            borderRadius: 10,
                            display: 'flex',
                            justifyContent: 'space-between',
                            alignItems: 'center',
                          }}
                        >
                          <div>
                            <div style={{ fontSize: '0.8rem', fontWeight: 700 }}>
                              {c.authorName} <span style={{ fontWeight: 400, color: 'var(--color-muted-fg)' }}>({c.authorRoleLabel})</span>
                            </div>
                            <div style={{ fontSize: '0.875rem', color: '#334155' }}>{c.content}</div>
                          </div>
                          <button
                            type="button"
                            onClick={() => handleDeleteComment(c.id)}
                            style={{
                              background: 'transparent',
                              border: 'none',
                              color: '#94A3B8',
                              cursor: 'pointer',
                              padding: 4,
                            }}
                            title="Xóa bình luận"
                          >
                            <Trash2 size={14} />
                          </button>
                        </div>
                      ))}
                    </div>
                  )}
                </div>
              ))}
            </div>
          )}
        </div>
      )}

      {/* TAB 2: SỔ NHẬN XÉT HỌC SINH */}
      {activeTab === 'evaluations' && (
        <div>
          {/* Week Selector */}
          <div className="card" style={{ marginBottom: 20, display: 'flex', alignItems: 'center', justifyContent: 'space-between', flexWrap: 'wrap', gap: 12 }}>
            <div style={{ display: 'flex', alignItems: 'center', gap: 12 }}>
              <span style={{ fontWeight: 700 }}>Chọn tuần học:</span>
              <select
                className="input"
                style={{ width: 240 }}
                value={weekNumber}
                onChange={(e) => {
                  const w = Number(e.target.value);
                  setWeekNumber(w);
                  if (homeroom) loadRoster(homeroom.classId, w);
                }}
              >
                {schoolWeeks.map((w) => (
                  <option key={w.weekNumber} value={w.weekNumber} disabled={w.isFuture}>
                    {w.label}{w.isFuture ? ' (Chưa đến)' : ''}
                  </option>
                ))}
              </select>
            </div>

            <div style={{ fontSize: '0.875rem', color: 'var(--color-muted-fg)' }}>
              Đã nhận xét:{' '}
              <strong>{roster.filter((r) => r.evaluation !== null).length}</strong> / {roster.length} học sinh
            </div>
          </div>

          {/* Roster Table */}
          {loadingRoster ? (
            <div className="card" style={{ textAlign: 'center', padding: 48 }}>
              <RefreshCw size={28} className="spin" color="var(--color-accent)" style={{ margin: '0 auto 12px' }} />
              <div>Đang tải danh sách học sinh...</div>
            </div>
          ) : (
            <>
              <div className="card desktop-table-wrap" style={{ padding: 0, overflow: 'hidden' }}>
                <table style={{ width: '100%', borderCollapse: 'collapse', textAlign: 'left' }}>
                  <thead>
                    <tr style={{ background: '#F8FAFC', borderBottom: '2px solid var(--color-border)' }}>
                      <th style={{ padding: '12px 16px' }}>Học sinh</th>
                      <th style={{ padding: '12px 16px' }}>Đánh giá nề nếp</th>
                      <th style={{ padding: '12px 16px' }}>Huy hiệu</th>
                      <th style={{ padding: '12px 16px' }}>Xác nhận PH</th>
                      <th style={{ padding: '12px 16px', textAlign: 'right' }}>Thao tác</th>
                    </tr>
                  </thead>
                  <tbody>
                    {roster.map((item) => (
                      <tr key={item.studentId} style={{ borderBottom: '1px solid var(--color-border)' }}>
                        <td style={{ padding: '12px 16px' }}>
                          <div style={{ fontWeight: 700 }}>{item.studentName}</div>
                          <div style={{ fontSize: '0.8rem', color: 'var(--color-muted-fg)' }}>{item.studentCode}</div>
                        </td>

                        <td style={{ padding: '12px 16px' }}>
                          {item.evaluation ? (
                            <span
                              className={`badge ${
                                item.evaluation.rating === 'Excellent'
                                  ? 'badge--pink'
                                  : item.evaluation.rating === 'Good'
                                  ? 'badge--green'
                                  : 'badge--yellow'
                              }`}
                            >
                              {item.evaluation.rating === 'Excellent'
                                ? '⭐⭐⭐ Tích cực'
                                : item.evaluation.rating === 'Good'
                                ? '⭐⭐ Đạt'
                                : '⭐ Cần lưu ý'}
                            </span>
                          ) : (
                            <span style={{ color: 'var(--color-muted-fg)', fontStyle: 'italic', fontSize: '0.85rem' }}>
                              Chưa nhận xét
                            </span>
                          )}
                        </td>

                        <td style={{ padding: '12px 16px' }}>
                          {item.evaluation?.badges && item.evaluation.badges.length > 0 ? (
                            <span style={{ fontSize: '0.85rem' }}>{item.evaluation.badges.length} huy hiệu</span>
                          ) : (
                            <span style={{ color: 'var(--color-muted-fg)', fontSize: '0.85rem' }}>--</span>
                          )}
                        </td>

                        <td style={{ padding: '12px 16px' }}>
                          {item.evaluation?.isAcknowledged ? (
                            <span style={{ color: '#15803D', fontWeight: 700, fontSize: '0.8rem', display: 'flex', alignItems: 'center', gap: 4 }}>
                              <CheckCircle2 size={14} /> Đã xác nhận
                            </span>
                          ) : item.evaluation ? (
                            <span style={{ color: '#B45309', fontSize: '0.8rem' }}>Chờ phụ huynh xác nhận</span>
                          ) : (
                            <span style={{ color: 'var(--color-muted-fg)', fontSize: '0.8rem' }}>--</span>
                          )}
                        </td>

                        <td style={{ padding: '12px 16px', textAlign: 'right' }}>
                          <button
                            type="button"
                            className="btn btn--primary btn--sm"
                            onClick={() => openEvaluationModal(item)}
                          >
                            {item.evaluation ? 'Sửa nhận xét' : 'Viết nhận xét'}
                          </button>
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>

              <div className="mobile-card-list">
                {roster.map((item) => (
                  <div key={`m-${item.studentId}`} className="mobile-item-card">
                    <div className="mobile-item-card__head">
                      <div>
                        <strong style={{ fontSize: 'var(--fs-sm)', color: 'var(--color-fg)' }}>{item.studentName}</strong>
                        <div className="mobile-item-card__meta">{item.studentCode}</div>
                      </div>
                      {item.evaluation ? (
                        <span
                          className={`badge ${
                            item.evaluation.rating === 'Excellent'
                              ? 'badge--pink'
                              : item.evaluation.rating === 'Good'
                              ? 'badge--green'
                              : 'badge--yellow'
                          }`}
                        >
                          {item.evaluation.rating === 'Excellent'
                            ? '⭐⭐⭐ Tích cực'
                            : item.evaluation.rating === 'Good'
                            ? '⭐⭐ Đạt'
                            : '⭐ Cần lưu ý'}
                        </span>
                      ) : (
                        <span className="badge badge--muted">Chưa nhận xét</span>
                      )}
                    </div>

                    <div className="mobile-item-card__foot">
                      <div style={{ display: 'flex', flexWrap: 'wrap', gap: 8, alignItems: 'center', fontSize: 'var(--fs-xs)' }}>
                        {item.evaluation?.badges && item.evaluation.badges.length > 0 && (
                          <span className="badge badge--amber">{item.evaluation.badges.length} huy hiệu</span>
                        )}
                        {item.evaluation?.isAcknowledged ? (
                          <span style={{ color: '#15803D', fontWeight: 700, display: 'inline-flex', alignItems: 'center', gap: 4 }}>
                            <CheckCircle2 size={14} /> Đã xác nhận
                          </span>
                        ) : item.evaluation ? (
                          <span style={{ color: '#B45309', fontWeight: 600 }}>Chờ phụ huynh xác nhận</span>
                        ) : null}
                      </div>
                      <button
                        type="button"
                        className="btn btn--primary btn--sm"
                        onClick={() => openEvaluationModal(item)}
                      >
                        {item.evaluation ? 'Sửa nhận xét' : 'Viết nhận xét'}
                      </button>
                    </div>
                  </div>
                ))}
              </div>
            </>
          )}
        </div>
      )}

      {/* MODAL 1: CREATE / EDIT CLASS POST */}
      <GeometricModal
        open={showCreateModal}
        onClose={() => {
          setShowCreateModal(false);
          setEditingPost(null);
        }}
        title={editingPost ? 'Chỉnh sửa bài viết lớp' : 'Đăng bài viết mới cho lớp'}
        subtitle={`Lớp ${homeroom?.className} · Năm học ${homeroom?.academicYear || homeroom?.schoolYearName || '—'}`}
        badgeText="Bảng tin lớp"
        badgeColor="violet"
        icon={<Newspaper size={24} color="#1E293B" />}
        maxWidth="md"
      >
        <div style={{ display: 'flex', flexDirection: 'column', gap: 14 }}>
          <div>
            <label style={{ display: 'block', fontWeight: 700, fontSize: '0.85rem', marginBottom: 4 }}>
              Tiêu đề bài viết (*)
            </label>
            <input
              type="text"
              className="input"
              placeholder="Ví dụ: Chuẩn bị dụng cụ môn Mỹ thuật ngày mai..."
              value={postTitle}
              onChange={(e) => setPostTitle(e.target.value)}
            />
          </div>

          <div>
            <label style={{ display: 'block', fontWeight: 700, fontSize: '0.85rem', marginBottom: 4 }}>
              Nội dung chi tiết (*)
            </label>
            <textarea
              className="input"
              rows={4}
              placeholder="Nhập nội dung dặn dò chi tiết cho phụ huynh..."
              value={postContent}
              onChange={(e) => setPostContent(e.target.value)}
            />
          </div>

          <div>
            <label style={{ display: 'block', fontWeight: 700, fontSize: '0.85rem', marginBottom: 4 }}>
              Tải lên tệp đính kèm (Ảnh hoặc PDF / Word / Excel ≤ 5MB) (BR-50)
            </label>
            <input
              type="file"
              multiple
              accept=".jpg,.jpeg,.png,.webp,.pdf,.doc,.docx,.xls,.xlsx"
              className="input"
              disabled={uploadingFiles}
              onChange={handleFileChange}
            />
            {uploadingFiles && (
              <div style={{ fontSize: '0.8rem', color: 'var(--color-accent)', marginTop: 4 }}>
                Đang tải tệp lên máy chủ...
              </div>
            )}
            {postAttachments.length > 0 && (
              <div style={{ display: 'flex', flexDirection: 'column', gap: 6, marginTop: 8 }}>
                {postAttachments.map((att, idx) => (
                  <div
                    key={idx}
                    style={{
                      display: 'flex',
                      alignItems: 'center',
                      justifyContent: 'space-between',
                      background: '#F1F5F9',
                      padding: '6px 12px',
                      borderRadius: 8,
                      fontSize: '0.8rem',
                    }}
                  >
                    <span style={{ fontWeight: 600, overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap', maxWidth: 360 }}>
                      📎 {att.fileName} ({(att.fileSizeBytes / (1024 * 1024)).toFixed(2)} MB)
                    </span>
                    <button
                      type="button"
                      onClick={() => setPostAttachments((prev) => prev.filter((_, i) => i !== idx))}
                      style={{ background: 'transparent', border: 'none', color: '#DC2626', cursor: 'pointer', fontWeight: 700 }}
                    >
                      ✕
                    </button>
                  </div>
                ))}
              </div>
            )}
          </div>

          <div>
            <label style={{ display: 'block', fontWeight: 700, fontSize: '0.85rem', marginBottom: 4 }}>
              Đính kèm liên kết ngoài (Video YouTube / Ảnh / Tài liệu Drive)
            </label>
            <input
              type="text"
              className="input"
              placeholder="https://... (Ví dụ: Link video YouTube, ảnh, tài liệu)"
              value={postImageUrl}
              onChange={(e) => setPostImageUrl(e.target.value)}
            />
          </div>

          <div style={{ display: 'flex', gap: 20, marginTop: 4 }}>
            <label style={{ display: 'flex', alignItems: 'center', gap: 6, cursor: 'pointer', fontSize: '0.9rem' }}>
              <input
                type="checkbox"
                checked={postIsPinned}
                onChange={(e) => setPostIsPinned(e.target.checked)}
              />
              Ghim bài viết lên đầu trang
            </label>
            <label style={{ display: 'flex', alignItems: 'center', gap: 6, cursor: 'pointer', fontSize: '0.9rem' }}>
              <input
                type="checkbox"
                checked={postAllowComments}
                onChange={(e) => setPostAllowComments(e.target.checked)}
              />
              Cho phép phụ huynh bình luận
            </label>
          </div>

          <div style={{ display: 'flex', justifyContent: 'flex-end', gap: 10, marginTop: 16 }}>
            <button
              type="button"
              className="btn btn--outline"
              onClick={() => {
                setShowCreateModal(false);
                setEditingPost(null);
              }}
            >
              Hủy
            </button>
            <button
              type="button"
              className="btn btn--primary"
              disabled={submittingPost}
              onClick={handleCreatePost}
            >
              <Send size={15} /> {editingPost ? 'Lưu thay đổi' : 'Đăng bài ngay'}
            </button>
          </div>
        </div>
      </GeometricModal>

      {/* MODAL 2: EVALUATION FORM */}
      <GeometricModal
        open={!!selectedStudent}
        onClose={() => setSelectedStudent(null)}
        title={`Nhận xét nề nếp - Tuần ${weekNumber}`}
        subtitle={selectedStudent ? `Học sinh: ${selectedStudent.studentName} (${selectedStudent.studentCode})` : undefined}
        badgeText="Sổ nhận xét"
        badgeColor="pink"
        icon={<Award size={24} color="#1E293B" />}
        maxWidth="md"
      >
        {selectedStudent && (
          <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
              {/* Parent Acknowledgment & Feedback Notice (BR-59 & TC_CTB_24) */}
              {selectedStudent.evaluation?.isAcknowledged && (
                <div style={{ background: '#F0FDF4', padding: 14, borderRadius: 12, border: '1.5px solid #86EFAC' }}>
                  <div style={{ fontWeight: 800, color: '#166534', fontSize: '0.9rem', display: 'flex', alignItems: 'center', gap: 6 }}>
                    <CheckCircle2 size={16} /> Đã được phụ huynh xác nhận lúc{' '}
                    {selectedStudent.evaluation?.acknowledgedAt
                      ? formatViFullDateTime(selectedStudent.evaluation.acknowledgedAt)
                      : ''}
                  </div>
                  <div style={{ marginTop: 6, fontSize: '0.75rem', color: '#15803D' }}>
                    🔒 Theo BR-59, phiếu đánh giá đã khóa (Read-only). Không thể chỉnh sửa sau khi phụ huynh đã xác nhận.
                  </div>
                </div>
              )}

              {/* Rating selection */}
              <div>
                <label style={{ display: 'block', fontWeight: 700, fontSize: '0.85rem', marginBottom: 6 }}>
                  Đánh giá nề nếp chung (*)
                </label>
                <div style={{ display: 'flex', gap: 10 }}>
                  <button
                    type="button"
                    disabled={Boolean(selectedStudent.evaluation?.isAcknowledged)}
                    onClick={() => setEvalRating('Excellent')}
                    style={{
                      flex: 1,
                      padding: '10px 8px',
                      borderRadius: 12,
                      border: '2px solid',
                      borderColor: evalRating === 'Excellent' ? 'var(--color-accent)' : '#E2E8F0',
                      background: evalRating === 'Excellent' ? '#EDE9FE' : '#FFFFFF',
                      fontWeight: 700,
                      cursor: selectedStudent.evaluation?.isAcknowledged ? 'not-allowed' : 'pointer',
                      fontSize: '0.8rem',
                      opacity: selectedStudent.evaluation?.isAcknowledged ? 0.75 : 1,
                    }}
                  >
                    ⭐⭐⭐ Tích cực
                  </button>
                  <button
                    type="button"
                    disabled={Boolean(selectedStudent.evaluation?.isAcknowledged)}
                    onClick={() => setEvalRating('Good')}
                    style={{
                      flex: 1,
                      padding: '10px 8px',
                      borderRadius: 12,
                      border: '2px solid',
                      borderColor: evalRating === 'Good' ? 'var(--color-accent)' : '#E2E8F0',
                      background: evalRating === 'Good' ? '#EDE9FE' : '#FFFFFF',
                      fontWeight: 700,
                      cursor: selectedStudent.evaluation?.isAcknowledged ? 'not-allowed' : 'pointer',
                      fontSize: '0.8rem',
                      opacity: selectedStudent.evaluation?.isAcknowledged ? 0.75 : 1,
                    }}
                  >
                    ⭐⭐ Đạt yêu cầu
                  </button>
                  <button
                    type="button"
                    disabled={Boolean(selectedStudent.evaluation?.isAcknowledged)}
                    onClick={() => setEvalRating('NeedsAttention')}
                    style={{
                      flex: 1,
                      padding: '10px 8px',
                      borderRadius: 12,
                      border: '2px solid',
                      borderColor: evalRating === 'NeedsAttention' ? 'var(--color-accent)' : '#E2E8F0',
                      background: evalRating === 'NeedsAttention' ? '#EDE9FE' : '#FFFFFF',
                      fontWeight: 700,
                      cursor: selectedStudent.evaluation?.isAcknowledged ? 'not-allowed' : 'pointer',
                      fontSize: '0.8rem',
                      opacity: selectedStudent.evaluation?.isAcknowledged ? 0.75 : 1,
                    }}
                  >
                    ⭐ Cần nhắc nhở
                  </button>
                </div>
              </div>

              {/* Badges selection */}
              <div>
                <label style={{ display: 'block', fontWeight: 700, fontSize: '0.85rem', marginBottom: 6 }}>
                  Trao tặng Huy hiệu tuyên dương (Chọn 1 hoặc nhiều)
                </label>
                <div style={{ display: 'flex', gap: 8, flexWrap: 'wrap' }}>
                  {AVAILABLE_BADGES.map((badge) => {
                    const isSelected = evalBadges.includes(badge);
                    return (
                      <button
                        key={badge}
                        type="button"
                        disabled={Boolean(selectedStudent.evaluation?.isAcknowledged)}
                        onClick={() => toggleBadge(badge)}
                        style={{
                          background: isSelected ? '#DDD6FE' : '#F1F5F9',
                          color: isSelected ? '#5B21B6' : '#475569',
                          border: '1.5px solid',
                          borderColor: isSelected ? '#7C3AED' : '#CBD5E1',
                          padding: '6px 12px',
                          borderRadius: 999,
                          fontSize: '0.8rem',
                          fontWeight: 700,
                          cursor: selectedStudent.evaluation?.isAcknowledged ? 'not-allowed' : 'pointer',
                          opacity: selectedStudent.evaluation?.isAcknowledged ? 0.75 : 1,
                        }}
                      >
                        {isSelected ? '✓ ' : '+ '}
                        {badge}
                      </button>
                    );
                  })}
                </div>
              </div>

              {/* Teacher Note */}
              <div>
                <label style={{ display: 'block', fontWeight: 700, fontSize: '0.85rem', marginBottom: 4 }}>
                  Lời nhận xét của cô giáo (*)
                </label>
                <textarea
                  className="input"
                  rows={3}
                  maxLength={1500}
                  disabled={Boolean(selectedStudent.evaluation?.isAcknowledged)}
                  placeholder="Ghi nhận xét về nề nếp, sinh hoạt, sự tiến bộ của em..."
                  value={evalNote}
                  onChange={(e) => setEvalNote(e.target.value)}
                />
              </div>

              {/* Private reminder */}
              <div>
                <label style={{ display: 'block', fontWeight: 700, fontSize: '0.85rem', marginBottom: 4 }}>
                  Dặn dò riêng cho phụ huynh (Tùy chọn)
                </label>
                <input
                  type="text"
                  className="input"
                  maxLength={500}
                  disabled={Boolean(selectedStudent.evaluation?.isAcknowledged)}
                  placeholder="Ví dụ: Nhắc phụ huynh bọc lại vở bài tập Toán của con..."
                  value={evalRemind}
                  onChange={(e) => setEvalRemind(e.target.value)}
                />
              </div>

              <div style={{ display: 'flex', justifyContent: 'flex-end', gap: 10, marginTop: 8 }}>
                <button
                  type="button"
                  className="btn btn--outline"
                  onClick={() => setSelectedStudent(null)}
                >
                  {selectedStudent.evaluation?.isAcknowledged ? 'Đóng' : 'Hủy'}
                </button>
                {!selectedStudent.evaluation?.isAcknowledged && (
                  <button
                    type="button"
                    className="btn btn--primary"
                    disabled={submittingEval}
                    onClick={handleSaveEvaluation}
                  >
                    <Check size={16} /> Lưu & Gửi phiếu nhận xét
                  </button>
                )}
              </div>
            </div>
          )}
      </GeometricModal>
    </div>
  );
}

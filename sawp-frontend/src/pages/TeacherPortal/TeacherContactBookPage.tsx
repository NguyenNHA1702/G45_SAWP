import { useEffect, useState } from 'react';
import {
  Award,
  Check,
  CheckCircle2,
  Heart,
  MessageCircle,
  Newspaper,
  Pin,
  Plus,
  RefreshCw,
  Send,
  Sparkles,
  Trash2,
  Users,
  X,
} from 'lucide-react';
import toast from 'react-hot-toast';
import { contactBookApi } from '../../services/implementations/contactBookApi';
import { classApi } from '../../services/implementations/classApi';
import type {
  ClassPostDto,
  CreateClassPostDto,
  StudentEvaluationRosterItemDto,
  CreateStudentEvaluationDto,
} from '../../types/contactBook.types';
import type { ClassRosterDto } from '../../types/class.types';
import { getSystemMessage } from '../../constants/messages';

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

export default function TeacherContactBookPage() {
  const [homeroom, setHomeroom] = useState<ClassRosterDto | null>(null);
  const [loadingHomeroom, setLoadingHomeroom] = useState(false);
  const [activeTab, setActiveTab] = useState<TabKey>('feed');

  // Tab 1: Posts
  const [posts, setPosts] = useState<ClassPostDto[]>([]);
  const [loadingPosts, setLoadingPosts] = useState(false);
  const [showCreateModal, setShowCreateModal] = useState(false);
  const [postTitle, setPostTitle] = useState('');
  const [postContent, setPostContent] = useState('');
  const [postCategory, setPostCategory] = useState('Dặn dò bài vở');
  const [postIsPinned, setPostIsPinned] = useState(false);
  const [postAllowComments, setPostAllowComments] = useState(true);
  const [postImageUrl, setPostImageUrl] = useState('');
  const [submittingPost, setSubmittingPost] = useState(false);

  // Tab 2: Evaluations
  const [weekNumber, setWeekNumber] = useState<number>(8);
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
        loadPosts(res.data.classId);
        loadRoster(res.data.classId, weekNumber);
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

  const handleCreatePost = async () => {
    if (!postTitle.trim() || !postContent.trim()) {
      toast.error(getSystemMessage('MSG01')?.content || 'Vui lòng nhập đầy đủ tiêu đề và nội dung.');
      return;
    }
    if (!homeroom) return;

    setSubmittingPost(true);
    try {
      const payload: CreateClassPostDto = {
        classId: homeroom.classId,
        title: postTitle.trim(),
        content: postContent.trim(),
        category: postCategory,
        isPinned: postIsPinned,
        allowComments: postAllowComments,
        attachmentUrls: postImageUrl.trim() ? [postImageUrl.trim()] : [],
      };

      const res = await contactBookApi.createClassPost(payload);
      if (res.success) {
        toast.success(getSystemMessage('MSG51')?.content || 'Đăng bài viết thành công!');
        setShowCreateModal(false);
        setPostTitle('');
        setPostContent('');
        setPostImageUrl('');
        loadPosts(homeroom.classId);
      } else {
        toast.error(res.message || 'Lỗi khi đăng bài');
      }
    } catch {
      toast.error('Lỗi khi đăng bài');
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
    if (!evalNote.trim()) {
      toast.error(getSystemMessage('MSG01')?.content || 'Vui lòng nhập lời nhận xét cho học sinh.');
      return;
    }

    setSubmittingEval(true);
    try {
      const payload: CreateStudentEvaluationDto = {
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
        toast.success(getSystemMessage('MSG52')?.content || 'Đã gửi phiếu nhận xét rèn luyện thành công!');
        setSelectedStudent(null);
        loadRoster(homeroom.classId, weekNumber);
      } else {
        toast.error(res.message || 'Lỗi khi lưu nhận xét');
      }
    } catch {
      toast.error('Lỗi khi lưu nhận xét');
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
              Sĩ số: <strong>{homeroom.totalStudents} học sinh</strong> · Năm học: <strong>{homeroom.schoolYearName}</strong>
            </p>
          </div>

          {activeTab === 'feed' && (
            <button
              type="button"
              className="btn btn--primary"
              onClick={() => setShowCreateModal(true)}
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
                onClick={() => setShowCreateModal(true)}
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
                        Đăng lúc: {new Date(post.createdAt).toLocaleString('vi-VN')}
                      </div>
                    </div>

                    <div style={{ display: 'flex', gap: 8 }}>
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
                  {post.attachments && post.attachments.length > 0 && (
                    <div style={{ display: 'flex', gap: 12, flexWrap: 'wrap', marginBottom: 16 }}>
                      {post.attachments.map((att) => (
                        <img
                          key={att.id}
                          src={att.fileUrl}
                          alt={att.fileName}
                          style={{
                            width: 140,
                            height: 100,
                            objectFit: 'cover',
                            borderRadius: 12,
                            border: '1.5px solid var(--color-border)',
                          }}
                        />
                      ))}
                    </div>
                  )}

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
                style={{ width: 140 }}
                value={weekNumber}
                onChange={(e) => {
                  const w = Number(e.target.value);
                  setWeekNumber(w);
                  if (homeroom) loadRoster(homeroom.classId, w);
                }}
              >
                {Array.from({ length: 35 }, (_, i) => i + 1).map((w) => (
                  <option key={w} value={w}>
                    Tuần {w}
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
            <div className="card" style={{ padding: 0, overflow: 'hidden' }}>
              <table style={{ width: '100%', borderCollapse: 'collapse', textAlign: 'left' }}>
                <thead>
                  <tr style={{ background: '#F8FAFC', borderBottom: '2px solid var(--color-border)' }}>
                    <th style={{ padding: '12px 16px' }}>Học sinh</th>
                    <th style={{ padding: '12px 16px' }}>Đánh giá nề nếp</th>
                    <th style={{ padding: '12px 16px' }}>Huy hiệu</th>
                    <th style={{ padding: '12px 16px' }}>Ký nhận PH</th>
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
                            <CheckCircle2 size={14} /> Đã ký nhận
                          </span>
                        ) : item.evaluation ? (
                          <span style={{ color: '#B45309', fontSize: '0.8rem' }}>Chờ phụ huynh ký</span>
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
          )}
        </div>
      )}

      {/* MODAL 1: CREATE CLASS POST */}
      {showCreateModal && (
        <div
          style={{
            position: 'fixed',
            inset: 0,
            background: 'rgba(0,0,0,0.5)',
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
            zIndex: 1000,
            padding: 16,
          }}
        >
          <div className="card pop-in" style={{ width: '100%', maxWidth: 540, maxHeight: '90vh', overflowY: 'auto' }}>
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 16 }}>
              <h3 style={{ margin: 0 }}>Đăng bài viết mới cho lớp</h3>
              <button
                type="button"
                onClick={() => setShowCreateModal(false)}
                style={{ background: 'none', border: 'none', cursor: 'pointer' }}
              >
                <X size={20} />
              </button>
            </div>

            <div style={{ display: 'flex', flexDirection: 'column', gap: 14 }}>
              <div>
                <label style={{ display: 'block', fontWeight: 700, fontSize: '0.85rem', marginBottom: 4 }}>
                  Phân loại bài viết
                </label>
                <select
                  className="input"
                  value={postCategory}
                  onChange={(e) => setPostCategory(e.target.value)}
                >
                  <option value="Dặn dò bài vở">Dặn dò bài vở & Đồ dùng học tập</option>
                  <option value="Hoạt động lớp">Hoạt động trải nghiệm / Ngoại khóa</option>
                  <option value="Bán trú">Nhật ký giờ ăn & Ngủ bán trú</option>
                  <option value="Thông báo chung">Thông báo chung của lớp</option>
                </select>
              </div>

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
                  Đính kèm URL hình ảnh (nếu có)
                </label>
                <input
                  type="text"
                  className="input"
                  placeholder="https://example.com/photo.jpg"
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
                  onClick={() => setShowCreateModal(false)}
                >
                  Hủy
                </button>
                <button
                  type="button"
                  className="btn btn--primary"
                  disabled={submittingPost}
                  onClick={handleCreatePost}
                >
                  <Send size={15} /> Đăng bài ngay
                </button>
              </div>
            </div>
          </div>
        </div>
      )}

      {/* MODAL 2: EVALUATION FORM */}
      {selectedStudent && (
        <div
          style={{
            position: 'fixed',
            inset: 0,
            background: 'rgba(0,0,0,0.5)',
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
            zIndex: 1000,
            padding: 16,
          }}
        >
          <div className="card pop-in" style={{ width: '100%', maxWidth: 580, maxHeight: '90vh', overflowY: 'auto' }}>
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 16 }}>
              <div>
                <h3 style={{ margin: 0 }}>Nhận xét nề nếp - Tuần {weekNumber}</h3>
                <div style={{ fontSize: '0.85rem', color: 'var(--color-muted-fg)' }}>
                  Học sinh: <strong>{selectedStudent.studentName}</strong> ({selectedStudent.studentCode})
                </div>
              </div>
              <button
                type="button"
                onClick={() => setSelectedStudent(null)}
                style={{ background: 'none', border: 'none', cursor: 'pointer' }}
              >
                <X size={20} />
              </button>
            </div>

            <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
              {/* Rating selection */}
              <div>
                <label style={{ display: 'block', fontWeight: 700, fontSize: '0.85rem', marginBottom: 6 }}>
                  Đánh giá nề nếp chung (*)
                </label>
                <div style={{ display: 'flex', gap: 10 }}>
                  <button
                    type="button"
                    onClick={() => setEvalRating('Excellent')}
                    style={{
                      flex: 1,
                      padding: '10px 8px',
                      borderRadius: 12,
                      border: '2px solid',
                      borderColor: evalRating === 'Excellent' ? 'var(--color-accent)' : '#E2E8F0',
                      background: evalRating === 'Excellent' ? '#EDE9FE' : '#FFFFFF',
                      fontWeight: 700,
                      cursor: 'pointer',
                      fontSize: '0.8rem',
                    }}
                  >
                    ⭐⭐⭐ Tích cực
                  </button>
                  <button
                    type="button"
                    onClick={() => setEvalRating('Good')}
                    style={{
                      flex: 1,
                      padding: '10px 8px',
                      borderRadius: 12,
                      border: '2px solid',
                      borderColor: evalRating === 'Good' ? 'var(--color-accent)' : '#E2E8F0',
                      background: evalRating === 'Good' ? '#EDE9FE' : '#FFFFFF',
                      fontWeight: 700,
                      cursor: 'pointer',
                      fontSize: '0.8rem',
                    }}
                  >
                    ⭐⭐ Đạt yêu cầu
                  </button>
                  <button
                    type="button"
                    onClick={() => setEvalRating('NeedsAttention')}
                    style={{
                      flex: 1,
                      padding: '10px 8px',
                      borderRadius: 12,
                      border: '2px solid',
                      borderColor: evalRating === 'NeedsAttention' ? 'var(--color-accent)' : '#E2E8F0',
                      background: evalRating === 'NeedsAttention' ? '#EDE9FE' : '#FFFFFF',
                      fontWeight: 700,
                      cursor: 'pointer',
                      fontSize: '0.8rem',
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
                          cursor: 'pointer',
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
                  Hủy
                </button>
                <button
                  type="button"
                  className="btn btn--primary"
                  disabled={submittingEval}
                  onClick={handleSaveEvaluation}
                >
                  <Check size={16} /> Lưu & Gửi phiếu nhận xét
                </button>
              </div>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}

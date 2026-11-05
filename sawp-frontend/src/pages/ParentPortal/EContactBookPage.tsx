import { useEffect, useState } from 'react';
import {
  Award,
  BookOpen,
  CalendarCheck2,
  CheckCircle2,
  Clock,
  Heart,
  MessageCircle,
  Newspaper,
  Pin,
  RefreshCw,
  Send,
  Sparkles,
  User,
  UtensilsCrossed,
} from 'lucide-react';
import toast from 'react-hot-toast';
import { useParentChild } from '../../contexts/ParentChildContext';
import { contactBookApi } from '../../services/implementations/contactBookApi';
import { attendanceApi } from '../../services/implementations/attendanceApi';
import type { ClassPostDto, StudentEvaluationDto } from '../../types/contactBook.types';
import type { AttendanceDay } from '../../types/attendance.types';
import { getSystemMessage } from '../../constants/messages';

type TabKey = 'feed' | 'evaluation' | 'overview';

export default function EContactBookPage() {
  const { activeChild, loading: childLoading } = useParentChild();
  const [activeTab, setActiveTab] = useState<TabKey>('feed');

  // Tab 1: Class Feed
  const [posts, setPosts] = useState<ClassPostDto[]>([]);
  const [postsLoading, setPostsLoading] = useState(false);
  const [commentInputs, setCommentInputs] = useState<Record<number, string>>({});
  const [submittingComment, setSubmittingComment] = useState<number | null>(null);

  // Tab 2: Evaluations
  const [evaluations, setEvaluations] = useState<StudentEvaluationDto[]>([]);
  const [evalLoading, setEvalLoading] = useState(false);
  const [parentFeedbackInputs, setParentFeedbackInputs] = useState<Record<number, string>>({});
  const [acknowledgingId, setAcknowledgingId] = useState<number | null>(null);

  // Tab 3: Daily overview
  const [todayAttendance, setTodayAttendance] = useState<AttendanceDay | null>(null);
  const [overviewLoading, setOverviewLoading] = useState(false);

  // Load feed when child or class changes
  useEffect(() => {
    if (activeChild?.currentClassId) {
      loadFeed(activeChild.currentClassId);
    } else {
      setPosts([]);
    }

    if (activeChild?.studentId) {
      loadEvaluations(activeChild.studentId);
      loadTodayAttendance(activeChild.studentId);
    } else {
      setEvaluations([]);
      setTodayAttendance(null);
    }
  }, [activeChild?.studentId, activeChild?.currentClassId]);

  const loadFeed = async (classId: number) => {
    setPostsLoading(true);
    try {
      const res = await contactBookApi.getClassPosts(classId);
      if (res.success && res.data) {
        setPosts(res.data);
      }
    } catch {
      // silent
    } finally {
      setPostsLoading(false);
    }
  };

  const loadEvaluations = async (studentId: number) => {
    setEvalLoading(true);
    try {
      const res = await contactBookApi.getStudentEvaluations(studentId);
      if (res.success && res.data) {
        setEvaluations(res.data);
      }
    } catch {
      // silent
    } finally {
      setEvalLoading(false);
    }
  };

  const loadTodayAttendance = async (studentId: number) => {
    setOverviewLoading(true);
    try {
      const todayStr = new Date().toISOString().split('T')[0];
      const res = await attendanceApi.getStudentHistory(studentId, todayStr, todayStr);
      if (res && res.days && res.days.length > 0) {
        setTodayAttendance(res.days[0]);
      } else {
        setTodayAttendance(null);
      }
    } catch {
      // silent
    } finally {
      setOverviewLoading(false);
    }
  };

  // Reactions
  const handleToggleReaction = async (postId: number) => {
    try {
      await contactBookApi.toggleReaction(postId, 'Heart');
      if (activeChild?.currentClassId) {
        loadFeed(activeChild.currentClassId);
      }
    } catch {
      toast.error('Không thể thực hiện thao tác.');
    }
  };

  // Comments
  const handleAddComment = async (postId: number) => {
    const text = (commentInputs[postId] || '').trim();
    if (!text) {
      toast.error(getSystemMessage('MSG01')?.content || 'Vui lòng nhập nội dung bình luận.');
      return;
    }

    setSubmittingComment(postId);
    try {
      const res = await contactBookApi.addComment(postId, { content: text });
      if (res.success) {
        toast.success('Đã gửi bình luận');
        setCommentInputs((prev) => ({ ...prev, [postId]: '' }));
        if (activeChild?.currentClassId) {
          loadFeed(activeChild.currentClassId);
        }
      } else {
        toast.error(res.message || 'Lỗi khi gửi bình luận');
      }
    } catch {
      toast.error('Lỗi khi gửi bình luận');
    } finally {
      setSubmittingComment(null);
    }
  };

  // Acknowledge evaluation
  const handleAcknowledge = async (evaluationId: number) => {
    const feedback = (parentFeedbackInputs[evaluationId] || '').trim();
    setAcknowledgingId(evaluationId);
    try {
      const res = await contactBookApi.acknowledgeEvaluation(evaluationId, {
        parentFeedback: feedback || undefined,
      });
      if (res.success) {
        toast.success(getSystemMessage('MSG53')?.content || 'Đã ký nhận phiếu nhận xét thành công.');
        if (activeChild?.studentId) {
          loadEvaluations(activeChild.studentId);
        }
      } else {
        toast.error(res.message || 'Lỗi khi ký nhận');
      }
    } catch {
      toast.error('Lỗi khi ký nhận');
    } finally {
      setAcknowledgingId(null);
    }
  };

  if (childLoading) {
    return (
      <div className="container" style={{ textAlign: 'center', padding: '60px 0' }}>
        <RefreshCw size={36} className="spin" color="var(--color-accent)" style={{ margin: '0 auto 16px' }} />
        <div style={{ fontWeight: 700 }}>Đang tải thông tin học sinh...</div>
      </div>
    );
  }

  if (!activeChild) {
    return (
      <div className="container">
        <div className="card" style={{ textAlign: 'center', padding: 48, marginTop: 32 }}>
          <User size={48} color="var(--color-muted-fg)" style={{ margin: '0 auto 16px' }} />
          <h3>Chưa có hồ sơ học sinh liên kết</h3>
          <p style={{ color: 'var(--color-muted-fg)' }}>
            Vui lòng liên kết với con của bạn trong mục "Hồ sơ các con" để theo dõi sổ liên lạc điện tử.
          </p>
        </div>
      </div>
    );
  }

  return (
    <div className="container pop-in">
      {/* Decorative Memphis shapes */}
      <span className="decor decor--circle" style={{ top: -20, right: 40 }} aria-hidden="true" />
      <span className="decor decor--triangle" style={{ bottom: 20, left: 10 }} aria-hidden="true" />

      {/* Header */}
      <header className="page-header" style={{ marginBottom: 20 }}>
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', flexWrap: 'wrap', gap: 16 }}>
          <div>
            <span className="badge badge--yellow" style={{ marginBottom: 8 }}>
              <Sparkles size={14} /> Sổ liên lạc điện tử (Không chấm điểm số)
            </span>
            <h1 style={{ margin: '4px 0 8px' }}>
              Đồng hành cùng con <span className="squiggle">{activeChild.fullName}</span>
            </h1>
            <p style={{ color: 'var(--color-muted-fg)', margin: 0 }}>
              Lớp: <strong>{activeChild.currentClassName || 'Chưa xếp lớp'}</strong> · Mã học sinh: <strong>{activeChild.studentCode}</strong>
            </p>
          </div>
        </div>
      </header>

      {/* Navigation Tabs */}
      <div style={{ display: 'flex', gap: 12, marginBottom: 24, borderBottom: '2px solid var(--color-border)', paddingBottom: 8, flexWrap: 'wrap' }}>
        <button
          type="button"
          className={`btn ${activeTab === 'feed' ? 'btn--primary' : 'btn--outline'}`}
          onClick={() => setActiveTab('feed')}
        >
          <Newspaper size={16} /> Bảng tin lớp ({posts.length})
        </button>
        <button
          type="button"
          className={`btn ${activeTab === 'evaluation' ? 'btn--primary' : 'btn--outline'}`}
          onClick={() => setActiveTab('evaluation')}
        >
          <Award size={16} /> Sổ nhận xét của con ({evaluations.length})
        </button>
        <button
          type="button"
          className={`btn ${activeTab === 'overview' ? 'btn--primary' : 'btn--outline'}`}
          onClick={() => setActiveTab('overview')}
        >
          <CalendarCheck2 size={16} /> Tổng quan hôm nay
        </button>
      </div>

      {/* TAB 1: BẢNG TIN LỚP HỌC */}
      {activeTab === 'feed' && (
        <div>
          {postsLoading ? (
            <div className="card" style={{ textAlign: 'center', padding: 48 }}>
              <RefreshCw size={28} className="spin" color="var(--color-accent)" style={{ margin: '0 auto 12px' }} />
              <div>Đang tải bài viết lớp...</div>
            </div>
          ) : posts.length === 0 ? (
            <div className="card" style={{ textAlign: 'center', padding: 48 }}>
              <Newspaper size={48} color="var(--color-muted-fg)" style={{ margin: '0 auto 16px' }} />
              <h3>Chưa có bài viết nào trên bảng tin</h3>
              <p style={{ color: 'var(--color-muted-fg)' }}>
                Khi giáo viên chủ nhiệm đăng thông báo dặn dò hoặc ảnh hoạt động lớp, thông tin sẽ xuất hiện tại đây.
              </p>
            </div>
          ) : (
            <div style={{ display: 'flex', flexDirection: 'column', gap: 20 }}>
              {posts.map((post) => (
                <div key={post.id} className="card" style={{ position: 'relative' }}>
                  {/* Pinned badge */}
                  {post.isPinned && (
                    <div
                      style={{
                        display: 'inline-flex',
                        alignItems: 'center',
                        gap: 4,
                        background: '#FEF08A',
                        color: '#854D0E',
                        padding: '4px 10px',
                        borderRadius: 999,
                        fontSize: '0.8rem',
                        fontWeight: 700,
                        marginBottom: 12,
                        border: '1.5px solid #854D0E',
                      }}
                    >
                      <Pin size={13} /> Đã ghim
                    </div>
                  )}

                  {/* Post header */}
                  <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 12 }}>
                    <div>
                      <div style={{ fontWeight: 800, fontSize: '1.05rem', color: 'var(--color-foreground)' }}>
                        {post.authorName}
                      </div>
                      <div style={{ fontSize: '0.8rem', color: 'var(--color-muted-fg)' }}>
                        {new Date(post.createdAt).toLocaleString('vi-VN')} · {post.className}
                      </div>
                    </div>
                    <span className="badge badge--pink">{post.category}</span>
                  </div>

                  {/* Post Title & Content */}
                  <h3 style={{ margin: '8px 0 10px', fontSize: '1.25rem' }}>{post.title}</h3>
                  <p style={{ whiteSpace: 'pre-line', lineHeight: 1.6, color: '#334155', margin: '0 0 16px' }}>
                    {post.content}
                  </p>

                  {/* Attachments / Images Gallery */}
                  {post.attachments && post.attachments.length > 0 && (
                    <div
                      style={{
                        display: 'grid',
                        gridTemplateColumns: 'repeat(auto-fill, minmax(180px, 1fr))',
                        gap: 12,
                        marginBottom: 16,
                      }}
                    >
                      {post.attachments.map((att) => (
                        <div
                          key={att.id}
                          style={{
                            border: '2px solid var(--color-border)',
                            borderRadius: 12,
                            overflow: 'hidden',
                            background: '#F8FAFC',
                          }}
                        >
                          {att.fileType === 'Image' ? (
                            <img
                              src={att.fileUrl}
                              alt={att.fileName}
                              style={{ width: '100%', height: 140, objectFit: 'cover' }}
                            />
                          ) : (
                            <div style={{ padding: 16, textAlign: 'center' }}>
                              <BookOpen size={28} color="var(--color-accent)" style={{ margin: '0 auto 8px' }} />
                              <a
                                href={att.fileUrl}
                                target="_blank"
                                rel="noreferrer"
                                style={{ fontSize: '0.85rem', fontWeight: 600, wordBreak: 'break-all' }}
                              >
                                {att.fileName}
                              </a>
                            </div>
                          )}
                        </div>
                      ))}
                    </div>
                  )}

                  {/* Reaction & Comments stats */}
                  <div
                    style={{
                      display: 'flex',
                      alignItems: 'center',
                      gap: 16,
                      paddingTop: 12,
                      borderTop: '1px solid var(--color-border)',
                    }}
                  >
                    <button
                      type="button"
                      onClick={() => handleToggleReaction(post.id)}
                      style={{
                        display: 'flex',
                        alignItems: 'center',
                        gap: 6,
                        background: post.userHasReacted ? '#FEE2E2' : '#F1F5F9',
                        color: post.userHasReacted ? '#DC2626' : '#475569',
                        border: '1.5px solid #CBD5E1',
                        borderRadius: 999,
                        padding: '6px 14px',
                        cursor: 'pointer',
                        fontWeight: 700,
                        fontSize: '0.875rem',
                      }}
                    >
                      <Heart size={16} fill={post.userHasReacted ? '#DC2626' : 'none'} />
                      <span>{post.reactionCount} Thả tim</span>
                    </button>

                    <span style={{ fontSize: '0.875rem', color: 'var(--color-muted-fg)', display: 'flex', alignItems: 'center', gap: 4 }}>
                      <MessageCircle size={16} /> {post.comments.length} bình luận
                    </span>
                  </div>

                  {/* Comment list */}
                  {post.comments.length > 0 && (
                    <div style={{ marginTop: 16, display: 'flex', flexDirection: 'column', gap: 10 }}>
                      {post.comments.map((cm) => (
                        <div
                          key={cm.id}
                          style={{
                            background: '#F8FAFC',
                            padding: '10px 14px',
                            borderRadius: 12,
                            border: '1px solid #E2E8F0',
                          }}
                        >
                          <div style={{ display: 'flex', justifyContent: 'space-between', marginBottom: 4 }}>
                            <div style={{ fontWeight: 700, fontSize: '0.85rem' }}>
                              {cm.authorName}{' '}
                              <span style={{ fontWeight: 500, fontSize: '0.75rem', color: 'var(--color-muted-fg)' }}>
                                ({cm.authorRoleLabel})
                              </span>
                            </div>
                            <div style={{ fontSize: '0.75rem', color: 'var(--color-muted-fg)' }}>
                              {new Date(cm.createdAt).toLocaleTimeString('vi-VN', { hour: '2-digit', minute: '2-digit' })}
                            </div>
                          </div>
                          <div style={{ fontSize: '0.9rem', color: '#334155' }}>{cm.content}</div>
                        </div>
                      ))}
                    </div>
                  )}

                  {/* Comment Form */}
                  {post.allowComments ? (
                    <div style={{ display: 'flex', gap: 8, marginTop: 16 }}>
                      <input
                        type="text"
                        className="input"
                        placeholder="Viết lời nhắn / bình luận cho lớp..."
                        value={commentInputs[post.id] || ''}
                        onChange={(e) => setCommentInputs({ ...commentInputs, [post.id]: e.target.value })}
                        onKeyDown={(e) => {
                          if (e.key === 'Enter') handleAddComment(post.id);
                        }}
                        style={{ flex: 1 }}
                      />
                      <button
                        type="button"
                        className="btn btn--primary btn--sm"
                        disabled={submittingComment === post.id}
                        onClick={() => handleAddComment(post.id)}
                      >
                        <Send size={15} /> Gửi
                      </button>
                    </div>
                  ) : (
                    <div style={{ marginTop: 12, fontSize: '0.8rem', color: 'var(--color-muted-fg)', fontStyle: 'italic' }}>
                      🔒 Bài viết này hiện đã tắt tính năng bình luận.
                    </div>
                  )}
                </div>
              ))}
            </div>
          )}
        </div>
      )}

      {/* TAB 2: SỔ NHẬN XÉT CỦA CON */}
      {activeTab === 'evaluation' && (
        <div>
          {evalLoading ? (
            <div className="card" style={{ textAlign: 'center', padding: 48 }}>
              <RefreshCw size={28} className="spin" color="var(--color-accent)" style={{ margin: '0 auto 12px' }} />
              <div>Đang tải sổ nhận xét...</div>
            </div>
          ) : evaluations.length === 0 ? (
            <div className="card" style={{ textAlign: 'center', padding: 48 }}>
              <Award size={48} color="var(--color-muted-fg)" style={{ margin: '0 auto 16px' }} />
              <h3>Chưa có phiếu nhận xét nào</h3>
              <p style={{ color: 'var(--color-muted-fg)' }}>
                Giáo viên chủ nhiệm sẽ gửi nhận xét nề nếp định kỳ vào cuối tuần.
              </p>
            </div>
          ) : (
            <div style={{ display: 'flex', flexDirection: 'column', gap: 20 }}>
              {evaluations.map((ev) => (
                <div key={ev.id} className="card" style={{ borderLeft: '6px solid var(--color-accent)' }}>
                  <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', flexWrap: 'wrap', gap: 8 }}>
                    <div>
                      <span className="badge badge--yellow" style={{ marginBottom: 6 }}>
                        Tuần {ev.weekNumber} · {new Date(ev.evaluationDate).toLocaleDateString('vi-VN')}
                      </span>
                      <h3 style={{ margin: '4px 0' }}>Phiếu nhận xét rèn luyện & nề nếp</h3>
                      <div style={{ fontSize: '0.85rem', color: 'var(--color-muted-fg)' }}>
                        Giáo viên: <strong>{ev.teacherName}</strong> · Lớp {ev.className}
                      </div>
                    </div>

                    <div>
                      {ev.isAcknowledged ? (
                        <span
                          style={{
                            display: 'inline-flex',
                            alignItems: 'center',
                            gap: 4,
                            background: '#DCFCE7',
                            color: '#15803D',
                            padding: '4px 12px',
                            borderRadius: 999,
                            fontWeight: 700,
                            fontSize: '0.8rem',
                          }}
                        >
                          <CheckCircle2 size={14} /> Đã ký nhận
                        </span>
                      ) : (
                        <span
                          style={{
                            display: 'inline-flex',
                            alignItems: 'center',
                            gap: 4,
                            background: '#FEF3C7',
                            color: '#B45309',
                            padding: '4px 12px',
                            borderRadius: 999,
                            fontWeight: 700,
                            fontSize: '0.8rem',
                          }}
                        >
                          <Clock size={14} /> Chờ phụ huynh ký nhận
                        </span>
                      )}
                    </div>
                  </div>

                  {/* Rating display */}
                  <div style={{ marginTop: 16, display: 'flex', alignItems: 'center', gap: 8 }}>
                    <span style={{ fontWeight: 700, fontSize: '0.9rem' }}>Đánh giá chung:</span>
                    {ev.rating === 'Excellent' && (
                      <span className="badge badge--pink" style={{ fontSize: '0.85rem' }}>
                        ⭐⭐⭐ Tích cực & Tiến bộ vượt bậc
                      </span>
                    )}
                    {ev.rating === 'Good' && (
                      <span className="badge badge--green" style={{ fontSize: '0.85rem' }}>
                        ⭐⭐ Đạt yêu cầu chăm ngoan
                      </span>
                    )}
                    {ev.rating === 'NeedsAttention' && (
                      <span className="badge badge--yellow" style={{ fontSize: '0.85rem' }}>
                        ⭐ Cần lưu ý nhắc nhở
                      </span>
                    )}
                  </div>

                  {/* Badges */}
                  {ev.badges && ev.badges.length > 0 && (
                    <div style={{ marginTop: 12 }}>
                      <span style={{ fontWeight: 700, fontSize: '0.9rem', marginRight: 8 }}>Huy hiệu nề nếp:</span>
                      <div style={{ display: 'inline-flex', gap: 8, flexWrap: 'wrap', marginTop: 4 }}>
                        {ev.badges.map((b, idx) => (
                          <span
                            key={idx}
                            style={{
                              background: '#EDE9FE',
                              color: '#6D28D9',
                              padding: '4px 10px',
                              borderRadius: 999,
                              fontWeight: 700,
                              fontSize: '0.8rem',
                              border: '1.5px solid #C4B5FD',
                            }}
                          >
                            🏅 {b}
                          </span>
                        ))}
                      </div>
                    </div>
                  )}

                  {/* Teacher Note */}
                  <div
                    style={{
                      marginTop: 16,
                      background: '#F8FAFC',
                      padding: 16,
                      borderRadius: 12,
                      border: '1.5px solid #E2E8F0',
                    }}
                  >
                    <div style={{ fontWeight: 800, fontSize: '0.9rem', marginBottom: 6, color: '#1E293B' }}>
                      📝 Lời nhắn của GVCN:
                    </div>
                    <p style={{ margin: 0, lineHeight: 1.6, color: '#334155', whiteSpace: 'pre-line' }}>
                      {ev.teacherNote}
                    </p>

                    {ev.privateRemind && (
                      <div style={{ marginTop: 10, paddingTop: 10, borderTop: '1px dashed #CBD5E1', fontSize: '0.85rem', color: '#B45309' }}>
                        <strong>📌 Dặn dò riêng gia đình:</strong> {ev.privateRemind}
                      </div>
                    )}
                  </div>

                  {/* Parent Acknowledgment & Feedback */}
                  <div style={{ marginTop: 16, paddingTop: 16, borderTop: '1px solid var(--color-border)' }}>
                    {ev.isAcknowledged ? (
                      <div style={{ background: '#F0FDF4', padding: 14, borderRadius: 12, border: '1px solid #BBF7D0' }}>
                        <div style={{ fontWeight: 700, color: '#166534', fontSize: '0.9rem' }}>
                          ✍️ Đã ký nhận bởi {ev.acknowledgedByGuardianName || 'Phụ huynh'} lúc{' '}
                          {ev.acknowledgedAt ? new Date(ev.acknowledgedAt).toLocaleString('vi-VN') : ''}
                        </div>
                        {ev.parentFeedback && (
                          <p style={{ margin: '6px 0 0', fontStyle: 'italic', color: '#14532D' }}>
                            " {ev.parentFeedback} "
                          </p>
                        )}
                      </div>
                    ) : (
                      <div>
                        <div style={{ fontWeight: 700, fontSize: '0.9rem', marginBottom: 8 }}>
                          ✍️ Ký nhận điện tử của Phụ huynh:
                        </div>
                        <input
                          type="text"
                          className="input"
                          placeholder="Nhập lời nhắn gửi cô giáo (ví dụ: Gia đình đã nắm thông tin và sẽ nhắc nhở con...)"
                          value={parentFeedbackInputs[ev.id] || ''}
                          onChange={(e) => setParentFeedbackInputs({ ...parentFeedbackInputs, [ev.id]: e.target.value })}
                          style={{ marginBottom: 10 }}
                        />
                        <button
                          type="button"
                          className="btn btn--primary"
                          disabled={acknowledgingId === ev.id}
                          onClick={() => handleAcknowledge(ev.id)}
                        >
                          <CheckCircle2 size={16} /> Ký xác nhận "Đã đọc" và gửi phản hồi
                        </button>
                      </div>
                    )}
                  </div>
                </div>
              ))}
            </div>
          )}
        </div>
      )}

      {/* TAB 3: TỔNG QUAN HÔM NAY (WIDGET READ-ONLY) */}
      {activeTab === 'overview' && (
        <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(320px, 1fr))', gap: 20 }}>
          {/* Card 1: Chuyên cần hôm nay */}
          <div className="card">
            <div style={{ display: 'flex', alignItems: 'center', gap: 10, marginBottom: 14 }}>
              <div
                style={{
                  width: 40,
                  height: 40,
                  borderRadius: 12,
                  background: '#DCFCE7',
                  display: 'flex',
                  alignItems: 'center',
                  justifyContent: 'center',
                }}
              >
                <CalendarCheck2 size={22} color="#15803D" />
              </div>
              <div>
                <h3 style={{ margin: 0, fontSize: '1.1rem' }}>Chuyên cần hôm nay</h3>
                <div style={{ fontSize: '0.8rem', color: 'var(--color-muted-fg)' }}>
                  {new Date().toLocaleDateString('vi-VN', { weekday: 'long', year: 'numeric', month: 'long', day: 'numeric' })}
                </div>
              </div>
            </div>

            {overviewLoading ? (
              <div style={{ textAlign: 'center', padding: 20 }}>Đang kiểm tra...</div>
            ) : todayAttendance ? (
              <div>
                <div style={{ display: 'flex', alignItems: 'center', gap: 8, marginBottom: 8 }}>
                  <span style={{ fontWeight: 700 }}>Trạng thái:</span>
                  <span
                    className={`badge ${
                      todayAttendance.status === 'Present'
                        ? 'badge--green'
                        : todayAttendance.status === 'Late'
                        ? 'badge--yellow'
                        : 'badge--pink'
                    }`}
                  >
                    {todayAttendance.status === 'Present'
                      ? 'Có mặt tại lớp'
                      : todayAttendance.status === 'Late'
                      ? 'Đi muộn'
                      : 'Vắng mặt'}
                  </span>
                </div>
                {todayAttendance.remarks && (
                  <div style={{ fontSize: '0.85rem', color: 'var(--color-muted-fg)' }}>
                    Ghi chú: {todayAttendance.remarks}
                  </div>
                )}
              </div>
            ) : (
              <div style={{ background: '#F8FAFC', padding: 14, borderRadius: 12, color: '#64748B', fontSize: '0.9rem' }}>
                Chưa có dữ liệu điểm danh hôm nay hoặc lớp chưa thực hiện roll call.
              </div>
            )}
          </div>

          {/* Card 2: Bán trú bữa trưa */}
          <div className="card">
            <div style={{ display: 'flex', alignItems: 'center', gap: 10, marginBottom: 14 }}>
              <div
                style={{
                  width: 40,
                  height: 40,
                  borderRadius: 12,
                  background: '#FEF08A',
                  display: 'flex',
                  alignItems: 'center',
                  justifyContent: 'center',
                }}
              >
                <UtensilsCrossed size={22} color="#854D0E" />
              </div>
              <div>
                <h3 style={{ margin: 0, fontSize: '1.1rem' }}>Bữa trưa bán trú</h3>
                <div style={{ fontSize: '0.8rem', color: 'var(--color-muted-fg)' }}>Thực đơn & Suất ăn của con</div>
              </div>
            </div>

            <div style={{ background: '#FFFDF5', border: '1.5px solid #FDE68A', padding: 14, borderRadius: 12 }}>
              <div style={{ fontWeight: 700, fontSize: '0.9rem', marginBottom: 6 }}>Thực đơn hôm nay:</div>
              <ul style={{ margin: 0, paddingLeft: 20, fontSize: '0.875rem', lineHeight: 1.6, color: '#475569' }}>
                <li>Thịt nạc kho tàu cùng trứng cút</li>
                <li>Canh bí đỏ nấu thịt bằm</li>
                <li>Rau muống xào tỏi</li>
                <li>Tráng miệng: Chuối tây / Sữa chua</li>
              </ul>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}

import { useCallback, useEffect, useState } from 'react';
import { useSearchParams } from 'react-router';
import toast from 'react-hot-toast';
import { CheckCheck, Check, Mail, X } from 'lucide-react';
import { accountRequestApi } from '../../services/implementations/accountRequestApi';
import { PaginationBar } from '../../components/common/PaginationBar';
import { asApiError } from '../../utils/apiError';
import { formatViDateTime } from '../../utils/date';
import { SYSTEM_MESSAGES } from '../../constants/messages';
import {
  EMAIL_CHANGE_STATUS,
  ITEM_STATUS,
  REASON_LABEL,
  REQUEST_STATUS,
  type AccountRequestDecisionDto,
  type AccountRequestDto,
  type AccountRequestStatus,
  type EmailChangeRequestDto,
} from '../../types/accountRequest.types';

const showError = (err: unknown) => {
  const e = asApiError(err);
  toast.error(e.errors?.[0] ?? e.message);
};

/**
 * UC-68 Review Account Requests (System Administrator): approve all valid rows, approve or reject selected rows
 * (one reason for several rows), or reject the whole request. Each approved row gets its own Pending Activation
 * Parent account linked to its guardian record (BR-66, BR-67).
 */
export default function AccountRequestReviewPage() {
  const [params] = useSearchParams();
  const [tab, setTab] = useState<'accounts' | 'email-change'>(() =>
    params.get('tab') === 'email-change' ? 'email-change' : 'accounts'
  );
  const [status, setStatus] = useState<string>('');
  const [requests, setRequests] = useState<AccountRequestDto[]>([]);
  const [totalCount, setTotalCount] = useState(0);
  const [pageIndex, setPageIndex] = useState(1);
  const [detail, setDetail] = useState<AccountRequestDto | null>(null);
  const [selected, setSelected] = useState<number[]>([]);
  const [reason, setReason] = useState('');
  const [busy, setBusy] = useState(false);

  const [emailStatus, setEmailStatus] = useState('');
  const [emailReqs, setEmailReqs] = useState<EmailChangeRequestDto[]>([]);
  const [emailTotal, setEmailTotal] = useState(0);
  const [emailPage, setEmailPage] = useState(1);

  const loadEmailReqs = useCallback(async (s?: string, page = 1) => {
    try {
      const res = await accountRequestApi.reviewEmailChangePaged(s || undefined, undefined, page, 15);
      if (res.data) {
        setEmailReqs(res.data.items);
        setEmailTotal(res.data.totalCount);
      }
    } catch (err) {
      showError(err);
    }
  }, []);

  const load = useCallback(async (s?: string, page = 1) => {
    try {
      const res = await accountRequestApi.reviewPaged(s || undefined, undefined, page, 15);
      if (res.data) {
        setRequests(res.data.items);
        setTotalCount(res.data.totalCount);
      }
    } catch (err) {
      showError(err);
    }
  }, []);

  const open = useCallback(async (id: number) => {
    try {
      setDetail((await accountRequestApi.get(id)).data ?? null);
      setSelected([]);
      setReason('');
    } catch (err) {
      showError(err);
    }
  }, []);

  useEffect(() => {
    void load();
    void loadEmailReqs();
    const id = Number(params.get('id'));
    if (id > 0) void open(id);
  }, [params, load, loadEmailReqs, open]);

  const decide = async (partial: Partial<AccountRequestDecisionDto>) => {
    if (!detail) return;
    const decision: AccountRequestDecisionDto = {
      approveItemIds: [],
      rejectItemIds: [],
      approveAllValid: false,
      rejectAll: false,
      rejectReason: reason.trim() || undefined,
      ...partial,
    };
    if ((decision.rejectAll || decision.rejectItemIds.length > 0) && !decision.rejectReason) {
      toast.error('Vui lòng nhập lý do từ chối.');
      return;
    }
    setBusy(true);
    try {
      const res = await accountRequestApi.decide(detail.id, decision);
      if (res.code === 'MSG59') toast.error(res.message || SYSTEM_MESSAGES.MSG59.content, { duration: 8000 });
      else toast.success(res.message || SYSTEM_MESSAGES.MSG61.content);
      res.data?.notApproved.forEach((n) => toast.error(`${n.fullName}: ${n.message} – chưa được duyệt.`, { duration: 8000 }));
      if (res.data) setDetail(res.data.request);
      setSelected([]);
      setReason('');
      await load(status);
    } catch (err) {
      showError(err);
    } finally {
      setBusy(false);
    }
  };

  const waiting = detail?.items?.filter((i) => i.status === 'Waiting') ?? [];
  const canDecide = detail && (detail.status === 'Submitted' || detail.status === 'InReview') && waiting.length > 0;

  return (
    <div className="container pop-in" style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
      <header>
        <h1 style={{ margin: '0 0 4px' }}>Duyệt yêu cầu tài khoản phụ huynh</h1>
        <p style={{ margin: 0, color: 'var(--color-muted-fg)' }}>
          Duyệt cấp tài khoản mới hoặc duyệt yêu cầu đổi email đăng nhập cho Phụ huynh từ Phòng Giáo vụ (BR-66, BR-69).
        </p>
      </header>

      <div style={{ display: 'flex', gap: 8 }} role="tablist">
        <button
          role="tab"
          aria-selected={tab === 'accounts'}
          className={`btn btn--sm ${tab === 'accounts' ? 'btn--primary' : 'btn--secondary'}`}
          onClick={() => setTab('accounts')}
        >
          Yêu cầu cấp tài khoản mới
        </button>
        <button
          role="tab"
          aria-selected={tab === 'email-change'}
          className={`btn btn--sm ${tab === 'email-change' ? 'btn--primary' : 'btn--secondary'}`}
          onClick={() => {
            setTab('email-change');
            void loadEmailReqs(emailStatus, emailPage);
          }}
        >
          <Mail size={14} /> Yêu cầu đổi email Phụ huynh
        </button>
      </div>

      {tab === 'accounts' && !detail && (
        <section className="card" aria-label="Danh sách yêu cầu">
          <div style={{ marginBottom: 12 }}>
            <select className="select" aria-label="Lọc theo trạng thái" value={status} onChange={(e) => {
              setStatus(e.target.value);
              setPageIndex(1);
              void load(e.target.value, 1);
            }}>
              <option value="">Tất cả trạng thái</option>
              {(Object.keys(REQUEST_STATUS) as AccountRequestStatus[]).filter((s) => s !== 'Draft').map((s) => (
                <option key={s} value={s}>{REQUEST_STATUS[s].label}</option>
              ))}
            </select>
          </div>
          <div className="data-table-wrapper">
            <table className="data-table">
              <thead><tr><th>#</th><th>Tiêu đề</th><th>Lý do</th><th>Người gửi</th><th>Ngày gửi</th><th>Số dòng</th><th>Trạng thái</th></tr></thead>
              <tbody>
                {requests.map((r) => (
                  <tr key={r.id} style={{ cursor: 'pointer' }} onClick={() => open(r.id)}>
                    <td>{r.id}</td>
                    <td>{r.title}</td>
                    <td>{REASON_LABEL[r.reason]}</td>
                    <td>{r.requestedByName}</td>
                    <td>{r.submittedAt ? formatViDateTime(r.submittedAt) : '-'}</td>
                    <td>{r.totalRows} ({r.waitingRows} chờ duyệt)</td>
                    <td><span className={REQUEST_STATUS[r.status].badge}>{REQUEST_STATUS[r.status].label}</span></td>
                  </tr>
                ))}
                {requests.length === 0 && <tr><td colSpan={7}>Không có yêu cầu nào.</td></tr>}
              </tbody>
            </table>
          </div>
          <PaginationBar
            pageIndex={pageIndex}
            pageSize={15}
            totalCount={totalCount}
            onPageChange={(p) => {
              setPageIndex(p);
              void load(status, p);
            }}
            itemLabel="yêu cầu"
          />
        </section>
      )}

      {tab === 'accounts' && detail && (
        <section className="card" aria-label="Chi tiết yêu cầu" style={{ display: 'flex', flexDirection: 'column', gap: 10 }}>
          <div style={{ display: 'flex', justifyContent: 'space-between', gap: 8, flexWrap: 'wrap' }}>
            <div>
              <h2 style={{ margin: 0 }}>#{detail.id} {detail.title}</h2>
              <div style={{ fontSize: 'var(--fs-sm)', color: 'var(--color-muted-fg)' }}>
                {REASON_LABEL[detail.reason]} • {detail.requestedByName}{detail.note ? ` • ${detail.note}` : ''}
              </div>
            </div>
            <span className={REQUEST_STATUS[detail.status].badge}>{REQUEST_STATUS[detail.status].label}</span>
          </div>
          <div className="data-table-wrapper">
            <table className="data-table">
              <thead><tr><th /><th>Người giám hộ</th><th>Email</th><th>Học sinh</th><th>Kiểm tra / Trạng thái</th></tr></thead>
              <tbody>
                {(detail.items ?? []).map((i) => (
                  <tr key={i.id}>
                    <td>
                      {i.status === 'Waiting' && (
                        <input type="checkbox" aria-label={`Chọn dòng ${i.guardianName}`} checked={selected.includes(i.id)}
                          onChange={(e) => setSelected(e.target.checked ? [...selected, i.id] : selected.filter((x) => x !== i.id))} />
                      )}
                    </td>
                    <td>{i.guardianName}</td>
                    <td>{i.currentEmail || i.email}</td>
                    <td>{i.students.join(', ')}</td>
                    <td>
                      {i.status === 'Waiting' ? (
                        i.checkResult === 'Passed'
                          ? <span className="badge badge--mint">Hợp lệ</span>
                          : <span className="badge badge--pink" title={i.checkMessage}>{i.checkMessage}</span>
                      ) : (
                        <>
                          <span className={ITEM_STATUS[i.status].badge}>{ITEM_STATUS[i.status].label}</span>
                          {i.reason && <div style={{ fontSize: 'var(--fs-xs)' }}>{i.reason}</div>}
                        </>
                      )}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>

          {canDecide && (
            <div style={{ display: 'flex', flexDirection: 'column', gap: 8 }}>
              <input id="reject-reason" className="input" placeholder="Lý do từ chối (bắt buộc khi từ chối; dùng chung cho các dòng đã chọn)"
                value={reason} onChange={(e) => setReason(e.target.value)} />
              <div style={{ display: 'flex', gap: 8, justifyContent: 'flex-end', flexWrap: 'wrap' }}>
                <button id="btn-reject-all" className="btn btn--secondary btn--sm" disabled={busy} onClick={() => decide({ rejectAll: true })}>
                  <X size={14} /> Từ chối toàn bộ yêu cầu
                </button>
                <button id="btn-reject-selected" className="btn btn--secondary btn--sm" disabled={busy || selected.length === 0}
                  onClick={() => decide({ rejectItemIds: selected })}>
                  <X size={14} /> Từ chối dòng đã chọn
                </button>
                <button id="btn-approve-selected" className="btn btn--secondary btn--sm" disabled={busy || selected.length === 0}
                  onClick={() => decide({ approveItemIds: selected, rejectReason: undefined })}>
                  <Check size={14} /> Duyệt dòng đã chọn
                </button>
                <button id="btn-approve-valid" className="btn btn--primary btn--sm" disabled={busy}
                  onClick={() => decide({ approveAllValid: true, rejectReason: undefined })}>
                  <CheckCheck size={14} /> Duyệt tất cả dòng hợp lệ
                </button>
              </div>
            </div>
          )}

          <div>
            <button className="btn btn--secondary btn--sm" onClick={() => setDetail(null)}>Quay lại danh sách</button>
          </div>
        </section>
      )}

      {tab === 'email-change' && (
        <section className="card" aria-label="Danh sách yêu cầu đổi email phụ huynh" style={{ display: 'flex', flexDirection: 'column', gap: 12 }}>
          <div>
            <select
              className="select"
              aria-label="Lọc trạng thái đổi email"
              value={emailStatus}
              onChange={(e) => {
                setEmailStatus(e.target.value);
                setEmailPage(1);
                void loadEmailReqs(e.target.value, 1);
              }}
            >
              <option value="">Tất cả trạng thái</option>
              {(Object.keys(EMAIL_CHANGE_STATUS) as EmailChangeRequestDto['status'][]).map((s) => (
                <option key={s} value={s}>{EMAIL_CHANGE_STATUS[s].label}</option>
              ))}
            </select>
          </div>
          <div className="data-table-wrapper">
            <table className="data-table">
              <thead>
                <tr>
                  <th>Phụ huynh</th>
                  <th>Email cũ</th>
                  <th>Email mới</th>
                  <th>Lý do / Người gửi</th>
                  <th>Trạng thái</th>
                  <th>Thao tác</th>
                </tr>
              </thead>
              <tbody>
                {emailReqs.map((r) => (
                  <tr key={r.id}>
                    <td><strong>{r.targetFullName}</strong></td>
                    <td>{r.oldEmail}</td>
                    <td><strong>{r.newEmail}</strong></td>
                    <td>
                      <div>{r.reason}</div>
                      <div style={{ fontSize: 'var(--fs-xs)', color: 'var(--color-muted-fg)' }}>Gửi bởi {r.requestedByName}</div>
                    </td>
                    <td>
                      <span className={EMAIL_CHANGE_STATUS[r.status].badge}>{EMAIL_CHANGE_STATUS[r.status].label}</span>
                      {r.reviewNote && <div style={{ fontSize: 'var(--fs-xs)', marginTop: 2 }}>{r.reviewNote}</div>}
                    </td>
                    <td>
                      {r.status === 'Pending' && (
                        <div style={{ display: 'flex', gap: 6, flexWrap: 'wrap' }}>
                          <button
                            type="button"
                            className="btn btn--primary btn--sm"
                            disabled={busy}
                            onClick={async () => {
                              setBusy(true);
                              try {
                                const res = await accountRequestApi.decideEmailChange(r.id, { approve: true });
                                toast.success(res.message || 'Đã duyệt và gửi link xác nhận.');
                                void loadEmailReqs(emailStatus, emailPage);
                              } catch (err) {
                                showError(err);
                              } finally {
                                setBusy(false);
                              }
                            }}
                          >
                            <Check size={14} /> Duyệt & Gửi link
                          </button>
                          <button
                            type="button"
                            className="btn btn--secondary btn--sm"
                            disabled={busy}
                            onClick={async () => {
                              const rejectReason = window.prompt('Nhập lý do từ chối yêu cầu đổi email:');
                              if (!rejectReason?.trim()) return;
                              setBusy(true);
                              try {
                                const res = await accountRequestApi.decideEmailChange(r.id, { approve: false, rejectReason: rejectReason.trim() });
                                toast.success(res.message || 'Đã từ chối yêu cầu.');
                                void loadEmailReqs(emailStatus, emailPage);
                              } catch (err) {
                                showError(err);
                              } finally {
                                setBusy(false);
                              }
                            }}
                          >
                            <X size={14} /> Từ chối
                          </button>
                        </div>
                      )}
                      {(r.status === 'Approved' || r.status === 'EmailFailed') && (
                        <button
                          type="button"
                          className="btn btn--secondary btn--sm"
                          disabled={busy}
                          onClick={async () => {
                            try {
                              const res = await accountRequestApi.resendEmailChangeLink(r.id);
                              toast.success(res.message || 'Đã gửi lại liên kết xác nhận.');
                            } catch (err) {
                              showError(err);
                            }
                          }}
                        >
                          <Mail size={14} /> Gửi lại link
                        </button>
                      )}
                    </td>
                  </tr>
                ))}
                {emailReqs.length === 0 && <tr><td colSpan={6}>Không có yêu cầu đổi email nào.</td></tr>}
              </tbody>
            </table>
          </div>
          <PaginationBar
            pageIndex={emailPage}
            pageSize={15}
            totalCount={emailTotal}
            onPageChange={(p) => {
              setEmailPage(p);
              void loadEmailReqs(emailStatus, p);
            }}
            itemLabel="yêu cầu"
          />
        </section>
      )}
    </div>
  );
}

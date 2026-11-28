import { useCallback, useEffect, useState } from 'react';
import { useSearchParams } from 'react-router';
import toast from 'react-hot-toast';
import { FilePlus2, Mail, RefreshCw, Search, Send, Trash2, UserCheck } from 'lucide-react';
import { accountRequestApi } from '../../services/implementations/accountRequestApi';
import { PaginationBar } from '../../components/common/PaginationBar';
import { asApiError } from '../../utils/apiError';
import { formatViDateTime, formatViDate, todayIso } from '../../utils/date';
import { SYSTEM_MESSAGES } from '../../constants/messages';
import {
  EMAIL_CHANGE_STATUS,
  ITEM_STATUS,
  REASON_LABEL,
  REQUEST_STATUS,
  type AccountRequestDto,
  type AccountRequestReason,
  type AccountRequestRowCheckDto,
  type EmailChangeRequestDto,
  type GuardianWithAccountDto,
  type GuardianWithoutAccountDto,
} from '../../types/accountRequest.types';

type Tab = 'guardians' | 'form' | 'mine' | 'email-change';

interface FormState {
  draftId?: number;
  title: string;
  reason: AccountRequestReason;
  note: string;
  source: 'Manual' | 'Import';
  sourceReference?: string;
  rows: AccountRequestRowCheckDto[];
}

const newForm = (source: 'Manual' | 'Import' = 'Manual', sourceReference?: string): FormState => ({
  title: `${source === 'Import' ? 'Cấp tài khoản sau khi nhập dữ liệu' : 'Yêu cầu cấp tài khoản phụ huynh'} ${formatViDate(todayIso())}`,
  reason: 'NewSchoolYearIntake',
  note: '',
  source,
  sourceReference,
  rows: [],
});

const showError = (err: unknown) => {
  const e = asApiError(err);
  toast.error(e.errors?.[0] ?? e.message);
  e.errors?.slice(1).forEach((m) => toast.error(m));
};

/**
 * UC-67 Request Parent Accounts (Office Staff): Guardians Without Account, the request form (draft or submit, MSG60),
 * and My Account Requests with resubmit, close and resend activation (A1–A4).
 */
export default function AccountRequestsPage() {
  const [params, setParams] = useSearchParams();
  const [tab, setTab] = useState<Tab>('guardians');
  const [search, setSearch] = useState('');
  const [guardians, setGuardians] = useState<GuardianWithoutAccountDto[]>([]);
  const [guardiansTotal, setGuardiansTotal] = useState(0);
  const [guardiansPage, setGuardiansPage] = useState(1);
  const [selected, setSelected] = useState<number[]>([]);
  const [form, setForm] = useState<FormState>(newForm());
  const [busy, setBusy] = useState(false);
  const [mine, setMine] = useState<AccountRequestDto[]>([]);
  const [mineTotal, setMineTotal] = useState(0);
  const [minePage, setMinePage] = useState(1);
  const [detail, setDetail] = useState<AccountRequestDto | null>(null);
  const [rowSel, setRowSel] = useState<number[]>([]);

  // Email change request tab (BR-69, UC-11)
  const [ecSearch, setEcSearch] = useState('');
  const [ecGuardians, setEcGuardians] = useState<GuardianWithAccountDto[]>([]);
  const [ecGuardiansTotal, setEcGuardiansTotal] = useState(0);
  const [ecGuardiansPage, setEcGuardiansPage] = useState(1);
  const [ecRequests, setEcRequests] = useState<EmailChangeRequestDto[]>([]);
  const [ecReqTotal, setEcReqTotal] = useState(0);
  const [ecReqPage, setEcReqPage] = useState(1);
  const [ecTarget, setEcTarget] = useState<GuardianWithAccountDto | null>(null);
  const [ecNewEmail, setEcNewEmail] = useState('');
  const [ecReason, setEcReason] = useState('');

  const loadEmailChangeData = useCallback(async (q?: string, gPage = 1, rPage = 1) => {
    try {
      const [gRes, rRes] = await Promise.all([
        accountRequestApi.guardiansWithAccountPaged(q, gPage, 15),
        accountRequestApi.myEmailChangePaged(rPage, 15),
      ]);
      if (gRes.data) {
        setEcGuardians(gRes.data.items);
        setEcGuardiansTotal(gRes.data.totalCount);
      }
      if (rRes.data) {
        setEcRequests(rRes.data.items);
        setEcReqTotal(rRes.data.totalCount);
      }
    } catch (err) {
      showError(err);
    }
  }, []);

  const loadGuardians = useCallback(async (q?: string, page = 1) => {
    try {
      const res = await accountRequestApi.guardiansWithoutAccountPaged(q, page, 25);
      if (res.data) {
        setGuardians(res.data.items);
        setGuardiansTotal(res.data.totalCount);
      }
    } catch (err) {
      showError(err);
    }
  }, []);

  const loadMine = useCallback(async (page = 1) => {
    try {
      const res = await accountRequestApi.minePaged(page, 15);
      if (res.data) {
        setMine(res.data.items);
        setMineTotal(res.data.totalCount);
      }
    } catch (err) {
      showError(err);
    }
  }, []);

  const openDetail = useCallback(async (id: number) => {
    try {
      setDetail((await accountRequestApi.get(id)).data ?? null);
      setRowSel([]);
      setTab('mine');
    } catch (err) {
      showError(err);
    }
  }, []);

  const startForm = useCallback(async (ids: number[], source: 'Manual' | 'Import' = 'Manual', ref?: string) => {
    const base = newForm(source, ref);
    if (ids.length > 0) {
      try {
        base.rows = (await accountRequestApi.checkRows(ids)).data ?? [];
      } catch (err) {
        showError(err);
      }
    }
    setForm(base);
    setTab('form');
  }, []);

  // Entry points (UC-67 step 1): ?guardianIds=1,2&source=Import from the import result or a student's guardians,
  // ?id=5 from a notification
  useEffect(() => {
    const ids = (params.get('guardianIds') ?? '').split(',').map(Number).filter((n) => n > 0);
    const id = Number(params.get('id'));
    if (ids.length > 0) {
      void startForm(ids, params.get('source') === 'Import' ? 'Import' : 'Manual', params.get('ref') ?? undefined);
    } else if (id > 0) {
      void loadMine().then(() => openDetail(id));
    } else if (params.get('tab') === 'email-change') {
      setTab('email-change');
      void loadEmailChangeData();
    } else {
      void loadGuardians();
    }
  }, [params, startForm, openDetail, loadMine, loadGuardians, loadEmailChangeData]);

  const save = async (submit: boolean) => {
    if (!form.title.trim()) {
      toast.error(SYSTEM_MESSAGES.MSG01.content);
      return;
    }
    if (form.rows.length === 0) {
      toast.error('Yêu cầu cần ít nhất một người giám hộ.');
      return;
    }
    setBusy(true);
    const payload = {
      title: form.title.trim(),
      reason: form.reason,
      note: form.note.trim() || undefined,
      guardianIds: form.rows.map((r) => r.guardianId),
      source: form.source,
      sourceReference: form.sourceReference,
      submit,
    };
    try {
      const res = form.draftId ? await accountRequestApi.updateDraft(form.draftId, payload) : await accountRequestApi.create(payload);
      toast.success(res.message || (submit ? SYSTEM_MESSAGES.MSG60.content : SYSTEM_MESSAGES.MSG08.content));
      res.data?.excludedRows.forEach((e) => toast.error(`${e.fullName}: ${e.message} – không đưa vào yêu cầu.`, { duration: 8000 }));
      setParams({});
      await loadMine();
      if (res.data) await openDetail(res.data.request.id);
    } catch (err) {
      showError(err);
    } finally {
      setBusy(false);
    }
  };

  const continueDraft = (r: AccountRequestDto) => {
    setForm({
      draftId: r.id,
      title: r.title,
      reason: r.reason,
      note: r.note ?? '',
      source: r.source === 'Import' ? 'Import' : 'Manual',
      sourceReference: r.sourceReference,
      rows: (r.items ?? []).map((i) => ({
        guardianId: i.guardianId,
        fullName: i.guardianName,
        email: i.currentEmail,
        checkResult: i.checkResult ?? 'Passed',
        message: i.checkMessage,
        passed: (i.checkResult ?? 'Passed') === 'Passed',
      })),
    });
    setTab('form');
  };

  const followUpRows = detail?.items?.filter((i) => rowSel.includes(i.id)) ?? [];

  const resubmit = async () => {
    if (!detail) return;
    setBusy(true);
    try {
      const res = await accountRequestApi.resubmit(detail.id, rowSel);
      toast.success(res.message || SYSTEM_MESSAGES.MSG60.content);
      res.data?.excludedRows.forEach((e) => toast.error(`${e.fullName}: ${e.message}`, { duration: 8000 }));
      await loadMine();
      // Show the new request (linked to the original one, whose rows are now closed)
      await openDetail(res.data?.request.id ?? detail.id);
    } catch (err) {
      showError(err);
    } finally {
      setBusy(false);
    }
  };

  const closeRows = async () => {
    if (!detail) return;
    const note = window.prompt('Ghi chú khi đóng các dòng đã chọn:');
    if (!note) return;
    try {
      const res = await accountRequestApi.closeRows(detail.id, rowSel, note);
      toast.success(res.message || SYSTEM_MESSAGES.MSG08.content);
      if (res.data) setDetail(res.data);
      setRowSel([]);
    } catch (err) {
      showError(err);
    }
  };

  const resend = async (itemId: number, currentEmail?: string) => {
    const email = window.prompt('Email nhận liên kết kích hoạt (sửa nếu sai):', currentEmail ?? '');
    if (email === null) return;
    try {
      const res = await accountRequestApi.resendActivation(itemId, email.trim() || undefined);
      toast.success(res.message || SYSTEM_MESSAGES.MSG08.content);
      if (detail) await openDetail(detail.id);
    } catch (err) {
      showError(err);
    }
  };

  return (
    <div className="container pop-in" style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
      <header>
        <h1 style={{ margin: '0 0 4px' }}>Yêu cầu cấp tài khoản phụ huynh</h1>
        <p style={{ margin: 0, color: 'var(--color-muted-fg)' }}>
          Tài khoản phụ huynh chỉ được tạo từ hồ sơ người giám hộ có email, sau khi Quản trị viên duyệt yêu cầu.
        </p>
      </header>

      <div style={{ display: 'flex', gap: 8, flexWrap: 'wrap' }} role="tablist">
        <button role="tab" aria-selected={tab === 'guardians'} className={`btn btn--sm ${tab === 'guardians' ? 'btn--primary' : 'btn--secondary'}`}
          onClick={() => { setTab('guardians'); void loadGuardians(search); }}>
          <UserCheck size={15} /> Phụ huynh chưa có tài khoản
        </button>
        <button role="tab" aria-selected={tab === 'form'} className={`btn btn--sm ${tab === 'form' ? 'btn--primary' : 'btn--secondary'}`}
          onClick={() => setTab('form')}>
          <FilePlus2 size={15} /> Soạn yêu cầu
        </button>
        <button role="tab" aria-selected={tab === 'mine'} className={`btn btn--sm ${tab === 'mine' ? 'btn--primary' : 'btn--secondary'}`}
          onClick={() => { setTab('mine'); setDetail(null); void loadMine(); }}>
          <Send size={15} /> Yêu cầu của tôi
        </button>
        <button role="tab" aria-selected={tab === 'email-change'} className={`btn btn--sm ${tab === 'email-change' ? 'btn--primary' : 'btn--secondary'}`}
          onClick={() => { setTab('email-change'); void loadEmailChangeData(ecSearch, 1, 1); }}>
          <Mail size={15} /> Yêu cầu đổi email Phụ huynh
        </button>
      </div>

      {tab === 'guardians' && (
        <section className="card" aria-label="Phụ huynh chưa có tài khoản">
          <div style={{ display: 'flex', gap: 8, marginBottom: 12 }}>
            <input className="input" placeholder="Tìm theo tên, email, mã hoặc tên học sinh" value={search}
              onChange={(e) => setSearch(e.target.value)} onKeyDown={(e) => {
                if (e.key === 'Enter') {
                  setGuardiansPage(1);
                  void loadGuardians(search, 1);
                }
              }} />
            <button className="btn btn--secondary btn--sm" onClick={() => {
              setGuardiansPage(1);
              void loadGuardians(search, 1);
            }}><Search size={15} /> Tìm</button>
            <button id="btn-new-request" className="btn btn--primary btn--sm" disabled={selected.length === 0}
              onClick={() => startForm(selected)}>
              Tạo yêu cầu ({selected.length})
            </button>
          </div>
          <div className="data-table-wrapper">
            <table className="data-table">
              <thead><tr><th /><th>Người giám hộ</th><th>Email</th><th>Học sinh</th><th>Kiểm tra</th></tr></thead>
              <tbody>
                {guardians.map((g) => (
                  <tr key={g.guardianId}>
                    <td>
                      <input type="checkbox" aria-label={`Chọn ${g.fullName}`} disabled={!g.canRequest} checked={selected.includes(g.guardianId)}
                        onChange={(e) => setSelected(e.target.checked ? [...selected, g.guardianId] : selected.filter((x) => x !== g.guardianId))} />
                    </td>
                    <td>{g.fullName}</td>
                    <td>{g.email || '-'}</td>
                    <td>{g.students.join(', ')}</td>
                    <td>{g.canRequest ? <span className="badge badge--mint">Đủ điều kiện</span> : <span title={g.checkMessage}>{g.checkMessage}</span>}</td>
                  </tr>
                ))}
                {guardians.length === 0 && <tr><td colSpan={5}>Không có hồ sơ người giám hộ nào chưa có tài khoản.</td></tr>}
              </tbody>
            </table>
          </div>
          <PaginationBar
            pageIndex={guardiansPage}
            pageSize={25}
            totalCount={guardiansTotal}
            onPageChange={(p) => {
              setGuardiansPage(p);
              void loadGuardians(search, p);
            }}
            itemLabel="phụ huynh"
          />
        </section>
      )}

      {tab === 'form' && (
        <section className="card" aria-label="Soạn yêu cầu" style={{ display: 'flex', flexDirection: 'column', gap: 12 }}>
          <div className="field">
            <label className="label" htmlFor="request-title">Tiêu đề</label>
            <input id="request-title" className="input" value={form.title} maxLength={200} onChange={(e) => setForm({ ...form, title: e.target.value })} />
          </div>
          <div className="responsive-1-2">
            <div className="field">
              <label className="label" htmlFor="request-reason">Lý do</label>
              <select id="request-reason" className="select" value={form.reason} onChange={(e) => setForm({ ...form, reason: e.target.value as AccountRequestReason })}>
                {(Object.keys(REASON_LABEL) as AccountRequestReason[]).map((r) => <option key={r} value={r}>{REASON_LABEL[r]}</option>)}
              </select>
            </div>
            <div className="field">
              <label className="label" htmlFor="request-note">Ghi chú</label>
              <input id="request-note" className="input" value={form.note} onChange={(e) => setForm({ ...form, note: e.target.value })} />
            </div>
          </div>
          <div className="data-table-wrapper">
            <table className="data-table" aria-label="Các dòng của yêu cầu">
              <thead><tr><th>Người giám hộ</th><th>Email</th><th>Kiểm tra</th><th /></tr></thead>
              <tbody>
                {form.rows.map((r) => (
                  <tr key={r.guardianId}>
                    <td>{r.fullName}</td>
                    <td>{r.email || '-'}</td>
                    <td>
                      {r.passed ? <span className="badge badge--mint">Hợp lệ</span> : (
                        <span className="badge badge--pink" title={r.message}>{SYSTEM_MESSAGES.MSG01.content} {r.message}</span>
                      )}
                    </td>
                    <td>
                      <button className="btn btn--secondary btn--sm" aria-label={`Bỏ ${r.fullName}`}
                        onClick={() => setForm({ ...form, rows: form.rows.filter((x) => x.guardianId !== r.guardianId) })}>
                        <Trash2 size={14} />
                      </button>
                    </td>
                  </tr>
                ))}
                {form.rows.length === 0 && <tr><td colSpan={4}>Chọn người giám hộ ở thẻ "Phụ huynh chưa có tài khoản".</td></tr>}
              </tbody>
            </table>
          </div>
          <p style={{ margin: 0, fontSize: 'var(--fs-sm)', color: 'var(--color-muted-fg)' }}>
            Khi gửi, các dòng không hợp lệ sẽ không được đưa vào yêu cầu.
          </p>
          <div style={{ display: 'flex', justifyContent: 'flex-end', gap: 8 }}>
            <button id="btn-save-draft" className="btn btn--secondary btn--sm" disabled={busy} onClick={() => save(false)}>Lưu nháp</button>
            <button id="btn-submit-request" className="btn btn--primary btn--sm" disabled={busy} onClick={() => save(true)}>
              <Send size={15} /> Gửi yêu cầu
            </button>
          </div>
        </section>
      )}

      {tab === 'mine' && !detail && (
        <section className="card" aria-label="Yêu cầu của tôi">
          <div className="data-table-wrapper">
            <table className="data-table">
              <thead><tr><th>#</th><th>Tiêu đề</th><th>Lý do</th><th>Ngày</th><th>Trạng thái</th><th>Số dòng</th></tr></thead>
              <tbody>
                {mine.map((r) => (
                  <tr key={r.id} style={{ cursor: 'pointer' }} onClick={() => openDetail(r.id)}>
                    <td>{r.id}</td>
                    <td>{r.title}</td>
                    <td>{REASON_LABEL[r.reason]}</td>
                    <td>{formatViDateTime(r.submittedAt ?? r.createdAt)}</td>
                    <td><span className={REQUEST_STATUS[r.status].badge}>{REQUEST_STATUS[r.status].label}</span></td>
                    <td>{r.totalRows} ({r.createdRows} đã tạo, {r.rejectedRows + r.failedRows} từ chối/lỗi)</td>
                  </tr>
                ))}
                {mine.length === 0 && <tr><td colSpan={6}>Chưa có yêu cầu nào.</td></tr>}
              </tbody>
            </table>
          </div>
          <PaginationBar
            pageIndex={minePage}
            pageSize={15}
            totalCount={mineTotal}
            onPageChange={(p) => {
              setMinePage(p);
              void loadMine(p);
            }}
            itemLabel="yêu cầu"
          />
        </section>
      )}

      {tab === 'mine' && detail && (
        <section className="card" aria-label="Chi tiết yêu cầu" style={{ display: 'flex', flexDirection: 'column', gap: 10 }}>
          <div style={{ display: 'flex', justifyContent: 'space-between', gap: 8, flexWrap: 'wrap' }}>
            <div>
              <h2 style={{ margin: 0 }}>#{detail.id} {detail.title}</h2>
              <div style={{ fontSize: 'var(--fs-sm)', color: 'var(--color-muted-fg)' }}>
                {REASON_LABEL[detail.reason]}{detail.followsUpRequestId ? ` • Gửi lại từ yêu cầu #${detail.followsUpRequestId}` : ''}
              </div>
            </div>
            <span className={REQUEST_STATUS[detail.status].badge}>{REQUEST_STATUS[detail.status].label}</span>
          </div>
          <div className="data-table-wrapper">
            <table className="data-table">
              <thead><tr><th /><th>Người giám hộ</th><th>Email</th><th>Trạng thái</th><th>Lý do</th><th /></tr></thead>
              <tbody>
                {(detail.items ?? []).map((i) => {
                  const canFollowUp = i.status === 'Rejected' || i.status === 'Failed';
                  return (
                    <tr key={i.id}>
                      <td>
                        {canFollowUp && (
                          <input type="checkbox" aria-label={`Chọn dòng ${i.guardianName}`} checked={rowSel.includes(i.id)}
                            onChange={(e) => setRowSel(e.target.checked ? [...rowSel, i.id] : rowSel.filter((x) => x !== i.id))} />
                        )}
                      </td>
                      <td>{i.guardianName}</td>
                      <td>{i.currentEmail || i.email}</td>
                      <td><span className={ITEM_STATUS[i.status].badge}>{ITEM_STATUS[i.status].label}</span></td>
                      <td>{i.reason ?? i.checkMessage ?? ''}</td>
                      <td>
                        {(i.status === 'AccountCreated' || i.status === 'EmailFailed') && (
                          <button className="btn btn--secondary btn--sm" onClick={() => resend(i.id, i.currentEmail)}>
                            <RefreshCw size={14} /> Gửi lại liên kết kích hoạt
                          </button>
                        )}
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>
          <div style={{ display: 'flex', gap: 8, justifyContent: 'flex-end', flexWrap: 'wrap' }}>
            <button className="btn btn--secondary btn--sm" onClick={() => setDetail(null)}>Quay lại danh sách</button>
            {detail.status === 'Draft' && (
              <button className="btn btn--primary btn--sm" onClick={() => continueDraft(detail)}>Tiếp tục soạn</button>
            )}
            {followUpRows.length > 0 && (
              <>
                <button id="btn-close-rows" className="btn btn--secondary btn--sm" onClick={closeRows}>Đóng các dòng đã chọn</button>
                <button id="btn-resubmit-rows" className="btn btn--primary btn--sm" disabled={busy} onClick={resubmit}>Gửi lại các dòng đã chọn</button>
              </>
            )}
          </div>
        </section>
      )}

      {tab === 'email-change' && (
        <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
          <section className="card" style={{ display: 'flex', flexDirection: 'column', gap: 12 }}>
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', flexWrap: 'wrap', gap: 8 }}>
              <h2 style={{ margin: 0, fontSize: 'var(--fs-md)' }}>Phụ huynh đã có tài khoản</h2>
              <form
                style={{ display: 'flex', gap: 8 }}
                onSubmit={(e) => {
                  e.preventDefault();
                  setEcGuardiansPage(1);
                  void loadEmailChangeData(ecSearch, 1, ecReqPage);
                }}
              >
                <div style={{ position: 'relative' }}>
                  <Search size={14} style={{ position: 'absolute', left: 10, top: '50%', transform: 'translateY(-50%)', color: 'var(--color-muted-fg)' }} />
                  <input
                    className="input"
                    style={{ paddingLeft: 30, minWidth: 240 }}
                    placeholder="Tìm phụ huynh, email, học sinh..."
                    value={ecSearch}
                    onChange={(e) => setEcSearch(e.target.value)}
                  />
                </div>
                <button type="submit" className="btn btn--secondary btn--sm">Tìm</button>
              </form>
            </div>

            {ecTarget && (
              <form
                className="card"
                style={{ background: '#FFFBEB', border: '2px solid var(--color-fg)', display: 'flex', flexDirection: 'column', gap: 10 }}
                onSubmit={async (e) => {
                  e.preventDefault();
                  if (!ecNewEmail.trim() || ecReason.trim().length < 5) {
                    toast.error('Vui lòng nhập email mới hợp lệ và lý do từ 5 ký tự trở lên.');
                    return;
                  }
                  setBusy(true);
                  try {
                    const res = await accountRequestApi.createEmailChange({
                      guardianId: ecTarget.guardianId,
                      newEmail: ecNewEmail.trim(),
                      reason: ecReason.trim(),
                    });
                    toast.success(res.message || 'Đã gửi yêu cầu đổi email tới Quản trị viên.');
                    setEcTarget(null);
                    setEcNewEmail('');
                    setEcReason('');
                    void loadEmailChangeData(ecSearch, ecGuardiansPage, 1);
                  } catch (err) {
                    showError(err);
                  } finally {
                    setBusy(false);
                  }
                }}
              >
                <strong>Đề xuất đổi email cho: {ecTarget.fullName} (Hiện tại: {ecTarget.currentEmail})</strong>
                <div style={{ display: 'flex', gap: 10, flexWrap: 'wrap' }}>
                  <input
                    type="email"
                    className="input"
                    style={{ flex: '1 1 220px' }}
                    placeholder="Địa chỉ email mới *"
                    value={ecNewEmail}
                    onChange={(e) => setEcNewEmail(e.target.value)}
                  />
                  <input
                    type="text"
                    className="input"
                    style={{ flex: '2 1 280px' }}
                    placeholder="Lý do đổi email (vd: Phụ huynh mất quyền truy cập email cũ) *"
                    value={ecReason}
                    onChange={(e) => setEcReason(e.target.value)}
                  />
                </div>
                <div style={{ display: 'flex', gap: 8, justifyContent: 'flex-end' }}>
                  <button type="button" className="btn btn--secondary btn--sm" onClick={() => setEcTarget(null)}>Hủy</button>
                  <button type="submit" className="btn btn--primary btn--sm" disabled={busy}>Gửi Admin duyệt</button>
                </div>
              </form>
            )}

            <div className="data-table-wrapper">
              <table className="data-table">
                <thead>
                  <tr><th>Phụ huynh</th><th>Email hiện tại</th><th>Học sinh</th><th>Trạng thái TK</th><th /></tr>
                </thead>
                <tbody>
                  {ecGuardians.map((g) => (
                    <tr key={g.guardianId}>
                      <td><strong>{g.fullName}</strong></td>
                      <td>{g.currentEmail}</td>
                      <td>{g.students.join(', ')}</td>
                      <td>{g.accountStatus}</td>
                      <td>
                        <button
                          type="button"
                          className="btn btn--secondary btn--sm"
                          disabled={g.hasPendingEmailChange}
                          onClick={() => {
                            setEcTarget(g);
                            setEcNewEmail('');
                            setEcReason('');
                          }}
                        >
                          <Mail size={14} /> {g.hasPendingEmailChange ? 'Đang chờ duyệt' : 'Đề xuất đổi email'}
                        </button>
                      </td>
                    </tr>
                  ))}
                  {ecGuardians.length === 0 && <tr><td colSpan={5}>Không có phụ huynh đã cấp tài khoản phù hợp.</td></tr>}
                </tbody>
              </table>
            </div>
            <PaginationBar
              pageIndex={ecGuardiansPage}
              pageSize={15}
              totalCount={ecGuardiansTotal}
              onPageChange={(p) => {
                setEcGuardiansPage(p);
                void loadEmailChangeData(ecSearch, p, ecReqPage);
              }}
              itemLabel="phụ huynh"
            />
          </section>

          <section className="card" style={{ display: 'flex', flexDirection: 'column', gap: 12 }}>
            <h2 style={{ margin: 0, fontSize: 'var(--fs-md)' }}>Yêu cầu đổi email tôi đã gửi</h2>
            <div className="data-table-wrapper">
              <table className="data-table">
                <thead>
                  <tr><th>Phụ huynh</th><th>Email cũ → Email mới</th><th>Lý do</th><th>Trạng thái</th><th /></tr>
                </thead>
                <tbody>
                  {ecRequests.map((r) => (
                    <tr key={r.id}>
                      <td><strong>{r.targetFullName}</strong></td>
                      <td>{r.oldEmail} → <strong>{r.newEmail}</strong></td>
                      <td>{r.reason}</td>
                      <td>
                        <span className={EMAIL_CHANGE_STATUS[r.status].badge}>{EMAIL_CHANGE_STATUS[r.status].label}</span>
                        {r.reviewNote && <div style={{ fontSize: 'var(--fs-xs)', marginTop: 2 }}>{r.reviewNote}</div>}
                      </td>
                      <td>
                        {(r.status === 'Approved' || r.status === 'EmailFailed') && (
                          <button
                            type="button"
                            className="btn btn--secondary btn--sm"
                            onClick={async () => {
                              try {
                                const res = await accountRequestApi.resendEmailChangeLink(r.id);
                                toast.success(res.message || 'Đã gửi lại liên kết xác nhận.');
                              } catch (err) {
                                showError(err);
                              }
                            }}
                          >
                            <RefreshCw size={14} /> Gửi lại link
                          </button>
                        )}
                      </td>
                    </tr>
                  ))}
                  {ecRequests.length === 0 && <tr><td colSpan={5}>Chưa có yêu cầu đổi email nào.</td></tr>}
                </tbody>
              </table>
            </div>
            <PaginationBar
              pageIndex={ecReqPage}
              pageSize={15}
              totalCount={ecReqTotal}
              onPageChange={(p) => {
                setEcReqPage(p);
                void loadEmailChangeData(ecSearch, ecGuardiansPage, p);
              }}
              itemLabel="yêu cầu"
            />
          </section>
        </div>
      )}
    </div>
  );
}

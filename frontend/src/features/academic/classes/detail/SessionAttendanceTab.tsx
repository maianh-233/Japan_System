import { useEffect, useMemo, useState } from 'react';
import { Modal, Popover, QRCode, Radio } from 'antd';
import dayjs, { type Dayjs } from 'dayjs';
import { CheckCheck, DoorOpen, Headset, QrCode, ScanLine, Smartphone, UserRound, X } from 'lucide-react';
import { Button, EmptyState, Select, StatusTag, TextInput, TimeInput, UserCell, useNotify } from '@/components/common';
import { useCrmStore } from '@/features/crm/crmStore';
import { leaveRecord, usePortalStore } from '@/features/portal/portalStore';
import { useStudentStore } from '@/features/student/studentStore';
import { useAuth } from '@/hooks/useAuth';
import { cn } from '@/lib/cn';
import { fmtDate } from '@/lib/date';
import { hashString } from '@/lib/text';
import { qrCodeOf, qrPayload } from '../attendanceQr';
import { useClassStore } from '../classStore';
import { canTakeAttendance, defaultRecords, rosterFor, summarize } from '../sessionUtils';
import type { AttendanceMethod, AttendanceRecord, AttendanceStatus, ClassInfo, ClassSession, SupportEntry } from '../types';
import { USE_API } from '@/lib/api/config';
import * as portalApi from '@/features/portal/portalApi';

const ATT_OPTIONS: { value: AttendanceStatus; label: string }[] = [
  { value: 'present', label: 'Có mặt' },
  { value: 'late', label: 'Muộn' },
  { value: 'excused', label: 'Có phép' },
  { value: 'absent', label: 'Vắng' },
];

/** Quét QR sau giờ bắt đầu quá số phút này → tự tính "Đi muộn" */
export const LATE_AFTER_MIN = 10;

export const METHOD: Record<AttendanceMethod, { label: string; short: string; icon: typeof QrCode; cls: string }> = {
  qr: { label: 'Học viên tự quét QR', short: 'QR', icon: QrCode, cls: 'text-success-700' },
  teacher: { label: 'Giáo viên điểm danh', short: 'GV', icon: UserRound, cls: 'text-primary-700' },
  staff: { label: 'Nhân viên điểm danh hộ', short: 'NV', icon: Headset, cls: 'text-warning-700' },
};

const LEAVE_REASONS = ['Đau bụng / mệt', 'Sốt, đau đầu', 'Việc gia đình gấp', 'Phụ huynh đón sớm', 'Tăng ca / việc công ty'];

const toMin = (t: string) => {
  const [h, m] = t.split(':').map(Number);
  return h * 60 + m;
};
const atTime = (t?: string) => (t ? dayjs().hour(toMin(t) / 60 | 0).minute(toMin(t) % 60).second(0) : undefined);
/** Giờ "bây giờ" nếu đang trong buổi, ngoài ra lấy giờ bắt đầu */
export const nowOr = (s: ClassSession) => {
  const n = dayjs();
  return n.format('YYYY-MM-DD') === s.date && n.format('HH:mm') >= s.startTime && n.format('HH:mm') <= s.endTime ? n.format('HH:mm') : s.startTime;
};
const lateBy = (s: ClassSession, t?: string) => (t ? toMin(t) - toMin(s.startTime) : 0);

/**
 * Điểm danh học viên của 1 buổi:
 *  - HV tự quét mã QR (Cổng học viên) → ghi giờ vào lớp, quét trễ > 10 phút tự tính "Đi muộn"
 *  - GV điểm danh / nhân viên điểm danh HỘ khi HV không quét được (quên điện thoại, trẻ nhỏ...)
 *  - Ghi nhận HV về sớm giữa buổi (đau bụng, việc gấp) kèm giờ + lý do
 *  Thao tác của nhân viên được ghi vào nhật ký hỗ trợ của buổi.
 */
export function SessionAttendanceTab({ cls, s, siblings, editable }: { cls: ClassInfo; s: ClassSession; siblings: ClassSession[]; editable: boolean }) {
  const notify = useNotify();
  const { user } = useAuth();
  const students = useCrmStore((st) => st.students);
  const enrollments = useStudentStore((st) => st.enrollments);
  const updateSession = useClassStore((st) => st.updateSession);
  const roster = useMemo(() => rosterFor(cls.id, s.date, enrollments), [cls.id, s.date, enrollments]);
  const allLeaves = usePortalStore((st) => st.leaveRequests);
  const recordMakeup = usePortalStore((st) => st.recordMakeup);
  const classes = useClassStore((st) => st.classes);
  /** Đơn xin nghỉ của HV lớp này cho buổi này (bỏ đơn bị từ chối) */
  const leaveOf = (studentId: string) => allLeaves.find((l) => l.sessionId === s.id && l.studentId === studentId && l.status !== 'rejected');
  /** HV lớp khác đăng ký học bù vào buổi này (đơn đã duyệt) */
  const guests = useMemo(() => allLeaves.filter((l) => l.status === 'approved' && l.makeupSessionId === s.id), [allLeaves, s.id]);
  const [records, setRecords] = useState<AttendanceRecord[]>(() => {
    const saved = new Map((s.records ?? []).map((r) => [r.studentId, r]));
    // Có đơn nghỉ đã duyệt → "có phép" sẵn; bảo lưu = có phép
    const base = defaultRecords(roster).map((r) => {
      const lr = allLeaves.find((l) => l.sessionId === s.id && l.studentId === r.studentId && l.status === 'approved');
      return lr ? leaveRecord(lr) : r;
    });
    const guestRecs: AttendanceRecord[] = guests.map((l) => ({ studentId: l.studentId, status: 'absent', makeupFor: { sessionId: l.sessionId, classId: l.classId, leaveRequestId: l.id } }));
    // Chưa điểm danh: HV chưa quét = "Vắng" (chờ quét)
    return [...base, ...guestRecs].map((r) => saved.get(r.studentId) ?? (s.records || r.status === 'excused' ? r : { ...r, status: 'absent' as const }));
  });
  const [filter, setFilter] = useState<AttendanceStatus | 'all' | 'left'>('all');
  const [dirty, setDirty] = useState(false);
  const [qrOpen, setQrOpen] = useState(false);
  /** HV được nhân viên chỉnh tay trong lần lưu này */
  const [touched, setTouched] = useState<Set<string>>(new Set());

  const canEdit = editable && canTakeAttendance(s);
  const sum = summarize(records);
  const nameOf = (id: string) => students.find((x) => x.id === id)?.fullName ?? id;
  const by = user ? `${user.fullName} (${user.roleName})` : 'Nhân viên';

  const history = useMemo(() => {
    const prev = siblings
      .filter((x) => x.id !== s.id && x.records && (x.date < s.date || (x.date === s.date && x.startTime < s.startTime)))
      .sort((a, b) => a.date.localeCompare(b.date) || a.startTime.localeCompare(b.startTime));
    const last = prev[prev.length - 1];
    const map = new Map<string, { absent: number; lastAbsent: boolean }>();
    roster.forEach((e) =>
      map.set(e.studentId, {
        absent: prev.filter((x) => x.records!.some((r) => r.studentId === e.studentId && r.status === 'absent')).length,
        lastAbsent: Boolean(last?.records?.some((r) => r.studentId === e.studentId && r.status === 'absent')),
      }),
    );
    return map;
  }, [siblings, s, roster]);

  /** Nhân viên chỉnh tay → ghi nhận "điểm danh hộ" */
  const set = (studentId: string, patch: Partial<AttendanceRecord>) => {
    setRecords((rs) =>
      rs.map((r) => {
        if (r.studentId !== studentId) return r;
        const next = { ...r, ...patch };
        if (patch.status === 'present' || patch.status === 'late') {
          next.checkInAt = r.checkInAt ?? (patch.status === 'late' ? nowOr(s) : s.startTime);
          next.method = r.method === 'qr' ? 'qr' : 'staff';
          if (patch.status === 'present') next.note = undefined;
        }
        if (patch.status === 'absent' || patch.status === 'excused') Object.assign(next, { checkInAt: undefined, method: undefined, leftAt: undefined, leaveReason: undefined });
        // Sửa giờ vào → tự tính muộn
        if (patch.checkInAt) {
          next.method = r.method === 'qr' && !('method' in patch) ? 'staff' : (next.method ?? 'staff');
          next.status = lateBy(s, patch.checkInAt) > LATE_AFTER_MIN ? 'late' : 'present';
        }
        return next;
      }),
    );
    setTouched((t) => new Set(t).add(studentId));
    setDirty(true);
  };

  /** Lưu + ghi nhật ký hỗ trợ cho các thao tác của nhân viên. Chế độ API: server lưu, tự ghi nhật ký hỗ trợ / kết quả học bù, trả cảnh báo */
  const persist = async (list: AttendanceRecord[], extraLog: SupportEntry[] = []): Promise<string[]> => {
    if (USE_API) return (await portalApi.saveAttendance(s.id, list, user?.portal === 'teacher')).warnings;
    const sm = summarize(list);
    updateSession(s.id, {
      records: list,
      attendance: { present: sm.attended, total: sm.total },
      attendanceBy: { name: user?.fullName ?? 'Giáo vụ', at: new Date().toISOString() },
      status: s.status === 'scheduled' ? 'completed' : s.status,
      supportLog: extraLog.length ? [...(s.supportLog ?? []), ...extraLog] : s.supportLog,
    });
    // HV học bù: ghi kết quả về đơn xin nghỉ + buổi gốc ở lớp của HV
    list.filter((r) => r.makeupFor).forEach((r) => recordMakeup(r.makeupFor!.leaveRequestId, r.status));
    return [];
  };

  const save = async () => {
    const log: SupportEntry[] = [];
    const stamp = new Date().toISOString();
    const helped = records.filter((r) => touched.has(r.studentId) && r.method === 'staff' && (r.status === 'present' || r.status === 'late'));
    if (helped.length) log.push({ id: `SL-${Date.now()}-a`, kind: 'attendance', note: `Điểm danh hộ ${helped.length} HV: ${helped.map((r) => nameOf(r.studentId)).join(', ')}.`, by, at: stamp });
    const before = new Map((s.records ?? []).map((r) => [r.studentId, r]));
    records
      .filter((r) => r.leftAt && before.get(r.studentId)?.leftAt !== r.leftAt)
      .forEach((r, i) => log.push({ id: `SL-${Date.now()}-l${i}`, kind: 'early_leave', note: `${nameOf(r.studentId)} về sớm lúc ${r.leftAt}${r.leaveReason ? ` — ${r.leaveReason}` : ''}.`, by, at: stamp }));
    let warnings: string[];
    try {
      warnings = await persist(records, log);
    } catch (e) {
      return void notify.error({ title: 'Chưa lưu được điểm danh', description: (e as Error).message });
    }
    setDirty(false);
    setTouched(new Set());
    const streak = records.filter((r) => r.status === 'absent' && history.get(r.studentId)?.lastAbsent);
    notify.toast.success(`Đã lưu điểm danh: ${summarize(records).attended}/${records.length} có mặt`);
    if (USE_API) warnings.forEach((w) => notify.warning({ title: w }));
    else if (streak.length)
      notify.warning({ title: `${streak.length} học viên vắng 2 buổi liên tiếp`, description: `${streak.map((r) => nameOf(r.studentId)).join(', ')} — nhắc Giáo vụ / Sale liên hệ.` });
  };

  /** HV quét QR (mô phỏng) → ghi ngay, không cần bấm Lưu */
  const scan = (studentId: string) => {
    const t = nowOr(s);
    const next = records.map((r) => (r.studentId === studentId ? { ...r, status: (lateBy(s, t) > LATE_AFTER_MIN ? 'late' : 'present') as AttendanceStatus, checkInAt: t, method: 'qr' as const } : r));
    setRecords(next);
    void persist(next);
  };

  if (!roster.length && !guests.length) return <EmptyState size="compact" title="Lớp chưa có học viên" description="Thêm học viên ở tab Học viên & học phí." />;

  const left = records.filter((r) => r.leftAt);
  const methods = { qr: 0, teacher: 0, staff: 0 };
  records.forEach((r) => r.method && (r.status === 'present' || r.status === 'late') && methods[r.method]++);
  const rows = records.filter((r) => filter === 'all' || (filter === 'left' ? r.leftAt : r.status === filter));
  const notScanned = records.filter((r) => r.status === 'absent' && roster.find((e) => e.studentId === r.studentId)?.status !== 'reserved');

  return (
    <div className="flex flex-col gap-3">
      {s.status === 'cancelled' ? (
        <p className="rounded-lg bg-page px-3 py-2 text-[13px] text-ink-muted">Buổi đã hủy — không điểm danh.</p>
      ) : !canTakeAttendance(s) ? (
        <p className="rounded-lg border border-primary-200 bg-primary-50 px-3 py-2 text-[13px] text-primary-800">
          Chưa tới giờ học — mã QR điểm danh mở lúc {s.startTime} ngày {fmtDate(s.date)}. Học viên dự kiến: {roster.length}.
        </p>
      ) : (
        <div className="flex flex-wrap items-center justify-between gap-3 rounded-[var(--radius-card)] border border-line bg-page px-3.5 py-3">
          <div className="flex items-start gap-2.5 text-[13px]">
            <QrCode size={20} className="mt-0.5 shrink-0 text-primary-600" aria-hidden />
            <div>
              <p className="font-medium text-ink">Điểm danh bằng mã QR</p>
              <p className="text-ink-muted">
                Học viên quét trên Cổng học viên · quét sau {dayjs(`${s.date}T${s.startTime}`).add(LATE_AFTER_MIN, 'minute').format('HH:mm')} tính đi muộn · HV không quét được →
                nhân viên điểm danh hộ bên dưới
              </p>
            </div>
          </div>
          {canEdit && (
            <Button icon={ScanLine} variant="soft" onClick={() => setQrOpen(true)}>
              Mở mã QR
            </Button>
          )}
        </div>
      )}

      {/* Tổng hợp + lọc */}
      <div className="flex flex-wrap items-center gap-1.5">
        {([['all', `Tất cả ${sum.total}`], ...ATT_OPTIONS.map((o) => [o.value, `${o.label} ${sum[o.value]}`]), ['left', `Về sớm ${left.length}`]] as [typeof filter, string][]).map(([k, label]) => (
          <button
            key={k}
            type="button"
            onClick={() => setFilter(k)}
            className={cn(
              'tabular rounded-full border px-3 py-1 text-[13px] transition-colors',
              filter === k ? 'border-primary-500 bg-primary-50 font-medium text-primary-700' : 'border-line text-ink-secondary hover:bg-page',
            )}
          >
            {label}
          </button>
        ))}
        <span className="tabular ml-auto flex flex-wrap gap-x-3 text-xs text-ink-muted">
          {(Object.keys(METHOD) as AttendanceMethod[]).map((m) => {
            const M = METHOD[m];
            return (
              <span key={m} className={cn('inline-flex items-center gap-1', M.cls)} title={M.label}>
                <M.icon size={13} aria-hidden /> {M.short} {methods[m]}
              </span>
            );
          })}
        </span>
      </div>

      <ul className="divide-y divide-line rounded-[var(--radius-card)] border border-line">
        {rows.map((r) => {
          const st = students.find((x) => x.id === r.studentId);
          const enr = roster.find((e) => e.studentId === r.studentId);
          const h = history.get(r.studentId);
          const M = r.method && METHOD[r.method];
          const here = r.status === 'present' || r.status === 'late';
          return (
            <li key={r.studentId} className="flex flex-col gap-2 px-3 py-2.5">
              <div className="flex flex-col gap-2 sm:flex-row sm:items-center">
                <UserCell
                  size="sm"
                  name={st?.fullName ?? r.studentId}
                  subtitle={
                    <span className="flex flex-wrap gap-x-2">
                      <span className="font-mono">{st?.code}</span>
                      {enr?.status === 'reserved' && <span className="text-primary-700">Bảo lưu</span>}
                      {h && h.absent > 0 && <span className={cn(h.absent >= 3 ? 'text-danger-600' : 'text-ink-muted')}>Đã vắng {h.absent} buổi</span>}
                      {h?.lastAbsent && r.status === 'absent' && <span className="font-medium text-danger-600">Vắng 2 buổi liền</span>}
                      {r.makeupFor && <span className="font-medium text-warning-700">Học bù từ lớp {classes.find((c) => c.id === r.makeupFor!.classId)?.code.replace(/^GV[VN]*_/, '') ?? ''}</span>}
                      {(() => {
                        const lr = !r.makeupFor ? leaveOf(r.studentId) : undefined;
                        if (!lr) return null;
                        return lr.status === 'approved' ? (
                          <span className="text-primary-700" title={lr.reason}>
                            Có đơn nghỉ ({lr.reasonType})
                          </span>
                        ) : (
                          <span className="font-medium text-warning-700" title={lr.reason}>
                            Đơn nghỉ chờ duyệt
                          </span>
                        );
                      })()}
                      {r.madeUp && <span className="font-medium text-success-700">Đã học bù ở {r.madeUp.classCode}</span>}
                    </span>
                  }
                  className="min-w-0 flex-1"
                />
                {s.status !== 'cancelled' &&
                  (canEdit ? (
                    <Radio.Group size="small" optionType="button" buttonStyle="solid" value={r.status} onChange={(e) => set(r.studentId, { status: e.target.value })} options={ATT_OPTIONS} />
                  ) : s.records ? (
                    <StatusTag group="attendance" status={r.status} size="sm" />
                  ) : null)}
              </div>

              {/* Giờ vào · cách điểm danh · về sớm */}
              {s.status !== 'cancelled' && (here || r.note) && (
                <div className="flex flex-wrap items-center gap-x-3 gap-y-1.5 pl-0 text-[13px] sm:pl-11">
                  {here && (
                    <span className="inline-flex items-center gap-1.5">
                      <span className="text-ink-muted">Vào lớp</span>
                      {canEdit ? (
                        <TimeInput size="small" value={atTime(r.checkInAt)} onChange={(v: Dayjs | null) => v && set(r.studentId, { checkInAt: v.format('HH:mm') })} minuteStep={1} className="!w-24" allowClear={false} />
                      ) : (
                        <span className="tabular font-medium text-ink">{r.checkInAt ?? '—'}</span>
                      )}
                      {r.checkInAt && lateBy(s, r.checkInAt) > 0 && <span className="tabular text-xs text-warning-700">+{lateBy(s, r.checkInAt)}′</span>}
                    </span>
                  )}
                  {here && M && (
                    <span className={cn('inline-flex items-center gap-1 text-xs', M.cls)}>
                      <M.icon size={13} aria-hidden /> {M.label}
                    </span>
                  )}
                  {r.leftAt ? (
                    <span className="inline-flex items-center gap-1 rounded-md bg-warning-50 px-1.5 py-0.5 text-xs text-warning-700">
                      <DoorOpen size={13} aria-hidden /> Về sớm {r.leftAt}
                      {r.leaveReason && ` — ${r.leaveReason}`}
                      {canEdit && (
                        <button type="button" aria-label="Bỏ ghi nhận về sớm" className="ml-0.5 rounded hover:bg-warning-100" onClick={() => set(r.studentId, { leftAt: undefined, leaveReason: undefined })}>
                          <X size={12} aria-hidden />
                        </button>
                      )}
                    </span>
                  ) : (
                    here && canEdit && <EarlyLeave s={s} onSave={(t, reason) => set(r.studentId, { leftAt: t, leaveReason: reason })} />
                  )}
                  {canEdit && r.status !== 'present' ? (
                    <TextInput size="small" placeholder="Ghi chú (lý do...)" value={r.note} onChange={(e) => set(r.studentId, { note: e.target.value || undefined })} className="sm:!w-56" />
                  ) : (
                    r.note && <span className="text-xs text-ink-muted">{r.note}</span>
                  )}
                </div>
              )}
            </li>
          );
        })}
        {!rows.length && <li className="px-3 py-6 text-center text-[13px] text-ink-muted">Không có học viên nào ở mục này</li>}
      </ul>

      {canEdit && (
        <div className="sticky bottom-0 z-10 flex flex-wrap items-center justify-between gap-2 border-t border-line bg-surface py-3">
          <span className="text-xs text-ink-muted">{s.attendanceBy ? `Lần cuối: ${s.attendanceBy.name} · ${dayjs(s.attendanceBy.at).format('HH:mm DD/MM')}` : 'Chưa lưu điểm danh'}</span>
          <div className="flex gap-2">
            <Button
              icon={CheckCheck}
              variant="outline"
              onClick={() => {
                notScanned.forEach((r) => set(r.studentId, { status: 'present' }));
              }}
              disabled={!notScanned.length}
            >
              Điểm danh hộ còn lại ({notScanned.length})
            </Button>
            <Button action="save" disabled={!dirty && Boolean(s.records)} onClick={save}>
              Lưu điểm danh
            </Button>
          </div>
        </div>
      )}

      {qrOpen && (
        <QrModal
          s={s}
          cls={cls}
          scanned={records.filter((r) => r.method === 'qr' && r.checkInAt).length}
          total={records.length}
          waiting={notScanned.map((r) => r.studentId)}
          nameOf={nameOf}
          onScan={scan}
          onClose={() => setQrOpen(false)}
        />
      )}
    </div>
  );
}

/** Nút "Về sớm": chọn giờ + lý do */
function EarlyLeave({ s, onSave }: { s: ClassSession; onSave: (t: string, reason: string) => void }) {
  const [open, setOpen] = useState(false);
  const [time, setTime] = useState<Dayjs | undefined>();
  const [reason, setReason] = useState<string>();
  const [note, setNote] = useState('');
  return (
    <Popover
      open={open}
      onOpenChange={(v) => {
        setOpen(v);
        if (v) {
          setTime(atTime(nowOr(s) === s.startTime ? s.endTime : nowOr(s)));
          setReason(undefined);
          setNote('');
        }
      }}
      trigger="click"
      title="Học viên về sớm giữa buổi"
      content={
        <div className="flex w-64 flex-col gap-2">
          <TimeInput value={time} onChange={(v: Dayjs | null) => setTime(v ?? undefined)} minuteStep={5} />
          <Select placeholder="Lý do" value={reason} onChange={setReason} options={LEAVE_REASONS.map((x) => ({ value: x, label: x }))} showSearch={false} />
          <TextInput size="small" placeholder="Ghi chú thêm (ai đón, đã báo PH...)" value={note} onChange={(e) => setNote(e.target.value)} />
          <Button
            size="sm"
            action="save"
            disabled={!time || !reason}
            onClick={() => {
              if (!time || !reason) return;
              const t = time.format('HH:mm');
              if (t <= s.startTime || t >= s.endTime) return;
              onSave(t, note.trim() ? `${reason} (${note.trim()})` : reason);
              setOpen(false);
            }}
          >
            Ghi nhận
          </Button>
        </div>
      }
    >
      <button type="button" className="inline-flex items-center gap-1 text-xs text-ink-muted hover:text-warning-700">
        <DoorOpen size={13} aria-hidden /> Về sớm
      </button>
    </Popover>
  );
}

/**
 * Mã QR điểm danh — CỐ ĐỊNH cho mỗi buổi (không cần đổi: nhân viên trung tâm còn xác nhận lại buổi học).
 * (Mô phỏng: nút "HV quét" thay cho điện thoại học viên)
 */
export function QrModal({
  s,
  cls,
  scanned,
  total,
  waiting,
  nameOf,
  onScan,
  onClose,
}: {
  s: ClassSession;
  cls: ClassInfo;
  scanned: number;
  total: number;
  waiting: string[];
  nameOf: (id: string) => string;
  onScan: (studentId: string) => void;
  onClose: () => void;
}) {
  const { user } = useAuth();
  const [qr, setQr] = useState<portalApi.SessionQr>();
  useEffect(() => {
    if (!USE_API) return;
    portalApi.getSessionQr(s.id, user?.portal === 'teacher').then(setQr, () => undefined);
  }, [s.id, user?.portal]);
  return (
    <Modal open onCancel={onClose} footer={null} width={460} title={`Mã QR điểm danh · ${cls.code}`} destroyOnHidden>
      <div className="flex flex-col items-center gap-3">
        <QRCode value={qr?.payload ?? qrPayload(s)} size={256} bordered={false} className="!bg-white" errorLevel="M" status={USE_API && !qr ? 'loading' : 'active'} />
        <p className="text-center text-[13px] text-ink-muted">
          Học viên mở Cổng học viên → <b className="text-ink">Quét QR điểm danh</b>
          <br />
          Không quét được? Nhập mã: <span className="tabular rounded-md bg-page px-2 py-0.5 font-mono text-base font-bold tracking-[0.2em] text-ink">{USE_API ? (qr?.code ?? '······') : qrCodeOf(s.id)}</span>
        </p>
        <p className="text-xs text-ink-muted">
          Buổi {s.no || '(học thêm)'} · {s.startTime}–{s.endTime} · mã cố định cho buổi này
        </p>
        <p className="tabular text-sm">
          Đã quét <strong>{scanned}</strong>/{total} học viên
        </p>
        {USE_API && qr && !qr.isOpen && (
          <p className="text-center text-xs text-warning-700">
            Mã nhận quét từ {dayjs(qr.opensAt).format('HH:mm DD/MM')} đến {dayjs(qr.closesAt).format('HH:mm')} — ngoài giờ này học viên quét sẽ bị từ chối.
          </p>
        )}
        {USE_API ? (
          <p className="text-xs text-ink-muted">Danh sách tự cập nhật khi học viên quét.</p>
        ) : waiting.length > 0 ? (
          <div className="w-full rounded-lg border border-dashed border-line p-3">
            <p className="mb-2 flex items-center gap-1.5 text-xs text-ink-muted">
              <Smartphone size={13} aria-hidden /> Mô phỏng học viên quét (bản demo)
            </p>
            <div className="flex flex-wrap gap-1.5">
              {waiting.slice(0, 8).map((id) => (
                <button key={id} type="button" onClick={() => onScan(id)} className="rounded-full border border-line px-2.5 py-0.5 text-xs hover:border-primary-300 hover:bg-primary-50">
                  {nameOf(id)}
                </button>
              ))}
            </div>
          </div>
        ) : (
          <p className="text-[13px] text-success-700">Tất cả học viên đã điểm danh.</p>
        )}
      </div>
    </Modal>
  );
}

/** Hình QR giả (lưới ô vuông theo hash) — chỉ để trang trí, KHÔNG quét được. Điểm danh dùng QRCode thật (attendanceQr) */
export function FakeQr({ seed, className }: { seed: string; className?: string }) {
  const n = 25;
  const cells: [number, number][] = [];
  let h = hashString(seed);
  const finder = (x: number, y: number) => {
    const inBox = (ox: number, oy: number) => x >= ox && x < ox + 7 && y >= oy && y < oy + 7;
    return inBox(0, 0) || inBox(n - 7, 0) || inBox(0, n - 7);
  };
  for (let y = 0; y < n; y++)
    for (let x = 0; x < n; x++) {
      if (finder(x, y)) continue;
      h = (Math.imul(h, 1103515245) + 12345) >>> 0;
      if ((h >>> 16) & 1) cells.push([x, y]);
    }
  const Finder = ({ x, y }: { x: number; y: number }) => (
    <>
      <rect x={x} y={y} width={7} height={7} fill="currentColor" />
      <rect x={x + 1} y={y + 1} width={5} height={5} fill="white" />
      <rect x={x + 2} y={y + 2} width={3} height={3} fill="currentColor" />
    </>
  );
  return (
    <svg viewBox={`-1 -1 ${n + 2} ${n + 2}`} className={className ?? "h-56 w-56 rounded-lg bg-white text-ink"} role="img" aria-label="Mã QR">
      {cells.map(([x, y]) => (
        <rect key={`${x}-${y}`} x={x} y={y} width={1} height={1} fill="currentColor" />
      ))}
      <Finder x={0} y={0} />
      <Finder x={n - 7} y={0} />
      <Finder x={0} y={n - 7} />
    </svg>
  );
}

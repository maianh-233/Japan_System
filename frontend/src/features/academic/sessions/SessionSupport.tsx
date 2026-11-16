import { useMemo, useState } from 'react';
import { Modal, Radio } from 'antd';
import dayjs, { type Dayjs } from 'dayjs';
import { Headset, PauseCircle, Play, Plus, Square } from 'lucide-react';
import { Button, Select, TextInput, TimeInput, useConfirm, useNotify } from '@/components/common';
import { useAuth } from '@/hooks/useAuth';
import { useClassStore } from '../classes/classStore';
import { teacherName } from '../classes/constants';
import type { ClassInfo, ClassSession, SupportEntry, SupportKind, TeacherAttendance } from '../classes/types';
import { busyStaff, isPaused, staffOf, takeoverOptions } from './sessionOps';

export const SUPPORT_KIND: Record<SupportKind, string> = {
  attendance: 'Điểm danh hộ',
  early_leave: 'Học viên về sớm',
  teacher_checkin: 'Chấm công hộ GV',
  incident: 'Sự cố',
  room: 'Phòng / thiết bị',
  other: 'Hỗ trợ khác',
};

const by = (u: ReturnType<typeof useAuth>['user']) => (u ? `${u.fullName} (${u.roleName})` : 'Nhân viên');

/** Lớp đang TẠM DỪNG vì sự cố → cho học tiếp (ai dạy, lúc mấy giờ) hoặc cho nghỉ, xếp học bù */
export function PauseFollowUp({ cls, s, editable, onMakeup }: { cls: ClassInfo; s: ClassSession; editable: boolean; onMakeup: (s: ClassSession) => void }) {
  const { user } = useAuth();
  const notify = useNotify();
  const { confirm, confirmDialog } = useConfirm();
  const sessions = useClassStore((x) => x.sessions);
  const classes = useClassStore((x) => x.classes);
  const updateSession = useClassStore((x) => x.updateSession);
  const [open, setOpen] = useState(false);
  const [time, setTime] = useState<Dayjs | null>(null);
  const [who, setWho] = useState<'same' | 'other'>('same');
  const [teacherId, setTeacherId] = useState<string>();

  const leads = staffOf(s, cls).filter((x) => x.role !== 'assistant').map((x) => x.id);
  const assistants = staffOf(s, cls).filter((x) => x.role === 'assistant').map((x) => x.id);
  const busy = useMemo(() => busyStaff(s.date, time?.format('HH:mm') ?? s.startTime, s.endTime, sessions, classes, s.id), [s, time, sessions, classes]);

  if (!s.incident || !isPaused(s)) return null;
  const inc = s.incident;
  const pausedMin = Math.max(0, dayjs().diff(dayjs(`${s.date}T${inc.at}`), 'minute'));

  const resume = () => {
    if (!time) return;
    const t = time.format('HH:mm');
    const stamp = new Date().toISOString();
    const nextId = who === 'same' ? leads[0] : teacherId;
    if (!nextId) return;
    const rows: TeacherAttendance[] = [...(s.teacherAttendance ?? [])];
    if (who === 'same') {
      // GV cũ quay lại: mở lại giờ ra, ghi chú khoảng tạm dừng
      leads.forEach((id) => {
        const i = rows.findIndex((r) => r.teacherId === id);
        if (i >= 0) rows[i] = { ...rows[i], checkOut: undefined, note: `Tạm dừng ${inc.at}–${t} (sự cố), dạy tiếp`, source: 'staff', checkedAt: stamp };
      });
    } else if (!rows.some((r) => r.teacherId === nextId)) {
      rows.push({ teacherId: nextId, role: assistants.includes(nextId) ? 'assistant' : 'substitute', status: 'present', checkIn: t, note: `Dạy tiếp từ ${t} sau sự cố`, source: 'staff', checkedAt: stamp });
    }
    updateSession(s.id, {
      incident: { ...inc, outcome: { kind: 'resumed', at: t, teacherId: nextId, by: by(user) }, takeoverId: who === 'same' ? undefined : nextId },
      teacherAttendance: rows,
      note: `Tạm dừng ${inc.at}–${t} do GV gặp sự cố`,
      supportLog: [...(s.supportLog ?? []), { id: `SL-${Date.now()}`, kind: 'incident', note: `Lớp học tiếp lúc ${t} — ${teacherName(nextId)} đứng lớp.`, by: by(user), at: stamp }],
    });
    setOpen(false);
    notify.toast.success(`Lớp học tiếp lúc ${t}`);
  };

  const end = async () => {
    const ok = await confirm({
      title: 'Cho lớp nghỉ, xếp học bù?',
      description: `Buổi dừng từ ${inc.at}. Phần còn lại cần xếp học bù.`,
      consequences: ['Học viên / phụ huynh nhận thông báo nghỉ phần còn lại', 'Giờ dạy GV tính tới lúc xảy ra sự cố'],
      tone: 'warning',
      confirmAction: 'save',
      confirmText: 'Cho nghỉ',
    });
    if (!ok) return;
    const stamp = new Date().toISOString();
    updateSession(s.id, {
      incident: { ...inc, action: 'end_early', outcome: { kind: 'ended', at: dayjs().format('HH:mm'), by: by(user) } },
      note: `Nghỉ từ ${inc.at} do GV gặp sự cố — cần học bù phần còn lại`,
      supportLog: [...(s.supportLog ?? []), { id: `SL-${Date.now()}`, kind: 'incident', note: 'Cho lớp nghỉ phần còn lại, chờ xếp học bù.', by: by(user), at: stamp }],
    });
    onMakeup(s);
  };

  const otherOptions = [
    ...assistants.map((id) => ({ value: id, label: `${teacherName(id)} (trợ giảng)`, disabled: false })),
    ...takeoverOptions(cls, busy, leads),
  ];

  return (
    <div className="flex flex-wrap items-center justify-between gap-2 rounded-lg border border-danger-100 bg-surface px-3 py-2.5">
      <p className="flex items-center gap-2 text-[13px] font-medium text-danger-700">
        <PauseCircle size={16} className="animate-pulse" aria-hidden /> Lớp đang tạm dừng từ {inc.at}
        {s.date === dayjs().format('YYYY-MM-DD') && <span className="font-normal text-ink-muted">({pausedMin} phút)</span>}
      </p>
      {editable && (
        <div className="flex gap-1.5">
          <Button
            icon={Play}
            size="sm"
            tone="success"
            onClick={() => {
              setTime(dayjs());
              setWho('same');
              setTeacherId(undefined);
              setOpen(true);
            }}
          >
            Cho học tiếp
          </Button>
          <Button icon={Square} size="sm" tone="danger" variant="soft" onClick={end}>
            Cho nghỉ, xếp học bù
          </Button>
        </div>
      )}
      <Modal open={open} onCancel={() => setOpen(false)} title="Cho lớp học tiếp" okText="Học tiếp" onOk={resume} okButtonProps={{ disabled: !time || (who === 'other' && !teacherId) }} destroyOnHidden>
        <div className="flex flex-col gap-3 py-2">
          <label className="flex flex-col gap-1 text-[13px]">
            <span className="text-ink-muted">Học tiếp lúc</span>
            <TimeInput value={time} onChange={setTime} minuteStep={5} />
          </label>
          <Radio.Group value={who} onChange={(e) => setWho(e.target.value)} className="flex flex-col gap-1.5">
            <Radio value="same">{leads.map(teacherName).join(', ')} đã ổn, dạy tiếp</Radio>
            <Radio value="other">Người khác đứng lớp</Radio>
          </Radio.Group>
          {who === 'other' && <Select placeholder="Chọn GV rảnh / trợ giảng" value={teacherId} onChange={setTeacherId} options={otherOptions} />}
        </div>
      </Modal>
      {confirmDialog}
    </div>
  );
}

/** Nhật ký hỗ trợ của nhân viên trong buổi + ghi thêm */
export function SupportLogSection({ s, editable }: { s: ClassSession; editable: boolean }) {
  const { user } = useAuth();
  const updateSession = useClassStore((x) => x.updateSession);
  const [adding, setAdding] = useState(false);
  const [kind, setKind] = useState<SupportKind>('other');
  const [note, setNote] = useState('');
  const log = [...(s.supportLog ?? [])].sort((a, b) => a.at.localeCompare(b.at));

  const add = () => {
    if (!note.trim()) return;
    const e: SupportEntry = { id: `SL-${Date.now()}`, kind, note: note.trim(), by: by(user), at: new Date().toISOString() };
    updateSession(s.id, { supportLog: [...(s.supportLog ?? []), e] });
    setNote('');
    setAdding(false);
  };

  if (!log.length && !editable) return null;
  return (
    <section>
      <div className="mb-2 flex items-center justify-between gap-2">
        <h3 className="flex items-center gap-1.5 text-sm font-semibold text-ink">
          <Headset size={16} aria-hidden /> Nhân viên hỗ trợ trong buổi
        </h3>
        {editable && !adding && (
          <Button icon={Plus} size="sm" variant="ghost" onClick={() => setAdding(true)}>
            Ghi nhận hỗ trợ
          </Button>
        )}
      </div>
      {adding && (
        <div className="mb-2 flex flex-col gap-2 rounded-lg border border-line bg-page p-2.5 sm:flex-row">
          <Select value={kind} onChange={(v) => setKind(v as SupportKind)} options={(Object.keys(SUPPORT_KIND) as SupportKind[]).map((k) => ({ value: k, label: SUPPORT_KIND[k] }))} showSearch={false} allowClear={false} className="sm:!w-44" />
          <TextInput size="small" autoFocus placeholder="VD: HV không đăng nhập được Cổng HV — đặt lại mật khẩu tại quầy" value={note} onChange={(e) => setNote(e.target.value)} onPressEnter={add} className="flex-1" />
          <div className="flex gap-1">
            <Button size="sm" action="save" disabled={!note.trim()} onClick={add} />
            <Button size="sm" action="cancel" variant="ghost" onClick={() => setAdding(false)} />
          </div>
        </div>
      )}
      {log.length ? (
        <ol className="flex flex-col gap-1.5 border-l-2 border-line pl-3">
          {log.map((e) => (
            <li key={e.id} className="text-[13px]">
              <span className="tabular text-xs text-ink-muted">{dayjs(e.at).format('HH:mm DD/MM')}</span> · <span className="font-medium text-ink-secondary">{SUPPORT_KIND[e.kind]}</span> — {e.note}
              <span className="text-xs text-ink-muted"> ({e.by})</span>
            </li>
          ))}
        </ol>
      ) : (
        <p className="text-[13px] text-ink-muted">Chưa có. Ghi lại khi nhân viên làm thay GV / HV (điểm danh hộ, chấm công hộ, xử lý sự cố...).</p>
      )}
    </section>
  );
}

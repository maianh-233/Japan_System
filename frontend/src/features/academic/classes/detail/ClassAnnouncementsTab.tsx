import { useMemo, useState } from 'react';
import { Checkbox, Form } from 'antd';
import dayjs, { type Dayjs } from 'dayjs';
import { Clock, Mail, Megaphone, MessageCircle, MonitorSmartphone, Send, Users } from 'lucide-react';
import { Button, DateInput, EmptyState, ModalForm, Select, StatusTag, TextArea, TextInput, useNotify } from '@/components/common';
import { useCrmStore } from '@/features/crm/crmStore';
import { summarizeInvoice, useStudentStore } from '@/features/student/studentStore';
import { useAuth } from '@/hooks/useAuth';
import { cn } from '@/lib/cn';
import { fmtDate } from '@/lib/date';
import { useCurriculumStore } from '../../curriculum/curriculumStore';
import { useClassStore } from '../classStore';
import { teacherNames } from '../constants';
import type { AnnouncementAudience, AnnouncementChannel, ClassInfo } from '../types';

const AUDIENCE: Record<AnnouncementAudience, string> = { student: 'Học viên', parent: 'Phụ huynh', teacher: 'Giáo viên' };
const CHANNEL: Record<AnnouncementChannel, { label: string; icon: typeof Mail }> = {
  portal: { label: 'Cổng thông tin', icon: MonitorSmartphone },
  zalo: { label: 'Zalo', icon: MessageCircle },
  email: { label: 'Email', icon: Mail },
};

interface Values {
  template: string;
  title: string;
  content: string;
  audience: AnnouncementAudience[];
  channels: AnnouncementChannel[];
  debtorsOnly: boolean;
  schedule: boolean;
  scheduledAt?: Dayjs;
}

/** Tab Thông báo: soạn + gửi tin cho học viên / phụ huynh / giáo viên của lớp, lịch sử đã gửi */
export function ClassAnnouncementsTab({ cls }: { cls: ClassInfo }) {
  const { can, user } = useAuth();
  const notify = useNotify();
  const [form] = Form.useForm<Values>();
  const [open, setOpen] = useState(false);
  const all = useClassStore((s) => s.announcements);
  const sessions = useClassStore((s) => s.sessions);
  const addAnnouncement = useClassStore((s) => s.addAnnouncement);
  const enrollments = useStudentStore((s) => s.enrollments);
  const invoices = useStudentStore((s) => s.invoices);
  const links = useCrmStore((s) => s.links);
  const curriculum = useCurriculumStore((s) => s.curricula.find((c) => c.id === cls.curriculumId));
  const list = useMemo(() => all.filter((a) => a.classId === cls.id).sort((a, b) => (b.scheduledAt ?? b.sentAt).localeCompare(a.scheduledAt ?? a.sentAt)), [all, cls.id]);
  const editable = can('academic.classes') && cls.status !== 'cancelled';

  const active = enrollments.filter((e) => e.classId === cls.id && (e.status === 'studying' || e.status === 'reserved'));
  const debtors = active.filter((e) => {
    const inv = invoices.find((i) => i.enrollmentId === e.id);
    return inv && summarizeInvoice(inv).remaining > 0;
  });
  const countFor = (audience: AnnouncementAudience[], debtorsOnly: boolean) => {
    const base = debtorsOnly ? debtors : active;
    const ids = new Set(base.map((e) => e.studentId));
    return (
      (audience.includes('student') ? base.length : 0) +
      (audience.includes('parent') ? links.filter((l) => ids.has(l.studentId) && l.relationship !== 'self').length : 0) +
      (audience.includes('teacher') && !debtorsOnly ? cls.teacherIds.length : 0)
    );
  };

  const nextSession = sessions.filter((s) => s.classId === cls.id && s.status === 'scheduled').sort((a, b) => a.date.localeCompare(b.date))[0];
  const nextExam = sessions.filter((s) => s.classId === cls.id && s.kind === 'exam' && s.status === 'scheduled').sort((a, b) => a.date.localeCompare(b.date))[0];
  const examTitle = nextExam && curriculum?.exams.find((e) => e.id === nextExam.examId)?.title;

  const TEMPLATES: Record<string, Partial<Values> & { label: string }> = {
    custom: { label: 'Tự soạn', title: '', content: '' },
    off: {
      label: 'Thông báo nghỉ học',
      title: `Lớp ${cls.code} nghỉ học${nextSession ? ` ngày ${fmtDate(nextSession.date)}` : ''}`,
      content: `Trung tâm thông báo lớp ${cls.code} nghỉ buổi${nextSession ? ` ${fmtDate(nextSession.date)} (${nextSession.startTime}–${nextSession.endTime})` : ''} vì lý do: ... Buổi học bù sẽ được thông báo sau. Xin lỗi các bạn vì sự bất tiện.`,
      audience: ['student', 'parent', 'teacher'],
      debtorsOnly: false,
    },
    room: {
      label: 'Đổi phòng / đổi giờ',
      title: `Lớp ${cls.code} đổi phòng học`,
      content: `Từ buổi${nextSession ? ` ${fmtDate(nextSession.date)}` : ' tới'}, lớp ${cls.code} chuyển sang học tại phòng ... Giờ học giữ nguyên ${cls.startTime}–${cls.endTime}.`,
      audience: ['student', 'teacher'],
      debtorsOnly: false,
    },
    tuition: {
      label: 'Nhắc đóng học phí',
      title: `Nhắc đóng học phí lớp ${cls.code}`,
      content: `Chào bạn, trung tâm xin nhắc học phí lớp ${cls.code} vẫn còn chưa hoàn tất. Bạn có thể chuyển khoản / quét VietQR trên Cổng học viên hoặc đóng trực tiếp tại quầy. Cảm ơn bạn!`,
      audience: ['student', 'parent'],
      debtorsOnly: true,
    },
    exam: {
      label: 'Nhắc lịch thi',
      title: examTitle ? `Nhắc lịch ${examTitle.toLowerCase()}` : `Nhắc lịch thi lớp ${cls.code}`,
      content: `Lớp ${cls.code} sẽ ${examTitle?.toLowerCase() ?? 'thi'}${nextExam ? ` vào ${fmtDate(nextExam.date)}, ${nextExam.startTime}` : ''}. Các bạn ôn tập kỹ, mang theo bút chì, tẩy và đến sớm 10 phút.`,
      audience: ['student', 'parent'],
      debtorsOnly: false,
    },
  };

  return (
    <div className="flex flex-col gap-4">
      <div className="flex flex-wrap items-center gap-2">
        <p className="text-[13px] text-ink-muted">
          {active.length} học viên · {countFor(['parent'], false)} phụ huynh · GV {teacherNames(cls.teacherIds)}
        </p>
        {editable && (
          <Button icon={Megaphone} className="ml-auto" onClick={() => setOpen(true)}>
            Soạn thông báo
          </Button>
        )}
      </div>

      {!list.length ? (
        <EmptyState size="compact" title="Chưa gửi thông báo nào" description="Gửi tin cho học viên, phụ huynh và giáo viên qua Cổng thông tin, Zalo, email." />
      ) : (
        <ul className="flex flex-col gap-3">
          {list.map((a) => {
            const pending = a.scheduledAt && dayjs(a.scheduledAt).isAfter(dayjs());
            const readPct = a.readCount !== undefined && a.recipients ? Math.round((a.readCount / a.recipients) * 100) : undefined;
            return (
              <li key={a.id} className="rounded-[var(--radius-card)] border border-line p-4">
                <div className="flex flex-wrap items-start gap-2">
                  <div className="min-w-0 flex-1">
                    <p className="font-medium text-ink">{a.title}</p>
                    <p className="text-xs text-ink-muted">
                      {a.sentBy} · {dayjs(a.scheduledAt ?? a.sentAt).format('HH:mm DD/MM/YYYY')}
                    </p>
                  </div>
                  {pending ? (
                    <StatusTag tone="warning" icon={Clock} size="sm">
                      Chờ gửi
                    </StatusTag>
                  ) : (
                    <StatusTag tone="success" icon={Send} size="sm">
                      Đã gửi
                    </StatusTag>
                  )}
                </div>
                <p className="mt-2 line-clamp-3 whitespace-pre-line text-[13px] text-ink-secondary">{a.content}</p>
                <div className="mt-3 flex flex-wrap items-center gap-x-4 gap-y-1 text-xs text-ink-muted">
                  <span className="flex items-center gap-1">
                    <Users size={13} aria-hidden /> {a.audience.map((x) => AUDIENCE[x]).join(', ')}
                    {a.debtorsOnly && ' (còn nợ học phí)'} · {a.recipients} người
                  </span>
                  <span className="flex items-center gap-2">
                    {a.channels.map((c) => {
                      const Icon = CHANNEL[c].icon;
                      return (
                        <span key={c} className="flex items-center gap-1">
                          <Icon size={13} aria-hidden /> {CHANNEL[c].label}
                        </span>
                      );
                    })}
                  </span>
                  {!pending && readPct !== undefined && (
                    <span className="flex items-center gap-1.5">
                      <span className="h-1.5 w-16 overflow-hidden rounded-full bg-page">
                        <span className="block h-full bg-primary-500" style={{ width: `${readPct}%` }} />
                      </span>
                      {a.readCount}/{a.recipients} đã xem
                    </span>
                  )}
                </div>
              </li>
            );
          })}
        </ul>
      )}

      <ModalForm<Values>
        form={form}
        open={open}
        onClose={() => setOpen(false)}
        width={680}
        title="Soạn thông báo cho lớp"
        description={`${cls.code} — ${cls.name}`}
        submitAction="save"
        submitText="Gửi thông báo"
        successMessage="Đã gửi thông báo"
        initialValues={{ template: 'custom', audience: ['student', 'parent'], channels: ['portal', 'zalo'], debtorsOnly: false, schedule: false }}
        onSubmit={(v) => {
          const recipients = countFor(v.audience, v.debtorsOnly);
          if (!recipients) throw new Error('Không có người nhận');
          addAnnouncement({
            classId: cls.id,
            title: v.title.trim(),
            content: v.content.trim(),
            audience: v.audience,
            channels: v.channels,
            debtorsOnly: v.debtorsOnly || undefined,
            recipients,
            readCount: v.schedule ? undefined : 0,
            sentBy: user?.fullName ?? 'Giáo vụ',
            sentAt: new Date().toISOString(),
            scheduledAt: v.schedule && v.scheduledAt ? v.scheduledAt.toISOString() : undefined,
          });
          if (v.schedule && v.scheduledAt) notify.toast.info(`Sẽ gửi lúc ${v.scheduledAt.format('HH:mm DD/MM')}`);
        }}
      >
        <Form.Item name="template" label="Mẫu">
          <Select
            showSearch={false}
            options={Object.entries(TEMPLATES).map(([k, t]) => ({ value: k, label: t.label }))}
            onChange={(k) => {
              const { label: _l, ...rest } = TEMPLATES[k as string];
              form.setFieldsValue(rest);
            }}
          />
        </Form.Item>
        <Form.Item name="title" label="Tiêu đề" rules={[{ required: true, whitespace: true, message: 'Nhập tiêu đề' }]}>
          <TextInput maxLength={120} />
        </Form.Item>
        <Form.Item name="content" label="Nội dung" rules={[{ required: true, whitespace: true, message: 'Nhập nội dung' }]}>
          <TextArea rows={5} maxLength={1000} showCount />
        </Form.Item>
        <div className="grid grid-cols-1 gap-x-4 sm:grid-cols-2">
          <Form.Item name="audience" label="Gửi tới" rules={[{ required: true, type: 'array', min: 1, message: 'Chọn người nhận' }]}>
            <Checkbox.Group options={(Object.keys(AUDIENCE) as AnnouncementAudience[]).map((k) => ({ value: k, label: AUDIENCE[k] }))} />
          </Form.Item>
          <Form.Item name="channels" label="Kênh" rules={[{ required: true, type: 'array', min: 1, message: 'Chọn kênh' }]}>
            <Checkbox.Group options={(Object.keys(CHANNEL) as AnnouncementChannel[]).map((k) => ({ value: k, label: CHANNEL[k].label }))} />
          </Form.Item>
        </div>
        <Form.Item name="debtorsOnly" valuePropName="checked" className="!mb-2">
          <Checkbox>Chỉ gửi học viên còn nợ học phí ({debtors.length})</Checkbox>
        </Form.Item>
        <Form.Item name="schedule" valuePropName="checked" className="!mb-2">
          <Checkbox>Hẹn giờ gửi</Checkbox>
        </Form.Item>
        <Form.Item noStyle shouldUpdate>
          {({ getFieldValue }) => (
            <>
              {getFieldValue('schedule') && (
                <Form.Item name="scheduledAt" label="Gửi lúc" rules={[{ required: true, message: 'Chọn thời điểm' }]}>
                  <DateInput showTime disabledDate={(d) => d.isBefore(dayjs(), 'day')} />
                </Form.Item>
              )}
              <p className={cn('rounded-lg px-3 py-2 text-[13px]', countFor(getFieldValue('audience') ?? [], getFieldValue('debtorsOnly')) ? 'bg-primary-50 text-primary-800' : 'bg-warning-50 text-warning-700')}>
                Sẽ gửi tới <strong>{countFor(getFieldValue('audience') ?? [], getFieldValue('debtorsOnly'))}</strong> người
                {getFieldValue('debtorsOnly') && getFieldValue('audience')?.includes('teacher') ? ' (bỏ qua giáo viên với tin học phí)' : ''}
              </p>
            </>
          )}
        </Form.Item>
      </ModalForm>
    </div>
  );
}

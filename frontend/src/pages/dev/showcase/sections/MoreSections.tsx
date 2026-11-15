import { useState, type ReactNode } from 'react';
import dayjs, { type Dayjs } from 'dayjs';
import { Bell, Clock, Inbox, UserRoundX } from 'lucide-react';
import {
  Avatar,
  Button,
  DateInput,
  DateRangeInput,
  EmptyState,
  ErrorState,
  FileUpload,
  LoadingSkeleton,
  STATUS_PRESETS,
  Select,
  StatusTag,
  TimeInput,
  TimeRangeInput,
  Timetable,
  UserCell,
  useConfirm,
  useNotify,
  type DateRange,
  type SkeletonVariant,
  type StatusGroup,
  type TimetableEvent,
  type UploadItem,
} from '@/components/common';

export function Section({ id, title, description, children }: { id: string; title: string; description?: string; children: ReactNode }) {
  return (
    <section id={id} className="scroll-mt-20 rounded-[var(--radius-card)] border border-line bg-surface p-4 shadow-[var(--shadow-soft-sm)] sm:p-6">
      <h2 className="text-xl leading-7 font-semibold">{title}</h2>
      {description && <p className="mt-1 text-[13px] text-ink-muted">{description}</p>}
      <div className="mt-4">{children}</div>
    </section>
  );
}

// ---------------- Select / Ngày / Giờ / File ----------------
export function PickersSection() {
  const [teachers, setTeachers] = useState<string[]>([]);
  const [range, setRange] = useState<DateRange>(null);
  const [files, setFiles] = useState<UploadItem[]>([]);

  /** Giả lập upload có tiến độ */
  const fakeUpload = (file: File, onProgress: (p: number) => void) =>
    new Promise<{ url: string }>((resolve, reject) => {
      let p = 0;
      const t = setInterval(() => {
        p += 20;
        onProgress(p);
        if (p >= 100) {
          clearInterval(t);
          if (file.name.toLowerCase().includes('loi')) reject(new Error('Máy chủ từ chối file'));
          else resolve({ url: URL.createObjectURL(file) });
        }
      }, 250);
    });

  return (
    <Section id="pickers" title="Select · Ngày · Giờ · FileUpload" description="Tìm kiếm trong Select không phân biệt dấu: gõ “nguyen” sẽ ra “Nguyễn”.">
      <div className="grid grid-cols-1 gap-4 md:grid-cols-2">
        <Select
          label="Khóa học"
          required
          placeholder="Chọn khóa học"
          options={[
            { value: 'n5', label: 'JLPT N5 cơ bản' },
            { value: 'n4', label: 'JLPT N4' },
            { value: 'n3', label: 'JLPT N3 cấp tốc' },
            { value: 'kaiwa', label: 'Kaiwa giao tiếp' },
          ]}
        />
        <Select
          label="Giáo viên"
          mode="multiple"
          placeholder="Chọn một hoặc nhiều"
          value={teachers}
          onChange={setTeachers}
          options={['Nguyễn Thu Hà', 'Trần Đức Minh', 'Tanaka Yuki', 'Lê Ngọc Anh', 'Satō Kenji'].map((v) => ({ value: v, label: v }))}
        />
        <DateInput label="Ngày khai giảng" hint="Định dạng dd/mm/yyyy" />
        <DateInput label="Hạn nộp bài" showTime />
        <DateRangeInput label="Thời gian khóa học" value={range} onChange={setRange} hint="Mobile tự tách thành 2 ô" />
        <TimeInput label="Giờ check-in" />
        <TimeRangeInput label="Khung giờ học" />
        <Select label="Có lỗi" error="Vui lòng chọn phòng học" placeholder="Phòng học" options={[]} />
        <FileUpload
          label="Tài liệu bài giảng"
          kind={['document', 'spreadsheet']}
          upload={fakeUpload}
          value={files}
          onChange={setFiles}
          fieldClassName="md:col-span-2"
          hint="Đặt tên file có chữ “loi” để thử trạng thái lỗi"
        />
        <FileUpload label="File nghe (đề thi Choukai)" kind="audio" maxFiles={3} maxSizeMB={50} fieldClassName="md:col-span-2" />
      </div>
    </Section>
  );
}

// ---------------- Notification / Confirm ----------------
export function NotifySection() {
  const notify = useNotify();
  const { confirm, confirmDialog } = useConfirm();

  return (
    <Section id="notify" title="Notification · ConfirmDialog" description="Thông báo đổi màu, icon, thời gian hiển thị theo chức năng. Lỗi giữ lâu hơn thành công.">
      <div className="flex flex-col gap-5">
        <div className="flex flex-col gap-2">
          <p className="text-[13px] font-medium text-ink-secondary">Thông báo góc màn hình</p>
          <div className="flex flex-wrap gap-2">
            <Button action="approve" variant="soft" onClick={() => notify.success({ title: 'Đã lưu học viên', description: 'Nguyễn Văn An đã được thêm vào lớp N3-K12.' })}>
              Success
            </Button>
            <Button icon={Bell} variant="soft" onClick={() => notify.info({ title: 'Lịch học thay đổi', description: 'Lớp N4-K15 chuyển sang phòng 302 từ thứ 4.' })}>
              Info
            </Button>
            <Button action="edit" icon={Clock} onClick={() => notify.warning({ title: 'Lớp sắp đủ sĩ số', description: 'N3-K12 còn 2 chỗ trống.' })}>
              Warning
            </Button>
            <Button action="delete" onClick={() => notify.error({ title: 'Không lưu được hóa đơn', description: 'Mất kết nối máy chủ. Vui lòng thử lại.' })}>
              Error
            </Button>
            <Button
              action="send"
              variant="outline"
              onClick={() =>
                notify
                  .promise(new Promise((r) => setTimeout(r, 1800)), {
                    loading: 'Đang gửi thông báo nghỉ học...',
                    success: 'Đã gửi tới 18 học viên',
                  })
                  .catch(() => undefined)
              }
            >
              Loading → Success
            </Button>
            <Button
              action="approve"
              variant="outline"
              onClick={async () => {
                const ok = await notify.confirm({
                  title: 'Có 3 đơn xin nghỉ mới',
                  description: 'Lớp N4-K15 · buổi tối thứ 6',
                  confirmText: 'Duyệt ngay',
                  tone: 'success',
                });
                notify.toast[ok ? 'success' : 'info'](ok ? 'Đã duyệt 3 đơn' : 'Để sau');
              }}
            >
              Confirm trong thông báo
            </Button>
            <Button icon={Bell} tone="neutral" variant="ghost" onClick={() => notify.toast.success('Đã sao chép số điện thoại')}>
              Toast nhanh
            </Button>
          </div>
        </div>

        <div className="flex flex-col gap-2">
          <p className="text-[13px] font-medium text-ink-secondary">Hộp xác nhận</p>
          <div className="flex flex-wrap gap-2">
            <Button
              action="delete"
              onClick={() =>
                confirm({
                  title: 'Xóa tài liệu “Minna no Nihongo bài 12”?',
                  description: 'Tài liệu sẽ bị gỡ khỏi 4 lớp đang dùng.',
                  onConfirm: () => new Promise((r) => setTimeout(r, 800)),
                })
              }
            >
              Xóa (danger)
            </Button>
            <Button
              action="reject"
              onClick={() =>
                confirm({
                  title: 'Từ chối đơn xin hủy buổi?',
                  tone: 'danger',
                  confirmAction: 'reject',
                  requireReason: { label: 'Lý do từ chối', placeholder: 'Giáo viên sẽ nhận được lý do này' },
                })
              }
            >
              Từ chối (bắt nhập lý do)
            </Button>
            <Button
              icon={UserRoundX}
              tone="danger"
              variant="solid"
              onClick={() =>
                confirm({
                  title: 'Hủy toàn bộ lớp N3-K12?',
                  description: 'Chỉ trung tâm được hủy cả lớp. Thao tác không thể hoàn tác.',
                  consequences: ['18 học viên nhận thông báo hủy lớp', '24 buổi còn lại bị hủy', 'Phát sinh yêu cầu hoàn học phí cho học viên đã đóng'],
                  confirmAction: 'delete',
                  confirmText: 'Hủy lớp',
                  requireReason: true,
                  typeToConfirm: 'N3-K12',
                  onConfirm: async () => {
                    await new Promise((r) => setTimeout(r, 900));
                  },
                })
              }
            >
              Hủy cả lớp (gõ xác nhận)
            </Button>
            <Button
              action="approve"
              onClick={() =>
                confirm({ title: 'Duyệt 5 đơn xin nghỉ đã chọn?', tone: 'success', confirmText: 'Duyệt 5 đơn' })
              }
            >
              Duyệt (success)
            </Button>
          </div>
        </div>
      </div>
      {confirmDialog}
    </Section>
  );
}

// ---------------- StatusTag / Avatar ----------------
export function TagsSection() {
  return (
    <Section id="tags" title="StatusTag · Avatar · UserCell" description="Mỗi trạng thái luôn có màu + icon + chữ.">
      <div className="flex flex-col gap-4">
        {(Object.keys(STATUS_PRESETS) as StatusGroup[]).map((g) => (
          <div key={g} className="flex flex-col gap-2 sm:flex-row sm:items-center">
            <span className="w-24 shrink-0 text-[13px] font-medium text-ink-secondary">{g}</span>
            <div className="flex flex-wrap gap-2">
              {Object.keys(STATUS_PRESETS[g]).map((s) => (
                <StatusTag key={s} group={g} status={s} />
              ))}
            </div>
          </div>
        ))}
        <div className="flex flex-col gap-2 sm:flex-row sm:items-center">
          <span className="w-24 shrink-0 text-[13px] font-medium text-ink-secondary">tùy chỉnh</span>
          <StatusTag tone="warning" icon={Clock}>Hết hạn sau 3 ngày</StatusTag>
        </div>

        <div className="mt-2 flex flex-wrap items-end gap-3 border-t border-line pt-4">
          {(['xs', 'sm', 'md', 'lg', 'xl'] as const).map((s, i) => (
            <Avatar key={s} size={s} name={['Nguyễn Văn An', 'Trần Thu Hà', 'Tanaka Yuki', 'Lê Minh', 'Phạm Quân'][i]} status={i === 2 ? 'online' : undefined} />
          ))}
          <Avatar size="lg" src="https://invalid.local/x.png" name="Ảnh lỗi Fallback" />
        </div>
        <div className="grid grid-cols-1 gap-3 sm:grid-cols-3">
          <UserCell name="Nguyễn Thu Hà" subtitle="Giáo viên · N2, N1" status="online" />
          <UserCell name="Tanaka Yuki" subtitle="Giáo viên bản ngữ" extra={<StatusTag group="account" status="active" size="sm" />} />
          <UserCell name="Trần Đức Minh" subtitle="minh.td@example.com" onClick={() => undefined} />
        </div>
      </div>
    </Section>
  );
}

// ---------------- Empty / Error / Skeleton ----------------
export function FeedbackSection() {
  const [variant, setVariant] = useState<SkeletonVariant>('table');
  return (
    <Section id="feedback" title="EmptyState · ErrorState · LoadingSkeleton">
      <div className="grid grid-cols-1 gap-4 md:grid-cols-2">
        <div className="rounded-lg border border-line">
          <EmptyState
            icon={Inbox}
            title="Chưa có bài tập nào"
            description="Tạo bài tập đầu tiên để giao cho lớp N3-K12."
            action={<Button action="create">Tạo bài tập</Button>}
          />
        </div>
        <div className="rounded-lg border border-line">
          <ErrorState onRetry={() => undefined} />
        </div>
      </div>
      <div className="mt-4 flex flex-col gap-3">
        <Select
          value={variant}
          onChange={setVariant}
          allowClear={false}
          showSearch={false}
          options={(['table', 'cards', 'list', 'form', 'detail', 'stats'] as SkeletonVariant[]).map((v) => ({ value: v, label: `Skeleton: ${v}` }))}
          className="sm:!w-56"
        />
        <LoadingSkeleton variant={variant} rows={4} />
      </div>
    </Section>
  );
}

// ---------------- Timetable ----------------
function buildEvents(weekStart: Dayjs): TimetableEvent[] {
  const at = (day: number, h: number, m: number) => weekStart.add(day, 'day').hour(h).minute(m).second(0);
  return [
    { id: 1, start: at(0, 18, 0), end: at(0, 19, 30), title: 'N3-K12', subtitle: 'GV Nguyễn Thu Hà', location: 'P.301 · Cầu Giấy', tone: 'primary' },
    { id: 2, start: at(0, 18, 30), end: at(0, 20, 0), title: 'Kaiwa B1', subtitle: 'GV Tanaka Yuki', location: 'Online · Zoom', tone: 'success' },
    { id: 3, start: at(2, 18, 0), end: at(2, 19, 30), title: 'N3-K12', subtitle: 'GV Nguyễn Thu Hà', location: 'P.301 · Cầu Giấy', tone: 'primary' },
    { id: 4, start: at(2, 8, 0), end: at(2, 10, 0), title: 'N5-K20 (sáng)', subtitle: 'GV Lê Ngọc Anh', location: 'P.102', tone: 'info' },
    { id: 5, start: at(3, 19, 0), end: at(3, 20, 30), title: 'N4-K15', subtitle: 'Đã hủy — GV ốm', location: 'P.205', tone: 'danger', cancelled: true, extra: <StatusTag group="session" status="cancelled" size="sm" /> },
    { id: 6, start: at(4, 18, 0), end: at(4, 19, 30), title: 'N3-K12', subtitle: 'GV Nguyễn Thu Hà', location: 'P.301', tone: 'primary' },
    { id: 7, start: at(5, 9, 0), end: at(5, 11, 0), title: 'Học bù N4-K15', subtitle: 'GV Trần Đức Minh', location: 'P.205', tone: 'warning', extra: <StatusTag group="session" status="makeup" size="sm" /> },
    { id: 8, start: at(5, 14, 0), end: at(5, 16, 30), title: 'Thi thử JLPT N2', subtitle: 'Hội trường', location: 'Tầng 5', tone: 'neutral' },
  ];
}

export function TimetableSection() {
  const notify = useNotify();
  const [week, setWeek] = useState(() => dayjs());
  const ws = week.subtract((week.day() + 6) % 7, 'day').startOf('day');
  return (
    <Section id="timetable" title="Timetable" description="Desktop: lưới tuần (buổi trùng giờ xếp cạnh nhau, vạch đỏ là giờ hiện tại). Mobile: chọn ngày + danh sách.">
      <Timetable
        events={buildEvents(ws)}
        week={week}
        onWeekChange={setWeek}
        onEventClick={(e) => notify.info({ title: e.title, description: `${e.subtitle ?? ''} · ${e.location ?? ''}` })}
        toolbarExtra={
          <Select
            placeholder="Lọc theo phòng"
            options={['P.102', 'P.205', 'P.301'].map((v) => ({ value: v, label: v }))}
            className="sm:!w-40"
          />
        }
      />
    </Section>
  );
}

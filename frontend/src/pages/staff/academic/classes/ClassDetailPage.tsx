import { isTaught } from '@/features/academic/classes/sessionUtils';
import { useEffect, useState } from 'react';
import { Checkbox, Form, Tabs } from 'antd';
import dayjs, { type Dayjs } from 'dayjs';
import { ArrowUpRight, BarChart3, BookOpenCheck, CalendarCheck, CalendarDays, Megaphone, Star, CirclePause, CirclePlay, Flag, Info, NotebookPen, Rocket, Users, XCircle } from 'lucide-react';
import { useNavigate, useParams, useSearchParams } from 'react-router-dom';
import { Button, DateInput, EmptyState, ModalForm, PageHeader, StatusTag, useConfirm, useNotify } from '@/components/common';
import { useClassStore } from '@/features/academic/classes/classStore';
import * as classApi from '@/features/academic/classes/classApi';
import * as portalApi from '@/features/portal/portalApi';
import * as studentApi from '@/features/student/studentApi';
import { USE_API } from '@/lib/api/config';
import { CLASS_STATUS, effectiveStart, scheduleLabel, teacherNames, totalSessionsOf } from '@/features/academic/classes/constants';
import { ClassAnnouncementsTab } from '@/features/academic/classes/detail/ClassAnnouncementsTab';
import { ClassAssignmentsTab } from '@/features/academic/classes/detail/ClassAssignmentsTab';
import { ClassAttendanceTab } from '@/features/academic/classes/detail/ClassAttendanceTab';
import { ClassTeacherReviewsTab } from '@/features/academic/classes/detail/ClassTeacherReviewsTab';
import { ClassExamsTab } from '@/features/academic/classes/detail/ClassExamsTab';
import { ClassOverviewTab } from '@/features/academic/classes/detail/ClassOverviewTab';
import { ClassProgressTab } from '@/features/academic/classes/detail/ClassProgressTab';
import { ClassSessionsTab } from '@/features/academic/classes/detail/ClassSessionsTab';
import { ClassStudentsTab } from '@/features/academic/classes/detail/ClassStudentsTab';
import { ClassPromotionTab } from '@/features/academic/promotion/ClassPromotionTab';
import { usePromotionStore } from '@/features/academic/promotion/promotionStore';
import { seatActive } from '@/features/academic/promotion/promotionUtils';
import { generateSessions } from '@/features/academic/classes/sessionGenerator';
import type { ClassInfo } from '@/features/academic/classes/types';
import { useCurriculumStore } from '@/features/academic/curriculum/curriculumStore';
import { summarizeInvoice, useStudentStore } from '@/features/student/studentStore';
import { useAuth } from '@/hooks/useAuth';
import { fmtDate } from '@/lib/date';
import { getBranches } from '@/features/system/useBranches';
import { loadClassCareLogs } from '@/features/student/atRisk/careApi';

type TabKey = 'overview' | 'students' | 'sessions' | 'attendance' | 'progress' | 'assignments' | 'exams' | 'reviews' | 'announcements' | 'promotion';
const TAB_KEYS: TabKey[] = ['overview', 'students', 'sessions', 'attendance', 'progress', 'assignments', 'exams', 'reviews', 'announcements', 'promotion'];

/** Trang quản lý 1 lớp: /admin/academic/classes/:id?tab=... */
export default function ClassDetailPage() {
  const { id } = useParams();
  const navigate = useNavigate();
  const [params, setParams] = useSearchParams();
  const cls = useClassStore((s) => s.classes.find((c) => c.id === id));
  const sessions = useClassStore((s) => s.sessions);
  const enrollments = useStudentStore((s) => s.enrollments);
  const promotions = usePromotionStore((s) => s.candidates);
  const tabParam = params.get('tab') as TabKey | null;
  const tab: TabKey = tabParam && TAB_KEYS.includes(tabParam) ? tabParam : 'overview';
  const setTab = (k: string) => setParams({ tab: k }, { replace: true });

  // Chế độ API: tải đủ buổi của lớp (lúc đăng nhập chỉ tải ±45 ngày)
  useEffect(() => {
    if (USE_API && id) {
      void classApi.loadClassSessions(id).catch(() => undefined);
      void portalApi.loadEvaluations(id, false).catch(() => undefined);
      void loadClassCareLogs(id).catch(() => undefined);
      void studentApi.loadEnrollments(id).catch(() => undefined);
    }
  }, [id]);

  if (!cls)
    return (
      <EmptyState
        title="Không tìm thấy lớp"
        description="Lớp có thể đã bị xóa hoặc đường dẫn không đúng."
        action={<Button action="back" onClick={() => navigate('/admin/academic/classes')}>Về danh sách lớp</Button>}
      />
    );

  const mine = sessions.filter((s) => s.classId === cls.id);
  const done = mine.filter(isTaught).length;
  const enrolled = enrollments.filter((e) => e.classId === cls.id && (e.status === 'studying' || e.status === 'reserved')).length;
  const st = CLASS_STATUS[cls.status];
  const heldIn = promotions.filter((c) => c.toClassId === cls.id && seatActive(c)).length;
  const label = (Icon: typeof Info, text: string) => (
    <span className="inline-flex items-center gap-1.5">
      <Icon size={16} aria-hidden /> {text}
    </span>
  );

  return (
    <>
      <PageHeader
        breadcrumb={[{ title: 'Đào tạo' }, { title: 'Lớp học', href: '/admin/academic/classes' }, { title: cls.code }]}
        title={
          <span className="flex flex-wrap items-center gap-2">
            <span className="font-mono">{cls.code}</span>
            <StatusTag tone={st.tone} icon={st.icon} size="sm">
              {st.label}
            </StatusTag>
          </span>
        }
        subtitle={`${cls.name} · ${getBranches(false).find((b) => b.id === cls.branchId)?.name}${cls.online ? ' · Online' : cls.room ? ` · ${cls.room}` : ''}`}
        actions={
          <>
            <Button action="back" variant="outline" collapseOnMobile onClick={() => navigate('/admin/academic/classes')}>
              Danh sách lớp
            </Button>
            <StatusActions cls={cls} />
          </>
        }
      />

      {/* Thông tin nhanh */}
      <dl className="mb-4 grid grid-cols-2 gap-2 sm:grid-cols-3 xl:grid-cols-6">
        {[
          ['Giáo viên', teacherNames(cls.teacherIds)],
          ['Lịch học', scheduleLabel(cls)],
          ['Khai giảng', `${fmtDate(effectiveStart(cls))}${cls.actualStartDate ? '' : ' (dự kiến)'}`],
          ['Kết thúc', `${fmtDate(cls.actualEndDate ?? cls.plannedEndDate)}${cls.actualEndDate ? '' : ' (dự kiến)'}`],
          ['Sĩ số', `${enrolled}${heldIn ? ` + ${heldIn} giữ chỗ` : ''}/${cls.maxStudents} (tối thiểu ${cls.minStudents})`],
          ['Tiến độ', `${done}/${totalSessionsOf(cls)} buổi · ${cls.totalHours} giờ`],
        ].map(([k, v]) => (
          <div key={k} className="rounded-[var(--radius-card)] border border-line bg-surface px-3 py-2.5">
            <dt className="text-xs text-ink-muted">{k}</dt>
            <dd className="truncate text-[13px] font-medium text-ink" title={v}>
              {v}
            </dd>
          </div>
        ))}
      </dl>
      {cls.status === 'cancelled' && cls.cancelReason && (
        <p className="mb-4 rounded-lg border border-danger-100 bg-danger-50 px-3 py-2 text-[13px] text-danger-700">Lý do hủy: {cls.cancelReason}</p>
      )}

      <div className="rounded-[var(--radius-card)] border border-line bg-surface px-4 pb-4 sm:px-6">
        <Tabs
          activeKey={tab}
          onChange={setTab}
          items={[
            { key: 'overview', label: label(Info, 'Thông tin lớp'), children: <ClassOverviewTab cls={cls} /> },
            { key: 'students', label: label(Users, `Học viên & học phí (${enrolled})`), children: <ClassStudentsTab cls={cls} /> },
            { key: 'sessions', label: label(CalendarDays, `Buổi học (${mine.length})`), children: <ClassSessionsTab cls={cls} /> },
            { key: 'attendance', label: label(CalendarCheck, 'Chuyên cần'), children: <ClassAttendanceTab cls={cls} /> },
            { key: 'progress', label: label(BarChart3, 'Tiến độ'), children: <ClassProgressTab cls={cls} /> },
            { key: 'assignments', label: label(NotebookPen, 'Bài tập'), children: <ClassAssignmentsTab cls={cls} /> },
            { key: 'exams', label: label(BookOpenCheck, 'Bài thi'), children: <ClassExamsTab cls={cls} /> },
            { key: 'reviews', label: label(Star, 'Đánh giá GV'), children: <ClassTeacherReviewsTab cls={cls} /> },
            { key: 'announcements', label: label(Megaphone, 'Thông báo'), children: <ClassAnnouncementsTab cls={cls} /> },
            ...(cls.status !== 'cancelled' ? [{ key: 'promotion', label: label(ArrowUpRight, cls.status === 'enrolling' ? `Học viên lên lớp${heldIn ? ` (${heldIn})` : ''}` : 'Lên lớp'), children: <ClassPromotionTab cls={cls} /> }] : []),
          ]}
        />
      </div>
    </>
  );
}

// ---------------- Chuyển trạng thái lớp ----------------

function StatusActions({ cls }: { cls: ClassInfo }) {
  const { can } = useAuth();
  const notify = useNotify();
  const { confirm, confirmDialog } = useConfirm();
  const updateClass = useClassStore((s) => s.updateClass);
  const sessions = useClassStore((s) => s.sessions);
  const updateSession = useClassStore((s) => s.updateSession);
  const replaceUpcoming = useClassStore((s) => s.replaceUpcomingSessions);
  const enrollments = useStudentStore((s) => s.enrollments);
  const invoices = useStudentStore((s) => s.invoices);
  const dropEnrollment = useStudentStore((s) => s.dropEnrollment);
  const setEnrollmentStatus = useStudentStore((s) => s.setEnrollmentStatus);
  const curricula = useCurriculumStore((s) => s.curricula);
  const [dialog, setDialog] = useState<'start' | 'finish' | null>(null);
  const promotions = usePromotionStore((s) => s.candidates);
  const promoList = promotions.filter((c) => c.fromClassId === cls.id && c.status !== 'confirmed' && c.status !== 'declined');
  const handOver = usePromotionStore((s) => s.handOver);

  if (!can('academic.classes')) return null;
  const active = enrollments.filter((e) => e.classId === cls.id && (e.status === 'studying' || e.status === 'reserved'));
  const upcoming = sessions.filter((s) => s.classId === cls.id && s.status === 'scheduled');

  const cancelClass = async () => {
    const paid = active.filter((e) => {
      const inv = invoices.find((i) => i.enrollmentId === e.id);
      return inv && summarizeInvoice(inv).paid > 0;
    }).length;
    const res = await confirm({
      title: `Hủy lớp ${cls.code}?`,
      description: 'Không thể hoàn tác.',
      consequences: [
        `${active.length} học viên chuyển sang "Đã nghỉ", hủy phần học phí còn lại`,
        paid ? <span className="font-medium text-danger-700">{paid} học viên đã đóng tiền — hoàn tiền ở tab Học viên & học phí</span> : 'Chưa có học viên nào đóng tiền',
        `${upcoming.length} buổi chưa dạy bị hủy`,
      ],
      tone: 'danger',
      confirmAction: 'reject',
      confirmText: 'Hủy lớp',
      requireReason: { label: 'Lý do hủy lớp', placeholder: 'VD: Không đủ học viên tối thiểu sau 3 tuần' },
      typeToConfirm: cls.code,
      onConfirm: USE_API ? async ({ reason }) => void (await classApi.cancelClass(cls.id, reason ?? '', cls.code)) : undefined,
    });
    if (!res) return;
    if (USE_API) {
      // Server cho nghỉ mọi ghi danh, Tài chính hủy công nợ còn lại (chạy nền) → tải lại
      studentApi.reloadClassEnrollmentsSoon(cls.id);
      setTimeout(() => void import('@/features/finance/financeApi').then((m) => m.loadInvoices()).catch(() => undefined), 3000);
    } else {
      active.forEach((e) => dropEnrollment(e.id, `Lớp bị hủy: ${res.reason}`));
      upcoming.forEach((s) => updateSession(s.id, { status: 'cancelled', note: 'Lớp bị hủy' }));
      updateClass(cls.id, { status: 'cancelled', cancelReason: res.reason });
    }
    notify.success({ title: 'Đã hủy lớp', description: cls.code });
  };

  const pause = async () => {
    const res = await confirm({
      title: `Tạm dừng lớp ${cls.code}?`,
      description: 'Các buổi chưa dạy giữ nguyên trên lịch — khi học lại, vào tab Buổi học bấm "Tạo lịch tự động" để dời lịch.',
      tone: 'warning',
      confirmAction: 'save',
      confirmText: 'Tạm dừng',
      requireReason: { label: 'Lý do tạm dừng' },
      onConfirm: USE_API ? async ({ reason }) => void (await classApi.pauseClass(cls.id, reason ?? '')) : undefined,
    });
    if (!res) return;
    if (!USE_API) updateClass(cls.id, { status: 'paused', note: `Tạm dừng: ${res.reason}` });
    notify.success({ title: 'Đã tạm dừng lớp', description: cls.code });
  };

  const resume = async () => {
    if (USE_API) {
      try {
        await classApi.resumeClass(cls.id);
      } catch (e) {
        notify.error({ title: 'Chưa cho học lại được', description: (e as Error).message });
        return;
      }
    } else updateClass(cls.id, { status: 'ongoing' });
    notify.info({ title: 'Lớp học lại', description: 'Nhớ kiểm tra lịch các buổi còn lại ở tab Buổi học.' });
  };

  const actions = (
    <>
      {cls.status === 'enrolling' && (
        <Button icon={Rocket} tone="success" collapseOnMobile onClick={() => setDialog('start')}>
          Khai giảng
        </Button>
      )}
      {cls.status === 'ongoing' && (
        <>
          <Button icon={CirclePause} tone="warning" variant="outline" collapseOnMobile onClick={pause}>
            Tạm dừng
          </Button>
          <Button icon={Flag} collapseOnMobile onClick={() => setDialog('finish')}>
            Kết thúc lớp
          </Button>
        </>
      )}
      {cls.status === 'paused' && (
        <Button icon={CirclePlay} tone="success" collapseOnMobile onClick={resume}>
          Học lại
        </Button>
      )}
      {(cls.status === 'enrolling' || cls.status === 'ongoing' || cls.status === 'paused') && (
        <Button icon={XCircle} tone="danger" variant="ghost" collapseOnMobile onClick={cancelClass}>
          Hủy lớp
        </Button>
      )}
    </>
  );

  return (
    <>
      {actions}

      {/* Khai giảng: chọn ngày thực tế (có thể trễ hơn dự kiến) */}
      <ModalForm<{ actualStartDate: Dayjs; reschedule: boolean }>
        open={dialog === 'start'}
        onClose={() => setDialog(null)}
        title={`Khai giảng lớp ${cls.code}`}
        description={`Dự kiến ${fmtDate(cls.plannedStartDate)} · đã có ${active.length}/${cls.minStudents} học viên tối thiểu`}
        submitAction="approve"
        submitText="Khai giảng"
        successMessage="Lớp đã khai giảng"
        initialValues={{ actualStartDate: dayjs().isAfter(dayjs(cls.plannedStartDate)) ? dayjs() : dayjs(cls.plannedStartDate), reschedule: true }}
        onSubmit={async (v) => {
          const date = v.actualStartDate.format('YYYY-MM-DD');
          if (USE_API) {
            const warnings = await classApi.startClass(cls.id, date, v.reschedule);
            if (warnings.length) notify.info({ title: 'Lớp đã khai giảng', description: warnings.join(' · ') });
            return;
          }
          let end = cls.plannedEndDate;
          if (v.reschedule) {
            const plan = generateSessions({
              classId: cls.id,
              startDate: date,
              weekdays: cls.weekdays,
              startTime: cls.startTime,
              endTime: cls.endTime,
              count: totalSessionsOf(cls),
              skipHolidays: true,
              curriculum: curricula.find((c) => c.id === cls.curriculumId),
            });
            replaceUpcoming(cls.id, plan.sessions);
            end = plan.endDate ?? end;
          }
          updateClass(cls.id, { status: 'ongoing', actualStartDate: date, plannedEndDate: end });
        }}
      >
        {active.length < cls.minStudents && (
          <p className="mb-4 rounded-lg bg-warning-50 px-3 py-2 text-[13px] text-warning-700">
            Lớp mới có {active.length} học viên, chưa đủ tối thiểu {cls.minStudents}. Vẫn khai giảng được nếu trung tâm chấp nhận.
          </p>
        )}
        <Form.Item name="actualStartDate" label="Ngày khai giảng thực tế" rules={[{ required: true, message: 'Chọn ngày' }]}>
          <DateInput />
        </Form.Item>
        <Form.Item name="reschedule" valuePropName="checked" extra="Sinh lại các buổi chưa dạy bắt đầu từ ngày khai giảng thực tế (bỏ ngày lễ)">
          <Checkbox>Dời lịch học theo ngày khai giảng thực tế</Checkbox>
        </Form.Item>
      </ModalForm>

      {/* Kết thúc lớp */}
      <ModalForm<{ actualEndDate: Dayjs }>
        open={dialog === 'finish'}
        onClose={() => setDialog(null)}
        title={`Kết thúc lớp ${cls.code}`}
        description={
          <>
            {upcoming.length} buổi chưa dạy sẽ bị hủy · {active.filter((e) => e.status === 'studying').length} học viên đang học chuyển sang "Hoàn thành".
            <br />
            {promoList.length
              ? `Lên lớp: ${promoList.length} học viên trong danh sách (${promoList.filter(seatActive).length} đang giữ chỗ) → giao cho Sale tư vấn.`
              : 'Chưa có danh sách lên lớp — nên giữ chỗ cho học viên ở tab "Lên lớp" trước khi kết thúc.'}
          </>
        }
        submitAction="approve"
        submitText="Kết thúc lớp"
        successMessage="Đã kết thúc lớp"
        initialValues={{ actualEndDate: dayjs() }}
        onSubmit={async (v) => {
          if (USE_API) {
            const warnings = await classApi.finishClass(cls.id, v.actualEndDate.format('YYYY-MM-DD'));
            if (warnings.length) notify.info({ title: 'Đã kết thúc lớp', description: warnings.join(' · ') });
          } else {
            upcoming.forEach((s) => updateSession(s.id, { status: 'cancelled', note: 'Lớp kết thúc sớm' }));
            updateClass(cls.id, { status: 'finished', actualEndDate: v.actualEndDate.format('YYYY-MM-DD') });
          }
          // Ghi danh: server chuyển "hoàn thành" (chạy nền) → tải lại. ⏳ Lên lớp còn dữ liệu giả
          if (USE_API) studentApi.reloadClassEnrollmentsSoon(cls.id);
          else active.filter((e) => e.status === 'studying').forEach((e) => setEnrollmentStatus(e.id, 'completed'));
          // Giao danh sách lên lớp cho Sale
          handOver(cls.id);
        }}
      >
        <Form.Item name="actualEndDate" label="Ngày kết thúc thực tế" rules={[{ required: true, message: 'Chọn ngày' }]}>
          <DateInput />
        </Form.Item>
      </ModalForm>
      {confirmDialog}
    </>
  );
}

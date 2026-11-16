import { useMemo, useState, type Key } from 'react';
import { Form, Tooltip } from 'antd';
import dayjs from 'dayjs';
import { ArrowRight, Bookmark, BookmarkX, CalendarClock, CheckCheck, CirclePlus, ListPlus, Send, Sparkles, UserRound, Users } from 'lucide-react';
import { useNavigate } from 'react-router-dom';
import { Button, DataTable, EmptyState, ModalForm, StatusTag, UserCell, useConfirm, useNotify, type DataColumn } from '@/components/common';
import { useClassStore } from '@/features/academic/classes/classStore';
import { ClassFormFields, formToClass, type ClassFormValues } from '@/features/academic/classes/ClassFormFields';
import { CODE_PREFIX, EVAL_RECOMMENDATION, effectiveStart, scheduleLabel, teacherNames } from '@/features/academic/classes/constants';
import { useClassStudentStats, type StudentStats } from '@/features/academic/classes/studentInsights';
import type { ClassInfo, StudentEvaluation } from '@/features/academic/classes/types';
import { courseTotalFee, totalHours as courseHours } from '@/features/academic/courses/constants';
import { useCourseStore } from '@/features/academic/courses/courseStore';
import { useCurriculumStore } from '@/features/academic/curriculum/curriculumStore';
import { useCrmStore } from '@/features/crm/crmStore';
import { saleName } from '@/features/crm/constants';
import { useAuth } from '@/hooks/useAuth';
import { cn } from '@/lib/cn';
import { fmtDate } from '@/lib/date';
import { usePromotionStore } from './promotionStore';
import { holdUntilFor, isOpen, nextCoursesOf, PROMOTION_STATUS, seatActive, suggestionFor, targetClassesFor, viewStatus, type Decider, type Suggestion } from './promotionUtils';
import type { PromotionCandidate } from './types';
import { useDecider, useSeats } from './usePromotion';

interface Row {
  id: string;
  name: string;
  code: string;
  st: StudentStats;
  ev?: StudentEvaluation;
  sug: Suggestion;
  cand?: PromotionCandidate;
  dec: Decider;
}

/**
 * Chi tiết lớp › Lên lớp
 *  - Lớp đang học / đã kết thúc: chọn (hoặc mở) lớp kế tiếp, giữ chỗ tạm cho học viên học tốt, giao danh sách cho Sale
 *  - Lớp đang tuyển (lớp kế tiếp): xem học viên được giữ chỗ từ lớp trước
 */
export function ClassPromotionTab({ cls }: { cls: ClassInfo }) {
  const candidates = usePromotionStore((s) => s.candidates);
  const incoming = useMemo(() => candidates.filter((c) => c.toClassId === cls.id), [candidates, cls.id]);
  const outgoingAllowed = cls.status === 'ongoing' || cls.status === 'paused' || cls.status === 'finished';
  return (
    <div className="flex flex-col gap-6">
      {outgoingAllowed && <Outgoing cls={cls} />}
      {(incoming.length > 0 || !outgoingAllowed) && <Incoming cls={cls} list={incoming} />}
    </div>
  );
}

// ======================= Lớp hiện tại → lớp kế tiếp =======================

function Outgoing({ cls }: { cls: ClassInfo }) {
  const navigate = useNavigate();
  const notify = useNotify();
  const { confirm, confirmDialog } = useConfirm();
  const { user } = useAuth();
  const classes = useClassStore((s) => s.classes);
  const sessions = useClassStore((s) => s.sessions);
  const evaluations = useClassStore((s) => s.evaluations);
  const addClass = useClassStore((s) => s.addClass);
  const courses = useCourseStore((s) => s.courses);
  const curriculumLinks = useCurriculumStore((s) => s.links);
  const students = useCrmStore((s) => s.students);
  const allCandidates = usePromotionStore((s) => s.candidates);
  const { upsertMany, update, remove, handOver } = usePromotionStore();
  const decider = useDecider();
  const { map } = useClassStudentStats(cls);

  const course = courses.find((c) => c.id === cls.courseId);
  const nextCourses = useMemo(() => nextCoursesOf(course, courses), [course, courses]);
  const targets = useMemo(() => targetClassesFor(cls, classes, nextCourses.map((c) => c.id)), [cls, classes, nextCourses]);
  const mine = useMemo(() => allCandidates.filter((c) => c.fromClassId === cls.id), [allCandidates, cls.id]);
  const usedTarget = mine.find((c) => c.toClassId)?.toClassId;
  const [targetId, setTargetId] = useState<string | undefined>(usedTarget ?? targets[0]?.cls.id);
  const target = classes.find((c) => c.id === targetId);
  const [selected, setSelected] = useState<Key[]>([]);
  const [creating, setCreating] = useState(false);
  const [showAll, setShowAll] = useState(false);
  // Chỉ hiện lớp gợi ý (khóa kế tiếp / lớp nối tiếp) + lớp đang chọn; lớp khác ẩn sau "Xem thêm"
  const recommendedTargets = targets.filter((t) => t.recommended || t.cls.id === targetId);
  const shownTargets = showAll ? targets : recommendedTargets;
  const otherCount = targets.length - recommendedTargets.length;
  const by = user ? `${user.fullName} (${user.roleName})` : 'Giáo vụ';

  const left = sessions.filter((s) => s.classId === cls.id && s.status === 'scheduled').length;
  const finished = cls.status === 'finished';

  const rows: Row[] = [...map.values()]
    .filter((st) => st.enrollment.status === 'studying' || st.enrollment.status === 'completed' || st.enrollment.status === 'reserved')
    .map((st) => {
      const id = st.enrollment.studentId;
      const s = students.find((x) => x.id === id);
      const ev = evaluations.find((e) => e.classId === cls.id && e.studentId === id);
      return { id, name: s?.fullName ?? id, code: s?.code ?? '', st, ev, sug: suggestionFor(st, ev), cand: mine.find((c) => c.studentId === id), dec: decider(id) };
    })
    .sort((a, b) => Number(b.sug.hold) - Number(a.sug.hold) || a.name.localeCompare(b.name, 'vi'));

  const counts = {
    list: mine.length,
    held: mine.filter(seatActive).length,
    confirmed: mine.filter((c) => c.status === 'confirmed').length,
    declined: mine.filter((c) => c.status === 'declined').length,
    notHanded: mine.filter((c) => !c.handedOverAt && isOpen(c)).length,
  };
  const rate = rows.length ? Math.round((counts.confirmed / rows.length) * 100) : 0;

  const editable = (r: Row) => !r.cand || r.cand.status !== 'confirmed';
  const pick = (keys: Key[]) => rows.filter((r) => keys.includes(r.id) && editable(r));

  const put = (list: Row[], hold: boolean) => {
    if (!target) return notify.error({ title: 'Chưa chọn lớp kế tiếp', description: 'Chọn hoặc mở lớp kế tiếp trước khi giữ chỗ.' });
    upsertMany(
      list.map((r) => ({
        studentId: r.id,
        fromClassId: cls.id,
        toClassId: target.id,
        seatHeld: hold,
        holdUntil: hold ? holdUntilFor(target) : undefined,
        status: r.cand?.status === 'declined' ? 'pending' : (r.cand?.status ?? 'pending'),
        source: r.cand?.source ?? (r.sug.hold ? r.sug.source : 'manual'),
        saleId: r.cand?.saleId ?? r.dec.saleId,
        handedOverAt: r.cand?.handedOverAt ?? (finished ? new Date().toISOString() : undefined),
        createdBy: r.cand?.createdBy ?? by,
      })),
    );
    setSelected([]);
    notify.toast.success(hold ? `Đã giữ chỗ ${list.length} học viên vào ${target.code}` : `Đã thêm ${list.length} học viên vào danh sách tư vấn`);
  };

  const holdSelected = async (keys: Key[]) => {
    const list = pick(keys);
    if (!list.length || !target) return put(list, true);
    const already = list.filter((r) => r.cand && seatActive(r.cand) && r.cand.toClassId === target.id).length;
    const need = list.length - already;
    if (need > seats.free) {
      const ok = await confirm({ title: `Lớp ${target.code} chỉ còn ${seats.free} chỗ trống`, description: `Bạn đang giữ thêm ${need} chỗ — lớp sẽ vượt sĩ số tối đa (${target.maxStudents}). Vẫn giữ chỗ?`, tone: 'warning' });
      if (!ok) return;
    }
    put(list, true);
  };

  const seats = useSeats(target?.id ?? '', target?.maxStudents ?? 0);

  const columns: DataColumn<Row>[] = [
    {
      key: 'name',
      title: 'Học viên',
      width: 230,
      mobile: 'title',
      render: (_, r) => (
        <UserCell
          size="sm"
          name={r.name}
          subtitle={
            <span>
              {r.code}
              {r.dec.kind === 'guardian' && <span className="text-primary-700"> · PH quyết định: {r.dec.name}</span>}
            </span>
          }
        />
      ),
    },
    { key: 'att', title: 'Chuyên cần', width: 100, align: 'center', render: (_, r) => (r.st.attendance.rate !== undefined ? <span className="tabular">{r.st.attendance.rate}%</span> : '—') },
    { key: 'exam', title: 'Điểm thi', width: 90, align: 'center', render: (_, r) => (r.st.exams.avgPct !== undefined ? <span className="tabular">{r.st.exams.avgPct}%</span> : '—') },
    {
      key: 'ev',
      title: 'Nhận xét cuối khóa',
      width: 170,
      render: (_, r) =>
        r.ev ? (
          <StatusTag tone={EVAL_RECOMMENDATION[r.ev.recommendation].tone} icon={UserRound} size="sm">
            {EVAL_RECOMMENDATION[r.ev.recommendation].label}
          </StatusTag>
        ) : (
          <span className="text-xs text-ink-muted">GV chưa nhận xét</span>
        ),
    },
    {
      key: 'sug',
      title: 'Gợi ý',
      width: 170,
      render: (_, r) => (
        <Tooltip title={r.sug.reason}>
          <span className={cn('inline-flex items-center gap-1 rounded px-1.5 py-0.5 text-xs font-medium', r.sug.hold ? 'bg-success-50 text-success-700' : 'bg-warning-50 text-warning-700')}>
            {r.sug.hold ? <Sparkles size={12} aria-hidden /> : <Users size={12} aria-hidden />}
            {r.sug.hold ? 'Nên giữ chỗ' : 'Cần tư vấn thêm'}
          </span>
        </Tooltip>
      ),
    },
    {
      key: 'state',
      title: 'Lên lớp',
      width: 220,
      mobile: 'aside',
      render: (_, r) => {
        if (!r.cand) return <span className="text-xs text-ink-muted">Chưa vào danh sách</span>;
        const v = PROMOTION_STATUS[viewStatus(r.cand)];
        const to = classes.find((c) => c.id === r.cand!.toClassId);
        return (
          <span className="flex flex-col gap-0.5 leading-5">
            <StatusTag tone={v.tone} icon={v.icon} size="sm">
              {v.label}
            </StatusTag>
            <span className="text-xs text-ink-muted">
              {to ? `→ ${to.code}` : 'Chưa chọn lớp'}
              {seatActive(r.cand) ? ` · giữ chỗ đến ${fmtDate(r.cand.holdUntil)}` : r.cand.seatHeld ? '' : ' · không giữ chỗ'}
            </span>
          </span>
        );
      },
    },
  ];

  // Mở lớp kế tiếp: điền sẵn theo lớp hiện tại + khóa kế tiếp
  const nextCourse = nextCourses[0] ?? course;
  const createInitial: Partial<ClassFormValues> | undefined = nextCourse && {
    courseId: nextCourse.id,
    curriculumId: curriculumLinks.find((l) => l.courseId === nextCourse.id && l.isDefault)?.curriculumId,
    branchId: cls.branchId,
    name: `${nextCourse.name} — ${scheduleLabel(cls)}`,
    teacherIds: cls.teacherIds,
    assistantIds: cls.assistantIds,
    room: cls.room,
    meetingUrl: cls.meetingUrl,
    weekdays: cls.weekdays,
    startTime: dayjs(`2000-01-01T${cls.startTime}`),
    endTime: dayjs(`2000-01-01T${cls.endTime}`),
    plannedStartDate: dayjs(cls.actualEndDate ?? cls.plannedEndDate).add(7, 'day'),
    totalHours: courseHours(nextCourse),
    fee: courseTotalFee(nextCourse),
    maxStudents: nextCourse.maxStudents ?? cls.maxStudents,
    minStudents: Math.min(cls.minStudents, nextCourse.maxStudents ?? cls.minStudents),
  };

  return (
    <section className="flex flex-col gap-4">
      {/* Trạng thái chung */}
      {finished ? (
        counts.notHanded > 0 ? (
          <div className="flex flex-col gap-2 rounded-[var(--radius-card)] border border-warning-100 bg-warning-50 px-4 py-3 text-[13px] text-warning-700 sm:flex-row sm:items-center">
            <Send size={18} className="shrink-0" aria-hidden />
            <span className="flex-1">Lớp đã kết thúc — còn {counts.notHanded} học viên trong danh sách chưa giao cho Sale tư vấn.</span>
            <Button icon={Send} size="sm" onClick={() => notify.toast.success(`Đã giao ${handOver(cls.id)} học viên cho Sale`)}>
              Giao cho Sale
            </Button>
          </div>
        ) : (
          mine.length > 0 && (
            <p className="rounded-[var(--radius-card)] border border-success-100 bg-success-50 px-4 py-3 text-[13px] text-success-700">
              Đã giao danh sách cho Sale — theo dõi ở <button type="button" className="font-medium underline" onClick={() => navigate('/admin/crm/promotions')}>Tuyển sinh › Tư vấn lên lớp</button>.
            </p>
          )
        )
      ) : (
        <p className="flex items-start gap-2 rounded-[var(--radius-card)] border border-primary-200 bg-primary-50 px-4 py-3 text-[13px] text-primary-800">
          <CalendarClock size={18} className="mt-0.5 shrink-0" aria-hidden />
          <span>
            {left > 0 ? `Còn ${left} buổi là kết thúc lớp. ` : ''}Chọn lớp kế tiếp và <b>giữ chỗ tạm</b> cho học viên học tốt (chưa phải ghi danh, chưa có hóa đơn). Khi bấm “Kết thúc lớp”, danh sách tự giao cho Sale tư vấn — học viên nhỏ tuổi thì Sale tư vấn phụ huynh.
          </span>
        </p>
      )}

      {/* Chọn lớp kế tiếp */}
      <div>
        <h3 className="mb-2 text-sm font-semibold text-ink">
          Lớp kế tiếp
          {nextCourses.length > 0 && <span className="ml-2 font-normal text-ink-muted">Khóa gợi ý: {nextCourses.slice(0, 2).map((c) => c.name).join(', ')}</span>}
        </h3>
        <div className="grid gap-3 md:grid-cols-2 xl:grid-cols-3">
          {shownTargets.map(({ cls: t, recommended }) => (
            <TargetCard key={t.id} cls={t} from={cls} active={t.id === targetId} recommended={recommended} onPick={() => setTargetId(t.id)} />
          ))}
          <button
            type="button"
            onClick={() => setCreating(true)}
            className="flex min-h-[120px] flex-col items-center justify-center gap-1.5 rounded-[var(--radius-card)] border-2 border-dashed border-line text-[13px] text-ink-secondary hover:border-primary-300 hover:bg-primary-50 hover:text-primary-700"
          >
            <CirclePlus size={22} aria-hidden />
            Mở lớp kế tiếp
            <span className="text-xs text-ink-muted">Điền sẵn khóa {nextCourse?.name ?? ''}, giáo viên, lịch học</span>
          </button>
        </div>
        {otherCount > 0 && (
          <button type="button" onClick={() => setShowAll((v) => !v)} className="mt-2 text-[13px] text-primary-700 hover:underline">
            {showAll ? 'Chỉ hiện lớp gợi ý' : `Xem thêm ${otherCount} lớp đang tuyển khác`}
          </button>
        )}
      </div>

      {/* Số liệu */}
      <div className="flex flex-wrap gap-x-5 gap-y-1 text-[13px] text-ink-secondary">
        <span>
          Trong danh sách: <b className="tabular text-ink">{counts.list}</b>/{rows.length}
        </span>
        <span>
          Đang giữ chỗ: <b className="tabular text-ink">{counts.held}</b>
        </span>
        <span>
          Đã ghi danh lớp mới: <b className="tabular text-success-700">{counts.confirmed}</b>
        </span>
        <span>
          Không học tiếp: <b className="tabular text-danger-700">{counts.declined}</b>
        </span>
        <span>
          Tỉ lệ lên lớp: <b className="tabular text-ink">{rate}%</b>
        </span>
      </div>

      <DataTable<Row>
        storageKey="class-promotion"
        columns={columns}
        data={rows}
        rowKey="id"
        unitLabel="học viên"
        searchKeys={['name', 'code']}
        pagination={false}
        selectedKeys={selected}
        onSelectionChange={(k) => setSelected(k)}
        search={{
          placeholder: 'Tìm học viên...',
          actions: (
            <Button icon={CheckCheck} variant="outline" onClick={() => setSelected(rows.filter((r) => r.sug.hold && editable(r)).map((r) => r.id))}>
              Chọn theo gợi ý
            </Button>
          ),
        }}
        bulkActions={(keys) => (
          <>
            <Button icon={Bookmark} size="sm" disabled={!target} onClick={() => holdSelected(keys)}>
              Giữ chỗ vào {target?.code ?? '...'}
            </Button>
            <Button icon={ListPlus} size="sm" variant="soft" disabled={!target} onClick={() => put(pick(keys), false)}>
              Tư vấn, không giữ chỗ
            </Button>
            <Button
              icon={BookmarkX}
              size="sm"
              variant="ghost"
              tone="danger"
              onClick={() => {
                pick(keys).forEach((r) => r.cand && (r.cand.handedOverAt || r.cand.notes.length ? update(r.cand.id, { seatHeld: false, holdUntil: undefined }) : remove(r.cand.id)));
                setSelected([]);
                notify.toast.success('Đã bỏ giữ chỗ');
              }}
            >
              Bỏ giữ chỗ
            </Button>
          </>
        )}
        empty={{ icon: Users, title: 'Lớp không có học viên' }}
      />

      {createInitial && (
        <ModalForm<ClassFormValues>
          key={String(creating)}
          open={creating}
          onClose={() => setCreating(false)}
          columns={2}
          width={820}
          title={`Mở lớp kế tiếp cho ${cls.code}`}
          description="Điền sẵn theo lớp hiện tại và khóa kế tiếp — sửa lại nếu cần. Lớp ở trạng thái Chờ khai giảng."
          submitAction="create"
          submitText="Mở lớp"
          successMessage="Đã mở lớp kế tiếp — vào trang lớp để xếp lịch học"
          initialValues={createInitial}
          onSubmit={(v) => {
            const c = courses.find((x) => x.id === v.courseId)!;
            const created = addClass({ ...formToClass(v, CODE_PREFIX[c.teacherType], c.name, c.mode === 'online'), previousClassId: cls.id });
            setTargetId(created.id);
          }}
        >
          <Form.Item noStyle>
            <ClassFormFields mode="create" prefilled />
          </Form.Item>
        </ModalForm>
      )}
      {confirmDialog}
    </section>
  );
}

function TargetCard({ cls, from, active, recommended, onPick }: { cls: ClassInfo; from: ClassInfo; active: boolean; recommended: boolean; onPick: () => void }) {
  const seats = useSeats(cls.id, cls.maxStudents);
  const pct = (n: number) => `${Math.min(100, (n / Math.max(1, seats.max)) * 100)}%`;
  return (
    <button
      type="button"
      onClick={onPick}
      aria-pressed={active}
      className={cn('flex flex-col gap-2 rounded-[var(--radius-card)] border bg-surface p-3.5 text-left transition-colors', active ? 'border-primary-500 ring-2 ring-primary-100' : 'border-line hover:border-primary-300')}
    >
      <span className="flex flex-wrap items-center gap-1.5">
        <span className="font-mono text-[13px] font-semibold text-ink">{cls.code}</span>
        {cls.previousClassId === from.id ? (
          <span className="rounded bg-primary-50 px-1.5 text-[11px] font-medium text-primary-700">Lớp nối tiếp</span>
        ) : (
          recommended && <span className="rounded bg-success-50 px-1.5 text-[11px] font-medium text-success-700">Gợi ý</span>
        )}
      </span>
      <span className="text-[13px] text-ink-secondary">{cls.courseLabel}</span>
      <span className="text-xs text-ink-muted">
        Khai giảng {fmtDate(effectiveStart(cls))} · {scheduleLabel(cls)}
        <br />
        GV {teacherNames(cls.teacherIds)} · {cls.online ? 'Online' : (cls.room ?? '—')}
      </span>
      <span className="mt-auto">
        <span className="flex h-1.5 overflow-hidden rounded-full bg-page">
          <span className="bg-primary-600" style={{ width: pct(seats.enrolled) }} />
          <span className="bg-primary-300" style={{ width: pct(seats.held) }} />
        </span>
        <span className="tabular mt-1 block text-xs text-ink-muted">
          {seats.enrolled} ghi danh + {seats.held} giữ chỗ / {seats.max} · còn {seats.free} chỗ
        </span>
      </span>
    </button>
  );
}

// ======================= Lớp kế tiếp: học viên được giữ chỗ =======================

function Incoming({ cls, list }: { cls: ClassInfo; list: PromotionCandidate[] }) {
  const navigate = useNavigate();
  const classes = useClassStore((s) => s.classes);
  const students = useCrmStore((s) => s.students);
  const decider = useDecider();
  const seats = useSeats(cls.id, cls.maxStudents);

  if (!list.length)
    return <EmptyState size="compact" icon={Users} title="Chưa có học viên lên lớp" description="Học viên được giữ chỗ từ lớp trước (tab Lên lớp của lớp đó) sẽ hiện ở đây." />;

  return (
    <section className="flex flex-col gap-3">
      <div className="flex flex-wrap items-center justify-between gap-2">
        <h3 className="text-sm font-semibold text-ink">
          Học viên lên lớp vào {cls.code}
          <span className="ml-2 font-normal text-ink-muted">
            {seats.enrolled} ghi danh + {seats.held} đang giữ chỗ / {seats.max}
          </span>
        </h3>
        <Button icon={ArrowRight} size="sm" variant="outline" onClick={() => navigate('/admin/crm/promotions')}>
          Tư vấn lên lớp
        </Button>
      </div>
      <ul className="divide-y divide-line rounded-[var(--radius-card)] border border-line bg-surface">
        {list.map((c) => {
          const v = PROMOTION_STATUS[viewStatus(c)];
          const from = classes.find((x) => x.id === c.fromClassId);
          const dec = decider(c.studentId);
          return (
            <li key={c.id} className="flex flex-col gap-2 px-4 py-2.5 sm:flex-row sm:items-center">
              <UserCell size="sm" name={students.find((s) => s.id === c.studentId)?.fullName ?? c.studentId} subtitle={`từ ${from?.code ?? c.fromClassId}${dec.kind === 'guardian' ? ` · PH ${dec.name}` : ''}`} className="min-w-0 flex-1" />
              <span className="text-xs text-ink-muted">
                Sale: {saleName(c.saleId ?? dec.saleId) ?? '—'}
                {seatActive(c) && ` · giữ đến ${fmtDate(c.holdUntil)}`}
              </span>
              <StatusTag tone={v.tone} icon={v.icon} size="sm">
                {v.label}
              </StatusTag>
            </li>
          );
        })}
      </ul>
    </section>
  );
}

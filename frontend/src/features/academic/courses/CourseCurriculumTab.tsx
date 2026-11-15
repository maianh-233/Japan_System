import { useMemo, useState } from 'react';
import { useClassStore } from '@/features/academic/classes/classStore';
import { isOpenClass } from '@/features/academic/classes/constants';
import { Checkbox, Form, Tooltip } from 'antd';
import { BookOpen, ClipboardCheck, FileQuestion, FileText, Info, Link2, Route, Star, Unlink } from 'lucide-react';
import { Button, EmptyState, ModalForm, Select, StatusTag, useConfirm, useNotify } from '@/components/common';
import { cn } from '@/lib/cn';
import { fmtDate } from '@/lib/date';
import { CURRICULUM_STATUS, TIERS } from '../curriculum/constants';
import { CurriculumDetailDrawer } from '../curriculum/CurriculumDetailDrawer';
import { curriculumStats, useCurriculumStore } from '../curriculum/curriculumStore';
import type { Curriculum } from '../curriculum/types';
import { LEVELS } from './constants';
import { useCourseStore } from './courseStore';
import type { Course } from './types';

interface LinkValues {
  curriculumId: string;
  asDefault?: boolean;
}

/**
 * Tab Lộ trình của khóa: các lộ trình đang gắn + lộ trình MẶC ĐỊNH cho lớp mở mới.
 * Lớp đã mở giữ lộ trình chốt lúc mở → đổi mặc định không ảnh hưởng lớp đang học.
 */
export function CourseCurriculumTab({ course, editable }: { course: Course; editable: boolean }) {
  const notify = useNotify();
  const { confirm, confirmDialog } = useConfirm();
  const curricula = useCurriculumStore((s) => s.curricula);
  const links = useCurriculumStore((s) => s.links);
  const link = useCurriculumStore((s) => s.link);
  const unlink = useCurriculumStore((s) => s.unlink);
  const setDefault = useCurriculumStore((s) => s.setDefault);
  const courses = useCourseStore((s) => s.courses);
  const classes = useClassStore((s) => s.classes);
  const [linking, setLinking] = useState(false);
  const [viewing, setViewing] = useState<string | null>(null);

  const linked = useMemo(
    () =>
      links
        .filter((l) => l.courseId === course.id)
        .map((l) => ({ link: l, cur: curricula.find((c) => c.id === l.curriculumId)! }))
        .filter((x) => x.cur)
        .sort((a, b) => Number(b.link.isDefault) - Number(a.link.isDefault) || b.link.linkedAt.localeCompare(a.link.linkedAt)),
    [links, curricula, course.id],
  );
  const linkable = curricula.filter((c) => c.status !== 'archived' && !linked.some((x) => x.cur.id === c.id));

  /** Lớp CỦA KHÓA NÀY đang dùng lộ trình */
  const classesUsing = (curId: string) => classes.filter((k) => k.courseId === course.id && k.curriculumId === curId);
  /** Khóa KHÁC cũng gắn lộ trình này */
  const sharedWith = (curId: string) =>
    links
      .filter((l) => l.curriculumId === curId && l.courseId !== course.id)
      .map((l) => courses.find((c) => c.id === l.courseId))
      .filter((c): c is Course => Boolean(c));

  const unlinkBlockReason = (curId: string, isDefault: boolean): string | null => {
    if (isDefault) return 'Đang là lộ trình mặc định — đặt lộ trình khác làm mặc định trước khi gỡ';
    const open = classesUsing(curId).filter((k) => isOpenClass(k));
    return open.length ? `Lớp ${open.map((k) => k.code).join(', ')} đang học theo lộ trình này — không thể gỡ` : null;
  };

  const makeDefault = async (cur: Curriculum) => {
    const ok = await confirm({
      title: `Đặt "${cur.name} ${cur.version}" làm mặc định?`,
      consequences: [
        `Lớp mở mới của khóa ${course.code} sẽ học theo lộ trình này`,
        'Các lớp đã mở vẫn giữ lộ trình hiện tại của lớp',
      ],
      tone: 'primary',
      confirmAction: 'save',
      confirmText: 'Đặt mặc định',
    });
    if (!ok) return;
    setDefault(course.id, cur.id);
    notify.success({ title: 'Đã đổi lộ trình mặc định', description: `${cur.name} ${cur.version}` });
  };

  const remove = async (cur: Curriculum) => {
    const ok = await confirm({
      title: `Gỡ lộ trình "${cur.name} ${cur.version}" khỏi khóa?`,
      description: 'Chỉ gỡ liên kết — nội dung lộ trình và các khóa khác dùng chung không bị ảnh hưởng.',
      tone: 'warning',
      confirmAction: 'reject',
      confirmText: 'Gỡ lộ trình',
    });
    if (!ok) return;
    unlink(course.id, cur.id);
    notify.toast.success('Đã gỡ lộ trình');
  };

  return (
    <div className="flex flex-col gap-4">
      <div className="flex gap-2.5 rounded-lg border border-primary-200 bg-primary-50 px-3 py-2.5 text-[13px] text-primary-800">
        <Info size={16} className="mt-0.5 shrink-0" aria-hidden />
        <p>
          Lớp <strong>mở mới</strong> theo khóa sẽ dùng lộ trình <strong>Mặc định</strong>. Lớp đã mở giữ lộ trình chốt lúc mở lớp. Một lộ trình có thể dùng chung cho
          nhiều khóa (vd: N3 lớp nhóm và N3 1-1).
        </p>
      </div>

      <div className="flex flex-wrap items-center justify-between gap-2">
        <p className="text-[13px] text-ink-muted">{linked.length} lộ trình gắn với khóa</p>
        {editable && (
          <Button icon={Link2} onClick={() => setLinking(true)} disabled={!linkable.length}>
            Gắn lộ trình
          </Button>
        )}
      </div>

      {!linked.length ? (
        <div className="rounded-[var(--radius-card)] border border-line">
          <EmptyState
            size="compact"
            icon={Route}
            title="Khóa chưa có lộ trình"
            description="Gắn lộ trình để giáo viên biết dạy gì ở từng buổi. Chưa có lộ trình phù hợp? Tạo ở Đào tạo → Lộ trình."
          />
        </div>
      ) : (
        <ul className="flex flex-col gap-3">
          {linked.map(({ link: l, cur }) => (
            <CurriculumCard
              key={cur.id}
              cur={cur}
              isDefault={l.isDefault}
              linkedAt={l.linkedAt}
              classCodes={classesUsing(cur.id).map((k) => `${k.code}${k.status === 'finished' ? ' (đã kết thúc)' : ''}`)}
              shared={sharedWith(cur.id)}
              editable={editable}
              unlinkBlocked={unlinkBlockReason(cur.id, l.isDefault)}
              onDefault={() => makeDefault(cur)}
              onUnlink={() => remove(cur)}
              onView={() => setViewing(cur.id)}
            />
          ))}
        </ul>
      )}

      <ModalForm<LinkValues>
        open={linking}
        onClose={() => setLinking(false)}
        title="Gắn lộ trình vào khóa"
        description={`${course.code} — ${course.name}`}
        submitAction="save"
        submitText="Gắn lộ trình"
        successMessage="Đã gắn lộ trình"
        onSubmit={async (v) => {
          await new Promise((r) => setTimeout(r, 400));
          link(course.id, v.curriculumId, Boolean(v.asDefault));
        }}
      >
        <Form.Item name="curriculumId" label="Lộ trình" rules={[{ required: true, message: 'Chọn lộ trình' }]}>
          <Select
            placeholder="Tìm theo tên, mã, cấp độ..."
            options={linkable
              // Ưu tiên lộ trình đúng cấp đầu ra của khóa
              .sort((a, b) => Number(b.level === course.levelTo) - Number(a.level === course.levelTo))
              .map((c) => ({
                value: c.id,
                label: `${c.name} ${c.version} · ${LEVELS[c.level]} · ${TIERS[c.tier].label}${c.status === 'draft' ? ' (nháp)' : ''}`,
              }))}
          />
        </Form.Item>
        <Form.Item noStyle shouldUpdate={(a, b) => a.curriculumId !== b.curriculumId}>
          {({ getFieldValue }) => {
            const picked = curricula.find((c) => c.id === getFieldValue('curriculumId'));
            const draft = picked?.status === 'draft';
            return (
              <Form.Item name="asDefault" valuePropName="checked" extra={draft ? 'Lộ trình nháp chưa thể làm mặc định — phát hành trước ở mục Lộ trình' : undefined}>
                <Checkbox disabled={!picked || draft}>Đặt làm lộ trình mặc định cho lớp mở mới</Checkbox>
              </Form.Item>
            );
          }}
        </Form.Item>
      </ModalForm>
      <CurriculumDetailDrawer curriculumId={viewing} onClose={() => setViewing(null)} onOpen={setViewing} />
      {confirmDialog}
    </div>
  );
}

interface CardProps {
  cur: Curriculum;
  isDefault: boolean;
  linkedAt: string;
  classCodes: string[];
  shared: Course[];
  editable: boolean;
  unlinkBlocked: string | null;
  onDefault: () => void;
  onUnlink: () => void;
  onView: () => void;
}

function CurriculumCard({ cur, isDefault, linkedAt, classCodes, shared, editable, unlinkBlocked, onDefault, onUnlink, onView }: CardProps) {
  const tier = TIERS[cur.tier];
  const st = CURRICULUM_STATUS[cur.status];
  const stats = curriculumStats(cur);
  const canDefault = cur.status === 'published';

  return (
    <li className={cn('flex flex-col gap-3 rounded-[var(--radius-card)] border bg-surface p-4', isDefault ? 'border-primary-300 ring-1 ring-primary-200' : 'border-line')}>
      <div className="flex flex-wrap items-start justify-between gap-2">
        <div className="min-w-0">
          <p className="font-semibold text-ink">
            {cur.name} <span className="font-normal text-ink-muted">{cur.version}</span>
          </p>
          <p className="text-[13px] text-ink-muted">
            <span className="font-mono">{cur.code}</span> · {LEVELS[cur.level]} · Cập nhật {fmtDate(cur.updatedAt)} bởi {cur.author}
          </p>
        </div>
        <span className="flex flex-wrap gap-1">
          {isDefault && (
            <StatusTag tone="primary" icon={Star} size="sm">
              Mặc định
            </StatusTag>
          )}
          <StatusTag tone={tier.tone} icon={tier.icon} size="sm">
            {tier.label}
          </StatusTag>
          <StatusTag tone={st.tone} icon={st.icon} size="sm">
            {st.label}
          </StatusTag>
        </span>
      </div>

      <div className="flex flex-wrap gap-x-5 gap-y-1 text-[13px] text-ink-secondary">
        <span className="inline-flex items-center gap-1.5">
          <BookOpen size={14} className="text-ink-muted" aria-hidden /> {stats.lessons} lesson · {stats.chapters} chapter
        </span>
        <span className="inline-flex items-center gap-1.5">
          <FileText size={14} className="text-ink-muted" aria-hidden /> {stats.materials} tài liệu
        </span>
        <span className="inline-flex items-center gap-1.5">
          <ClipboardCheck size={14} className="text-ink-muted" aria-hidden /> {stats.quizzes} quiz
        </span>
        <span className="inline-flex items-center gap-1.5">
          <FileQuestion size={14} className="text-ink-muted" aria-hidden /> {stats.exams} mốc thi (giữa / cuối kỳ)
        </span>
      </div>

      <dl className="grid grid-cols-1 gap-x-3 gap-y-1 text-[13px] sm:grid-cols-[180px_1fr]">
        {cur.tier === 'advanced' && cur.entryRequirement && (
          <>
            <dt className="text-ink-muted">Áp dụng khi</dt>
            <dd className="text-ink">{cur.entryRequirement}</dd>
          </>
        )}
        {cur.textbook && (
          <>
            <dt className="text-ink-muted">Giáo trình</dt>
            <dd className="text-ink">{cur.textbook}</dd>
          </>
        )}
        <dt className="text-ink-muted">Lớp của khóa đang dùng</dt>
        <dd className="text-ink">{classCodes.length ? classCodes.join(', ') : '—'}</dd>
        <dt className="text-ink-muted">Dùng chung với khóa</dt>
        <dd className="text-ink">{shared.length ? shared.map((c) => c.code).join(', ') : '—'}</dd>
        <dt className="text-ink-muted">Gắn vào khóa</dt>
        <dd className="tabular text-ink">{fmtDate(linkedAt)}</dd>
      </dl>

      <div className="flex flex-wrap items-center justify-between gap-2 border-t border-line pt-3">
        <Button action="view" size="sm" variant="soft" onClick={onView}>
          Xem chi tiết lộ trình
        </Button>
        {editable && (
          <div className="flex flex-wrap gap-1">
            <Tooltip title={unlinkBlocked}>
              <span>
                <Button icon={Unlink} size="sm" tone="neutral" variant="ghost" disabled={Boolean(unlinkBlocked)} onClick={onUnlink}>
                  Gỡ
                </Button>
              </span>
            </Tooltip>
            {!isDefault && (
              <Tooltip title={canDefault ? undefined : 'Chỉ lộ trình đã phát hành mới làm mặc định được'}>
                <span>
                  <Button icon={Star} size="sm" variant="soft" disabled={!canDefault} onClick={onDefault}>
                    Đặt mặc định
                  </Button>
                </span>
              </Tooltip>
            )}
          </div>
        )}
      </div>
    </li>
  );
}

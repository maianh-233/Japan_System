import { useEffect, useState } from 'react';
import { useClassStore } from '@/features/academic/classes/classStore';
import { isOpenClass } from '@/features/academic/classes/constants';
import { Drawer, Form, Tabs } from 'antd';
import { ArchiveRestore, BookOpen, GitBranchPlus, Info, Library, Lock, Rocket, Send, Star } from 'lucide-react';
import { useNavigate } from 'react-router-dom';
import { Button, EmptyState, StatusTag, useConfirm, useNotify } from '@/components/common';
import { useAuth } from '@/hooks/useAuth';
import { useBreakpoint } from '@/hooks/useBreakpoint';
import { fmtDateTime } from '@/lib/date';
import { FORMATS, LEVELS, STUDY_MODES, TEACHER_TYPES } from '../courses/constants';
import { useCourseStore } from '../courses/courseStore';
import { CURRICULUM_STATUS, TIERS } from './constants';
import { CurriculumContent } from './CurriculumContent';
import { CurriculumFormFields, type CurriculumFormValues } from './CurriculumFormFields';
import { curriculumStats, nextVersion, useCurriculumStore } from './curriculumStore';
import * as curriculumApi from './curriculumApi';
import { USE_API } from '@/lib/api/config';
import type { Curriculum } from './types';

export type CurriculumTab = 'content' | 'info' | 'courses';

interface Props {
  curriculumId: string | null;
  initialTab?: CurriculumTab;
  onClose: () => void;
  /** Mở lộ trình khác (sau khi tạo phiên bản mới / biến thể) */
  onOpen?: (id: string) => void;
}

/** Chi tiết lộ trình: Nội dung (bài → chapter → tài liệu, mốc thi) · Thông tin · Khóa áp dụng */
export function CurriculumDetailDrawer({ curriculumId, initialTab = 'content', onClose, onOpen }: Props) {
  const { isMobile } = useBreakpoint();
  const { can, user } = useAuth();
  const notify = useNotify();
  const { confirm, confirmDialog } = useConfirm();
  const cur = useCurriculumStore((s) => s.curricula.find((c) => c.id === curriculumId));
  const links = useCurriculumStore((s) => s.links);
  const setStatus = useCurriculumStore((s) => s.setStatus);
  const clone = useCurriculumStore((s) => s.cloneCurriculum);
  const classes = useClassStore((s) => s.classes);
  const [tab, setTab] = useState<CurriculumTab>(initialTab);
  const editable = can('academic.curriculum');

  useEffect(() => {
    if (curriculumId) setTab(initialTab);
  }, [curriculumId, initialTab]);

  const courseCount = links.filter((l) => l.curriculumId === curriculumId).length;
  const openClasses = classes.filter((k) => k.curriculumId === curriculumId && (isOpenClass(k)));

  const publish = async (c: Curriculum) => {
    const st = curriculumStats(c);
    const ok = await confirm({
      title: `Phát hành ${c.name} ${c.version}?`,
      description: 'Sau khi phát hành, nội dung bị khóa để đảm bảo các lớp học đúng một phiên bản. Muốn sửa phải tạo phiên bản mới.',
      consequences: [
        `${st.lessons} bài · ${st.chapters} chapter · ${st.exams} mốc thi · ${st.materials} tài liệu`,
        st.emptyChapters
          ? <span className="font-medium text-warning-700">{st.emptyChapters} chapter chưa có tài liệu</span>
          : 'Mọi chapter đều đã có tài liệu',
        'Giáo vụ chọn được lộ trình này khi mở lớp',
      ],
      tone: 'success',
      confirmAction: 'approve',
      confirmText: 'Phát hành',
      onConfirm: USE_API ? () => curriculumApi.publishCurriculum(c.id) : undefined,
    });
    if (!ok) return;
    if (!USE_API) setStatus(c.id, 'published');
    notify.success({ title: 'Đã phát hành lộ trình', description: `${c.name} ${c.version}` });
  };

  const newVersion = async (c: Curriculum) => {
    let created: Curriculum | undefined;
    const ok = await confirm({
      title: `Tạo phiên bản mới từ ${c.version}?`,
      description: 'Sao chép toàn bộ bài học, chapter, tài liệu, mốc thi sang một bản nháp để chỉnh sửa. Bản hiện tại giữ nguyên cho các lớp đang học.',
      consequences: [`Bản nháp ${nextVersion(c.version)} được gắn vào ${courseCount} khóa đang dùng (không đặt mặc định)`],
      tone: 'primary',
      confirmAction: 'create',
      confirmText: 'Tạo phiên bản mới',
      onConfirm: USE_API
        ? async () => {
            created = await curriculumApi.newCurriculumVersion(c.id);
          }
        : undefined,
    });
    if (!ok) return;
    const copy = created ?? clone(c.id, { version: nextVersion(c.version) }, user?.fullName ?? 'Nhân viên');
    notify.success({ title: `Đã tạo bản nháp ${copy.version}`, description: 'Sửa nội dung rồi bấm Phát hành.' });
    onOpen?.(copy.id);
  };

  const archive = async (c: Curriculum) => {
    if (c.status === 'archived') {
      if (USE_API) {
        try {
          await curriculumApi.restoreCurriculum(c.id);
        } catch (e) {
          notify.error({ title: 'Chưa khôi phục được', description: (e as Error).message });
          return;
        }
      } else setStatus(c.id, 'published');
      notify.toast.success('Đã khôi phục lộ trình');
      return;
    }
    const defaults = links.filter((l) => l.curriculumId === c.id && l.isDefault).length;
    if (defaults) {
      notify.warning({ title: 'Chưa thể lưu trữ', description: `Lộ trình đang là mặc định của ${defaults} khóa — đặt lộ trình khác làm mặc định ở trang Khóa học trước.` });
      return;
    }
    const ok = await confirm({
      title: `Lưu trữ ${c.name} ${c.version}?`,
      consequences: [
        'Không chọn được khi mở lớp mới',
        openClasses.length ? `${openClasses.length} lớp đang học (${openClasses.map((k) => k.code).join(', ')}) vẫn dùng bình thường` : 'Không có lớp nào đang học theo lộ trình này',
      ],
      tone: 'warning',
      confirmAction: 'save',
      confirmText: 'Lưu trữ',
      onConfirm: USE_API ? async () => void (await curriculumApi.archiveCurriculum(c.id)) : undefined,
    });
    if (!ok) return;
    if (!USE_API) setStatus(c.id, 'archived');
    notify.success({ title: 'Đã lưu trữ lộ trình', description: `${c.name} ${c.version}` });
  };

  const st = cur && CURRICULUM_STATUS[cur.status];
  const tier = cur && TIERS[cur.tier];

  return (
    <Drawer
      open={Boolean(curriculumId)}
      onClose={onClose}
      width={isMobile ? '100%' : 880}
      destroyOnHidden
      styles={{ body: { paddingTop: 0 } }}
      title={
        cur &&
        st &&
        tier && (
          <div className="flex min-w-0 flex-col gap-1 pr-2">
            <div className="flex flex-wrap items-center gap-2">
              <span className="truncate text-base font-semibold">{cur.name}</span>
              <span className="text-sm font-normal text-ink-muted">{cur.version}</span>
              <StatusTag tone={tier.tone} icon={tier.icon} size="sm">
                {tier.label}
              </StatusTag>
              <StatusTag tone={st.tone} icon={st.icon} size="sm">
                {st.label}
              </StatusTag>
            </div>
            <span className="text-xs font-normal text-ink-muted">
              <span className="font-mono">{cur.code}</span> · {LEVELS[cur.level]} · Cập nhật {fmtDateTime(cur.updatedAt)} bởi {cur.author}
            </span>
          </div>
        )
      }
      extra={
        cur &&
        editable && (
          <div className="flex gap-2">
            {cur.status === 'draft' && (
              <Button icon={Send} size="sm" tone="success" collapseOnMobile onClick={() => publish(cur)}>
                Phát hành
              </Button>
            )}
            {cur.status === 'published' && (
              <Button icon={GitBranchPlus} size="sm" variant="outline" collapseOnMobile onClick={() => newVersion(cur)}>
                Tạo phiên bản mới
              </Button>
            )}
            {cur.status === 'archived' && (
              <Button icon={ArchiveRestore} size="sm" variant="outline" collapseOnMobile onClick={() => archive(cur)}>
                Khôi phục
              </Button>
            )}
          </div>
        )
      }
    >
      {cur && (
        <Tabs
          activeKey={tab}
          onChange={(k) => setTab(k as CurriculumTab)}
          items={[
            {
              key: 'content',
              label: (
                <span className="inline-flex items-center gap-1.5">
                  <BookOpen size={16} aria-hidden /> Nội dung
                </span>
              ),
              children: (
                <div className="flex flex-col gap-3">
                  {cur.status !== 'draft' && editable && (
                    <div className="flex flex-wrap items-center gap-2.5 rounded-lg border border-line bg-page px-3 py-2.5 text-[13px] text-ink-secondary">
                      <Lock size={16} className="shrink-0 text-ink-muted" aria-hidden />
                      <p className="min-w-0 flex-1">
                        {cur.status === 'published'
                          ? `Đã phát hành — nội dung bị khóa${openClasses.length ? ` (${openClasses.length} lớp đang học theo bản này)` : ''}. Muốn sửa, tạo phiên bản mới.`
                          : 'Đã lưu trữ — chỉ xem.'}
                      </p>
                    </div>
                  )}
                  {cur.tier === 'advanced' && cur.entryRequirement && (
                    <div className="flex gap-2.5 rounded-lg border border-primary-200 bg-primary-50 px-3 py-2.5 text-[13px] text-primary-800">
                      <Rocket size={16} className="mt-0.5 shrink-0" aria-hidden />
                      <p>
                        <strong>Áp dụng khi:</strong> {cur.entryRequirement}
                      </p>
                    </div>
                  )}
                  <CurriculumContent curriculum={cur} editable={editable && cur.status === 'draft'} />
                </div>
              ),
            },
            {
              key: 'info',
              label: (
                <span className="inline-flex items-center gap-1.5">
                  <Info size={16} aria-hidden /> Thông tin
                </span>
              ),
              children: <InfoTab key={cur.id} cur={cur} editable={editable && cur.status !== 'archived'} onArchive={() => archive(cur)} />,
            },
            {
              key: 'courses',
              label: (
                <span className="inline-flex items-center gap-1.5">
                  <Library size={16} aria-hidden /> Khóa áp dụng ({courseCount})
                </span>
              ),
              children: <CoursesTab cur={cur} />,
            },
          ]}
        />
      )}
      {confirmDialog}
    </Drawer>
  );
}

function InfoTab({ cur, editable, onArchive }: { cur: Curriculum; editable: boolean; onArchive: () => void }) {
  const [form] = Form.useForm<CurriculumFormValues>();
  const notify = useNotify();
  const update = useCurriculumStore((s) => s.updateCurriculum);
  const [saving, setSaving] = useState(false);
  const [dirty, setDirty] = useState(false);

  return (
    <Form
      form={form}
      layout="vertical"
      requiredMark
      disabled={!editable}
      initialValues={cur}
      onValuesChange={() => setDirty(true)}
      onFinish={async (v) => {
        setSaving(true);
        try {
          if (USE_API) await curriculumApi.updateCurriculumInfo(cur.id, { ...cur, ...v });
          else {
            await new Promise((r) => setTimeout(r, 500));
            update(cur.id, { ...v, name: v.name.trim(), entryRequirement: v.tier === 'advanced' ? v.entryRequirement?.trim() : undefined, tags: v.tags ?? [] });
          }
        } catch (e) {
          notify.error({ title: 'Chưa lưu được lộ trình', description: (e as Error).message });
          return;
        } finally {
          setSaving(false);
        }
        setDirty(false);
        notify.success({ title: 'Đã cập nhật lộ trình', description: `${v.name} ${v.version}` });
      }}
      className="grid grid-cols-1 gap-x-4 md:grid-cols-2"
    >
      <CurriculumFormFields />
      {editable && (
        <div className="sticky bottom-0 -mx-6 flex flex-col-reverse gap-2 border-t border-line bg-surface px-6 py-3 sm:flex-row sm:items-center md:col-span-2">
          {cur.status === 'published' && (
            <Button action="reject" variant="ghost" tone="neutral" className="sm:mr-auto" onClick={onArchive}>
              Lưu trữ lộ trình
            </Button>
          )}
          <Button
            action="reset"
            className={cur.status !== 'published' ? 'sm:ml-auto' : undefined}
            disabled={!dirty || saving}
            onClick={() => {
              form.resetFields();
              setDirty(false);
            }}
          >
            Hoàn tác thay đổi
          </Button>
          <Button action="save" htmlType="submit" loading={saving} disabled={!dirty}>
            Lưu thay đổi
          </Button>
        </div>
      )}
    </Form>
  );
}

/** Các khóa đang gắn lộ trình + lớp đang học theo lộ trình */
function CoursesTab({ cur }: { cur: Curriculum }) {
  const navigate = useNavigate();
  const links = useCurriculumStore((s) => s.links);
  const courses = useCourseStore((s) => s.courses);
  const classes = useClassStore((s) => s.classes);

  const rows = links
    .filter((l) => l.curriculumId === cur.id)
    .map((l) => ({ link: l, course: courses.find((c) => c.id === l.courseId) }))
    .filter((x): x is { link: typeof x.link; course: NonNullable<typeof x.course> } => Boolean(x.course));

  if (!rows.length)
    return (
      <div className="rounded-[var(--radius-card)] border border-line">
        <EmptyState size="compact" icon={Library} title="Chưa gắn vào khóa nào" description="Gắn lộ trình ở trang Khóa học → tab Lộ trình của khóa." />
      </div>
    );

  return (
    <ul className="flex flex-col gap-3">
      {rows.map(({ link, course }) => {
        const f = FORMATS[course.format];
        const t = TEACHER_TYPES[course.teacherType];
        const m = STUDY_MODES[course.mode];
        const using = classes.filter((k) => k.courseId === course.id && k.curriculumId === cur.id);
        return (
          <li key={course.id} className="flex flex-col gap-2 rounded-[var(--radius-card)] border border-line bg-surface p-4">
            <div className="flex flex-wrap items-start justify-between gap-2">
              <div className="min-w-0">
                <p className="font-semibold text-ink">{course.name}</p>
                <p className="text-[13px] text-ink-muted">
                  <span className="font-mono">{course.code}</span>
                </p>
              </div>
              {link.isDefault && (
                <StatusTag tone="primary" icon={Star} size="sm">
                  Mặc định của khóa
                </StatusTag>
              )}
            </div>
            <span className="flex flex-wrap gap-1">
              <StatusTag tone={f.tone} icon={f.icon} size="sm">
                {f.label}
              </StatusTag>
              <StatusTag tone={t.tone} icon={t.icon} size="sm">
                {t.short}
              </StatusTag>
              <StatusTag tone={m.tone} icon={m.icon} size="sm">
                {m.label}
              </StatusTag>
            </span>
            <div className="flex flex-wrap items-center justify-between gap-2 border-t border-line pt-2 text-[13px]">
              <span className="text-ink-secondary">
                Lớp dùng lộ trình này: {using.length ? using.map((k) => `${k.code}${k.status === 'finished' ? ' (đã kết thúc)' : ''}`).join(', ') : '—'}
              </span>
              <Button action="view" size="sm" variant="soft" onClick={() => navigate('/admin/academic/courses')}>
                Xem khóa học
              </Button>
            </div>
          </li>
        );
      })}
    </ul>
  );
}

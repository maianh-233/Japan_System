import { useMemo, useState, type Key } from 'react';
import { Tooltip } from 'antd';
import { Archive, CheckCircle2, FileQuestion, FileSpreadsheet, Hourglass, Link2Off, Send, TrendingDown } from 'lucide-react';
import { Button, DataTable, DrawerForm, PageHeader, Select, StatCard, StatusTag, useConfirm, useNotify, type DataColumn } from '@/components/common';
import { useCurriculumStore } from '@/features/academic/curriculum/curriculumStore';
import { correctRate, Q_DIFFICULTY, Q_DIFFICULTY_OPTIONS, Q_FORMATS, Q_LEVELS, Q_SKILL_OPTIONS, Q_SKILLS, Q_STATUS, Q_STATUS_OPTIONS, rateTone } from '@/features/academic/questions/constants';
import { plainText } from '@/features/academic/questions/Furigana';
import { QuestionDetailDrawer } from '@/features/academic/questions/QuestionDetailDrawer';
import { QuestionFormFields, type QuestionFormValues } from '@/features/academic/questions/QuestionFormFields';
import { useQuestionStore, type QuestionInput } from '@/features/academic/questions/questionStore';
import type { QLevel, Question } from '@/features/academic/questions/types';
import { useAuth } from '@/hooks/useAuth';
import { cn } from '@/lib/cn';

interface Row extends Question {
  /** Đề bài dạng chữ thuần — để tìm kiếm */
  plain: string;
  rate?: number;
}

type Dialog = { kind: 'create' } | { kind: 'edit'; id: string } | null;

const NEW_Q: Partial<QuestionFormValues> = { level: 'N5', skill: 'vocab', format: 'choice', difficulty: 'medium', options: ['', '', '', ''], answer: 0, lessonIds: [], tags: [] };

/** Ngân hàng câu hỏi — DỮ LIỆU GIẢ. Câu đã duyệt được bốc vào bài tập trắc nghiệm, kỳ thi, test xếp lớp. */
export default function QuestionsPage() {
  const { user, can } = useAuth();
  const notify = useNotify();
  const { confirm, confirmDialog } = useConfirm();
  const questions = useQuestionStore((s) => s.questions);
  const add = useQuestionStore((s) => s.add);
  const update = useQuestionStore((s) => s.update);
  const setStatus = useQuestionStore((s) => s.setStatus);
  const curricula = useCurriculumStore((s) => s.curricula);
  const editable = can('academic.questions');

  const [level, setLevel] = useState<QLevel | 'all'>('all');
  const [skill, setSkill] = useState<string>();
  const [difficulty, setDifficulty] = useState<string>();
  const [status, setStatusFilter] = useState<string>();
  const [curriculumId, setCurriculumId] = useState<string>();
  const [lessonId, setLessonId] = useState<string>();
  const [special, setSpecial] = useState<'hard' | 'unlinked' | undefined>();
  const [selected, setSelected] = useState<Key[]>([]);
  const [dialog, setDialog] = useState<Dialog>(null);
  const [openId, setOpenId] = useState<string | null>(null);

  const rows: Row[] = useMemo(() => questions.map((q) => ({ ...q, plain: plainText(`${q.stem} ${q.options.join(' ')} ${q.passage ?? ''}`), rate: correctRate(q.stats) })), [questions]);

  const base = useMemo(
    () =>
      rows.filter(
        (q) =>
          (!skill || q.skill === skill) &&
          (!difficulty || q.difficulty === difficulty) &&
          (!status || q.status === status) &&
          (!curriculumId || q.curriculumId === curriculumId) &&
          (!lessonId || q.lessonIds.includes(lessonId)) &&
          (special !== 'hard' || (q.rate !== undefined && q.rate < 40)) &&
          (special !== 'unlinked' || !q.lessonIds.length),
      ),
    [rows, skill, difficulty, status, curriculumId, lessonId, special],
  );
  const counts = useMemo(() => {
    const c: Record<string, number> = { all: base.length };
    base.forEach((q) => (c[q.level] = (c[q.level] ?? 0) + 1));
    return c;
  }, [base]);
  const data = level === 'all' ? base : base.filter((q) => q.level === level);

  const stats = useMemo(
    () => ({
      approved: questions.filter((q) => q.status === 'approved').length,
      review: questions.filter((q) => q.status === 'review').length,
      hard: rows.filter((q) => q.status === 'approved' && q.rate !== undefined && q.rate < 40).length,
      unlinked: questions.filter((q) => q.status !== 'retired' && !q.lessonIds.length).length,
    }),
    [questions, rows],
  );

  const me = user?.fullName ?? 'Nhân viên';
  const curriculum = curricula.find((c) => c.id === curriculumId);

  const columns: DataColumn<Row>[] = [
    {
      key: 'stem',
      title: 'Câu hỏi',
      width: 380,
      mobile: 'title',
      render: (_, q) => {
        const sk = Q_SKILLS[q.skill];
        return (
          <div className={cn('flex min-w-0 gap-2.5', q.status === 'retired' && 'opacity-60')}>
            <span className="grid size-8 shrink-0 place-items-center rounded-lg bg-primary-50 text-primary-600" title={sk.label}>
              <sk.icon size={16} aria-hidden />
            </span>
            <div className="min-w-0">
              <p className="truncate text-ink" lang="ja">
                {plainText(q.stem)}
              </p>
              <p className="truncate text-xs text-ink-muted">
                <span className="font-mono">{q.code}</span> · {sk.label}
                {q.format === 'star' && ` · ${Q_FORMATS.star.label}`}
                {q.passage && ' · có đoạn văn'}
                {q.audio && ' · có audio'}
              </p>
            </div>
          </div>
        );
      },
    },
    {
      key: 'level',
      title: 'Cấp',
      width: 70,
      align: 'center',
      mobile: 'subtitle',
      sorter: (a, b) => Q_LEVELS.indexOf(a.level) - Q_LEVELS.indexOf(b.level),
      render: (_, q) => <span className="rounded-md bg-primary-50 px-1.5 py-0.5 text-xs font-semibold text-primary-700">{q.level}</span>,
    },
    {
      key: 'difficulty',
      title: 'Độ khó',
      width: 110,
      render: (_, q) => <span className={cn('text-[13px]', Q_DIFFICULTY[q.difficulty].tone === 'danger' ? 'text-danger-600' : Q_DIFFICULTY[q.difficulty].tone === 'success' ? 'text-success-700' : 'text-ink-secondary')}>{Q_DIFFICULTY[q.difficulty].label}</span>,
    },
    {
      key: 'lesson',
      title: 'Bài học',
      width: 150,
      render: (_, q) => {
        if (!q.lessonIds.length) return <span className="text-[13px] text-warning-700">Chưa gắn</span>;
        const cur = curricula.find((c) => c.id === q.curriculumId);
        const idx = cur?.lessons.findIndex((l) => l.id === q.lessonIds[0]) ?? -1;
        return (
          <Tooltip title={cur ? `${cur.name} — ${cur.lessons[idx]?.title ?? ''}` : undefined}>
            <span className="text-[13px] text-ink-secondary">
              {cur?.code ?? '—'} · Bài {idx + 1}
              {q.lessonIds.length > 1 && ` +${q.lessonIds.length - 1}`}
            </span>
          </Tooltip>
        );
      },
    },
    {
      key: 'rate',
      title: 'Tỉ lệ đúng',
      width: 130,
      sorter: (a, b) => (a.rate ?? 101) - (b.rate ?? 101),
      render: (_, q) =>
        q.rate === undefined ? (
          <span className="text-[13px] text-ink-muted">Chưa có</span>
        ) : (
          <div className="flex flex-col gap-1">
            <span className="tabular flex justify-between text-[13px]">
              <span className={cn('font-medium', rateTone(q.rate) === 'danger' ? 'text-danger-600' : rateTone(q.rate) === 'warning' ? 'text-warning-700' : 'text-success-700')}>{q.rate}%</span>
              <span className="text-xs text-ink-muted">{q.stats.attempts} lượt</span>
            </span>
            <div className="h-1.5 rounded-full bg-disabled">
              <div className={cn('h-full rounded-full', rateTone(q.rate) === 'danger' ? 'bg-danger-600' : rateTone(q.rate) === 'warning' ? 'bg-warning-600' : 'bg-success-600')} style={{ width: `${q.rate}%` }} />
            </div>
          </div>
        ),
    },
    {
      key: 'status',
      title: 'Trạng thái',
      width: 130,
      mobile: 'aside',
      render: (_, q) => {
        const s = Q_STATUS[q.status];
        return (
          <StatusTag tone={s.tone} icon={s.icon} size="sm">
            {s.label}
          </StatusTag>
        );
      },
    },
  ];

  const toInput = (v: QuestionFormValues): QuestionInput => ({
    level: v.level,
    skill: v.skill,
    format: v.format,
    difficulty: v.difficulty,
    stem: v.stem.trim(),
    passage: v.skill === 'reading' ? v.passage?.trim() : undefined,
    audio: v.skill === 'listening' && v.audioFiles?.length ? { name: v.audioFiles[0].name, durationSec: 60 } : undefined,
    transcript: v.skill === 'listening' ? v.transcript?.trim() || undefined : undefined,
    options: v.options.map((o) => o.trim()),
    answer: v.answer,
    explanation: v.explanation?.trim() || undefined,
    curriculumId: v.curriculumId,
    lessonIds: v.lessonIds ?? [],
    tags: v.tags ?? [],
  });

  const editing = dialog?.kind === 'edit' ? questions.find((q) => q.id === dialog.id) : undefined;

  const bulk = async (kind: 'approve' | 'review' | 'retire') => {
    const ids = selected.map(String);
    const list = questions.filter((q) => ids.includes(q.id));
    const ok = list.filter((q) => (kind === 'approve' ? q.status === 'review' : kind === 'review' ? q.status === 'draft' : q.status === 'approved'));
    if (!ok.length) {
      notify.toast.warning(kind === 'approve' ? 'Chỉ duyệt được câu đang "Chờ duyệt"' : kind === 'review' ? 'Chỉ gửi duyệt câu "Nháp"' : 'Chỉ ngừng dùng câu "Đã duyệt"');
      return;
    }
    if (kind === 'retire') {
      const res = await confirm({ title: `Ngừng dùng ${ok.length} câu hỏi?`, tone: 'danger', confirmAction: 'reject', confirmText: 'Ngừng dùng', requireReason: { label: 'Lý do', placeholder: 'VD: lộ đề' } });
      if (!res) return;
      setStatus(ok.map((q) => q.id), 'retired', { by: me, note: res.reason });
    } else setStatus(ok.map((q) => q.id), kind === 'approve' ? 'approved' : 'review', kind === 'approve' ? { by: me } : undefined);
    notify.toast.success(`Đã cập nhật ${ok.length} câu${ok.length < list.length ? ` (bỏ qua ${list.length - ok.length} câu không phù hợp)` : ''}`);
    setSelected([]);
  };

  return (
    <>
      <PageHeader
        title="Ngân hàng câu hỏi"
        subtitle="Từ vựng, Kanji, ngữ pháp, đọc hiểu, nghe hiểu N5–N1 · dùng chung cho bài tập trắc nghiệm, kỳ thi, test xếp lớp"
        breadcrumb={[{ title: 'Đào tạo' }, { title: 'Ngân hàng câu hỏi' }]}
        actions={
          <>
            <Button icon={FileSpreadsheet} variant="outline" collapseOnMobile onClick={() => notify.info({ title: 'Nhập từ Excel (giả lập)', description: 'Tải file mẫu → điền câu hỏi → tải lên. Câu nhập vào ở trạng thái "Chờ duyệt".' })}>
              Nhập Excel
            </Button>
            {editable && (
              <Button action="create" collapseOnMobile onClick={() => setDialog({ kind: 'create' })}>
                Thêm câu hỏi
              </Button>
            )}
          </>
        }
      />

      <div className="mb-4 grid grid-cols-2 gap-3 lg:grid-cols-4">
        <StatCard label="Câu đã duyệt" value={stats.approved} icon={CheckCircle2} tone="success" hint={`Trên tổng ${questions.length} câu`} onClick={() => setStatusFilter('approved')} />
        <StatCard label="Chờ duyệt" value={stats.review} icon={Hourglass} tone={stats.review ? 'warning' : 'success'} hint="Tổ chuyên môn kiểm tra" onClick={() => setStatusFilter('review')} />
        <StatCard label="Khó hơn dự kiến" value={stats.hard} icon={TrendingDown} tone="danger" hint="Tỉ lệ đúng < 40% — xem lại đề / đáp án" onClick={() => setSpecial('hard')} />
        <StatCard label="Chưa gắn bài học" value={stats.unlinked} icon={Link2Off} tone="info" hint="Không bốc được theo bài" onClick={() => setSpecial('unlinked')} />
      </div>

      {/* Lọc nhanh theo cấp độ */}
      <div className="-mx-4 mb-4 flex gap-2 overflow-x-auto px-4 pb-1 [scrollbar-width:none] sm:mx-0 sm:flex-wrap sm:px-0 [&::-webkit-scrollbar]:hidden" role="tablist" aria-label="Lọc theo cấp độ">
        {(['all', ...Q_LEVELS] as const).map((l) => {
          const active = level === l;
          return (
            <button
              key={l}
              type="button"
              role="tab"
              aria-selected={active}
              onClick={() => setLevel(l)}
              className={cn(
                'inline-flex h-9 shrink-0 items-center gap-2 rounded-full border px-3.5 text-sm font-medium transition-colors',
                active ? 'border-primary-600 bg-primary-600 text-white' : 'border-line bg-surface text-ink-secondary hover:border-primary-300 hover:bg-primary-50',
              )}
            >
              {l === 'all' ? 'Tất cả' : l}
              <span className={cn('tabular rounded-full px-1.5 text-xs', active ? 'bg-white/20' : 'bg-disabled text-ink-muted')}>{counts[l] ?? 0}</span>
            </button>
          );
        })}
        {special && (
          <button type="button" onClick={() => setSpecial(undefined)} className="inline-flex h-9 shrink-0 items-center gap-1.5 rounded-full border border-warning-600 bg-warning-50 px-3.5 text-sm font-medium text-warning-700">
            {special === 'hard' ? 'Tỉ lệ đúng < 40%' : 'Chưa gắn bài học'} ✕
          </button>
        )}
      </div>

      <DataTable<Row>
        storageKey="academic-questions"
        columns={columns}
        data={data}
        rowKey="id"
        unitLabel="câu hỏi"
        searchKeys={['plain', 'code', 'explanation', 'author']}
        onRowClick={(q) => setOpenId(q.id)}
        selectedKeys={editable ? selected : undefined}
        onSelectionChange={editable ? (keys) => setSelected(keys) : undefined}
        bulkActions={
          editable
            ? () => (
                <>
                  <Button icon={Send} size="sm" variant="soft" onClick={() => bulk('review')}>
                    Gửi duyệt
                  </Button>
                  <Button icon={CheckCircle2} size="sm" tone="success" variant="soft" onClick={() => bulk('approve')}>
                    Duyệt
                  </Button>
                  <Button icon={Archive} size="sm" tone="danger" variant="soft" onClick={() => bulk('retire')}>
                    Ngừng dùng
                  </Button>
                </>
              )
            : undefined
        }
        search={{
          placeholder: 'Tìm theo nội dung (tiếng Nhật / Việt), mã câu, người soạn...',
          activeFilterCount: [skill, difficulty, status, curriculumId, lessonId].filter(Boolean).length,
          filters: (
            <>
              <Select placeholder="Kỹ năng" value={skill} onChange={setSkill} options={Q_SKILL_OPTIONS} showSearch={false} className="lg:!w-36" />
              <Select placeholder="Độ khó" value={difficulty} onChange={setDifficulty} options={Q_DIFFICULTY_OPTIONS} showSearch={false} className="lg:!w-32" />
              <Select placeholder="Trạng thái" value={status} onChange={setStatusFilter} options={Q_STATUS_OPTIONS} showSearch={false} className="lg:!w-36" />
              <Select
                placeholder="Lộ trình"
                value={curriculumId}
                onChange={(v) => {
                  setCurriculumId(v);
                  setLessonId(undefined);
                }}
                options={curricula.filter((c) => c.status !== 'archived').map((c) => ({ value: c.id, label: `${c.code} ${c.version}` }))}
                className="lg:!w-44"
              />
              <Select
                placeholder="Bài học"
                value={lessonId}
                onChange={setLessonId}
                disabled={!curriculum}
                options={(curriculum?.lessons ?? []).map((l, i) => ({ value: l.id, label: `Bài ${i + 1}: ${l.title}` }))}
                className="lg:!w-48"
              />
            </>
          ),
          onReset: () => {
            setSkill(undefined);
            setDifficulty(undefined);
            setStatusFilter(undefined);
            setCurriculumId(undefined);
            setLessonId(undefined);
            setSpecial(undefined);
            setLevel('all');
          },
        }}
        rowActions={editable ? (q) => <Button action="edit" iconOnly size="sm" onClick={() => setDialog({ kind: 'edit', id: q.id })} /> : undefined}
        actionsWidth={70}
        empty={{
          icon: FileQuestion,
          title: 'Không có câu hỏi phù hợp',
          description: 'Thêm câu hỏi mới hoặc nhập từ Excel.',
          action: editable ? <Button action="create" onClick={() => setDialog({ kind: 'create' })}>Thêm câu hỏi</Button> : undefined,
        }}
      />

      <DrawerForm<QuestionFormValues>
        key={editing ? `edit-${editing.id}` : 'create'}
        open={Boolean(dialog)}
        onClose={() => setDialog(null)}
        width={820}
        columns={2}
        title={editing ? `Sửa câu ${editing.code}` : 'Thêm câu hỏi'}
        description={editing?.status === 'approved' ? 'Câu đã duyệt — sau khi sửa sẽ chuyển về "Chờ duyệt".' : 'Lưu nháp hoặc gửi duyệt. Chỉ câu đã duyệt mới được bốc vào đề.'}
        submitAction={editing ? 'save' : 'send'}
        submitText={editing ? 'Lưu' : 'Lưu & gửi duyệt'}
        successMessage={editing ? 'Đã lưu câu hỏi' : 'Đã thêm câu hỏi — chờ duyệt'}
        initialValues={editing ? { ...editing, audioFiles: editing.audio ? [{ uid: 'a', name: editing.audio.name, size: 0, status: 'done' }] : undefined } : NEW_Q}
        onSubmit={async (v) => {
          await new Promise((r) => setTimeout(r, 400));
          if (editing) update(editing.id, toInput(v));
          else {
            const q = add(toInput(v), me, 'review');
            setTimeout(() => setOpenId(q.id), 300);
          }
        }}
      >
        <QuestionFormFields />
      </DrawerForm>

      <QuestionDetailDrawer questionId={openId} editable={editable} onClose={() => setOpenId(null)} onEdit={(id) => setDialog({ kind: 'edit', id })} onOpen={setOpenId} />
      {confirmDialog}
    </>
  );
}

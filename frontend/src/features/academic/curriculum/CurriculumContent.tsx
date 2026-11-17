import { Fragment, useState } from 'react';
import { Checkbox, Form, Radio } from 'antd';
import {
  ArrowDown,
  ArrowUp,
  ChevronRight,
  ClipboardCheck,
  Database,
  ExternalLink,
  FilePlus2,
  FileQuestion,
  GraduationCap,
  Lock,
  Pencil,
  Plus,
  Trash2,
  Users,
} from 'lucide-react';
import {
  Button,
  EmptyState,
  FileUpload,
  ModalForm,
  Select,
  StatusTag,
  TextInput,
  useConfirm,
  useNotify,
  type UploadItem,
} from '@/components/common';
import { USE_API } from '@/lib/api/config';
import * as curriculumApi from './curriculumApi';
import { cn } from '@/lib/cn';
import { formatFileSize } from '@/lib/format';
import { EXAM_KINDS, MATERIAL_TYPE_OPTIONS, MATERIAL_TYPES, materialTypeOf } from './constants';
import { examScope, useCurriculumStore } from './curriculumStore';
import type { Chapter, Curriculum, Exam, ExamKind, Lesson, Material, MaterialType } from './types';

/** Chapter tạo sẵn khi thêm bài học (sửa sau được) */
const DEFAULT_CHAPTERS = ['Từ vựng', 'Ngữ pháp', 'Luyện nói'];

const uid = (p: string) => `${p}-${Date.now().toString(36)}-${Math.random().toString(36).slice(2, 6)}`;

/** Đích để thêm tài liệu: chapter của lesson (mốc thi KHÔNG có tài liệu — đề tạo từ Ngân hàng câu hỏi khi tổ chức thi) */
type MaterialTarget = { lessonId: string; chapterId: string; label: string };

type Dialog =
  | { kind: 'lesson'; lesson?: Lesson }
  | { kind: 'chapter'; lessonId: string; chapter?: Chapter }
  | { kind: 'material'; target: MaterialTarget }
  | { kind: 'exam'; exam?: Exam }
  | null;

interface LessonValues {
  title: string;
  hasQuiz?: boolean;
}
interface ChapterValues {
  title: string;
}
interface MaterialValues {
  type: MaterialType;
  files?: UploadItem[];
  name?: string;
  url?: string;
  sharedWithStudents: boolean;
}
interface ExamValues {
  kind: ExamKind;
  title: string;
  afterLessonId: string;
  note?: string;
}

/**
 * Nội dung lộ trình: Bài học → Chapter → Tài liệu, chèn Thi giữa kỳ / cuối kỳ sau bài tương ứng.
 * `editable` = bản nháp → thêm / sửa / xóa / đổi thứ tự. Bản đã phát hành chỉ xem.
 */
export function CurriculumContent({ curriculum, editable }: { curriculum: Curriculum; editable: boolean }) {
  const update = useCurriculumStore((s) => s.updateCurriculum);
  const { confirm, confirmDialog } = useConfirm();
  const notify = useNotify();
  const [expanded, setExpanded] = useState<Set<string>>(new Set());
  const [dialog, setDialog] = useState<Dialog>(null);

  const { lessons, exams } = curriculum;
  const allOpen = lessons.length > 0 && expanded.size >= lessons.length;
  const setLessons = (next: Lesson[]) => update(curriculum.id, { lessons: next });
  const setExams = (next: Exam[]) => update(curriculum.id, { exams: next });
  const mapLesson = (id: string, fn: (l: Lesson) => Lesson) => setLessons(lessons.map((l) => (l.id === id ? fn(l) : l)));

  const toggle = (id: string) =>
    setExpanded((s) => {
      const n = new Set(s);
      if (n.has(id)) n.delete(id);
      else n.add(id);
      return n;
    });

  // ---------- thao tác ----------
  /** Chế độ API: chạy thao tác, lỗi thì báo (thao tác không có hộp xác nhận) */
  const run = async (fn: () => Promise<unknown>, failTitle: string) => {
    try {
      await fn();
    } catch (e) {
      notify.error({ title: failTitle, description: (e as Error).message });
    }
  };

  const moveLesson = (idx: number, dir: -1 | 1) => {
    const next = [...lessons];
    [next[idx], next[idx + dir]] = [next[idx + dir], next[idx]];
    if (USE_API) void run(() => curriculumApi.reorderLessons(curriculum.id, next.map((l) => l.id)), 'Chưa đổi được thứ tự');
    else setLessons(next);
  };

  const removeLesson = async (l: Lesson, no: number) => {
    const attached = exams.filter((e) => e.afterLessonId === l.id);
    const ok = await confirm({
      title: `Xóa bài ${no}: ${l.title}?`,
      consequences: [
        `${l.chapters.length} chapter và ${l.chapters.reduce((n, c) => n + c.materials.length, 0)} tài liệu trong bài sẽ bị xóa`,
        ...(attached.length ? [`${attached.map((e) => e.title).join(', ')} sẽ chuyển sang sau bài liền trước`] : []),
      ],
      tone: 'danger',
      confirmAction: 'delete',
      confirmText: 'Xóa bài học',
      onConfirm: USE_API ? async () => void (await curriculumApi.removeLesson(curriculum.id, l.id)) : undefined,
    });
    if (!ok || USE_API) return;
    const idx = lessons.findIndex((x) => x.id === l.id);
    const fallback = lessons[idx - 1]?.id ?? lessons[idx + 1]?.id ?? '';
    update(curriculum.id, {
      lessons: lessons.filter((x) => x.id !== l.id),
      exams: exams.map((e) => (e.afterLessonId === l.id ? { ...e, afterLessonId: fallback } : e)),
    });
  };

  const removeChapter = async (lessonId: string, ch: Chapter) => {
    const ok = await confirm({
      title: `Xóa chapter "${ch.title}"?`,
      description: ch.materials.length ? `${ch.materials.length} tài liệu trong chapter cũng bị xóa.` : undefined,
      tone: 'danger',
      confirmAction: 'delete',
      confirmText: 'Xóa chapter',
      onConfirm: USE_API ? async () => void (await curriculumApi.removeChapter(curriculum.id, lessonId, ch.id)) : undefined,
    });
    if (ok && !USE_API) mapLesson(lessonId, (l) => ({ ...l, chapters: l.chapters.filter((c) => c.id !== ch.id) }));
  };

  const removeMaterial = (target: MaterialTarget, materialId: string) => {
    if (USE_API) return void run(() => curriculumApi.removeMaterial(curriculum.id, materialId), 'Chưa xóa được tài liệu');
    mapLesson(target.lessonId, (l) => ({
        ...l,
        chapters: l.chapters.map((c) => (c.id === target.chapterId ? { ...c, materials: c.materials.filter((m) => m.id !== materialId) } : c)),
      }));
  };

  const removeExam = async (e: Exam) => {
    const ok = await confirm({ title: `Xóa "${e.title}"?`, description: 'Chỉ xóa mốc thi khỏi lộ trình. Các kỳ thi đã tổ chức không bị ảnh hưởng.', tone: 'danger', confirmAction: 'delete', confirmText: 'Xóa mốc thi', onConfirm: USE_API ? async () => void (await curriculumApi.removeExam(curriculum.id, e.id)) : undefined });
    if (ok && !USE_API) setExams(exams.filter((x) => x.id !== e.id));
  };

  // ---------- giao diện ----------
  return (
    <div className="flex flex-col gap-3">
      <div className="flex flex-wrap items-center justify-between gap-2">
        <p className="text-[13px] text-ink-muted">
          {lessons.length} bài học · {exams.length} mốc thi
        </p>
        <div className="flex flex-wrap gap-2">
          {lessons.length > 0 && (
            <Button icon={allOpen ? ArrowUp : ArrowDown} size="sm" tone="neutral" variant="ghost" onClick={() => setExpanded(allOpen ? new Set() : new Set(lessons.map((l) => l.id)))}>
              {allOpen ? 'Thu gọn tất cả' : 'Mở tất cả'}
            </Button>
          )}
          {editable && (
            <>
              <Button icon={FileQuestion} size="sm" variant="outline" disabled={!lessons.length} onClick={() => setDialog({ kind: 'exam' })}>
                Thêm mốc thi
              </Button>
              <Button icon={Plus} size="sm" onClick={() => setDialog({ kind: 'lesson' })}>
                Thêm bài học
              </Button>
            </>
          )}
        </div>
      </div>

      {!lessons.length ? (
        <div className="rounded-[var(--radius-card)] border border-line">
          <EmptyState
            size="compact"
            icon={GraduationCap}
            title="Lộ trình chưa có bài học"
            description={editable ? 'Thêm bài học, sau đó chia chapter và gắn tài liệu giảng dạy cho từng chapter.' : 'Bản nháp này chưa được soạn nội dung.'}
          />
        </div>
      ) : (
        <ol className="rounded-[var(--radius-card)] border border-line bg-surface px-3 py-2">
          {lessons.map((lesson, i) => {
            const isOpen = expanded.has(lesson.id);
            const materials = lesson.chapters.reduce((n, c) => n + c.materials.length, 0);
            const empty = lesson.chapters.filter((c) => !c.materials.length).length;
            const examsAfter = exams.filter((e) => e.afterLessonId === lesson.id).sort((a) => (a.kind === 'midterm' ? -1 : 1));
            const last = i === lessons.length - 1 && !examsAfter.length;
            return (
              <Fragment key={lesson.id}>
                <li className="relative pl-10">
                  {!last && <span className="absolute top-9 bottom-0 left-[13px] w-px bg-line" aria-hidden />}
                  <span
                    className={cn(
                      'tabular absolute top-2.5 left-0 flex size-7 items-center justify-center rounded-full text-xs font-semibold',
                      isOpen ? 'bg-primary-600 text-white' : 'bg-primary-50 text-primary-700',
                    )}
                    aria-hidden
                  >
                    {i + 1}
                  </span>

                  <div className="flex items-center gap-1">
                    <button type="button" onClick={() => toggle(lesson.id)} aria-expanded={isOpen} className="flex min-w-0 flex-1 items-center gap-2 rounded-md py-2.5 pr-1 text-left hover:bg-page">
                      <span className="min-w-0 flex-1">
                        <span className="block truncate text-sm font-medium text-ink">
                          Bài {i + 1}: {lesson.title}
                        </span>
                        <span className="text-xs text-ink-muted">
                          {lesson.chapters.length} chapter · {materials} tài liệu
                          {empty > 0 && <span className="text-warning-700"> · {empty} chapter chưa có tài liệu</span>}
                        </span>
                      </span>
                      {lesson.hasQuiz && (
                        <StatusTag tone="warning" icon={ClipboardCheck} size="sm">
                          Quiz
                        </StatusTag>
                      )}
                      <ChevronRight size={16} className={cn('shrink-0 text-ink-muted transition-transform', isOpen && 'rotate-90')} aria-hidden />
                    </button>
                    {editable && (
                      <span className="flex shrink-0 gap-0.5">
                        <Button icon={ArrowUp} iconOnly size="sm" tone="neutral" variant="ghost" aria-label="Chuyển lên" disabled={i === 0} onClick={() => moveLesson(i, -1)} />
                        <Button icon={ArrowDown} iconOnly size="sm" tone="neutral" variant="ghost" aria-label="Chuyển xuống" disabled={i === lessons.length - 1} onClick={() => moveLesson(i, 1)} />
                        <Button icon={Pencil} iconOnly size="sm" tone="neutral" variant="ghost" aria-label="Sửa bài học" onClick={() => setDialog({ kind: 'lesson', lesson })} />
                        <Button icon={Trash2} iconOnly size="sm" tone="danger" variant="ghost" aria-label="Xóa bài học" onClick={() => removeLesson(lesson, i + 1)} />
                      </span>
                    )}
                  </div>

                  {isOpen && (
                    <ul className="mb-3 flex flex-col gap-2">
                      {lesson.chapters.map((ch, j) => {
                        const target: MaterialTarget = { lessonId: lesson.id, chapterId: ch.id, label: `Bài ${i + 1} · ${ch.title}` };
                        return (
                          <li key={ch.id} className="rounded-lg border border-line bg-page">
                            <div className="flex items-center gap-2 px-3 py-2">
                              <span className="tabular text-xs font-semibold text-ink-muted">
                                {i + 1}.{j + 1}
                              </span>
                              <span className="min-w-0 flex-1 truncate text-[13px] font-medium text-ink">{ch.title}</span>
                              {editable && (
                                <span className="flex shrink-0 gap-0.5">
                                  <Button icon={FilePlus2} size="sm" variant="ghost" onClick={() => setDialog({ kind: 'material', target })}>
                                    Tài liệu
                                  </Button>
                                  <Button icon={Pencil} iconOnly size="sm" tone="neutral" variant="ghost" aria-label="Sửa chapter" onClick={() => setDialog({ kind: 'chapter', lessonId: lesson.id, chapter: ch })} />
                                  <Button icon={Trash2} iconOnly size="sm" tone="danger" variant="ghost" aria-label="Xóa chapter" onClick={() => removeChapter(lesson.id, ch)} />
                                </span>
                              )}
                            </div>
                            <MaterialList
                              materials={ch.materials}
                              editable={editable}
                              onRemove={(id) => removeMaterial(target, id)}
                              onOpen={(m) => run(() => curriculumApi.openMaterial(curriculum.id, m.id), 'Chưa mở được tài liệu')}
                            />
                          </li>
                        );
                      })}
                      {editable && (
                        <li>
                          <Button icon={Plus} size="sm" tone="neutral" variant="outline" onClick={() => setDialog({ kind: 'chapter', lessonId: lesson.id })}>
                            Thêm chapter
                          </Button>
                        </li>
                      )}
                    </ul>
                  )}
                </li>

                {examsAfter.map((exam, k) => {
                  const meta = EXAM_KINDS[exam.kind];
                  const Icon = meta.icon;
                  const scope = examScope(curriculum, exam.id);
                  const lastExam = i === lessons.length - 1 && k === examsAfter.length - 1;
                  return (
                    <li key={exam.id} className="relative pb-2 pl-10">
                      {!lastExam && <span className="absolute top-9 bottom-0 left-[13px] w-px bg-line" aria-hidden />}
                      <span
                        className={cn(
                          'absolute top-2.5 left-0 flex size-7 items-center justify-center rounded-full',
                          exam.kind === 'final' ? 'bg-danger-50 text-danger-600' : 'bg-warning-50 text-warning-700',
                        )}
                        aria-hidden
                      >
                        <Icon size={14} />
                      </span>
                      <div className={cn('rounded-lg border', exam.kind === 'final' ? 'border-danger-100 bg-danger-50/40' : 'border-warning-100 bg-warning-50/40')}>
                        <div className="flex flex-wrap items-center gap-2 px-3 py-2">
                          <span className="min-w-0 flex-1">
                            <span className="flex flex-wrap items-center gap-2">
                              <span className="text-sm font-semibold text-ink">{exam.title}</span>
                              <StatusTag tone={meta.tone} icon={meta.icon} size="sm">
                                {meta.label}
                              </StatusTag>
                            </span>
                            <span className="mt-0.5 flex flex-wrap gap-x-4 text-xs text-ink-secondary">
                              {scope && (
                                <span>
                                  Phạm vi: {scope.from === scope.to ? `Bài ${scope.to}` : `Bài ${scope.from}–${scope.to}`}
                                  {exam.kind === 'final' && scope.from === 1 && ' (toàn khóa)'}
                                </span>
                              )}
                              {exam.note && <span className="text-ink-muted">· {exam.note}</span>}
                            </span>
                            <span className="mt-1 inline-flex items-center gap-1 text-xs text-ink-muted">
                              <Database size={12} aria-hidden /> Đề & câu hỏi tạo khi tổ chức kỳ thi, lấy từ Ngân hàng câu hỏi
                            </span>
                          </span>
                          {editable && (
                            <span className="flex shrink-0 gap-0.5">
                              <Button icon={Pencil} iconOnly size="sm" tone="neutral" variant="ghost" aria-label="Sửa mốc thi" onClick={() => setDialog({ kind: 'exam', exam })} />
                              <Button icon={Trash2} iconOnly size="sm" tone="danger" variant="ghost" aria-label="Xóa mốc thi" onClick={() => removeExam(exam)} />
                            </span>
                          )}
                        </div>
                      </div>
                    </li>
                  );
                })}
              </Fragment>
            );
          })}
        </ol>
      )}

      {/* ===== Bài học ===== */}
      <ModalForm<LessonValues>
        key={dialog?.kind === 'lesson' ? (dialog.lesson?.id ?? 'new-lesson') : 'lesson'}
        open={dialog?.kind === 'lesson'}
        onClose={() => setDialog(null)}
        title={dialog?.kind === 'lesson' && dialog.lesson ? 'Sửa bài học' : 'Thêm bài học'}
        description={dialog?.kind === 'lesson' && !dialog.lesson ? `Bài ${lessons.length + 1} — tự tạo sẵn các chapter Từ vựng · Ngữ pháp · Luyện nói, sửa sau được` : undefined}
        submitAction={dialog?.kind === 'lesson' && dialog.lesson ? 'save' : 'create'}
        successMessage={dialog?.kind === 'lesson' && dialog.lesson ? 'Đã cập nhật bài học' : 'Đã thêm bài học'}
        initialValues={dialog?.kind === 'lesson' && dialog.lesson ? { title: dialog.lesson.title, hasQuiz: dialog.lesson.hasQuiz } : { hasQuiz: false }}
        onSubmit={async (v) => {
          if (dialog?.kind !== 'lesson') return;
          if (USE_API) {
            if (dialog.lesson) await curriculumApi.updateLesson(curriculum.id, dialog.lesson.id, v.title.trim(), Boolean(v.hasQuiz));
            else {
              const id = await curriculumApi.addLesson(curriculum.id, v.title.trim(), Boolean(v.hasQuiz), DEFAULT_CHAPTERS);
              setExpanded((s) => new Set(s).add(id));
            }
            return;
          }
          if (dialog.lesson) mapLesson(dialog.lesson.id, (l) => ({ ...l, title: v.title.trim(), hasQuiz: v.hasQuiz }));
          else {
            const id = uid('L');
            setLessons([
              ...lessons,
              { id, title: v.title.trim(), hasQuiz: v.hasQuiz, chapters: DEFAULT_CHAPTERS.map((t) => ({ id: uid('C'), title: t, materials: [] })) },
            ]);
            setExpanded((s) => new Set(s).add(id));
          }
        }}
      >
        <Form.Item name="title" label="Tên bài học" rules={[{ required: true, whitespace: true, message: 'Nhập tên bài học' }]}>
          <TextInput placeholder="VD: Thể khả năng" maxLength={120} />
        </Form.Item>
        <Form.Item name="hasQuiz" valuePropName="checked">
          <Checkbox>Có bài kiểm tra nhỏ (quiz) cuối bài</Checkbox>
        </Form.Item>
      </ModalForm>

      {/* ===== Chapter ===== */}
      <ModalForm<ChapterValues>
        key={dialog?.kind === 'chapter' ? (dialog.chapter?.id ?? `new-ch-${dialog.lessonId}`) : 'chapter'}
        open={dialog?.kind === 'chapter'}
        onClose={() => setDialog(null)}
        title={dialog?.kind === 'chapter' && dialog.chapter ? 'Sửa chapter' : 'Thêm chapter'}
        submitAction={dialog?.kind === 'chapter' && dialog.chapter ? 'save' : 'create'}
        successMessage="Đã lưu chapter"
        initialValues={dialog?.kind === 'chapter' && dialog.chapter ? { title: dialog.chapter.title } : undefined}
        onSubmit={async (v) => {
          if (dialog?.kind !== 'chapter') return;
          const { lessonId, chapter } = dialog;
          if (USE_API) {
            if (chapter) await curriculumApi.renameChapter(curriculum.id, lessonId, chapter.id, v.title.trim());
            else await curriculumApi.addChapter(curriculum.id, lessonId, v.title.trim());
            return;
          }
          mapLesson(lessonId, (l) => ({
            ...l,
            chapters: chapter
              ? l.chapters.map((c) => (c.id === chapter.id ? { ...c, title: v.title.trim() } : c))
              : [...l.chapters, { id: uid('C'), title: v.title.trim(), materials: [] }],
          }));
        }}
      >
        <Form.Item name="title" label="Tên chapter" rules={[{ required: true, whitespace: true, message: 'Nhập tên chapter' }]}>
          <TextInput placeholder="VD: Nghe hiểu, Kanji, Luyện đề..." maxLength={80} />
        </Form.Item>
      </ModalForm>

      {/* ===== Tài liệu ===== */}
      <ModalForm<MaterialValues>
        key={dialog?.kind === 'material' ? `mat-${dialog.target.chapterId}` : 'material'}
        open={dialog?.kind === 'material'}
        onClose={() => setDialog(null)}
        title="Thêm tài liệu"
        description={dialog?.kind === 'material' ? dialog.target.label : undefined}
        submitAction="create"
        submitText="Thêm tài liệu"
        successMessage="Đã thêm tài liệu"
        initialValues={{ type: 'pdf', sharedWithStudents: true }}
        onSubmit={async (v) => {
          if (dialog?.kind !== 'material') return;
          const isFile = MATERIAL_TYPES[v.type].kind === 'file';
          if (USE_API) {
            const { lessonId, chapterId } = dialog.target;
            if (!isFile) {
              await curriculumApi.addLinkMaterial(curriculum.id, lessonId, chapterId, {
                name: v.name!.trim(),
                type: v.type,
                url: v.url!.trim(),
                sharedWithStudents: v.sharedWithStudents,
              });
              return;
            }
            // Mỗi tệp 1 request — server kiểm đuôi + nội dung thật; tệp lỗi báo riêng, tệp khác vẫn lưu
            const failed: string[] = [];
            for (const f of v.files ?? []) {
              if (!f.file) continue;
              try {
                await curriculumApi.uploadMaterial(curriculum.id, lessonId, chapterId, f.file, v.sharedWithStudents);
              } catch (e) {
                failed.push(`${f.name}: ${(e as Error).message}`);
              }
            }
            const files = (v.files ?? []).filter((f) => f.file);
            // Tất cả lỗi → giữ hộp thoại để chọn lại; một phần lỗi → đóng (tránh tải trùng tệp đã lưu) và báo tệp lỗi
            if (failed.length === files.length) throw new Error(failed.join(' · '));
            if (failed.length) notify.warning({ title: `${failed.length} tệp chưa tải lên được`, description: failed.join(' · ') });
            return;
          }
          await new Promise((r) => setTimeout(r, 500)); // giả lập tải lên
          const added: Material[] = isFile
            ? (v.files ?? []).map((f) => ({ id: uid('M'), name: f.name, type: materialTypeOf(f.name), url: `/files/demo/${encodeURIComponent(f.name)}`, size: f.size, sharedWithStudents: v.sharedWithStudents }))
            : [{ id: uid('M'), name: v.name!.trim(), type: v.type, url: v.url!.trim(), sharedWithStudents: v.sharedWithStudents }];
          const { target } = dialog;
          mapLesson(target.lessonId, (l) => ({
              ...l,
              chapters: l.chapters.map((c) => (c.id === target.chapterId ? { ...c, materials: [...c.materials, ...added] } : c)),
            }));
        }}
      >
        <Form.Item name="type" label="Loại tài liệu" rules={[{ required: true }]}>
          <Select options={MATERIAL_TYPE_OPTIONS} showSearch={false} allowClear={false} />
        </Form.Item>
        <Form.Item noStyle shouldUpdate={(a, b) => a.type !== b.type}>
          {({ getFieldValue }) => {
            const type = getFieldValue('type') as MaterialType;
            return MATERIAL_TYPES[type].kind === 'file' ? (
              <Form.Item
                name="files"
                label="Tệp"
                rules={[{ required: true, type: 'array', min: 1, message: 'Chọn ít nhất 1 tệp' }]}
                extra="Có thể chọn nhiều tệp cùng lúc — loại tài liệu tự nhận theo đuôi file"
              >
                <FileUpload kind={['document', 'spreadsheet', 'audio']} maxSizeMB={50} />
              </Form.Item>
            ) : (
              <>
                <Form.Item name="name" label="Tên hiển thị" rules={[{ required: true, whitespace: true, message: 'Nhập tên tài liệu' }]}>
                  <TextInput placeholder={type === 'drive' ? 'VD: Thư mục flashcard bài 5' : type === 'video' ? 'VD: Video giải thích ngữ pháp' : 'VD: Bài đọc thêm'} />
                </Form.Item>
                <Form.Item
                  name="url"
                  label="Đường dẫn"
                  rules={[
                    { required: true, message: 'Dán đường dẫn' },
                    { type: 'url', message: 'Đường dẫn không hợp lệ (bắt đầu bằng https://)' },
                    ...(type === 'drive' ? [{ pattern: /drive\.google\.com|docs\.google\.com/, message: 'Không phải link Google Drive / Docs' }] : []),
                  ]}
                  extra={type === 'drive' ? 'Nhớ bật quyền "Bất kỳ ai có đường liên kết đều xem được"' : undefined}
                >
                  <TextInput icon={ExternalLink} placeholder="https://" />
                </Form.Item>
              </>
            );
          }}
        </Form.Item>
        <Form.Item name="sharedWithStudents" label="Ai được xem">
          <Radio.Group className="flex flex-col gap-2">
            <Radio value>Giáo viên và học viên (hiện trên Cổng học viên)</Radio>
            <Radio value={false}>Chỉ giáo viên (giáo án, đáp án)</Radio>
          </Radio.Group>
        </Form.Item>
      </ModalForm>

      {/* ===== Mốc thi (đề + câu hỏi tạo sau ở chức năng Kỳ thi) ===== */}
      <ModalForm<ExamValues>
        key={dialog?.kind === 'exam' ? (dialog.exam?.id ?? 'new-exam') : 'exam'}
        open={dialog?.kind === 'exam'}
        onClose={() => setDialog(null)}
        title={dialog?.kind === 'exam' && dialog.exam ? 'Sửa mốc thi' : 'Thêm mốc thi'}
        description="Đánh dấu thời điểm thi trong lộ trình. Đề & câu hỏi chọn từ Ngân hàng câu hỏi khi tổ chức kỳ thi."
        submitAction={dialog?.kind === 'exam' && dialog.exam ? 'save' : 'create'}
        successMessage="Đã lưu mốc thi"
        initialValues={
          dialog?.kind === 'exam' && dialog.exam
            ? dialog.exam
            : { kind: exams.some((e) => e.kind === 'midterm') ? 'final' : 'midterm', title: exams.some((e) => e.kind === 'midterm') ? 'Thi cuối kỳ' : 'Kiểm tra giữa kỳ', afterLessonId: lessons[lessons.length - 1]?.id }
        }
        onSubmit={async (v) => {
          if (dialog?.kind !== 'exam') return;
          const data = { ...v, title: v.title.trim() };
          if (USE_API) {
            if (dialog.exam) await curriculumApi.updateExam(curriculum.id, dialog.exam.id, data);
            else await curriculumApi.addExam(curriculum.id, { ...data, note: data.note?.trim() || undefined });
            return;
          }
          if (dialog.exam) setExams(exams.map((e) => (e.id === dialog.exam!.id ? { ...e, ...data } : e)));
          else setExams([...exams, { ...data, note: data.note?.trim() || undefined, id: uid('EX') }]);
        }}
      >
        <Form.Item name="kind" label="Loại" rules={[{ required: true }]}>
          <Radio.Group optionType="button" buttonStyle="solid" options={(Object.keys(EXAM_KINDS) as ExamKind[]).map((k) => ({ value: k, label: EXAM_KINDS[k].label }))} />
        </Form.Item>
        <Form.Item name="title" label="Tên mốc thi" rules={[{ required: true, whitespace: true, message: 'Nhập tên' }]}>
          <TextInput maxLength={80} />
        </Form.Item>
        <Form.Item name="afterLessonId" label="Thi sau bài" rules={[{ required: true, message: 'Chọn bài' }]}>
          <Select showSearch={false} allowClear={false} options={lessons.map((l, i) => ({ value: l.id, label: `Bài ${i + 1}: ${l.title}` }))} />
        </Form.Item>
        <Form.Item name="note" label="Ghi chú cho người ra đề">
          <TextInput placeholder="VD: Trọng tâm nghe hiểu, thêm 1 phần viết luận" maxLength={150} />
        </Form.Item>
      </ModalForm>

      {confirmDialog}
    </div>
  );
}

/** Danh sách tài liệu của 1 chapter */
function MaterialList({
  materials,
  editable,
  onRemove,
  onOpen,
}: {
  materials: Material[];
  editable: boolean;
  onRemove: (id: string) => void;
  /** Tài liệu file ở kho riêng tư (chế độ API) — xin link tạm khi bấm */
  onOpen: (m: Material) => void;
}) {
  if (!materials.length) return <p className="border-t border-line px-3 py-2 text-xs text-warning-700">Chưa có tài liệu</p>;
  return (
    <ul className="divide-y divide-line border-t border-line">
      {materials.map((m) => {
        const t = MATERIAL_TYPES[m.type];
        const Icon = t.icon;
        return (
          <li key={m.id} className="flex items-center gap-2.5 px-3 py-1.5 text-[13px]">
            <Icon size={16} className={cn('shrink-0', t.color)} aria-hidden />
            {m.isFile ? (
              <button type="button" onClick={() => onOpen(m)} className="min-w-0 flex-1 truncate text-left text-ink hover:text-primary-700 hover:underline" title={m.name}>
                {m.name}
              </button>
            ) : (
              <a href={m.url} target="_blank" rel="noreferrer" className="min-w-0 flex-1 truncate text-ink hover:text-primary-700 hover:underline" title={m.name}>
                {m.name}
              </a>
            )}
            <span className="hidden shrink-0 text-xs text-ink-muted sm:inline">
              {t.label}
              {m.size ? ` · ${formatFileSize(m.size)}` : ''}
            </span>
            <span
              className={cn(
                'inline-flex shrink-0 items-center gap-1 rounded-full px-1.5 py-0.5 text-[11px] font-medium',
                m.sharedWithStudents ? 'bg-success-50 text-success-700' : 'bg-disabled text-ink-muted',
              )}
              title={m.sharedWithStudents ? 'Học viên xem được' : 'Chỉ giáo viên'}
            >
              {m.sharedWithStudents ? <Users size={11} aria-hidden /> : <Lock size={11} aria-hidden />}
              {m.sharedWithStudents ? 'GV + HV' : 'Chỉ GV'}
            </span>
            {editable && <Button icon={Trash2} iconOnly size="sm" tone="danger" variant="ghost" aria-label={`Xóa ${m.name}`} onClick={() => onRemove(m.id)} />}
          </li>
        );
      })}
    </ul>
  );
}

import { useCallback, useEffect, useState } from 'react';
import { Collapse, Form, Radio } from 'antd';
import { Layers, Plus, Trash2 } from 'lucide-react';
import { Button, EmptyState, ModalForm, Select, TextArea, TextInput, useConfirm, useNotify } from '@/components/common';
import * as learningApi from '@/features/academic/classes/learningApi';
import type { FlashDeck } from '@/features/portal/types';
import type { Curriculum } from './types';

type Deck = FlashDeck & { lessonId?: string };

interface DeckValues {
  title: string;
  kind: FlashDeck['kind'];
  lessonId: string;
  lines: string;
}

/** "mặt trước | cách đọc | nghĩa | ví dụ" mỗi dòng 1 thẻ (cách đọc / ví dụ bỏ trống được) */
const parseLines = (text: string) =>
  text
    .split('\n')
    .map((l) => l.trim())
    .filter(Boolean)
    .map((l) => {
      const [front = '', reading = '', meaning = '', example = ''] = l.split('|').map((x) => x.trim());
      return { front, reading: reading || undefined, meaning, example: example || undefined };
    });

/**
 * Flashcard của lộ trình (ND-09, quyền <c>academic.curriculum</c>): bộ thẻ từ vựng / Kanji theo bài học — HV các lớp học lộ trình này ôn trên Cổng HV.
 * Bộ thẻ là dữ liệu riêng (không khóa theo phiên bản lộ trình). Xóa thẻ → xóa luôn tiến độ ôn của thẻ đó.
 */
export function FlashDeckManager({ curriculum, editable }: { curriculum: Curriculum; editable: boolean }) {
  const notify = useNotify();
  const { confirm, confirmDialog } = useConfirm();
  const [decks, setDecks] = useState<Deck[]>([]);
  const [creating, setCreating] = useState(false);
  const [adding, setAdding] = useState<Deck | null>(null);

  const load = useCallback(() => void learningApi.loadDecks(curriculum.id).then(setDecks).catch((e: Error) => notify.error({ title: e.message })), [curriculum.id, notify]);
  useEffect(load, [load]);

  const lessonLabel = (id?: string) => {
    const i = curriculum.lessons.findIndex((l) => l.id === id);
    return i >= 0 ? `Bài ${i + 1}: ${curriculum.lessons[i].title}` : 'Chưa gắn bài';
  };
  const replace = (d: FlashDeck) => setDecks((list) => list.map((x) => (x.id === d.id ? { ...x, ...d } : x)));

  return (
    <div className="flex flex-col gap-3">
      <div className="flex flex-wrap items-center gap-2">
        <p className="min-w-0 flex-1 text-[13px] text-ink-secondary">Bộ thẻ gắn bài học — học viên lớp học lộ trình này ôn trên Cổng HV (hộp Leitner).</p>
        {editable && (
          <Button action="create" size="sm" onClick={() => setCreating(true)}>
            Tạo bộ thẻ
          </Button>
        )}
      </div>
      {!decks.length ? (
        <EmptyState size="compact" icon={Layers} title="Chưa có bộ flashcard" />
      ) : (
        <Collapse
          items={[...decks]
            .sort((a, b) => curriculum.lessons.findIndex((l) => l.id === a.lessonId) - curriculum.lessons.findIndex((l) => l.id === b.lessonId))
            .map((d) => ({
              key: d.id,
              label: (
                <span className="flex flex-wrap items-center gap-2">
                  <span className="font-medium text-ink">{d.title}</span>
                  <span className="text-xs text-ink-muted">
                    {lessonLabel(d.lessonId)} · {d.kind === 'kanji' ? 'Kanji' : 'Từ vựng'} · {d.cards.length} thẻ
                  </span>
                </span>
              ),
              extra: editable ? (
                <Button
                  icon={Trash2}
                  iconOnly
                  size="sm"
                  variant="ghost"
                  tone="danger"
                  aria-label="Xóa bộ thẻ"
                  onClick={async (e) => {
                    e.stopPropagation();
                    if (!(await confirm({ title: `Xóa bộ "${d.title}"?`, description: 'Bộ thẻ vào Thùng rác, học viên không ôn được nữa.', tone: 'danger', confirmAction: 'delete' }))) return;
                    await learningApi.deleteDeck(d.id).then(load).catch((err: Error) => notify.error({ title: err.message }));
                  }}
                />
              ) : undefined,
              children: (
                <div className="flex flex-col gap-2">
                  <ul className="flex flex-col divide-y divide-line text-[13px]">
                    {d.cards.map((c) => (
                      <li key={c.id} className="flex items-center gap-3 py-1.5">
                        <span className="w-24 shrink-0 text-base font-medium text-ink">{c.front}</span>
                        <span className="w-28 shrink-0 text-ink-secondary">{c.reading}</span>
                        <span className="min-w-0 flex-1 text-ink">{c.meaning}</span>
                        {editable && (
                          <Button
                            icon={Trash2}
                            iconOnly
                            size="sm"
                            variant="ghost"
                            tone="danger"
                            aria-label="Xóa thẻ"
                            onClick={async () => {
                              if (!(await confirm({ title: `Xóa thẻ "${c.front}"?`, description: 'Tiến độ ôn của học viên với thẻ này cũng bị xóa.', tone: 'danger', confirmAction: 'delete' })))
                                return;
                              await learningApi
                                .removeCard(d.id, c.id)
                                .then(replace)
                                .catch((err: Error) => notify.error({ title: err.message }));
                            }}
                          />
                        )}
                      </li>
                    ))}
                  </ul>
                  {editable && (
                    <Button icon={Plus} size="sm" variant="soft" className="self-start" onClick={() => setAdding(d)}>
                      Thêm thẻ
                    </Button>
                  )}
                </div>
              ),
            }))}
        />
      )}

      <ModalForm<DeckValues>
        key={`deck-${creating}`}
        open={creating}
        onClose={() => setCreating(false)}
        width={680}
        title="Tạo bộ flashcard"
        submitAction="save"
        successMessage="Đã tạo bộ thẻ"
        initialValues={{ kind: 'vocab' }}
        onSubmit={async (v) => {
          const cards = parseLines(v.lines ?? '');
          if (cards.some((c) => !c.front || !c.meaning)) throw new Error('Mỗi dòng cần mặt trước và nghĩa: mặt trước | cách đọc | nghĩa | ví dụ');
          await learningApi.saveDeck({ title: v.title, level: curriculum.level === 'beginner' ? 'N5' : curriculum.level, kind: v.kind, lessonId: v.lessonId, cards });
          load();
        }}
      >
        <Form.Item name="title" label="Tên bộ thẻ" rules={[{ required: true, whitespace: true, message: 'Nhập tên' }]}>
          <TextInput placeholder="VD: Từ vựng bài 5" maxLength={200} />
        </Form.Item>
        <Form.Item name="lessonId" label="Bài học" rules={[{ required: true, message: 'Chọn bài' }]}>
          <Select options={curriculum.lessons.map((l, i) => ({ value: l.id, label: `Bài ${i + 1}: ${l.title}` }))} />
        </Form.Item>
        <Form.Item name="kind" label="Loại">
          <Radio.Group optionType="button" options={[{ value: 'vocab', label: 'Từ vựng' }, { value: 'kanji', label: 'Kanji' }]} />
        </Form.Item>
        <CardLinesField />
      </ModalForm>

      <ModalForm<{ lines: string }>
        key={`add-${adding?.id}`}
        open={Boolean(adding)}
        onClose={() => setAdding(null)}
        width={680}
        title={`Thêm thẻ — ${adding?.title ?? ''}`}
        submitAction="save"
        successMessage="Đã thêm thẻ"
        onSubmit={async (v) => {
          const cards = parseLines(v.lines ?? '');
          if (!cards.length || cards.some((c) => !c.front || !c.meaning)) throw new Error('Mỗi dòng cần mặt trước và nghĩa: mặt trước | cách đọc | nghĩa | ví dụ');
          replace(await learningApi.addCards(adding!.id, cards));
        }}
      >
        <CardLinesField required />
      </ModalForm>
      {confirmDialog}
    </div>
  );
}

function CardLinesField({ required }: { required?: boolean }) {
  return (
    <Form.Item
      name="lines"
      label="Thẻ (mỗi dòng 1 thẻ)"
      extra="mặt trước | cách đọc | nghĩa | ví dụ — vd: 水 | みず | nước | 水を飲みます"
      rules={required ? [{ required: true, whitespace: true, message: 'Nhập ít nhất 1 thẻ' }] : undefined}
    >
      <TextArea rows={8} placeholder={'水 | みず | nước\n火 | ひ | lửa'} />
    </Form.Item>
  );
}

import { useEffect, useState, type ReactNode } from 'react';
import { Checkbox, Drawer, Switch, Tabs, Tooltip } from 'antd';
import { BarChart3, Eye, EyeOff, Info, ShieldCheck } from 'lucide-react';
import { Button, Select, StatusTag, UserCell, useNotify } from '@/components/common';
import { Q_SKILLS } from '@/features/academic/questions/constants';
import { useAuth } from '@/hooks/useAuth';
import { useBreakpoint } from '@/hooks/useBreakpoint';
import { fmtDate, fromNow } from '@/lib/date';
import { formatFileSize } from '@/lib/format';
import { getBranches } from '@/features/system/useBranches';
import { USE_API } from '@/lib/api/config';
import { DOC_AUDIENCE_OPTIONS, DOC_CATEGORIES, DOC_FILE_TYPES } from './constants';
import { DocViewer } from './DocViewer';
import { endStaffView, loadDocStats, setDocAccess, setDocStatus, startStaffView } from './libraryApi';
import { useLibraryStore } from './libraryStore';
import type { DocAudience, LibraryDoc } from './types';

export type DocTab = 'view' | 'access' | 'stats';

interface Props {
  docId: string | null;
  initialTab?: DocTab;
  editable: boolean;
  onClose: () => void;
  onEdit: (id: string) => void;
}

const label = (Icon: typeof Info, text: string) => (
  <span className="inline-flex items-center gap-1.5">
    <Icon size={16} aria-hidden /> {text}
  </span>
);

/** Chi tiết tài liệu: Xem trực tuyến (watermark) · Quyền truy cập & bảo vệ · Thống kê lượt đọc */
export function DocDetailDrawer({ docId, initialTab = 'view', editable, onClose, onEdit }: Props) {
  const { isMobile } = useBreakpoint();
  const { user } = useAuth();
  const notify = useNotify();
  const doc = useLibraryStore((s) => s.docs.find((d) => d.id === docId));
  const update = useLibraryStore((s) => s.update);
  const view = useLibraryStore((s) => s.view);
  const [tab, setTab] = useState<DocTab>(initialTab);

  useEffect(() => {
    if (docId) setTab(initialTab);
  }, [docId, initialTab]);

  // Chế độ API: "đọc nhiều nhất" chỉ có ở API thống kê riêng — tải khi mở tab
  useEffect(() => {
    if (USE_API && docId && tab === 'stats') void loadDocStats(docId).catch((e: Error) => notify.error({ title: e.message }));
  }, [docId, tab, notify]);

  if (!doc) return <Drawer open={false} onClose={onClose} />;

  /** Đổi quyền / bảo vệ: chế độ API gửi đủ bộ lên server (lỗi → báo, store giữ bản cũ) */
  const change = (patch: Partial<Pick<LibraryDoc, 'audience' | 'branchIds' | 'watermark' | 'allowDownload' | 'status'>>, done?: () => void) => {
    if (!USE_API) {
      update(doc.id, patch);
      done?.();
      return;
    }
    const { status, ...access } = patch;
    void (status ? setDocStatus(doc.id, status) : setDocAccess(doc, access)).then(done, (e: Error) => notify.error({ title: e.message }));
  };
  const cat = DOC_CATEGORIES[doc.category];
  const FT = DOC_FILE_TYPES[doc.fileType];
  const max = Math.max(1, ...doc.stats.daily);

  const info: [string, ReactNode][] = [
    ['Loại', `${cat.label} · ${FT.label}`],
    ['Cấp độ', doc.levels.join(', ') || '—'],
    ['Kỹ năng', doc.skills.map((k) => Q_SKILLS[k].label).join(', ') || '—'],
    ['Dung lượng', [doc.pages && `${doc.pages} trang`, doc.duration && `${doc.duration} phút`, doc.size && formatFileSize(doc.size)].filter(Boolean).join(' · ') || '—'],
    ['Nguồn', [doc.publisher, doc.year].filter(Boolean).join(', ') || '—'],
    ['Thẻ', doc.tags.join(', ') || '—'],
    ['Tải lên', `${doc.uploadedBy} · ${fmtDate(doc.uploadedAt)}`],
  ];

  return (
    <Drawer
      open={Boolean(docId)}
      onClose={onClose}
      width={isMobile ? '100%' : 860}
      destroyOnHidden
      styles={{ body: { paddingTop: 0 } }}
      title={
        <div className="flex min-w-0 flex-col gap-1 pr-2">
          <div className="flex flex-wrap items-center gap-2">
            <span className="truncate text-base font-semibold">{doc.title}</span>
            {doc.status === 'hidden' && (
              <StatusTag tone="neutral" icon={EyeOff} size="sm">
                Đang ẩn
              </StatusTag>
            )}
          </div>
          <span className="text-xs font-normal text-ink-muted">
            {cat.label} · {doc.levels.join(', ')} · {doc.stats.views.toLocaleString('vi-VN')} lượt xem
          </span>
        </div>
      }
      extra={editable && <Button action="edit" size="sm" collapseOnMobile onClick={() => onEdit(doc.id)} />}
    >
      <Tabs
        activeKey={tab}
        onChange={(k) => setTab(k as DocTab)}
        items={[
          {
            key: 'view',
            label: label(Eye, 'Xem trực tuyến'),
            children: (
              <div className="flex flex-col gap-4">
                <DocViewer
                  doc={doc}
                  onViewed={() => view(doc.id, user?.fullName ?? 'Nhân viên', user?.roleName ?? '')}
                  source={{ start: () => startStaffView(doc.id), end: (id) => void endStaffView(id) }}
                />
                {doc.description && <p className="text-[13px] text-ink-secondary">{doc.description}</p>}
                <dl className="grid grid-cols-1 gap-x-4 gap-y-2 text-[13px] sm:grid-cols-[110px_1fr]">
                  {info.map(([k, v]) => (
                    <div key={k} className="contents">
                      <dt className="text-ink-muted">{k}</dt>
                      <dd className="text-ink">{v}</dd>
                    </div>
                  ))}
                </dl>
              </div>
            ),
          },
          {
            key: 'access',
            label: label(ShieldCheck, 'Quyền & bảo vệ'),
            children: (
              <div className="flex flex-col gap-5 text-[13px]">
                <Setting title="Hiển thị trong thư viện" hint="Tắt = ẩn với mọi người (vd: bản thảo chưa kiểm tra).">
                  <Switch checked={doc.status === 'published'} disabled={!editable} onChange={(v) => change({ status: v ? 'published' : 'hidden' })} />
                </Setting>
                <Setting title="Watermark tên người xem" hint="In mờ tên + email + thời gian lên từng trang để truy nguồn khi bị chụp màn hình.">
                  <Switch checked={doc.watermark} disabled={!editable} onChange={(v) => change({ watermark: v })} />
                </Setting>
                <Setting title="Cho phép tải về / in" hint="Chỉ bật với tài liệu công khai (đề mẫu chính thức, tài liệu tự biên soạn).">
                  <Switch
                    checked={doc.allowDownload}
                    disabled={!editable}
                    onChange={(v) => change({ allowDownload: v }, () => v && notify.toast.warning('Người xem tải được bản gốc — không còn watermark trên file tải về'))}
                  />
                </Setting>
                <Setting title="Ai được xem" hint="Học viên chỉ thấy tài liệu đúng cấp độ lớp đang học (Cổng học viên).">
                  <Checkbox.Group
                    disabled={!editable}
                    value={doc.audience}
                    options={DOC_AUDIENCE_OPTIONS}
                    onChange={(v) => v.length && change({ audience: v as DocAudience[] })}
                  />
                </Setting>
                <Setting title="Chi nhánh" hint="Tài liệu riêng của chi nhánh (vd: đơn hàng XKLĐ Hải Châu).">
                  <Select
                    mode="multiple"
                    disabled={!editable}
                    className="!w-72"
                    placeholder="Tất cả chi nhánh"
                    value={doc.branchIds === 'all' ? [] : doc.branchIds}
                    onChange={(v) => change({ branchIds: (v as string[]).length ? (v as string[]) : 'all' })}
                    options={getBranches(false).map((b) => ({ value: b.id, label: b.name }))}
                  />
                </Setting>
              </div>
            ),
          },
          {
            key: 'stats',
            label: label(BarChart3, 'Thống kê'),
            children: (
              <div className="flex flex-col gap-5">
                <div className="grid grid-cols-3 gap-3">
                  <Num label="Lượt xem" value={doc.stats.views.toLocaleString('vi-VN')} />
                  <Num label="Người đọc" value={doc.stats.readers.toLocaleString('vi-VN')} />
                  <Num label="Thời gian TB" value={`${doc.stats.avgMinutes} phút`} />
                </div>
                <section>
                  <h3 className="mb-2 text-sm font-semibold text-ink">Lượt xem 14 ngày qua</h3>
                  <div className="flex h-32 items-end gap-1.5 rounded-[var(--radius-card)] border border-line p-3">
                    {doc.stats.daily.map((v, i) => (
                      <Tooltip key={i} title={`${fmtDate(new Date(Date.now() - (13 - i) * 86_400_000).toISOString())}: ${v} lượt`}>
                        <div className="flex flex-1 flex-col items-center justify-end self-stretch">
                          <div className="w-full rounded-t bg-primary-500 hover:bg-primary-600" style={{ height: `${(v / max) * 100}%`, minHeight: v ? 3 : 0 }} />
                        </div>
                      </Tooltip>
                    ))}
                  </div>
                  <p className="mt-1 text-xs text-ink-muted">{doc.stats.lastViewedAt ? `Lần xem gần nhất ${fromNow(doc.stats.lastViewedAt)}` : 'Chưa có ai xem'}</p>
                </section>
                <section>
                  <h3 className="mb-2 text-sm font-semibold text-ink">Đọc nhiều nhất</h3>
                  {doc.stats.top.length ? (
                    <ul className="divide-y divide-line rounded-[var(--radius-card)] border border-line">
                      {doc.stats.top.map((t) => (
                        <li key={t.name} className="flex items-center justify-between gap-2 px-3 py-2">
                          <UserCell size="sm" name={t.name} subtitle={t.role} />
                          <span className="tabular text-[13px] text-ink-secondary">{t.views} lượt</span>
                        </li>
                      ))}
                    </ul>
                  ) : (
                    <p className="text-[13px] text-ink-muted">Chưa có dữ liệu.</p>
                  )}
                </section>
              </div>
            ),
          },
        ]}
      />
    </Drawer>
  );
}

function Setting({ title, hint, children }: { title: string; hint: string; children: ReactNode }) {
  return (
    <div className="flex flex-col gap-2 border-b border-line pb-4 sm:flex-row sm:items-center sm:justify-between">
      <div>
        <p className="font-medium text-ink">{title}</p>
        <p className="text-xs text-ink-muted">{hint}</p>
      </div>
      <div>{children}</div>
    </div>
  );
}

function Num({ label: l, value }: { label: string; value: ReactNode }) {
  return (
    <div className="rounded-[var(--radius-card)] border border-line p-3">
      <p className="text-xs text-ink-muted">{l}</p>
      <p className="tabular mt-1 text-lg font-semibold text-ink">{value}</p>
    </div>
  );
}


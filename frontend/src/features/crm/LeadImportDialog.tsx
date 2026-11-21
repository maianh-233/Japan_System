import { useCallback, useState } from 'react';
import { FormField, ImportDialog, Select, useNotify, type ImportColumn, type ImportRow, type RowCheck } from '@/components/common';
import { useAuth } from '@/hooks/useAuth';
import { isValidEmail, isValidPhone, matchOption, normalizePhone } from '@/lib/spreadsheet';
import { ALL_BRANCHES } from '@/types/auth';
import { COURSE_OPTIONS, LEAD_SOURCE_OPTIONS, useSales } from './constants';
import { USE_API } from '@/lib/api/config';
import { errorText, importLeads } from './crmApi';
import { useCrmStore, type LeadInput } from './crmStore';
import type { LeadSource } from './types';

const COLUMNS: ImportColumn[] = [
  { key: 'fullName', label: 'Họ tên', required: true, aliases: ['Họ và tên', 'Tên', 'Name', 'Full name'], example: 'Nguyễn Văn An', width: 24 },
  { key: 'phone', label: 'Số điện thoại', required: true, aliases: ['SĐT', 'SDT', 'Điện thoại', 'Phone', 'Mobile'], example: '0912345678', hint: '10 số; +84 tự đổi thành 0', width: 16 },
  { key: 'email', label: 'Email', aliases: ['Mail'], example: 'an.nguyen@gmail.com', width: 26 },
  { key: 'source', label: 'Nguồn', aliases: ['Nguồn lead', 'Source', 'Kênh'], example: 'Facebook', hint: LEAD_SOURCE_OPTIONS.map((o) => o.label).join(', '), width: 16 },
  { key: 'interest', label: 'Khóa quan tâm', aliases: ['Khóa học', 'Quan tâm', 'Course'], example: 'JLPT N4', hint: 'VD: JLPT N5 cơ bản, Kaiwa giao tiếp…', width: 20 },
  { key: 'branch', label: 'Chi nhánh', aliases: ['Cơ sở', 'Branch'], example: '', hint: 'tên hoặc mã; trống = chi nhánh mặc định', width: 16 },
  { key: 'sale', label: 'Sale phụ trách', aliases: ['Sale', 'Người phụ trách', 'Nhân viên'], example: '', hint: 'tên / mã / email; trống = tự chia theo quy tắc', width: 20 },
  { key: 'note', label: 'Ghi chú', aliases: ['Note', 'Nhu cầu'], example: 'Muốn học buổi tối', width: 30 },
];

/** Nhập lead hàng loạt từ Excel: kiểm tra SĐT / email / nguồn / chi nhánh / Sale; bỏ qua lead trùng SĐT (đã có hoặc lặp trong file) */
export function LeadImportDialog({ open, onClose, onImported }: { open: boolean; onClose: () => void; onImported?: () => void }) {
  const { user, branches, branchId } = useAuth();
  const notify = useNotify();
  const leads = useCrmStore((s) => s.leads);
  const addLead = useCrmStore((s) => s.addLead);
  const allSales = useSales();
  const [defBranch, setDefBranch] = useState<string>(branchId !== ALL_BRANCHES ? branchId : (branches[0]?.id ?? ''));
  const [defSource, setDefSource] = useState<LeadSource>('event');

  const check = useCallback(
    (rows: ImportRow[]): RowCheck<LeadInput>[] => {
      const existing = new Map(leads.map((l) => [normalizePhone(l.phone), l]));
      const seen = new Map<string, number>();
      const branchOpts = branches.map((b) => ({ value: b.id, label: b.name, aliases: [b.code] }));
      return rows.map((r, i) => {
        const errors: string[] = [];
        const warnings: string[] = [];
        if (!r.fullName) errors.push('Thiếu họ tên');
        const phone = normalizePhone(r.phone);
        if (!r.phone) errors.push('Thiếu SĐT');
        else if (!isValidPhone(phone)) errors.push(`SĐT "${r.phone}" không hợp lệ`);
        if (r.email && !isValidEmail(r.email)) errors.push(`Email "${r.email}" sai định dạng`);

        const source = r.source ? matchOption(r.source, LEAD_SOURCE_OPTIONS) : defSource;
        if (r.source && !source) errors.push(`Nguồn "${r.source}" không có trong danh sách`);
        const branch = r.branch ? matchOption(r.branch, branchOpts) : defBranch;
        if (r.branch && !branch) errors.push(`Chi nhánh "${r.branch}" không có hoặc bạn không được quyền`);
        if (!r.branch && !branch) errors.push('Chưa chọn chi nhánh');

        let assigneeId: string | undefined;
        if (r.sale) {
          assigneeId = matchOption(r.sale, allSales.map((s) => ({ value: s.id, label: s.fullName, aliases: [s.code, s.email] })));
          if (!assigneeId) errors.push(`Không tìm thấy Sale "${r.sale}"`);
          else if (branch && allSales.find((s) => s.id === assigneeId)?.branchId !== branch) warnings.push('Sale khác chi nhánh của lead');
        }
        const interest = r.interest ? matchOption(r.interest, COURSE_OPTIONS) : undefined;
        if (r.interest && !interest) warnings.push(`Khóa "${r.interest}" lạ — ghi vào ghi chú`);

        let duplicate: string | undefined;
        if (isValidPhone(phone)) {
          const old = existing.get(phone);
          const firstLine = seen.get(phone);
          if (old) duplicate = `Trùng SĐT với lead ${old.code} — ${old.fullName}${old.hidden ? ' (đã ẩn)' : ''}`;
          else if (firstLine !== undefined) duplicate = `Trùng SĐT với dòng #${firstLine} trong file`;
          else seen.set(phone, i + 2);
        }
        const note = [r.note, interest ? undefined : r.interest && `Quan tâm: ${r.interest}`].filter(Boolean).join(' · ') || undefined;
        return {
          errors,
          warnings,
          duplicate,
          value: errors.length ? undefined : { fullName: r.fullName, phone, email: r.email || undefined, source: source!, status: 'new', branchId: branch!, assigneeId, interest, note },
        };
      });
    },
    [leads, branches, allSales, defBranch, defSource],
  );

  return (
    <ImportDialog<LeadInput>
      open={open}
      onClose={onClose}
      title="Nhập lead từ Excel"
      unitLabel="lead"
      templateName="mau-nhap-lead"
      columns={COLUMNS}
      check={check}
      previewKeys={['fullName', 'phone', 'source', 'sale']}
      settings={
        <div className="grid grid-cols-1 gap-x-3 sm:grid-cols-2">
          <FormField label="Chi nhánh mặc định" hint="Dùng khi cột Chi nhánh để trống">
            <Select value={defBranch} onChange={(v) => setDefBranch(v as string)} options={branches.map((b) => ({ value: b.id, label: b.name }))} allowClear={false} showSearch={false} />
          </FormField>
          <FormField label="Nguồn mặc định" hint="Dùng khi cột Nguồn để trống">
            <Select value={defSource} onChange={(v) => setDefSource(v as LeadSource)} options={LEAD_SOURCE_OPTIONS} allowClear={false} showSearch={false} />
          </FormField>
        </div>
      }
      notes="Lead trùng SĐT (đã có trong hệ thống hoặc lặp trong file) sẽ bị bỏ qua — không ghi đè. Sale để trống → hệ thống tự chia theo Chia lead & SLA."
      onImport={async (values) => {
        if (USE_API) {
          // 1 request; server kiểm lại (trùng SĐT với lead ngoài phạm vi mình thấy…). Số thứ tự = vị trí trong các dòng hợp lệ
          try {
            const r = await importLeads(values.map((value, i) => ({ row: i + 1, value })));
            const skipped = r.errors.length ? `${r.errors.length} dòng bị từ chối: ${r.errors.slice(0, 3).map((e) => `dòng ${e.row} — ${e.message}`).join('; ')}. ` : '';
            notify.success({ title: `Đã nhập ${r.created} lead`, description: `${skipped}Lead mới ở trạng thái "Mới", Sale được giao sẽ nhận thông báo.` });
            onImported?.();
            return r.created;
          } catch (e) {
            notify.toast.error(errorText(e, 'Không nhập được lead'));
            return 0;
          }
        }
        await new Promise((r) => setTimeout(r, 500)); // giả lập gọi API
        values.forEach((v) => addLead(v, user?.fullName ?? 'Nhập từ Excel'));
        notify.success({ title: `Đã nhập ${values.length} lead`, description: 'Lead mới ở trạng thái "Mới", Sale được giao sẽ nhận thông báo.' });
        onImported?.();
      }}
    />
  );
}

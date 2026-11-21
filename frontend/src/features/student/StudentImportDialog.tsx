import { useCallback, useState } from 'react';
import { FormField, ImportDialog, Select, useNotify, type ImportColumn, type ImportRow, type RowCheck } from '@/components/common';
import { useCrmStore } from '@/features/crm/crmStore';
import { createGuardian } from './guardians/guardianApi';
import * as studentApi from '@/features/student/studentApi';
import { USE_API } from '@/lib/api/config';
import { normalizeText } from '@/lib/text';
import { useAuth } from '@/hooks/useAuth';
import { isValidEmail, isValidPhone, matchOption, normalizePhone, parseDateCell } from '@/lib/spreadsheet';
import { ALL_BRANCHES } from '@/types/auth';
import { GENDER_OPTIONS } from './constants';
import type { Gender } from './types';

interface StudentRow {
  fullName: string;
  dob?: string;
  gender?: Gender;
  phone?: string;
  email?: string;
  branchId: string;
  parentName?: string;
  parentPhone?: string;
  note?: string;
}

const COLUMNS: ImportColumn[] = [
  { key: 'fullName', label: 'Họ tên học viên', required: true, aliases: ['Họ tên', 'Họ và tên', 'Tên học viên', 'Học viên', 'Name'], example: 'Trần Minh Khang', width: 24 },
  { key: 'dob', label: 'Ngày sinh', aliases: ['NS', 'DOB', 'Birthday'], example: '15/03/2010', hint: 'dd/mm/yyyy', width: 14 },
  { key: 'gender', label: 'Giới tính', aliases: ['Gender', 'GT'], example: 'Nam', hint: 'Nam / Nữ / Khác', width: 11 },
  { key: 'phone', label: 'SĐT học viên', aliases: ['Số điện thoại', 'SĐT', 'SDT', 'Điện thoại', 'Phone'], example: '', width: 16 },
  { key: 'email', label: 'Email', aliases: ['Mail'], example: '', width: 24 },
  { key: 'branch', label: 'Chi nhánh', aliases: ['Cơ sở', 'Branch'], example: '', hint: 'trống = chi nhánh mặc định', width: 16 },
  { key: 'parentName', label: 'Họ tên phụ huynh', aliases: ['Phụ huynh', 'Người giám hộ', 'Tên phụ huynh'], example: 'Trần Văn Hùng', width: 22 },
  { key: 'parentPhone', label: 'SĐT phụ huynh', aliases: ['Số điện thoại phụ huynh', 'SĐT PH', 'SDT PH'], example: '0987654321', hint: 'cần SĐT học viên hoặc phụ huynh', width: 16 },
  { key: 'note', label: 'Ghi chú', aliases: ['Note'], example: '', width: 28 },
];

/**
 * Nhập hồ sơ học viên từ Excel (chuyển dữ liệu từ sổ sách cũ).
 * Mỗi học viên cần 1 người liên hệ: có SĐT phụ huynh → gắn phụ huynh (dùng lại lead cũ nếu trùng SĐT), không có → chính học viên.
 * Trùng = đã có học viên cùng SĐT, hoặc cùng họ tên + ngày sinh; hoặc lặp trong file.
 */
export function StudentImportDialog({ open, onClose }: { open: boolean; onClose: () => void }) {
  const { user, branches, branchId } = useAuth();
  const notify = useNotify();
  const students = useCrmStore((s) => s.students);
  const [defBranch, setDefBranch] = useState<string>(branchId !== ALL_BRANCHES ? branchId : (branches[0]?.id ?? ''));

  const check = useCallback(
    (rows: ImportRow[]): RowCheck<StudentRow>[] => {
      const byPhone = new Map(students.filter((s) => s.phone).map((s) => [normalizePhone(s.phone!), s]));
      const byNameDob = new Map(students.filter((s) => s.dob).map((s) => [`${normalizeText(s.fullName)}|${s.dob}`, s]));
      const seen = new Map<string, number>();
      const branchOpts = branches.map((b) => ({ value: b.id, label: b.name, aliases: [b.code] }));
      return rows.map((r, i) => {
        const errors: string[] = [];
        if (!r.fullName) errors.push('Thiếu họ tên');
        const dob = parseDateCell(r.dob);
        if (dob === null) errors.push(`Ngày sinh "${r.dob}" sai (dùng dd/mm/yyyy)`);
        else if (dob && dob > new Date().toISOString().slice(0, 10)) errors.push('Ngày sinh ở tương lai');
        const gender = r.gender ? matchOption(r.gender, [...GENDER_OPTIONS.map((o) => ({ ...o, aliases: o.value === 'male' ? ['M', 'Trai'] : o.value === 'female' ? ['F', 'Gái'] : [] }))]) : undefined;
        if (r.gender && !gender) errors.push(`Giới tính "${r.gender}" không hợp lệ`);
        const phone = r.phone ? normalizePhone(r.phone) : undefined;
        if (phone !== undefined && !isValidPhone(phone)) errors.push(`SĐT học viên "${r.phone}" không hợp lệ`);
        const parentPhone = r.parentPhone ? normalizePhone(r.parentPhone) : undefined;
        if (parentPhone !== undefined && !isValidPhone(parentPhone)) errors.push(`SĐT phụ huynh "${r.parentPhone}" không hợp lệ`);
        if (!phone && !parentPhone) errors.push('Cần SĐT học viên hoặc SĐT phụ huynh');
        if (r.parentName && !parentPhone) errors.push('Có tên phụ huynh nhưng thiếu SĐT phụ huynh');
        if (r.email && !isValidEmail(r.email)) errors.push(`Email "${r.email}" sai định dạng`);
        const branch = r.branch ? matchOption(r.branch, branchOpts) : defBranch;
        if (r.branch && !branch) errors.push(`Chi nhánh "${r.branch}" không có hoặc bạn không được quyền`);
        if (!r.branch && !branch) errors.push('Chưa chọn chi nhánh');

        let duplicate: string | undefined;
        const nameKey = r.fullName && dob ? `${normalizeText(r.fullName)}|${dob}` : undefined;
        const old = (phone && byPhone.get(phone)) || (nameKey && byNameDob.get(nameKey));
        if (old) duplicate = `Đã có học viên ${old.code} — ${old.fullName}${phone && byPhone.get(phone) ? ' (cùng SĐT)' : ' (cùng họ tên + ngày sinh)'}`;
        else {
          const keys = [phone && `p:${phone}`, nameKey && `n:${nameKey}`].filter(Boolean) as string[];
          const hit = keys.map((k) => seen.get(k)).find((x) => x !== undefined);
          if (hit !== undefined) duplicate = `Trùng với dòng #${hit} trong file`;
          else keys.forEach((k) => seen.set(k, i + 2));
        }

        return {
          errors,
          duplicate,
          value: errors.length ? undefined : { fullName: r.fullName, dob: dob ?? undefined, gender, phone, email: r.email || undefined, branchId: branch!, parentName: r.parentName || undefined, parentPhone, note: r.note || undefined },
        };
      });
    },
    [students, branches, defBranch],
  );

  return (
    <ImportDialog<StudentRow>
      open={open}
      onClose={onClose}
      title="Nhập học viên từ Excel"
      unitLabel="học viên"
      templateName="mau-nhap-hoc-vien"
      columns={COLUMNS}
      check={check}
      previewKeys={['fullName', 'dob', 'phone', 'parentPhone']}
      settings={
        <FormField label="Chi nhánh mặc định" hint="Dùng khi cột Chi nhánh để trống">
          <Select value={defBranch} onChange={(v) => setDefBranch(v as string)} options={branches.map((b) => ({ value: b.id, label: b.name }))} allowClear={false} showSearch={false} />
        </FormField>
      }
      notes="Mỗi học viên được tạo kèm người liên hệ: SĐT phụ huynh đã có trong Lead → gắn vào phụ huynh đó (anh chị em dùng chung 1 phụ huynh). Chỉ tạo hồ sơ — xếp lớp làm ở trang Lớp học."
      onImport={async (values) => {
        const staff = user?.fullName ?? 'Nhập từ Excel';
        if (USE_API) {
          // Hồ sơ tạo trên server (1 request; dòng trùng đã được người dùng duyệt ở bước xem trước).
          // Có cột phụ huynh (HV-04.E3) → thêm người liên hệ qua Crm (trùng SĐT → anh chị em dùng chung 1 phụ huynh), lần lượt từng HV
          const r = await studentApi.importStudents(
            values.map((v) => ({ fullName: v.fullName, gender: v.gender, dob: v.dob, phone: v.phone, email: v.email, note: v.note, branchId: v.branchId })),
            true,
          );
          // Giống server: bỏ ký tự không phải số, +84 → 0
          const vnPhone = (p?: string) => (p ?? '').replace(/\D/g, '').replace(/^84(\d{9})$/, '0$1');
          const used = new Set<string>();
          let guardianErrors = 0;
          for (const v of values) {
            const st = r.created.find((c) => !used.has(c.id) && c.fullName === v.fullName.trim().replace(/\s+/g, ' ') && (c.phone ?? '') === vnPhone(v.phone));
            if (!st) continue;
            used.add(st.id);
            if (!v.parentPhone) continue;
            try {
              await createGuardian({
                fullName: v.parentName ?? `Phụ huynh ${v.fullName}`,
                phone: v.parentPhone,
                branchId: v.branchId,
                relationship: 'child',
                studentIds: [st.id],
                primary: true,
                note: 'Tạo khi nhập học viên từ Excel',
              });
            } catch {
              guardianErrors++;
            }
          }
          if (guardianErrors) notify.toast.warning(`${guardianErrors} phụ huynh chưa thêm được — bổ sung ở trang Phụ huynh / Người giám hộ`);
          if (r.errors.length)
            notify.warning({ title: `Đã nhập ${r.created.length} học viên · ${r.errors.length} dòng lỗi`, description: r.errors.slice(0, 3).map((e) => `Dòng ${e.row}: ${e.message}`).join(' · ') });
          else notify.success({ title: `Đã nhập ${r.created.length} học viên`, description: 'Hồ sơ ở trạng thái chưa xếp lớp.' });
          return;
        }
        await new Promise((r) => setTimeout(r, 500)); // giả lập gọi API
        values.forEach((v) => {
          const crm = useCrmStore.getState();
          const contactPhone = v.parentPhone ?? v.phone!;
          let lead = crm.findDuplicate(contactPhone);
          if (!lead)
            lead = crm.addLead(
              { fullName: v.parentPhone ? (v.parentName ?? `Phụ huynh ${v.fullName}`) : v.fullName, phone: contactPhone, email: v.parentPhone ? undefined : v.email, source: 'walk_in', status: 'won', branchId: v.branchId, note: 'Tạo khi nhập học viên từ Excel' },
              staff,
            );
          const st = crm.createStudentFromLead(lead.id, { fullName: v.fullName, dob: v.dob, phone: v.phone, relationship: v.parentPhone ? 'child' : 'self' }, staff);
          useCrmStore.getState().updateStudent(st.id, { gender: v.gender, email: v.email, note: v.note, branchId: v.branchId });
        });
        notify.success({ title: `Đã nhập ${values.length} học viên`, description: 'Hồ sơ ở trạng thái chưa xếp lớp.' });
      }}
    />
  );
}

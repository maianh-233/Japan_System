import { Checkbox, Form, Radio, Switch } from 'antd';
import type { Dayjs } from 'dayjs';
import dayjs from 'dayjs';
import { DateInput, NumberInput, Select, TextArea, TextInput } from '@/components/common';
import { Q_LEVELS } from '@/features/academic/questions/constants';
import type { QLevel } from '@/features/academic/questions/types';
import { getBranches } from '@/features/system/useBranches';
import { CONTRACT_TYPE_OPTIONS, DEPARTMENT_OPTIONS, EMPLOYEE_STATUS_OPTIONS } from './constants';
import { useHrStore } from './hrStore';
import type { ContractType, Department, Employee, EmployeeKind, EmployeeStatus } from './types';

export interface EmployeeFormValues {
  fullName: string;
  gender: 'male' | 'female';
  dob?: Dayjs;
  phone: string;
  email: string;
  branchId: string;
  department: Department;
  position: string;
  managerId?: string;
  shiftBased: boolean;
  status: EmployeeStatus;
  contractType: ContractType;
  contractStart: Dayjs;
  contractEnd?: Dayjs;
  baseSalary?: number;
  allowance?: number;
  note?: string;
  // Giáo viên
  teacherType?: 'vn' | 'jp';
  levels?: QLevel[];
  extraBranchIds?: string[];
  certificates?: string[];
  rateOffline?: number;
  rateOnline?: number;
  bio?: string;
}

/** Hồ sơ → giá trị form */
export function employeeToForm(e: Employee): EmployeeFormValues {
  return {
    fullName: e.fullName,
    gender: e.gender,
    dob: e.dob ? dayjs(e.dob) : undefined,
    phone: e.phone,
    email: e.email,
    branchId: e.branchId,
    department: e.department,
    position: e.position,
    managerId: e.managerId,
    shiftBased: e.shiftBased,
    status: e.status,
    contractType: e.contract.type,
    contractStart: dayjs(e.contract.start),
    contractEnd: e.contract.end ? dayjs(e.contract.end) : undefined,
    baseSalary: e.contract.baseSalary,
    allowance: e.contract.allowance,
    note: e.note,
    teacherType: e.teacher?.type,
    levels: e.teacher?.levels,
    extraBranchIds: e.teacher?.extraBranchIds,
    certificates: e.teacher?.certificates,
    rateOffline: e.teacher?.rate.offline,
    rateOnline: e.teacher?.rate.online,
    bio: e.teacher?.bio,
  };
}

/** Giá trị form → dữ liệu (giữ lịch rảnh / số HĐ cũ khi sửa) */
export function formToEmployee(v: EmployeeFormValues, kind: EmployeeKind, prev?: Employee): Omit<Employee, 'id' | 'code'> {
  const extraBranchIds = (v.extraBranchIds ?? []).filter((b) => b !== v.branchId);
  return {
    fullName: v.fullName.trim(),
    gender: v.gender,
    dob: v.dob?.format('YYYY-MM-DD'),
    phone: v.phone.trim(),
    email: v.email.trim(),
    branchId: v.branchId,
    kind,
    department: kind === 'staff' ? v.department : 'teaching',
    position: v.position.trim(),
    managerId: v.managerId,
    shiftBased: kind === 'staff' ? v.shiftBased : false,
    status: v.status,
    joinedAt: prev?.joinedAt ?? v.contractStart.format('YYYY-MM-DD'),
    contract: { type: v.contractType, no: prev?.contract.no ?? `HĐ-${dayjs().year()}/${Date.now().toString().slice(-3)}`, start: v.contractStart.format('YYYY-MM-DD'), end: v.contractEnd?.format('YYYY-MM-DD'), baseSalary: v.baseSalary, allowance: v.allowance },
    leaveBalance: prev?.leaveBalance ?? 12,
    hasAccount: prev?.hasAccount ?? false,
    note: v.note?.trim() || undefined,
    teacher:
      kind === 'staff'
        ? undefined
        : {
            type: v.teacherType ?? 'vn',
            levels: v.levels ?? [],
            certificates: v.certificates ?? [],
            rate: { offline: v.rateOffline ?? 0, online: v.rateOnline ?? 0 },
            availability: prev?.teacher?.availability ?? { 1: [], 2: [], 3: [], 4: [], 5: [], 6: [], 7: [] },
            extraBranchIds: extraBranchIds.length ? extraBranchIds : undefined,
            bio: v.bio?.trim() || undefined,
          },
  };
}

/** Ô nhập hồ sơ nhân viên / giáo viên / trợ giảng */
export function EmployeeFormFields({ kind, editingId }: { kind: EmployeeKind; editingId?: string }) {
  const employees = useHrStore((s) => s.employees);
  const form = Form.useFormInstance<EmployeeFormValues>();
  const ctype = Form.useWatch('contractType', form);
  const mainBranch = Form.useWatch('branchId', form);
  const managers = employees.filter((e) => e.kind === 'staff' && e.department === 'board' && e.id !== editingId).map((e) => ({ value: e.id, label: `${e.fullName} · ${e.position}` }));

  return (
    <>
      <p className="md:col-span-2 text-sm font-semibold text-ink">Thông tin cá nhân</p>
      <Form.Item name="fullName" label="Họ tên" rules={[{ required: true, whitespace: true, message: 'Nhập họ tên' }]}>
        <TextInput maxLength={80} />
      </Form.Item>
      <Form.Item name="gender" label="Giới tính">
        <Radio.Group options={[{ value: 'female', label: 'Nữ' }, { value: 'male', label: 'Nam' }]} />
      </Form.Item>
      <Form.Item name="phone" label="Điện thoại" rules={[{ required: true, pattern: /^0\d{9}$/, message: 'SĐT 10 số' }]}>
        <TextInput maxLength={10} inputMode="tel" />
      </Form.Item>
      <Form.Item name="email" label="Email" rules={[{ required: true, type: 'email', message: 'Email không hợp lệ' }]}>
        <TextInput />
      </Form.Item>
      <Form.Item name="dob" label="Ngày sinh">
        <DateInput />
      </Form.Item>
      <Form.Item name="branchId" label="Chi nhánh" rules={[{ required: true }]}>
        <Select options={getBranches(false).map((b) => ({ value: b.id, label: b.name }))} showSearch={false} allowClear={false} />
      </Form.Item>

      <p className="md:col-span-2 mt-2 text-sm font-semibold text-ink">Công việc</p>
      {kind === 'staff' && (
        <Form.Item name="department" label="Phòng ban" rules={[{ required: true }]}>
          <Select options={DEPARTMENT_OPTIONS.filter((d) => d.value !== 'teaching')} showSearch={false} allowClear={false} />
        </Form.Item>
      )}
      <Form.Item name="position" label="Vị trí" rules={[{ required: true, whitespace: true, message: 'Nhập vị trí' }]}>
        <TextInput placeholder={kind === 'staff' ? 'VD: Tư vấn tuyển sinh' : 'VD: Giáo viên'} />
      </Form.Item>
      <Form.Item name="managerId" label="Quản lý trực tiếp">
        <Select options={managers} placeholder="Chọn" />
      </Form.Item>
      <Form.Item name="status" label="Trạng thái" rules={[{ required: true }]}>
        <Select options={EMPLOYEE_STATUS_OPTIONS} showSearch={false} allowClear={false} />
      </Form.Item>
      {kind === 'staff' && (
        <Form.Item name="shiftBased" label="Làm theo ca" valuePropName="checked" extra="Bật cho Sale, lễ tân — xếp ca hàng tuần ở Công việc → Xếp ca.">
          <Switch />
        </Form.Item>
      )}

      {kind !== 'staff' && (
        <>
          <p className="md:col-span-2 mt-2 text-sm font-semibold text-ink">Giảng dạy</p>
          <Form.Item name="teacherType" label="Loại giáo viên">
            <Radio.Group options={[{ value: 'vn', label: 'GV Việt' }, { value: 'jp', label: 'GV người Nhật' }]} />
          </Form.Item>
          <Form.Item name="levels" label="Cấp độ được dạy" rules={[{ required: true, type: 'array', min: 1, message: 'Chọn cấp độ' }]}>
            <Checkbox.Group options={Q_LEVELS.map((l) => ({ value: l, label: l }))} />
          </Form.Item>
          <Form.Item name="extraBranchIds" label="Chi nhánh dạy thêm" extra="Ngoài chi nhánh chính — được xếp lớp / mời dạy thay ở các chi nhánh này.">
            <Select mode="multiple" placeholder="Chỉ dạy ở chi nhánh chính" options={getBranches().filter((b) => b.id !== mainBranch).map((b) => ({ value: b.id, label: b.name }))} />
          </Form.Item>
          <Form.Item name="certificates" label="Bằng cấp / chứng chỉ" className="md:col-span-2">
            <Select mode="tags" placeholder="VD: JLPT N1 (2020), Chứng chỉ sư phạm" />
          </Form.Item>
          <Form.Item name="rateOffline" label="Đơn giá offline / giờ" rules={[{ required: true, message: 'Nhập đơn giá' }]}>
            <NumberInput currency min={0} />
          </Form.Item>
          <Form.Item name="rateOnline" label="Đơn giá online / giờ" rules={[{ required: true, message: 'Nhập đơn giá' }]}>
            <NumberInput currency min={0} />
          </Form.Item>
          <Form.Item name="bio" label="Giới thiệu (hiện cho học viên)" className="md:col-span-2">
            <TextArea rows={2} maxLength={500} />
          </Form.Item>
        </>
      )}

      <p className="md:col-span-2 mt-2 text-sm font-semibold text-ink">Hợp đồng</p>
      <Form.Item name="contractType" label="Loại hợp đồng" rules={[{ required: true }]}>
        <Select options={CONTRACT_TYPE_OPTIONS} showSearch={false} allowClear={false} />
      </Form.Item>
      <div className="grid grid-cols-2 gap-x-3">
        <Form.Item name="contractStart" label="Từ ngày" rules={[{ required: true, message: 'Chọn ngày' }]}>
          <DateInput />
        </Form.Item>
        <Form.Item name="contractEnd" label="Đến ngày" rules={[{ required: ctype !== 'indefinite', message: 'Chọn ngày' }]}>
          <DateInput disabled={ctype === 'indefinite'} />
        </Form.Item>
      </div>
      <Form.Item name="baseSalary" label={kind === 'staff' ? 'Lương cơ bản / tháng' : 'Lương cứng (nếu có)'}>
        <NumberInput currency min={0} />
      </Form.Item>
      <Form.Item name="allowance" label="Phụ cấp / tháng">
        <NumberInput currency min={0} />
      </Form.Item>
      <Form.Item name="note" label="Ghi chú" className="md:col-span-2">
        <TextArea rows={2} />
      </Form.Item>
    </>
  );
}

import { useMemo, useState, type Key } from 'react';
import { Form } from 'antd';
import dayjs, { type Dayjs } from 'dayjs';
import { AlertCircle, GraduationCap, UserPlus, Users, Wallet } from 'lucide-react';
import {
  Button,
  DateInput,
  DrawerForm,
  ImagePicker,
  ModalForm,
  NumberInput,
  PageHeader,
  Select,
  StatCard,
  StatusTag,
  TextArea,
  TextInput,
  UserCell,
  useConfirm,
  useNotify,
  DataTable,
  type DataColumn,
} from '@/components/common';
import { formatCurrency } from '@/lib/format';

interface Student {
  id: number;
  code: string;
  name: string;
  phone: string;
  level: string;
  branch: string;
  className: string;
  status: 'paid' | 'partial' | 'unpaid' | 'overdue';
  debt: number;
  note: string;
  joinedAt: string;
}

const FIRST = ['Nguyễn', 'Trần', 'Lê', 'Phạm', 'Hoàng', 'Vũ', 'Đặng', 'Bùi'];
const MIDDLE = ['Văn', 'Thị', 'Minh', 'Đức', 'Thu', 'Ngọc'];
const LAST = ['An', 'Bình', 'Chi', 'Dũng', 'Giang', 'Hà', 'Khánh', 'Linh', 'Nam', 'Phương', 'Quân', 'Trang'];
const NOTES = [
  'Muốn thi JLPT N3 tháng 12, học buổi tối các ngày 2-4-6, cần giáo viên kèm thêm phần nghe hiểu vì nghe còn yếu.',
  'Phụ huynh đóng học phí, liên hệ mẹ qua Zalo.',
  'Chuẩn bị du học Nhật kỳ tháng 4, cần học cấp tốc.',
  '',
  'Đi làm ca, có thể nghỉ một số buổi cuối tuần — đã thông báo giáo vụ.',
];
const STATUSES: Student['status'][] = ['paid', 'partial', 'unpaid', 'overdue'];

const STUDENTS: Student[] = Array.from({ length: 57 }, (_, i) => {
  const status = STATUSES[i % 4];
  return {
    id: i + 1,
    code: `HV${String(1000 + i)}`,
    name: `${FIRST[i % 8]} ${MIDDLE[i % 6]} ${LAST[i % 12]}`,
    phone: `09${String(10000000 + i * 7919).slice(0, 8)}`,
    level: ['N5', 'N4', 'N3', 'N2', 'N1'][i % 5],
    branch: ['Cầu Giấy', 'Quận 1', 'Hải Châu'][i % 3],
    className: `${['N5', 'N4', 'N3', 'N2', 'N1'][i % 5]}-K${(i % 7) + 10} Tối 2-4-6`,
    status,
    debt: status === 'paid' ? 0 : status === 'partial' ? 1_500_000 : 4_500_000,
    note: NOTES[i % 5],
    joinedAt: dayjs().subtract(i * 3, 'day').format('YYYY-MM-DD'),
  };
});

interface StudentForm {
  name: string;
  phone: string;
  birthday?: Dayjs;
  level?: string;
  branch?: string;
  fee?: number;
  note?: string;
  avatar?: string | null;
}

/** Trang mẫu "Quản lý học viên" — cách ghép các component dùng chung thành một trang thật. */
export function StudentPageDemo({ embedded = false }: { embedded?: boolean }) {
  const notify = useNotify();
  const { confirm, confirmDialog } = useConfirm();
  const [data, setData] = useState(STUDENTS);
  const [branch, setBranch] = useState<string>();
  const [status, setStatus] = useState<string>();
  const [selected, setSelected] = useState<Key[]>([]);
  const [creating, setCreating] = useState(false);
  const [editing, setEditing] = useState<Student | null>(null);

  const filtered = useMemo(
    () => data.filter((s) => (!branch || s.branch === branch) && (!status || s.status === status)),
    [data, branch, status],
  );
  const totalDebt = data.reduce((s, x) => s + x.debt, 0);

  const removeStudent = async (s: Student) => {
    const ok = await confirm({
      title: `Xóa học viên ${s.name}?`,
      description: 'Thao tác này không thể hoàn tác.',
      consequences: [`Học viên bị gỡ khỏi lớp ${s.className}`, 'Lịch sử điểm danh và học phí vẫn được lưu để đối soát'],
      onConfirm: () => new Promise((r) => setTimeout(r, 600)),
    });
    if (ok) {
      setData((d) => d.filter((x) => x.id !== s.id));
      notify.success({ title: 'Đã xóa học viên', description: s.name });
    }
  };

  const columns: DataColumn<Student>[] = [
    {
      key: 'name',
      title: 'Học viên',
      width: 220,
      mobile: 'title',
      sorter: (a, b) => a.name.localeCompare(b.name, 'vi'),
      render: (_, r) => <UserCell name={r.name} subtitle={r.code} size="sm" />,
    },
    { key: 'phone', title: 'Điện thoại', dataIndex: 'phone', width: 130, mobile: 'subtitle' },
    { key: 'level', title: 'Trình độ', dataIndex: 'level', width: 100, sorter: (a, b) => a.level.localeCompare(b.level) },
    { key: 'className', title: 'Lớp', dataIndex: 'className', width: 150 },
    { key: 'branch', title: 'Chi nhánh', dataIndex: 'branch', width: 120 },
    {
      key: 'status',
      title: 'Học phí',
      width: 150,
      mobile: 'aside',
      render: (_, r) => <StatusTag group="invoice" status={r.status} size="sm" />,
    },
    {
      key: 'debt',
      title: 'Còn nợ',
      width: 130,
      align: 'right',
      sorter: (a, b) => a.debt - b.debt,
      render: (_, r) => <span className="tabular">{r.debt ? formatCurrency(r.debt) : '—'}</span>,
    },
    { key: 'note', title: 'Ghi chú tư vấn', dataIndex: 'note', width: 200, mobile: 'hidden' },
  ];

  return (
    <div className={embedded ? undefined : 'rounded-[var(--radius-card)] border border-line bg-page p-4 sm:p-6'}>
      <PageHeader
        breadcrumb={[{ title: 'Học viên' }, { title: 'Hồ sơ học viên' }]}
        title="Quản lý học viên"
        subtitle="Danh sách học viên đang theo học tại các chi nhánh"
        actions={
          <>
            <Button action="import" collapseOnMobile />
            <Button action="create" collapseOnMobile onClick={() => setCreating(true)}>
              Thêm học viên
            </Button>
          </>
        }
      />

      <div className="mb-6 grid grid-cols-1 gap-3 sm:grid-cols-2 xl:grid-cols-4">
        <StatCard label="Tổng học viên" value={data.length} icon={Users} trend={8.2} trendLabel="so với tháng trước" />
        <StatCard label="Học viên mới tháng này" value={12} icon={UserPlus} tone="success" trend={20} />
        <StatCard label="Đang học N3 trở lên" value={34} icon={GraduationCap} tone="info" hint="60% tổng số" />
        <StatCard
          label="Công nợ học phí"
          value={formatCurrency(totalDebt)}
          icon={Wallet}
          tone="warning"
          trend={-4.5}
          trendInverse
          trendLabel="so với tháng trước"
        />
      </div>

      <DataTable<Student>
        storageKey="demo-students"
        columns={columns}
        data={filtered}
        rowKey="id"
        unitLabel="học viên"
        searchKeys={['name', 'code', 'phone']}
        search={{
          placeholder: 'Tìm theo tên, mã HV, số điện thoại...',
          activeFilterCount: [branch, status].filter(Boolean).length,
          filters: (
            <>
              <Select
                placeholder="Chi nhánh"
                value={branch}
                onChange={setBranch}
                options={['Cầu Giấy', 'Quận 1', 'Hải Châu'].map((v) => ({ value: v, label: v }))}
                className="lg:!w-40"
              />
              <Select
                placeholder="Học phí"
                value={status}
                onChange={setStatus}
                options={[
                  { value: 'paid', label: 'Đã thu' },
                  { value: 'partial', label: 'Thu một phần' },
                  { value: 'unpaid', label: 'Chưa thu' },
                  { value: 'overdue', label: 'Quá hạn' },
                ]}
                className="lg:!w-40"
              />
            </>
          ),
          onReset: () => {
            setBranch(undefined);
            setStatus(undefined);
          },
          actions: <Button action="export" collapseOnMobile />,
        }}
        selectedKeys={selected}
        onSelectionChange={(keys) => setSelected(keys)}
        bulkActions={(keys) => (
          <>
            <Button action="send" size="sm" variant="soft" onClick={() => notify.info({ title: `Đã gửi nhắc học phí cho ${keys.length} học viên` })}>
              Nhắc học phí
            </Button>
            <Button action="delete" size="sm" onClick={() => setSelected([])}>
              Bỏ chọn
            </Button>
          </>
        )}
        rowActions={(r) => (
          <>
            <Button action="edit" iconOnly size="sm" onClick={() => setEditing(r)} />
            <Button action="delete" iconOnly size="sm" onClick={() => removeStudent(r)} />
          </>
        )}
        empty={{
          title: 'Chưa có học viên',
          description: 'Thêm học viên mới hoặc chuyển đổi từ lead.',
          action: <Button action="create" onClick={() => setCreating(true)}>Thêm học viên</Button>,
        }}
      />
      <p className="mt-2 flex items-center gap-1.5 text-[13px] text-ink-muted">
        <AlertCircle size={14} aria-hidden /> Kéo mép phải tiêu đề cột để đổi độ rộng · nhấp đúp để đặt lại · độ rộng được nhớ trên trình duyệt.
      </p>

      {/* Thêm mới: form ngắn → ModalForm */}
      <ModalForm<StudentForm>
        open={creating}
        onClose={() => setCreating(false)}
        title="Thêm học viên"
        description="Thông tin cơ bản — có thể bổ sung sau"
        columns={2}
        successMessage="Đã thêm học viên"
        onSubmit={async (v) => {
          await new Promise((r) => setTimeout(r, 700));
          setData((d) => [
            {
              id: Date.now(),
              code: `HV${Date.now() % 10000}`,
              name: v.name,
              phone: v.phone,
              level: v.level ?? 'N5',
              branch: v.branch ?? 'Cầu Giấy',
              className: 'Chưa xếp lớp',
              status: 'unpaid',
              debt: v.fee ?? 0,
              note: v.note ?? '',
              joinedAt: dayjs().format('YYYY-MM-DD'),
            },
            ...d,
          ]);
        }}
      >
        <StudentFields />
      </ModalForm>

      {/* Sửa: form dài → DrawerForm */}
      <DrawerForm<StudentForm>
        open={Boolean(editing)}
        onClose={() => setEditing(null)}
        title={`Sửa học viên ${editing?.code ?? ''}`}
        initialValues={editing ? { name: editing.name, phone: editing.phone, level: editing.level, branch: editing.branch, note: editing.note } : undefined}
        successMessage="Đã cập nhật học viên"
        onSubmit={async (v) => {
          await new Promise((r) => setTimeout(r, 600));
          setData((d) => d.map((x) => (x.id === editing?.id ? { ...x, ...v, level: v.level ?? x.level, branch: v.branch ?? x.branch, note: v.note ?? '' } : x)));
        }}
      >
        <Form.Item name="avatar" label="Ảnh đại diện" className="md:col-span-2">
          <ImagePicker shape="circle" />
        </Form.Item>
        <StudentFields />
      </DrawerForm>

      {confirmDialog}
    </div>
  );
}

function StudentFields() {
  return (
    <>
      <Form.Item name="name" label="Họ và tên" rules={[{ required: true, message: 'Nhập họ tên' }]}>
        <TextInput placeholder="Nguyễn Văn A" />
      </Form.Item>
      <Form.Item
        name="phone"
        label="Số điện thoại"
        rules={[
          { required: true, message: 'Nhập số điện thoại' },
          { pattern: /^0\d{9}$/, message: 'Số điện thoại gồm 10 số, bắt đầu bằng 0' },
        ]}
      >
        <TextInput type="tel" placeholder="09xxxxxxxx" />
      </Form.Item>
      <Form.Item name="birthday" label="Ngày sinh">
        <DateInput disabledDate={(d) => d.isAfter(dayjs())} />
      </Form.Item>
      <Form.Item name="level" label="Trình độ hiện tại">
        <Select placeholder="Chọn trình độ" options={['N5', 'N4', 'N3', 'N2', 'N1'].map((v) => ({ value: v, label: v }))} />
      </Form.Item>
      <Form.Item name="branch" label="Chi nhánh" rules={[{ required: true, message: 'Chọn chi nhánh' }]}>
        <Select placeholder="Chọn chi nhánh" options={['Cầu Giấy', 'Quận 1', 'Hải Châu'].map((v) => ({ value: v, label: v }))} />
      </Form.Item>
      <Form.Item name="fee" label="Học phí dự kiến">
        <NumberInput currency />
      </Form.Item>
      <Form.Item name="note" label="Ghi chú" className="md:col-span-2">
        <TextArea maxLength={500} placeholder="Nhu cầu, mục tiêu, thời gian rảnh..." />
      </Form.Item>
    </>
  );
}

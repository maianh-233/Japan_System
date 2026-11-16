import { useMemo, useState } from 'react';
import { Form } from 'antd';
import { Building2, CalendarClock, Clock, DoorOpen, GraduationCap, Landmark, Mail, MapPin, Phone, School, Search, UserRound, Users, Wallet } from 'lucide-react';
import { Button, DateInput, ModalForm, PageHeader, Select, StatCard, StatusTag, TextArea, TextInput, useConfirm, useNotify } from '@/components/common';
import { useNavigate } from 'react-router-dom';
import { useClassStore } from '@/features/academic/classes/classStore';
import { useRoomStore } from '@/features/academic/rooms/roomStore';
import { useHrStore } from '@/features/hr/hrStore';
import { summarizeInvoice, useStudentStore } from '@/features/student/studentStore';
import { BRANCH_STATUS } from '@/features/system/constants';
import { useSystemStore } from '@/features/system/systemStore';
import type { BranchProfile } from '@/features/system/types';
import { useActor } from '@/features/system/useActor';
import { useAuth } from '@/hooks/useAuth';
import { cn } from '@/lib/cn';
import { fmtDate } from '@/lib/date';
import { formatCurrency } from '@/lib/format';
import { normalizeText } from '@/lib/text';
import dayjs from 'dayjs';

type FormValues = Omit<BranchProfile, 'openedAt' | 'bank'> & { openedAt: dayjs.Dayjs; bankName?: string; bankAccount?: string; bankHolder?: string };

/** Chi nhánh: thông tin liên hệ, quản lý, giờ mở cửa, tài khoản nhận học phí, trạng thái. DỮ LIỆU GIẢ. */
export default function BranchesPage() {
  const { can, setBranch } = useAuth();
  const navigate = useNavigate();
  const notify = useNotify();
  const { confirm, confirmDialog } = useConfirm();
  const { actor } = useActor();
  const branches = useSystemStore((s) => s.branches);
  const saveBranch = useSystemStore((s) => s.saveBranch);
  const employees = useHrStore((s) => s.employees);
  const classes = useClassStore((s) => s.classes);
  const rooms = useRoomStore((s) => s.rooms);
  const enrollments = useStudentStore((s) => s.enrollments);
  const invoices = useStudentStore((s) => s.invoices);
  const editable = can('system.branches');
  const [editing, setEditing] = useState<BranchProfile | 'new' | null>(null);
  const [query, setQuery] = useState('');
  const [statusFilter, setStatusFilter] = useState('all');

  const stats = useMemo(() => {
    const classBranch = new Map(classes.map((c) => [c.id, c.branchId]));
    const enrollmentBranch = new Map(enrollments.map((enrollment) => {
      const branchId = classBranch.get(enrollment.classId);
      return [enrollment.id, branchId];
    }));
    const financeByBranch = new Map<string, { collected: number; receivable: number }>();
    invoices.forEach((invoice) => {
      const branchId = enrollmentBranch.get(invoice.enrollmentId);
      if (!branchId) return;
      const summary = summarizeInvoice(invoice);
      const current = financeByBranch.get(branchId) ?? { collected: 0, receivable: 0 };
      financeByBranch.set(branchId, {
        collected: current.collected + summary.paid,
        receivable: current.receivable + summary.remaining,
      });
    });
    return new Map(
      branches.map((b) => {
        const cls = classes.filter((c) => c.branchId === b.id);
        const branchEmployees = employees.filter((e) => e.branchId === b.id && e.status !== 'quit');
        const finance = financeByBranch.get(b.id) ?? { collected: 0, receivable: 0 };
        return [
          b.id,
          {
            staff: branchEmployees.length,
            teachers: branchEmployees.filter((e) => e.kind === 'teacher').length,
            rooms: rooms.filter((r) => r.branchId === b.id).length,
            activeClasses: cls.filter((c) => c.status === 'ongoing' || c.status === 'enrolling').length,
            students: new Set(enrollments.filter((e) => e.status === 'studying' && classBranch.get(e.classId) === b.id).map((e) => e.studentId)).size,
            collected: finance.collected,
            receivable: finance.receivable,
          },
        ];
      }),
    );
  }, [branches, classes, employees, rooms, enrollments, invoices]);

  const total = (k: 'staff' | 'teachers' | 'rooms' | 'activeClasses' | 'students' | 'collected' | 'receivable') => [...stats.values()].reduce((s, x) => s + x[k], 0);
  const managers = employees.filter((e) => e.status !== 'quit' && (e.department === 'board' || e.position.toLowerCase().includes('quản lý')));
  const normalizedQuery = normalizeText(query.trim());
  const visibleBranches = branches.filter((branch) => {
    const matchesStatus = statusFilter === 'all' || branch.status === statusFilter;
    const matchesQuery =
      !normalizedQuery ||
      normalizeText(`${branch.name} ${branch.code} ${branch.address} ${branch.phone} ${branch.email}`).includes(normalizedQuery);
    return matchesStatus && matchesQuery;
  });

  const setStatus = async (b: BranchProfile, status: BranchProfile['status']) => {
    const st = stats.get(b.id)!;
    if (status === 'closed') {
      if (st.activeClasses) {
        notify.error({ title: 'Chưa đóng được chi nhánh', description: `Còn ${st.activeClasses} lớp đang học / tuyển sinh. Kết thúc hoặc chuyển lớp trước.` });
        return;
      }
      if (st.staff) {
        notify.error({ title: 'Chưa đóng được chi nhánh', description: `Còn ${st.staff} nhân sự chưa chuyển chi nhánh hoặc chấm dứt công tác.` });
        return;
      }
      const ok = await confirm({
        title: `Đóng chi nhánh ${b.name}?`,
        tone: 'danger',
        consequences: ['Không nhận lead, ghi danh, xếp lớp mới ở chi nhánh này.', 'Dữ liệu cũ (học viên, nhân sự, phòng, hóa đơn) vẫn giữ để tra cứu.'],
        confirmText: 'Đóng chi nhánh',
        typeToConfirm: b.code,
      });
      if (!ok) return;
    }
    saveBranch({ ...b, status }, actor);
    notify.toast.success(`${b.name}: ${BRANCH_STATUS[status].label}`);
  };

  return (
    <>
      <PageHeader
        title="Chi nhánh"
        subtitle="Thông tin chi nhánh dùng trên hóa đơn, mã QR học phí, thông báo gửi phụ huynh."
        breadcrumb={[{ title: 'Hệ thống' }, { title: 'Chi nhánh' }]}
        actions={
          editable ? (
            <Button action="create" collapseOnMobile onClick={() => setEditing('new')}>
              Thêm chi nhánh
            </Button>
          ) : undefined
        }
      />

      <div className="mb-4 grid grid-cols-2 gap-3 sm:grid-cols-3 xl:grid-cols-4">
        <StatCard label="Chi nhánh hoạt động" value={branches.filter((b) => b.status === 'active').length} icon={Building2} tone="primary" hint={`${branches.filter((b) => b.status === 'preparing').length} đang chuẩn bị`} />
        <StatCard label="Học viên đang học" value={total('students')} icon={Users} tone="success" />
        <StatCard label="Lớp đang chạy" value={total('activeClasses')} icon={School} tone="info" />
        <StatCard label="Phòng học" value={total('rooms')} icon={DoorOpen} tone="info" />
        <StatCard label="Nhân sự" value={total('staff')} icon={UserRound} tone="warning" />
        <StatCard label="Giáo viên" value={total('teachers')} icon={School} tone="primary" />
        <StatCard label="Đã thu" value={formatCurrency(total('collected'))} icon={Landmark} tone="success" />
        <StatCard label="Còn phải thu" value={formatCurrency(total('receivable'))} icon={Wallet} tone="warning" />
      </div>

      <div className="mb-4 flex flex-col gap-2 sm:flex-row">
        <TextInput
          icon={Search}
          value={query}
          onChange={(event) => setQuery(event.target.value)}
          placeholder="Tìm tên, mã, địa chỉ, điện thoại hoặc email..."
          aria-label="Tìm chi nhánh"
        />
        <Select<string>
          value={statusFilter}
          onChange={setStatusFilter}
          options={[
            { value: 'all', label: 'Tất cả trạng thái' },
            ...Object.entries(BRANCH_STATUS).map(([value, status]) => ({ value, label: status.label })),
          ]}
          showSearch={false}
          allowClear={false}
          aria-label="Lọc trạng thái chi nhánh"
          className="sm:!w-56"
        />
      </div>

      <div className="grid gap-4 md:grid-cols-2">
        {visibleBranches.map((b) => {
          const st = stats.get(b.id)!;
          const mgr = employees.find((e) => e.id === b.managerId && e.status !== 'quit');
          const S = BRANCH_STATUS[b.status];
          return (
            <article key={b.id} className={cn('flex flex-col rounded-xl border bg-surface p-4', b.status === 'closed' ? 'border-dashed border-line opacity-70' : 'border-line')}>
              <div className="flex items-start justify-between gap-2">
                <div className="flex items-center gap-3">
                  <span className="grid size-11 place-items-center rounded-xl bg-primary-50 font-bold text-primary-700">{b.code}</span>
                  <div>
                    <h3 className="text-base font-semibold text-ink">{b.name}</h3>
                    <p className="text-xs text-ink-muted">{b.status === 'preparing' ? `Dự kiến khai trương ${fmtDate(b.openedAt)}` : `Hoạt động từ ${fmtDate(b.openedAt)}`}</p>
                  </div>
                </div>
                <StatusTag tone={S.tone} icon={S.icon} size="sm">
                  {S.label}
                </StatusTag>
              </div>

              <div className="mt-3 grid grid-cols-2 gap-2 rounded-lg bg-canvas p-2.5 text-center sm:grid-cols-3 lg:grid-cols-5">
                {(
                  [
                    ['Học viên', st.students, Users],
                    ['Lớp', st.activeClasses, School],
                    ['Phòng', st.rooms, DoorOpen],
                    ['Giáo viên', st.teachers, School],
                    ['Nhân sự', st.staff, UserRound],
                  ] as const
                ).map(([l, v, Icon]) => (
                  <div key={l}>
                    <p className="tabular text-lg font-semibold text-ink">{v}</p>
                    <p className="inline-flex items-center gap-1 text-[11px] text-ink-muted">
                      <Icon size={12} aria-hidden /> {l}
                    </p>
                  </div>
                ))}
              </div>

              <div className="mt-3 grid grid-cols-2 gap-2 text-[13px]">
                <div className="rounded-lg border border-line p-2.5">
                  <p className="text-xs text-ink-muted">Tổng đã thu</p>
                  <p className="mt-1 font-semibold text-success-700">{formatCurrency(st.collected)}</p>
                </div>
                <div className="rounded-lg border border-line p-2.5">
                  <p className="text-xs text-ink-muted">Còn phải thu</p>
                  <p className="mt-1 font-semibold text-warning-700">{formatCurrency(st.receivable)}</p>
                </div>
              </div>

              <ul className="mt-3 flex flex-col gap-1.5 text-[13px] text-ink-secondary">
                <li className="flex items-start gap-2">
                  <MapPin size={14} className="mt-0.5 shrink-0" aria-hidden /> {b.address}
                </li>
                <li className="flex flex-wrap items-center gap-x-4 gap-y-1">
                  <span className="inline-flex items-center gap-2">
                    <Phone size={14} aria-hidden /> {b.phone}
                  </span>
                  <span className="inline-flex items-center gap-2">
                    <Mail size={14} aria-hidden /> {b.email}
                  </span>
                </li>
                <li className="flex items-center gap-2">
                  <Clock size={14} aria-hidden /> {b.openHours}
                </li>
                <li className="flex items-center gap-2">
                  <UserRound size={14} aria-hidden /> Quản lý: {mgr ? <b className="font-medium text-ink">{mgr.fullName}</b> : <span className="text-warning-700">Chưa có</span>}
                </li>
                <li className="flex items-start gap-2">
                  <Landmark size={14} className="mt-0.5 shrink-0" aria-hidden />
                  {b.bank ? (
                    <span>
                      {b.bank.bank} · <span className="tabular">{b.bank.account}</span> · {b.bank.holder}
                      <span className="ml-1 text-xs text-ink-muted">(mã hóa đơn {b.invoicePrefix}-…)</span>
                    </span>
                  ) : (
                    <span className="text-warning-700">Chưa khai báo tài khoản nhận học phí — không sinh được VietQR</span>
                  )}
                </li>
                {b.note && <li className="rounded-lg bg-primary-50 px-2.5 py-1.5 text-xs text-ink-secondary">{b.note}</li>}
              </ul>

              {editable && (
                <div className="mt-auto flex flex-wrap gap-1.5 pt-3">
                  <Button action="edit" size="sm" onClick={() => setEditing(b)} />
                  {b.status === 'preparing' && (
                    <Button icon={CalendarClock} size="sm" variant="soft" tone="success" onClick={() => setStatus(b, 'active')}>
                      Khai trương
                    </Button>
                  )}
                  {b.status === 'active' && (
                    <Button size="sm" variant="ghost" tone="danger" icon={Building2} onClick={() => setStatus(b, 'closed')}>
                      Đóng chi nhánh
                    </Button>
                  )}
                  {b.status === 'closed' && (
                    <Button size="sm" variant="ghost" icon={Building2} onClick={() => setStatus(b, 'active')}>
                      Mở lại
                    </Button>
                  )}
                </div>
              )}
              <div className="flex flex-wrap gap-1.5 pt-2">
                {b.status === 'active' && can('students.profiles') && (
                  <Button size="sm" variant="outline" icon={GraduationCap} onClick={() => { setBranch(b.id); navigate('/admin/students/profiles'); }}>
                    Học viên
                  </Button>
                )}
                {b.status !== 'closed' && can('hr.teachers') && (
                  <Button size="sm" variant="outline" icon={School} onClick={() => { setBranch(b.id); navigate('/admin/hr/teachers'); }}>
                    Giáo viên
                  </Button>
                )}
                {b.status !== 'closed' && can('academic.rooms') && (
                  <Button size="sm" variant="outline" icon={DoorOpen} onClick={() => { setBranch(b.id); navigate('/admin/academic/rooms'); }}>
                    Phòng học
                  </Button>
                )}
                {b.status !== 'closed' && can('hr.employees') && (
                  <Button size="sm" variant="outline" icon={Users} onClick={() => { setBranch(b.id); navigate('/admin/hr/employees'); }}>
                    Nhân sự
                  </Button>
                )}
                {b.status === 'active' && can('finance.debts') && (
                  <Button size="sm" variant="outline" icon={Wallet} onClick={() => { setBranch(b.id); navigate('/admin/finance/debts'); }}>
                    Công nợ
                  </Button>
                )}
              </div>
            </article>
          );
        })}
        {!visibleBranches.length && <p className="rounded-xl border border-dashed border-line p-8 text-center text-sm text-ink-muted md:col-span-2">Không tìm thấy chi nhánh phù hợp.</p>}
      </div>

      <ModalForm<FormValues>
        key={editing === 'new' ? 'new' : (editing?.id ?? 'none')}
        open={Boolean(editing)}
        onClose={() => setEditing(null)}
        title={editing === 'new' ? 'Thêm chi nhánh' : `Sửa chi nhánh ${editing ? (editing as BranchProfile).name : ''}`}
        columns={2}
        width={760}
        submitAction="save"
        successMessage="Đã lưu chi nhánh"
        initialValues={
          editing && editing !== 'new'
            ? { ...editing, openedAt: dayjs(editing.openedAt), bankName: editing.bank?.bank, bankAccount: editing.bank?.account, bankHolder: editing.bank?.holder }
            : { status: 'preparing', openedAt: dayjs().add(30, 'day'), openHours: 'T2–T7 08:00–21:30' }
        }
        onSubmit={(v) => {
          const code = v.code.trim().toUpperCase();
          if (editing === 'new' && branches.some((b) => b.code === code)) throw new Error(`Mã ${code} đã dùng cho chi nhánh khác`);
          const { bankName, bankAccount, bankHolder, ...rest } = v;
          if ((bankName || bankAccount || bankHolder) && (!bankName?.trim() || !bankAccount?.trim() || !bankHolder?.trim())) {
            throw new Error('Nếu khai báo tài khoản nhận học phí, cần nhập đủ ngân hàng, số tài khoản và chủ tài khoản.');
          }
          saveBranch(
            {
              ...rest,
              code,
              id: editing === 'new' ? code.toLowerCase() : (editing as BranchProfile).id,
              openedAt: v.openedAt.format('YYYY-MM-DD'),
              invoicePrefix: (v.invoicePrefix || code).toUpperCase(),
              bank: bankName && bankAccount ? { bank: bankName, account: bankAccount, holder: (bankHolder ?? '').toUpperCase() } : undefined,
            },
            actor,
          );
        }}
      >
        <Form.Item name="name" label="Tên chi nhánh" rules={[{ required: true, message: 'Nhập tên' }]}>
          <TextInput placeholder="Thủ Đức" />
        </Form.Item>
        <Form.Item name="code" label="Mã (2–4 ký tự)" rules={[{ required: true, message: 'Nhập mã' }, { pattern: /^[A-Za-z0-9]{2,4}$/, message: '2–4 chữ / số' }]}>
          <TextInput placeholder="TD" disabled={editing !== 'new'} />
        </Form.Item>
        <Form.Item name="address" label="Địa chỉ" rules={[{ required: true, message: 'Nhập địa chỉ' }]} className="md:col-span-2">
          <TextInput icon={MapPin} />
        </Form.Item>
        <Form.Item name="phone" label="Điện thoại" rules={[{ required: true, message: 'Nhập SĐT' }]}>
          <TextInput type="tel" icon={Phone} />
        </Form.Item>
        <Form.Item name="email" label="Email" rules={[{ required: true, type: 'email', message: 'Email không hợp lệ' }]}>
          <TextInput type="email" icon={Mail} />
        </Form.Item>
        <Form.Item name="managerId" label="Quản lý chi nhánh">
          <Select placeholder="Chọn quản lý" options={managers.map((e) => ({ value: e.id, label: `${e.fullName} · ${e.position}` }))} />
        </Form.Item>
        <Form.Item name="openHours" label="Giờ mở cửa" rules={[{ required: true }]}>
          <TextInput icon={Clock} />
        </Form.Item>
        <Form.Item name="openedAt" label="Ngày khai trương" rules={[{ required: true }]}>
          <DateInput />
        </Form.Item>
        <Form.Item name="invoicePrefix" label="Tiền tố số hóa đơn" tooltip="Vd CG → HD-CG-0001. Bỏ trống = theo mã chi nhánh" rules={[{ pattern: /^[A-Za-z0-9]{2,4}$/, message: '2–4 chữ / số' }]}>
          <TextInput placeholder="Theo mã chi nhánh" />
        </Form.Item>
        <p className="mb-2 text-sm font-semibold text-ink md:col-span-2">Tài khoản nhận học phí (VietQR)</p>
        <Form.Item name="bankName" label="Ngân hàng">
          <TextInput icon={Landmark} placeholder="Vietcombank" />
        </Form.Item>
        <Form.Item name="bankAccount" label="Số tài khoản" rules={[{ pattern: /^\d{6,20}$/, message: '6–20 chữ số' }]}>
          <TextInput inputMode="numeric" />
        </Form.Item>
        <Form.Item name="bankHolder" label="Chủ tài khoản" className="md:col-span-2">
          <TextInput placeholder="CONG TY TNHH ..." />
        </Form.Item>
        <Form.Item name="note" label="Ghi chú" className="md:col-span-2">
          <TextArea rows={2} maxLength={500} />
        </Form.Item>
      </ModalForm>
      {confirmDialog}
    </>
  );
}

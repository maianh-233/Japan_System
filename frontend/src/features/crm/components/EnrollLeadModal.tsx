import { useMemo, useRef, useState } from 'react';
import { Checkbox, Modal, Radio, Steps } from 'antd';
import dayjs from 'dayjs';
import { CheckCircle2, ClipboardCheck, GraduationCap, Receipt, Sparkles, User, Users } from 'lucide-react';
import { useNavigate } from 'react-router-dom';
import { Button, DateInput, NumberInput, Select, StatusTag, TextInput } from '@/components/common';
import { useClassStore } from '@/features/academic/classes/classStore';
import { CLASS_STATUS, scheduleLabel } from '@/features/academic/classes/constants';
import type { ClassInfo } from '@/features/academic/classes/types';
import { useCourseStore } from '@/features/academic/courses/courseStore';
import type { Course } from '@/features/academic/courses/types';
import { ACTIVE_ENROLLMENT, PAYMENT_METHODS } from '@/features/student/constants';
import { useStudentStore } from '@/features/student/studentStore';
import type { Invoice, PaymentMethod } from '@/features/student/types';
import { useAuth } from '@/hooks/useAuth';
import { useBreakpoint } from '@/hooks/useBreakpoint';
import { cn } from '@/lib/cn';
import { formatCurrency } from '@/lib/format';
import { useAdmissionStore } from '../admission/admissionStore';
import { courseLabel, RELATIONSHIPS } from '../constants';
import { USE_API } from '@/lib/api/config';
import { enrollLead, errorText } from '../crmApi';
import { useCrmStore } from '../crmStore';
import type { Lead, Relationship, Student } from '../types';

/**
 * CHỐT & GHI DANH — gom 3 việc vốn nằm ở 3 trang thành 1 luồng:
 *   1. Người học: chính lead / người thân mới / học viên đã liên kết
 *   2. Chọn lớp: ưu tiên lớp đúng khóa gợi ý (kết quả test xếp lớp → khóa quan tâm), còn chỗ, đúng chi nhánh
 *   3. Học phí: giảm giá, hạn đóng, thu ngay (tùy chọn)
 * → tạo hồ sơ học viên + ghi danh + hóa đơn (+ phiếu thu), lead → "Đã chốt".
 */

type Who = 'self' | 'other' | 'existing';

interface Props {
  lead: Lead;
  open: boolean;
  onClose: () => void;
  /** Mở sẵn với học viên đã liên kết (nút "Ghi danh lớp" trên thẻ học viên) */
  presetStudentId?: string;
}

const JLPT_INTEREST: Record<string, string> = { n5: 'N5', n4: 'N4', n3: 'N3', n2: 'N2', n1: 'N1' };

/** Khóa có khớp khóa quan tâm của lead không (n5..n1 = JLPT theo cấp độ đầu ra; còn lại theo nhóm khóa) */
export function courseMatchesInterest(course: Course | undefined, interest?: string): boolean {
  if (!course || !interest) return false;
  const level = JLPT_INTEREST[interest];
  return level ? course.category === 'jlpt' && course.levelTo === level : course.category === interest;
}

export function EnrollLeadModal({ lead, open, onClose, presetStudentId }: Props) {
  const { user } = useAuth();
  const navigate = useNavigate();
  const { isMobile } = useBreakpoint();
  const staffName = user?.fullName ?? 'Nhân viên';

  const students = useCrmStore((s) => s.students);
  const links = useCrmStore((s) => s.links);
  const { createStudentFromLead, updateLead, addConsultation } = useCrmStore.getState();
  const classes = useClassStore((s) => s.classes);
  const courses = useCourseStore((s) => s.courses);
  const enrollments = useStudentStore((s) => s.enrollments);
  const attempts = useAdmissionStore((s) => s.attempts);

  const linked = useMemo(
    () => links.filter((k) => k.leadId === lead.id).map((k) => ({ link: k, student: students.find((s) => s.id === k.studentId) })).filter((x): x is { link: typeof x.link; student: Student } => Boolean(x.student)),
    [links, students, lead.id],
  );
  const hasSelf = linked.some((x) => x.link.relationship === 'self');

  // Kết quả test xếp lớp gần nhất → gợi ý khóa (ưu tiên hơn "khóa quan tâm" nhập tay)
  const lastTest = useMemo(
    () => attempts.filter((a) => a.leadId === lead.id && a.status === 'done').sort((a, b) => (b.submittedAt ?? '').localeCompare(a.submittedAt ?? ''))[0],
    [attempts, lead.id],
  );
  const suggestion = lastTest?.suggestedCourse ?? lead.interest;

  // ---------------- State ----------------
  const [step, setStep] = useState(0);
  const [who, setWho] = useState<Who>(presetStudentId ? 'existing' : hasSelf ? 'other' : 'self');
  const [existingId, setExistingId] = useState<string | undefined>(presetStudentId ?? linked[0]?.student.id);
  const [fullName, setFullName] = useState('');
  const [relationship, setRelationship] = useState<Exclude<Relationship, 'self'>>('child');
  const [dob, setDob] = useState<dayjs.Dayjs | null>(null);
  const [phone, setPhone] = useState('');
  const [classId, setClassId] = useState<string | undefined>();
  const [showAll, setShowAll] = useState(false);
  const [discount, setDiscount] = useState<number>(0);
  const [dueDate, setDueDate] = useState<dayjs.Dayjs | null>(dayjs().add(7, 'day'));
  const [collectNow, setCollectNow] = useState(false);
  const [paid, setPaid] = useState<number>(0);
  const [method, setMethod] = useState<PaymentMethod>('transfer');
  const [error, setError] = useState<string>();
  const [result, setResult] = useState<{ student: Student; cls?: ClassInfo; invoice?: Invoice; paid: number }>();
  const [busy, setBusy] = useState(false);
  /** 1 lần chốt = 1 khóa chống gửi trùng; lỗi → khóa mới cho lần thử sau */
  const idemKey = useRef(crypto.randomUUID());

  const studentIdForCheck = who === 'existing' ? existingId : undefined;

  // ---------------- Lớp ----------------
  const seatsOf = (c: ClassInfo) => c.maxStudents - enrollments.filter((e) => e.classId === c.id && ACTIVE_ENROLLMENT.includes(e.status)).length;
  const classRows = useMemo(() => {
    return classes
      .filter((c) => c.branchId === lead.branchId && (c.status === 'enrolling' || c.status === 'ongoing'))
      .map((c) => {
        const course = courses.find((x) => x.id === c.courseId);
        const already = Boolean(studentIdForCheck && enrollments.some((e) => e.classId === c.id && e.studentId === studentIdForCheck && ACTIVE_ENROLLMENT.includes(e.status)));
        return { cls: c, match: courseMatchesInterest(course, suggestion), seats: seatsOf(c), already };
      })
      .sort((a, b) => Number(b.match) - Number(a.match) || Number(b.cls.status === 'enrolling') - Number(a.cls.status === 'enrolling') || b.seats - a.seats);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [classes, courses, enrollments, lead.branchId, suggestion, studentIdForCheck]);
  const matched = classRows.filter((r) => r.match);
  const visibleClasses = showAll || !matched.length ? classRows : matched;
  const cls = classes.find((c) => c.id === classId);
  const net = Math.max(0, (cls?.fee ?? 0) - (discount || 0));

  // ---------------- Điều hướng bước ----------------
  const validateStep = (): string | undefined => {
    if (step === 0) {
      if (who === 'other' && !fullName.trim()) return 'Nhập họ tên học viên';
      if (who === 'other' && phone && !/^0\d{9}$/.test(phone)) return 'SĐT gồm 10 số, bắt đầu bằng 0';
      if (who === 'existing' && !existingId) return 'Chọn học viên';
    }
    if (step === 1 && classId) {
      const row = classRows.find((r) => r.cls.id === classId);
      if (row?.already) return 'Học viên đã học lớp này';
      if (row && row.seats <= 0) return 'Lớp đã đủ sĩ số';
    }
    if (step === 2 && cls) {
      if (discount < 0 || discount > cls.fee) return 'Giảm giá không hợp lệ';
      if (!dueDate) return 'Chọn hạn đóng học phí';
      if (collectNow && (paid <= 0 || paid > net)) return `Số tiền thu từ 1 đến ${formatCurrency(net)}`;
    }
    return undefined;
  };

  const next = () => {
    const err = validateStep();
    setError(err);
    if (err) return;
    // Chưa chọn lớp → bỏ qua bước học phí
    if (step === 1 && !classId) return finish();
    if (step === 2) return finish();
    setStep(step + 1);
  };

  /** Chế độ API: server tạo HV + ghi danh + liên kết + chốt lead (CRM-07); ⏳ hóa đơn / thu tiền còn giả đến giai đoạn 19 */
  const finishApi = async () => {
    setBusy(true);
    try {
      const r = await enrollLead(
        lead.id,
        {
          who: who === 'existing' ? 'linked' : who,
          studentId: who === 'existing' ? existingId : undefined,
          student: who === 'other' ? { fullName: fullName.trim(), phone: phone || undefined, dob: dob?.format('YYYY-MM-DD'), relationship } : undefined,
          classId: cls?.id,
          discount: cls ? discount || 0 : 0,
          dueDate: cls ? dueDate?.format('YYYY-MM-DD') : undefined,
        },
        idemKey.current,
      );
      const student = useCrmStore.getState().students.find((s) => s.id === r.studentId) ?? {
        id: r.studentId,
        code: r.studentCode,
        fullName: r.studentName,
        branchId: lead.branchId,
        joinedAt: new Date().toISOString(),
        hidden: false,
      };
      const invoice = r.enrollmentId ? useStudentStore.getState().invoices.find((i) => i.enrollmentId === r.enrollmentId) : undefined;
      const paidNow = invoice && collectNow ? paid : 0;
      if (invoice && paidNow > 0)
        useStudentStore.getState().addPayment(invoice.id, { amount: paidNow, method, paidAt: new Date().toISOString(), collector: staffName });
      setResult({ student, cls, invoice, paid: paidNow });
    } catch (e) {
      idemKey.current = crypto.randomUUID();
      setError(errorText(e, 'Không chốt được lead, vui lòng thử lại'));
    } finally {
      setBusy(false);
    }
  };

  const finish = () => {
    if (USE_API) return void finishApi();
    // 1. Người học
    let student: Student;
    if (who === 'existing') {
      student = students.find((s) => s.id === existingId)!;
    } else {
      student = createStudentFromLead(
        lead.id,
        who === 'self'
          ? { fullName: lead.fullName, phone: lead.phone, relationship: 'self' }
          : { fullName: fullName.trim(), phone: phone || undefined, dob: dob?.format('YYYY-MM-DD'), relationship },
        staffName,
      );
    }

    // 2. Ghi danh + hóa đơn  3. Thu ngay
    let invoice: Invoice | undefined;
    const paidNow = cls && collectNow ? paid : 0;
    if (cls) {
      const r = useStudentStore.getState().enroll(student.id, cls.id, { discount: discount || 0, dueDate: dueDate?.format('YYYY-MM-DD') });
      invoice = r?.invoice;
      if (invoice && paidNow > 0) {
        useStudentStore.getState().addPayment(invoice.id, { amount: paidNow, method, paidAt: new Date().toISOString(), collector: staffName });
      }
    }

    // Ghi lịch sử tư vấn + chốt lead
    if (cls) {
      addConsultation({
        leadId: lead.id,
        channel: 'meeting',
        result: 'enrolled',
        content: `Ghi danh ${student.fullName} vào lớp ${cls.code} — học phí ${formatCurrency(cls.fee)}${discount ? `, giảm ${formatCurrency(discount)}` : ''}${paidNow ? `, đã thu ${formatCurrency(paidNow)} (${PAYMENT_METHODS[method]})` : ''}.`,
        staffName,
        system: true,
      });
    }
    updateLead(lead.id, { status: 'won', lostReason: undefined });
    setResult({ student, cls, invoice, paid: paidNow });
  };

  const close = () => {
    onClose();
    // Reset để lần mở sau bắt đầu lại từ đầu
    setTimeout(() => {
      setStep(0);
      setResult(undefined);
      setClassId(undefined);
      setDiscount(0);
      setCollectNow(false);
      setPaid(0);
      setError(undefined);
      setFullName('');
      setPhone('');
      setDob(null);
      idemKey.current = crypto.randomUUID();
    }, 300);
  };

  // ---------------- Giao diện ----------------
  const stepItems = [{ title: 'Người học' }, { title: 'Chọn lớp' }, { title: 'Học phí' }];

  const footer = result ? (
    <div className="flex flex-wrap justify-end gap-2">
      <Button icon={User} variant="outline" onClick={() => { close(); navigate(`/admin/students/profiles?open=${result.student.id}`); }}>
        Xem hồ sơ học viên
      </Button>
      {result.invoice && (
        <Button icon={Receipt} variant="outline" onClick={() => { close(); navigate(`/admin/finance/invoices?open=${result.invoice!.id}`); }}>
          Xem hóa đơn
        </Button>
      )}
      <Button icon={CheckCircle2} onClick={close}>
        Xong
      </Button>
    </div>
  ) : (
    <div className="flex flex-wrap items-center justify-between gap-2">
      <span className="text-[13px] text-danger-600">{error}</span>
      <div className="flex gap-2">
        {step > 0 ? (
          <Button action="back" onClick={() => { setError(undefined); setStep(step - 1); }}>
            Quay lại
          </Button>
        ) : (
          <Button action="cancel" onClick={close} />
        )}
        <Button icon={step === 2 || (step === 1 && !classId) ? CheckCircle2 : GraduationCap} onClick={next} loading={busy}>
          {step === 0 ? 'Tiếp: chọn lớp' : step === 1 ? (classId ? 'Tiếp: học phí' : 'Tạo hồ sơ, chưa xếp lớp') : 'Hoàn tất ghi danh'}
        </Button>
      </div>
    </div>
  );

  return (
    <Modal open={open} onCancel={close} width={isMobile ? '100%' : 720} title={`Chốt & ghi danh — ${lead.fullName}`} footer={footer} destroyOnHidden>
      {result ? (
        <ResultView result={result} />
      ) : (
        <>
          <Steps size="small" current={step} items={stepItems} className="!mb-5" />

          {step === 0 && (
            <div className="flex flex-col gap-3">
              <Radio.Group value={who} onChange={(e) => { setWho(e.target.value as Who); setError(undefined); }} className="flex flex-col gap-2">
                <Radio value="self" disabled={hasSelf}>
                  Chính người liên hệ ({lead.fullName}){hasSelf ? ' — đã có hồ sơ' : ''}
                </Radio>
                <Radio value="other">Người khác (con, em, người thân...) — người liên hệ là phụ huynh / người giám hộ</Radio>
                <Radio value="existing" disabled={!linked.length}>
                  Học viên đã liên kết với lead này{linked.length ? ` (${linked.length})` : ' — chưa có'}
                </Radio>
              </Radio.Group>

              {who === 'self' && (
                <p className="rounded-lg bg-page px-3 py-2 text-[13px] text-ink-secondary">
                  Tạo hồ sơ từ thông tin lead: <strong>{lead.fullName}</strong> · {lead.phone}
                </p>
              )}
              {who === 'other' && (
                <div className="grid gap-3 md:grid-cols-2">
                  <label className="flex flex-col gap-1 text-[13px]">
                    <span className="font-medium text-ink">Họ tên học viên *</span>
                    <TextInput icon={User} value={fullName} onChange={(e) => setFullName(e.target.value)} placeholder="Nguyễn Văn C" />
                  </label>
                  <label className="flex flex-col gap-1 text-[13px]">
                    <span className="font-medium text-ink">Quan hệ với người liên hệ</span>
                    <Select
                      value={relationship}
                      onChange={(v) => v && setRelationship(v as Exclude<Relationship, 'self'>)}
                      allowClear={false}
                      showSearch={false}
                      options={(Object.keys(RELATIONSHIPS) as Relationship[]).filter((k) => k !== 'self').map((k) => ({ value: k, label: RELATIONSHIPS[k] }))}
                    />
                  </label>
                  <label className="flex flex-col gap-1 text-[13px]">
                    <span className="font-medium text-ink">Ngày sinh</span>
                    <DateInput value={dob} onChange={(d) => setDob(d)} disabledDate={(d) => d.isAfter(dayjs())} />
                  </label>
                  <label className="flex flex-col gap-1 text-[13px]">
                    <span className="font-medium text-ink">SĐT học viên (nếu có)</span>
                    <TextInput type="tel" value={phone} onChange={(e) => setPhone(e.target.value)} />
                  </label>
                </div>
              )}
              {who === 'existing' && (
                <Select
                  value={existingId}
                  onChange={(v) => setExistingId(v as string)}
                  allowClear={false}
                  options={linked.map(({ link, student }) => ({ value: student.id, label: `${student.code} — ${student.fullName} (${RELATIONSHIPS[link.relationship]})` }))}
                />
              )}
            </div>
          )}

          {step === 1 && (
            <div className="flex flex-col gap-3">
              <div className="flex flex-wrap items-center gap-2 rounded-lg bg-primary-50 px-3 py-2 text-[13px] text-primary-800">
                {lastTest ? <ClipboardCheck size={16} aria-hidden /> : <Sparkles size={16} aria-hidden />}
                {suggestion ? (
                  <span>
                    Gợi ý khóa <b>{courseLabel(suggestion) ?? suggestion}</b>
                    {lastTest ? ` — theo test xếp lớp (${lastTest.suggestedLevel ?? ''})` : ' — theo khóa quan tâm của lead'}
                  </span>
                ) : (
                  <span>Lead chưa có khóa quan tâm / kết quả test — đang hiện mọi lớp đang mở của chi nhánh.</span>
                )}
              </div>

              {matched.length > 0 && (
                <Checkbox checked={showAll} onChange={(e) => setShowAll(e.target.checked)}>
                  Hiện cả lớp khóa khác ({classRows.length - matched.length})
                </Checkbox>
              )}

              <ul className="flex max-h-[46vh] flex-col gap-2 overflow-y-auto pr-1">
                {visibleClasses.map(({ cls: c, match, seats, already }) => {
                  const disabled = already || seats <= 0;
                  const selected = classId === c.id;
                  return (
                    <li key={c.id}>
                      <button
                        type="button"
                        disabled={disabled}
                        onClick={() => { setClassId(selected ? undefined : c.id); setError(undefined); }}
                        className={cn(
                          'w-full rounded-xl border p-3 text-left text-[13px] transition-colors',
                          selected ? 'border-primary-500 bg-primary-50 ring-1 ring-primary-500' : 'border-line bg-surface hover:border-primary-300',
                          disabled && 'cursor-not-allowed opacity-50 hover:border-line',
                        )}
                      >
                        <div className="flex flex-wrap items-center gap-2">
                          <span className="font-semibold text-ink">{c.code}</span>
                          <StatusTag tone={CLASS_STATUS[c.status].tone} icon={CLASS_STATUS[c.status].icon} size="sm">
                            {CLASS_STATUS[c.status].label}
                          </StatusTag>
                          {match && <StatusTag tone="success" icon={Sparkles} size="sm">Đúng khóa gợi ý</StatusTag>}
                          {already && <StatusTag tone="neutral" size="sm" icon={GraduationCap}>Đang học lớp này</StatusTag>}
                        </div>
                        <p className="mt-1 text-ink-secondary">{c.name}</p>
                        <p className="mt-1 flex flex-wrap gap-x-4 gap-y-0.5 text-ink-muted">
                          <span>{scheduleLabel(c)}</span>
                          <span>Khai giảng {dayjs(c.actualStartDate ?? c.plannedStartDate).format('DD/MM/YYYY')}</span>
                          <span className={cn('inline-flex items-center gap-1', seats <= 0 ? 'text-danger-600' : seats <= 3 && 'text-warning-700')}>
                            <Users size={13} aria-hidden /> {seats <= 0 ? 'Đã đủ sĩ số' : `Còn ${seats} chỗ`}
                          </span>
                          <span className="font-medium text-ink">{formatCurrency(c.fee)}</span>
                        </p>
                      </button>
                    </li>
                  );
                })}
                {!visibleClasses.length && <li className="rounded-xl border border-dashed border-line p-4 text-center text-[13px] text-ink-muted">Chi nhánh chưa có lớp nào đang mở.</li>}
              </ul>
              <p className="text-xs text-ink-muted">Chưa có lớp phù hợp? Bấm "Tạo hồ sơ, chưa xếp lớp" — xếp lớp sau ở hồ sơ học viên.</p>
            </div>
          )}

          {step === 2 && cls && (
            <div className="flex flex-col gap-4 text-[13px]">
              <div className="rounded-lg bg-page px-3 py-2">
                <p className="font-medium text-ink">
                  {cls.code} · {scheduleLabel(cls)}
                </p>
                <p className="text-ink-secondary">Học phí lớp: {formatCurrency(cls.fee)}</p>
              </div>
              <div className="grid gap-3 md:grid-cols-2">
                <label className="flex flex-col gap-1">
                  <span className="font-medium text-ink">Giảm giá</span>
                  <NumberInput currency min={0} max={cls.fee} value={discount} onChange={(v) => setDiscount(Number(v) || 0)} />
                </label>
                <label className="flex flex-col gap-1">
                  <span className="font-medium text-ink">Hạn đóng học phí *</span>
                  <DateInput value={dueDate} onChange={(d) => setDueDate(d)} disabledDate={(d) => d.isBefore(dayjs(), 'day')} />
                </label>
              </div>
              <Checkbox checked={collectNow} onChange={(e) => { setCollectNow(e.target.checked); if (e.target.checked && !paid) setPaid(net); }}>
                Thu tiền ngay tại quầy
              </Checkbox>
              {collectNow && (
                <div className="grid gap-3 md:grid-cols-2">
                  <label className="flex flex-col gap-1">
                    <span className="font-medium text-ink">Số tiền thu *</span>
                    <NumberInput currency min={0} max={net} value={paid} onChange={(v) => setPaid(Number(v) || 0)} />
                  </label>
                  <label className="flex flex-col gap-1">
                    <span className="font-medium text-ink">Hình thức</span>
                    <Select value={method} onChange={(v) => v && setMethod(v as PaymentMethod)} allowClear={false} showSearch={false} options={Object.entries(PAYMENT_METHODS).map(([value, label]) => ({ value, label }))} />
                  </label>
                </div>
              )}
              <dl className="grid grid-cols-[1fr_auto] gap-y-1 rounded-lg border border-line p-3">
                <dt className="text-ink-muted">Học phí</dt>
                <dd className="tabular text-right">{formatCurrency(cls.fee)}</dd>
                <dt className="text-ink-muted">Giảm giá</dt>
                <dd className="tabular text-right">−{formatCurrency(discount || 0)}</dd>
                <dt className="font-semibold text-ink">Phải đóng</dt>
                <dd className="tabular text-right font-semibold">{formatCurrency(net)}</dd>
                {collectNow && (
                  <>
                    <dt className="text-ink-muted">Thu ngay</dt>
                    <dd className="tabular text-right text-success-700">{formatCurrency(paid)}</dd>
                    <dt className="text-ink-muted">Còn nợ</dt>
                    <dd className="tabular text-right">{formatCurrency(Math.max(0, net - paid))}</dd>
                  </>
                )}
              </dl>
            </div>
          )}
        </>
      )}
    </Modal>
  );
}

function ResultView({ result }: { result: { student: Student; cls?: ClassInfo; invoice?: Invoice; paid: number } }) {
  const { student, cls, invoice, paid } = result;
  return (
    <div className="flex flex-col items-center gap-3 py-4 text-center text-[13px]">
      <span className="grid size-12 place-items-center rounded-full bg-success-50 text-success-600">
        <CheckCircle2 size={28} aria-hidden />
      </span>
      <p className="text-base font-semibold text-ink">{cls ? 'Đã ghi danh thành công' : 'Đã tạo hồ sơ học viên'}</p>
      <ul className="flex flex-col gap-1 text-ink-secondary">
        <li>
          Học viên <b className="text-ink">{student.code} — {student.fullName}</b>
        </li>
        {cls && (
          <li>
            Lớp <b className="text-ink">{cls.code}</b> · {scheduleLabel(cls)}
          </li>
        )}
        {invoice && (
          <li>
            Hóa đơn <b className="text-ink">{invoice.code}</b> · {formatCurrency(invoice.amount - invoice.discount)} · hạn {dayjs(invoice.dueDate).format('DD/MM/YYYY')}
            {paid > 0 && <> · đã thu {formatCurrency(paid)}</>}
          </li>
        )}
        <li>Lead chuyển sang "Đã chốt", lịch sử tư vấn đã được ghi.</li>
      </ul>
    </div>
  );
}

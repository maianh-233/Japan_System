import { useMemo, useState } from 'react';
import { useClassStore } from '@/features/academic/classes/classStore';
import { Form } from 'antd';
import { GraduationCap, Info, Link2, Unlink, UserPlus } from 'lucide-react';
import { useNavigate } from 'react-router-dom';
import { Button, EmptyState, ModalForm, Select, StatusTag, UserCell, useConfirm, useNotify } from '@/components/common';
import { fmtDate } from '@/lib/date';
import { useAuth } from '@/hooks/useAuth';
import { STUDY_STATUS } from '@/features/student/constants';
import { studyStatusOf, useStudentStore } from '@/features/student/studentStore';
import { RELATIONSHIPS } from '../constants';
import { USE_API } from '@/lib/api/config';
import { errorText, linkStudent as saveLink, unlinkStudent as removeLink } from '../crmApi';
import { useCrmStore } from '../crmStore';
import type { Lead, Relationship } from '../types';
import { EnrollLeadModal } from './EnrollLeadModal';

interface LinkValues {
  studentId: string;
  relationship: Relationship;
}

/**
 * Tab 3 — Hồ sơ học viên gắn với lead.
 * Lead là NGƯỜI LIÊN HỆ; học viên là NGƯỜI HỌC → có thể khác nhau (phụ huynh đăng ký cho con, 1 phụ huynh nhiều con).
 */
export function LinkedStudentsTab({ lead }: { lead: Lead }) {
  const { user } = useAuth();
  const notify = useNotify();
  const navigate = useNavigate();
  const { confirm, confirmDialog } = useConfirm();
  const students = useCrmStore((s) => s.students);
  const leads = useCrmStore((s) => s.leads);
  const links = useCrmStore((s) => s.links);
  const linkStudent = useCrmStore((s) => s.linkStudent);
  const unlinkStudent = useCrmStore((s) => s.unlinkStudent);
  const classes = useClassStore((s) => s.classes);
  const enrollments = useStudentStore((s) => s.enrollments);
  /** Lớp đang học (hoặc bảo lưu) của học viên */
  const currentClasses = (studentId: string) =>
    enrollments
      .filter((e) => e.studentId === studentId && (e.status === 'studying' || e.status === 'reserved'))
      .map((e) => classes.find((c) => c.id === e.classId)?.code)
      .filter(Boolean)
      .join(', ');
  const [enrolling, setEnrolling] = useState<{ studentId?: string } | null>(null);
  const [linking, setLinking] = useState(false);

  const linked = useMemo(
    () =>
      links
        .filter((k) => k.leadId === lead.id)
        .map((k) => ({ link: k, student: students.find((s) => s.id === k.studentId)! }))
        .filter((x) => x.student),
    [links, students, lead.id],
  );
  /** Người liên hệ khác của cùng học viên (vd: bố và mẹ cùng là lead) */
  const otherLeadsOf = (studentId: string) =>
    links
      .filter((k) => k.studentId === studentId && k.leadId !== lead.id)
      .map((k) => leads.find((l) => l.id === k.leadId))
      .filter((l): l is NonNullable<typeof l> => Boolean(l));
  const linkable = students.filter((s) => !linked.some((x) => x.student.id === s.id));

  const unlink = async (studentId: string, name: string) => {
    const ok = await confirm({
      title: `Gỡ liên kết học viên ${name}?`,
      description: 'Chỉ gỡ mối liên kết với lead này. Hồ sơ học viên, lớp học và học phí vẫn giữ nguyên.',
      tone: 'warning',
      confirmAction: 'reject',
      confirmText: 'Gỡ liên kết',
    });
    if (!ok) return;
    if (USE_API) {
      try {
        (await removeLink(lead.id, studentId)).forEach((w) => notify.toast.warning(w.message));
      } catch (e) {
        return notify.toast.error(errorText(e, 'Không gỡ được liên kết'));
      }
    } else unlinkStudent(lead.id, studentId);
    notify.toast.success('Đã gỡ liên kết');
  };

  return (
    <div className="flex flex-col gap-4">
      <div className="flex gap-2.5 rounded-lg border border-primary-200 bg-primary-50 px-3 py-2.5 text-[13px] text-primary-800">
        <Info size={16} className="mt-0.5 shrink-0" aria-hidden />
        <p>
          <strong>Lead là người liên hệ</strong>, học viên là người học — có thể khác nhau. Ví dụ phụ huynh đăng ký cho con: lead là phụ huynh, mỗi người con là một
          hồ sơ học viên riêng. Ẩn lead không ảnh hưởng đến các hồ sơ học viên bên dưới.
        </p>
      </div>

      <div className="flex flex-wrap items-center justify-between gap-2">
        <p className="text-[13px] text-ink-muted">{linked.length} học viên liên kết</p>
        <div className="flex flex-wrap gap-2">
          <Button icon={Link2} variant="outline" onClick={() => setLinking(true)} disabled={linkable.length === 0}>
            Liên kết học viên có sẵn
          </Button>
          <Button icon={UserPlus} onClick={() => setEnrolling({})}>
            Tạo hồ sơ & ghi danh
          </Button>
        </div>
      </div>

      {linked.length === 0 ? (
        <div className="rounded-[var(--radius-card)] border border-line">
          <EmptyState
            size="compact"
            icon={GraduationCap}
            title="Chưa có học viên liên kết"
            description="Khi lead đăng ký học, tạo hồ sơ học viên cho người học (chính lead hoặc người thân)."
          />
        </div>
      ) : (
        <ul className="grid grid-cols-1 gap-3 sm:grid-cols-2">
          {linked.map(({ link, student }) => {
            const st = STUDY_STATUS[studyStatusOf(enrollments.filter((e) => e.studentId === student.id))];
            const others = otherLeadsOf(student.id);
            return (
              <li key={student.id} className="flex flex-col gap-3 rounded-[var(--radius-card)] border border-line bg-surface p-4">
                <div className="flex items-start justify-between gap-2">
                  <UserCell name={student.fullName} subtitle={student.code} />
                  <StatusTag tone={st.tone} icon={GraduationCap} size="sm">
                    {st.label}
                  </StatusTag>
                </div>
                <dl className="grid grid-cols-2 gap-x-3 gap-y-1.5 text-[13px]">
                  <dt className="text-ink-muted">Quan hệ với lead</dt>
                  <dd className="font-medium text-ink">{RELATIONSHIPS[link.relationship]}</dd>
                  <dt className="text-ink-muted">Lớp</dt>
                  <dd className="truncate text-ink">{currentClasses(student.id) || '—'}</dd>
                  {student.dob && (
                    <>
                      <dt className="text-ink-muted">Ngày sinh</dt>
                      <dd className="text-ink">{fmtDate(student.dob)}</dd>
                    </>
                  )}
                  {others.length > 0 && (
                    <>
                      <dt className="text-ink-muted">Người liên hệ khác</dt>
                      <dd className="text-ink">{others.map((o) => `${o.fullName} (${o.code})`).join(', ')}</dd>
                    </>
                  )}
                </dl>
                <div className="flex justify-end gap-1 border-t border-line pt-3">
                  <Button icon={Unlink} size="sm" tone="neutral" variant="ghost" onClick={() => unlink(student.id, student.fullName)}>
                    Gỡ liên kết
                  </Button>
                  <Button icon={GraduationCap} size="sm" variant="outline" onClick={() => setEnrolling({ studentId: student.id })}>
                    Ghi danh lớp
                  </Button>
                  <Button
                    action="view"
                    size="sm"
                    variant="soft"
                    onClick={() => {
                      navigate(`/admin/students/profiles?open=${student.id}`);
                    }}
                  >
                    Xem hồ sơ
                  </Button>
                </div>
              </li>
            );
          })}
        </ul>
      )}

      {/* ===== Chốt & ghi danh (tạo hồ sơ + xếp lớp + hóa đơn) ===== */}
      {enrolling && <EnrollLeadModal lead={lead} open presetStudentId={enrolling.studentId} onClose={() => setEnrolling(null)} />}

      {/* ===== Liên kết học viên có sẵn (vd: anh/chị đang học, bố mẹ cùng liên hệ) ===== */}
      <ModalForm<LinkValues>
        open={linking}
        onClose={() => setLinking(false)}
        title="Liên kết học viên có sẵn"
        description="Dùng khi học viên đã có hồ sơ — vd: con đã học từ trước, hoặc bố và mẹ cùng là người liên hệ."
        submitAction="save"
        submitText="Liên kết"
        successMessage="Đã liên kết học viên"
        initialValues={{ relationship: 'child' }}
        onSubmit={async (v) => {
          if (USE_API) {
            // HV chưa có liên hệ chính → người này là liên hệ chính, nhận hóa đơn & thông báo
            const hasPrimary = links.some((k) => k.studentId === v.studentId && k.primary);
            const warnings = await saveLink(lead.id, v.studentId, { relationship: v.relationship, primary: !hasPrimary, billing: !hasPrimary, notify: true });
            warnings.forEach((w) => notify.toast.warning(w.message));
            return;
          }
          await new Promise((r) => setTimeout(r, 500));
          linkStudent(lead.id, v.studentId, v.relationship, user?.fullName ?? 'Nhân viên');
        }}
      >
        <Form.Item name="studentId" label="Học viên" rules={[{ required: true, message: 'Chọn học viên' }]}>
          <Select
            placeholder="Tìm theo tên, mã HV..."
            options={linkable.map((s) => ({ value: s.id, label: `${s.code} — ${s.fullName}${currentClasses(s.id) ? ` (${currentClasses(s.id)})` : ''}` }))}
          />
        </Form.Item>
        <Form.Item name="relationship" label="Quan hệ với người liên hệ" rules={[{ required: true }]}>
          <Select
            allowClear={false}
            showSearch={false}
            options={(Object.keys(RELATIONSHIPS) as Relationship[]).map((k) => ({ value: k, label: RELATIONSHIPS[k] }))}
          />
        </Form.Item>
      </ModalForm>
      {confirmDialog}
    </div>
  );
}

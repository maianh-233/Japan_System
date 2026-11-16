import { useState } from 'react';
import { Form, Steps } from 'antd';
import dayjs from 'dayjs';
import { Award, BadgeCheck, Check, Download, FileBadge, X } from 'lucide-react';
import { Button, EmptyState, ModalForm, PageHeader, Select, StatusTag, useNotify } from '@/components/common';
import { certificateChecks } from '@/features/academic/classes/detail/StudentActions';
import type { QLevel } from '@/features/academic/questions/types';
import { useCurriculumStore } from '@/features/academic/curriculum/curriculumStore';
import { levelOf } from '@/features/portal/quiz';
import { shortCode, useStudentId, useStudentLearning } from '@/features/portal/usePortal';
import { DOCS, JLPT_FEE, JLPT_SESSIONS, JLPT_STATUS, JLPT_VENUES, judge, SECTIONS, useJlptStore } from '@/features/student/jlpt/jlpt';
import { cn } from '@/lib/cn';
import { fmtDate } from '@/lib/date';
import { formatCurrency } from '@/lib/format';

const LEVELS: QLevel[] = ['N5', 'N4', 'N3', 'N2', 'N1'];

/** Cổng HV › Chứng nhận & JLPT: chứng nhận hoàn thành khóa (điều kiện cấp) + đăng ký / kết quả thi JLPT */
export default function StudentCertificatesPage() {
  const notify = useNotify();
  const studentId = useStudentId();
  const learning = useStudentLearning(studentId);
  const curricula = useCurriculumStore((s) => s.curricula);
  const regs = useJlptStore((s) => s.regs);
  const addReg = useJlptStore((s) => s.add);
  const [registering, setRegistering] = useState(false);

  const mine = regs.filter((r) => r.studentId === studentId).sort((a, b) => b.sessionId.localeCompare(a.sessionId));
  const today = dayjs().format('YYYY-MM-DD');
  const openSessions = JLPT_SESSIONS.filter((s) => today <= s.regClose && !mine.some((r) => r.sessionId === s.id && r.status !== 'cancelled'));
  const suggestLevel = levelOf(curricula.find((c) => c.id === learning.find((x) => x.enrollment.status === 'studying')?.cls.curriculumId)?.level);

  return (
    <>
      <PageHeader title="Chứng nhận & JLPT" subtitle="Chứng nhận hoàn thành khóa do trung tâm cấp khi đủ điều kiện. Đăng ký thi JLPT qua trung tâm và xem kết quả." />

      <h2 className="mb-2 text-base font-semibold text-ink">Chứng nhận hoàn thành khóa</h2>
      <div className="mb-6 grid gap-3 md:grid-cols-2">
        {learning
          .filter((x) => x.enrollment.status !== 'dropped' && x.enrollment.status !== 'transferred')
          .map(({ cls, enrollment, stats }) => {
            const cert = enrollment.certificate;
            const checks = certificateChecks(cls, stats);
            return (
              <article key={enrollment.id} className={cn('rounded-xl border p-4', cert ? 'border-success-100 bg-success-50/40' : 'border-line bg-surface')}>
                <div className="flex items-start justify-between gap-2">
                  <div>
                    <p className="font-semibold text-ink">{cls.courseLabel}</p>
                    <p className="text-xs text-ink-muted">
                      {shortCode(cls)} · {enrollment.status === 'completed' ? `hoàn thành ${fmtDate(enrollment.endedAt)}` : 'đang học'}
                    </p>
                  </div>
                  {cert ? (
                    <StatusTag tone="success" icon={BadgeCheck} size="sm">
                      Đã cấp
                    </StatusTag>
                  ) : (
                    <StatusTag tone={checks.every((c) => c.ok) ? 'info' : 'neutral'} icon={Award} size="sm">
                      {checks.every((c) => c.ok) ? 'Đủ điều kiện — chờ cấp' : 'Chưa đủ điều kiện'}
                    </StatusTag>
                  )}
                </div>
                {cert ? (
                  <div className="mt-3 text-[13px]">
                    <p>
                      Mã tra cứu: <code className="rounded bg-surface px-1.5 py-0.5">{cert.code}</code>
                    </p>
                    <p className="text-xs text-ink-muted">
                      Cấp {fmtDate(cert.issuedAt)} · đã gửi tới {cert.sentTo}
                    </p>
                    <Button icon={Download} size="sm" variant="soft" className="mt-2" onClick={() => notify.toast.info(`Tải ${cert.code}.pdf (giả lập)`)}>
                      Tải chứng nhận (PDF)
                    </Button>
                  </div>
                ) : (
                  <ul className="mt-3 flex flex-col gap-1 text-[13px]">
                    {checks.map((c) => (
                      <li key={c.label} className={cn('flex items-start gap-1.5', c.ok ? 'text-success-700' : 'text-ink-secondary')}>
                        {c.ok ? <Check size={14} className="mt-0.5 shrink-0" aria-hidden /> : <X size={14} className="mt-0.5 shrink-0 text-danger-600" aria-hidden />}
                        {c.label}
                      </li>
                    ))}
                  </ul>
                )}
              </article>
            );
          })}
      </div>

      <div className="mb-2 flex flex-wrap items-center justify-between gap-2">
        <h2 className="text-base font-semibold text-ink">Thi JLPT</h2>
        <Button icon={FileBadge} size="sm" disabled={!openSessions.length} onClick={() => setRegistering(true)} tooltip={!openSessions.length ? 'Chưa mở đợt đăng ký mới — trung tâm sẽ thông báo' : undefined}>
          Đăng ký thi
        </Button>
      </div>
      {!mine.length && <EmptyState icon={FileBadge} title="Bạn chưa đăng ký thi JLPT qua trung tâm" />}
      <div className="flex flex-col gap-3">
        {mine.map((r) => {
          const ses = JLPT_SESSIONS.find((s) => s.id === r.sessionId);
          const S = JLPT_STATUS[r.status];
          const res = judge(r.level, r.scores);
          return (
            <article key={r.id} className="rounded-xl border border-line bg-surface p-4">
              <div className="flex flex-wrap items-start justify-between gap-2">
                <div>
                  <p className="font-semibold text-ink">
                    JLPT {r.level} · {ses?.label}
                  </p>
                  <p className="text-xs text-ink-muted">
                    Thi {ses && fmtDate(ses.examDate)} · {r.venue}
                    {r.admitNo && ` · SBD ${r.admitNo}`}
                  </p>
                </div>
                <StatusTag tone={S.tone} icon={S.icon} size="sm">
                  {S.label}
                </StatusTag>
              </div>
              {r.status !== 'cancelled' && r.status !== 'done' && r.status !== 'absent' && (
                <>
                  <Steps
                    size="small"
                    className="mt-3"
                    current={S.step}
                    items={['Đăng ký', 'Đóng lệ phí', 'Nộp hội đồng', 'Phiếu dự thi', 'Kết quả'].map((t) => ({ title: t }))}
                  />
                  <p className="mt-2 flex flex-wrap gap-3 text-xs">
                    {DOCS.map((d) => (
                      <span key={d.key} className={r.docs[d.key] ? 'text-success-700' : 'text-danger-700'}>
                        {r.docs[d.key] ? '✓' : '✗'} {d.label}
                      </span>
                    ))}
                    {!r.paidAt && <span className="text-danger-700">Chưa đóng lệ phí {formatCurrency(JLPT_FEE[r.level])}</span>}
                  </p>
                </>
              )}
              {res && (
                <div className={cn('mt-3 rounded-lg p-3 text-[13px]', res.passed ? 'bg-success-50' : 'bg-danger-50')}>
                  <p className={cn('font-semibold', res.passed ? 'text-success-700' : 'text-danger-700')}>
                    {res.passed ? 'ĐỖ' : 'CHƯA ĐỖ'} · Tổng {res.total}/180
                    {res.failSections.length > 0 && ` · điểm liệt: ${res.failSections.join(', ')}`}
                  </p>
                  <p className="mt-1 flex flex-wrap gap-x-4 text-xs text-ink-secondary">
                    {SECTIONS[r.level].map((s, i) => (
                      <span key={s.name}>
                        {s.name}: <b className="tabular">{r.scores?.[i]}</b>/{s.max} (liệt &lt; {s.min})
                      </span>
                    ))}
                  </p>
                </div>
              )}
            </article>
          );
        })}
      </div>

      <ModalForm<{ sessionId: string; level: QLevel; venue: string }>
        open={registering}
        onClose={() => setRegistering(false)}
        title="Đăng ký thi JLPT qua trung tâm"
        description="Trung tâm nộp hồ sơ giúp bạn. Sau khi đăng ký: đóng lệ phí tại quầy / VietQR và nộp ảnh 3×4, CCCD."
        submitAction="send"
        submitText="Đăng ký"
        successMessage="Đã đăng ký — giáo vụ sẽ liên hệ hướng dẫn hồ sơ"
        initialValues={{ sessionId: openSessions[0]?.id, level: suggestLevel, venue: JLPT_VENUES[0] }}
        onSubmit={(v) => addReg({ studentId, sessionId: v.sessionId, level: v.level, venue: v.venue, status: 'registered', docs: { photo: false, idCard: false, form: false } })}
      >
        <Form.Item name="sessionId" label="Đợt thi" rules={[{ required: true }]}>
          <Select options={openSessions.map((s) => ({ value: s.id, label: `${s.label} · thi ${fmtDate(s.examDate)} · hạn ĐK ${fmtDate(s.regClose)}` }))} allowClear={false} />
        </Form.Item>
        <Form.Item name="level" label="Cấp độ" extra={`Gợi ý theo lớp đang học: ${suggestLevel}`} rules={[{ required: true }]}>
          <Select options={LEVELS.map((l) => ({ value: l, label: `${l} · lệ phí ${formatCurrency(JLPT_FEE[l])}` }))} allowClear={false} showSearch={false} />
        </Form.Item>
        <Form.Item name="venue" label="Địa điểm thi" rules={[{ required: true }]}>
          <Select options={JLPT_VENUES.map((v) => ({ value: v, label: v }))} allowClear={false} />
        </Form.Item>
      </ModalForm>
    </>
  );
}

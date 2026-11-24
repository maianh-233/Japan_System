import { useEffect, useState } from 'react';
import { Form, Steps } from 'antd';
import dayjs from 'dayjs';
import { Award, FileBadge } from 'lucide-react';
import { Button, EmptyState, ModalForm, PageHeader, Select, StatusTag, useNotify } from '@/components/common';
import type { QLevel } from '@/features/academic/questions/types';
import { useCurriculumStore } from '@/features/academic/curriculum/curriculumStore';
import { levelOf } from '@/features/portal/quiz';
import { useStudentId, useStudentLearning } from '@/features/portal/usePortal';
import { CERT_STAGE, certStage, DOCS, feeOf, JLPT_STATUS, judge, SECTIONS, useJlptStore, venuesOf } from '@/features/student/jlpt/jlpt';
import { loadMyJlpt, registerMyJlpt } from '@/features/student/jlpt/jlptApi';
import { USE_API } from '@/lib/api/config';
import { cn } from '@/lib/cn';
import { fmtDate } from '@/lib/date';
import { formatCurrency } from '@/lib/format';

const LEVELS: QLevel[] = ['N5', 'N4', 'N3', 'N2', 'N1'];

/**
 * Cổng HV › Chứng chỉ JLPT: đăng ký thi QUA trung tâm, theo dõi hồ sơ, kết quả, nhận chứng chỉ gốc.
 * (Chứng chỉ do hội đồng thi JLPT cấp — khác chứng nhận hoàn thành khóa của trung tâm.)
 */
export default function StudentJlptPage() {
  const studentId = useStudentId();
  const learning = useStudentLearning(studentId);
  const curricula = useCurriculumStore((s) => s.curricula);
  const regs = useJlptStore((s) => s.regs);
  const sessions = useJlptStore((s) => s.sessions);
  const addReg = useJlptStore((s) => s.add);
  const notify = useNotify();
  const [registering, setRegistering] = useState(false);

  // Chế độ API: kỳ thi + lượt đăng ký của HV (PH: con đang chọn) — server chỉ trả của tài khoản
  useEffect(() => {
    if (USE_API) void loadMyJlpt(studentId).catch((e: Error) => notify.error({ title: 'Không tải được thông tin JLPT', description: e.message }));
  }, [studentId, notify]);

  const examOf = (sid: string) => sessions.find((s) => s.id === sid)?.examDate ?? '';
  const mine = regs.filter((r) => r.studentId === studentId).sort((a, b) => examOf(b.sessionId).localeCompare(examOf(a.sessionId)));
  const today = dayjs().format('YYYY-MM-DD');
  const openSessions = sessions.filter((s) => today <= s.regClose && !mine.some((r) => r.sessionId === s.id && r.status !== 'cancelled'));
  const suggestLevel = levelOf(curricula.find((c) => c.id === learning.find((x) => x.enrollment.status === 'studying')?.cls.curriculumId)?.level);

  return (
    <>
      <PageHeader title="Chứng chỉ JLPT" subtitle="Đăng ký thi JLPT qua trung tâm, theo dõi hồ sơ & lệ phí, xem kết quả và nhận chứng chỉ gốc." />

      <div className="mb-3 flex flex-wrap items-center justify-between gap-2 rounded-[var(--radius-card)] border border-line bg-surface px-4 py-3 text-[13px] text-ink-secondary">
        <span>Kỳ thi tháng 7 & 12. Trung tâm hỗ trợ: nhận hồ sơ, thu lệ phí, nộp hội đồng thi, phát phiếu dự thi và <b>chứng chỉ gốc</b> khi bạn đỗ.</span>
        <Button icon={FileBadge} size="sm" disabled={!openSessions.length} onClick={() => setRegistering(true)} tooltip={!openSessions.length ? 'Chưa mở đợt đăng ký mới — trung tâm sẽ thông báo' : undefined}>
          Đăng ký thi
        </Button>
      </div>
      {!mine.length && <EmptyState icon={FileBadge} title="Bạn chưa đăng ký thi JLPT qua trung tâm" />}
      <div className="flex flex-col gap-3">
        {mine.map((r) => {
          const ses = sessions.find((s) => s.id === r.sessionId);
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
                    {!r.paidAt && <span className="text-danger-700">Chưa đóng lệ phí {formatCurrency(r.fee ?? feeOf(ses, r.level))}</span>}
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
              {res?.passed && (() => {
                const stage = certStage(r);
                return (
                  <div className="mt-3 flex flex-wrap items-center gap-2 rounded-lg border border-line px-3 py-2 text-[13px]">
                    <Award size={16} className="text-primary-600" aria-hidden />
                    <span className="font-medium text-ink">Chứng chỉ gốc JLPT {r.level}</span>
                    <StatusTag tone={CERT_STAGE[stage].tone} icon={Award} size="sm">
                      {CERT_STAGE[stage].label}
                    </StatusTag>
                    <span className="text-xs text-ink-muted">
                      {stage === 'waiting' && 'Hội đồng thi gửi về trung tâm khoảng 1–2 tháng sau khi có điểm — trung tâm sẽ báo bạn.'}
                      {stage === 'received' && `Số ${r.certificate!.no} · về ngày ${fmtDate(r.certificate!.receivedAt)} — mang CCCD đến quầy lễ tân để nhận.`}
                      {stage === 'handed' && `Số ${r.certificate!.no} · nhận ngày ${fmtDate(r.certificate!.handedAt)}`}
                    </span>
                  </div>
                );
              })()}
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
        initialValues={{ sessionId: openSessions[0]?.id, level: suggestLevel, venue: venuesOf(openSessions[0])[0] }}
        onSubmit={async (v) => {
          if (USE_API) return void (await registerMyJlpt(studentId, v.sessionId, v.level, v.venue));
          addReg({ studentId, sessionId: v.sessionId, level: v.level, venue: v.venue, status: 'registered', docs: { photo: false, idCard: false, form: false } });
        }}
      >
        <Form.Item name="sessionId" label="Đợt thi" rules={[{ required: true }]}>
          <Select options={openSessions.map((s) => ({ value: s.id, label: `${s.label} · thi ${fmtDate(s.examDate)} · hạn ĐK ${fmtDate(s.regClose)}` }))} allowClear={false} />
        </Form.Item>
        {/* Lệ phí / địa điểm theo đợt thi đang chọn */}
        <Form.Item noStyle shouldUpdate={(a, b) => a.sessionId !== b.sessionId}>
          {({ getFieldValue }) => {
            const ses = openSessions.find((s) => s.id === getFieldValue('sessionId'));
            return (
              <>
                <Form.Item name="level" label="Cấp độ" extra={`Gợi ý theo lớp đang học: ${suggestLevel}`} rules={[{ required: true }]}>
                  <Select options={LEVELS.map((l) => ({ value: l, label: `${l} · lệ phí ${formatCurrency(feeOf(ses, l))}` }))} allowClear={false} showSearch={false} />
                </Form.Item>
                <Form.Item name="venue" label="Địa điểm thi" rules={[{ required: true }]}>
                  <Select options={venuesOf(ses).map((v) => ({ value: v, label: v }))} allowClear={false} />
                </Form.Item>
              </>
            );
          }}
        </Form.Item>
      </ModalForm>
    </>
  );
}

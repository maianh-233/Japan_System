import { useEffect, useState } from 'react';
import { BadgeCheck, CircleAlert, Search } from 'lucide-react';
import { useNavigate, useParams } from 'react-router-dom';
import { Button, TextInput } from '@/components/common';
import { Logo } from '@/components/layout/Logo';
import { verifyCertificate, type CertVerification } from '@/features/student/certificateApi';
import { USE_API } from '@/lib/api/config';
import { fmtDate } from '@/lib/date';

/**
 * Trang XÁC THỰC CHỨNG NHẬN công khai (/verify/:code) — link trong email + mã QR trên chứng nhận (HV-15 bước 4).
 * Không cần đăng nhập; chỉ hiện thông tin in trên chứng nhận (không email / lý do nội bộ).
 */
export default function VerifyCertificatePage() {
  const { code = '' } = useParams();
  const navigate = useNavigate();
  const [input, setInput] = useState(code);
  const [result, setResult] = useState<CertVerification | null | undefined>(undefined);
  const [error, setError] = useState<string>();

  useEffect(() => {
    setInput(code);
    setResult(undefined);
    setError(undefined);
    if (!code) return;
    if (!USE_API) return setError('Trang tra cứu chỉ chạy khi kết nối máy chủ.');
    verifyCertificate(code).then(setResult, (e: Error) => setError(e.message));
  }, [code]);

  const go = () => input.trim() && navigate(`/verify/${encodeURIComponent(input.trim())}`);

  return (
    <main className="min-h-dvh bg-page px-4 py-6 sm:py-10">
      <div className="mx-auto flex w-full max-w-[640px] flex-col gap-4">
        <Logo className="mb-2" />
        <section className="rounded-2xl border border-line bg-surface p-5 sm:p-6">
          <h1 className="text-xl font-semibold text-ink">Xác thực chứng nhận hoàn thành khóa học</h1>
          <p className="mt-1 text-[13px] text-ink-secondary">Nhập mã in trên chứng nhận (vd: CN-2026-N4K11-0001) để kiểm tra chứng nhận có do trung tâm cấp không.</p>
          <div className="mt-4 flex gap-2">
            <TextInput value={input} onChange={(e) => setInput(e.target.value)} onPressEnter={go} placeholder="CN-2026-…" className="font-mono" />
            <Button icon={Search} onClick={go} disabled={!input.trim()}>
              Tra cứu
            </Button>
          </div>
        </section>

        {error && (
          <section className="flex items-start gap-3 rounded-2xl border border-danger-100 bg-surface p-5 text-ink">
            <CircleAlert className="mt-0.5 shrink-0 text-danger-600" size={20} aria-hidden />
            <p>{error}</p>
          </section>
        )}
        {code && !error && result === undefined && <p className="text-ink-muted">Đang kiểm tra…</p>}
        {result === null && (
          <section className="flex items-start gap-3 rounded-2xl border border-danger-100 bg-surface p-5 text-ink">
            <CircleAlert className="mt-0.5 shrink-0 text-danger-600" size={20} aria-hidden />
            <div>
              <p className="font-semibold">Không tìm thấy chứng nhận với mã này</p>
              <p className="text-[13px] text-ink-secondary">Kiểm tra lại mã. Nếu mã đúng như in trên chứng nhận, chứng nhận có thể không do trung tâm cấp.</p>
            </div>
          </section>
        )}
        {result && (
          <section className="flex items-start gap-3 rounded-2xl border border-success-100 bg-surface p-5 text-ink">
            <BadgeCheck className="mt-0.5 shrink-0 text-success-700" size={22} aria-hidden />
            <div className="flex flex-col gap-1">
              <p className="font-semibold text-success-700">Chứng nhận hợp lệ</p>
              <dl className="grid grid-cols-[110px_1fr] gap-x-3 gap-y-1 text-[13px]">
                <dt className="text-ink-muted">Mã</dt>
                <dd className="font-mono">{result.code}</dd>
                <dt className="text-ink-muted">Học viên</dt>
                <dd className="font-medium">{result.studentName}</dd>
                <dt className="text-ink-muted">Khóa / lớp</dt>
                <dd>
                  {result.className ?? '—'}
                  {result.classCode && ` (${result.classCode})`}
                  {result.level && ` · ${result.level}`}
                </dd>
                <dt className="text-ink-muted">Ngày cấp</dt>
                <dd>{fmtDate(result.issuedAt)}</dd>
              </dl>
            </div>
          </section>
        )}
      </div>
    </main>
  );
}

import { useEffect, useRef, useState } from 'react';
import { Modal } from 'antd';
import dayjs from 'dayjs';
import { CameraOff, Check, CheckCircle2, X, Keyboard, RotateCcw, ScanLine, XCircle } from 'lucide-react';
import { Button, TextInput } from '@/components/common';
import { useQrCheckIn, type ScanResult } from '@/features/academic/classes/attendanceQr';
import { useBreakpoint } from '@/hooks/useBreakpoint';
import { cn } from '@/lib/cn';
import { USE_API } from '@/lib/api/config';
import * as portalApi from '@/features/portal/portalApi';

interface Props {
  open: boolean;
  onClose: () => void;
  studentId: string;
}

type Mode = 'camera' | 'code';

/** Cổng HV › Quét QR điểm danh: camera (BarcodeDetector của trình duyệt, không có thì dùng jsQR) hoặc nhập mã 6 ký tự */
export function ScanAttendanceModal({ open, onClose, studentId }: Props) {
  const { isMobile } = useBreakpoint();
  const checkIn = useQrCheckIn(studentId);
  const [mode, setMode] = useState<Mode>('camera');
  const [result, setResult] = useState<ScanResult | null>(null);
  const [code, setCode] = useState('');

  const handle = (text: string) => {
    if (!USE_API) return setResult(checkIn(text));
    // Chế độ API: server kiểm mã, giờ mở QR, HV có trong danh sách (kể cả học bù), đi muộn
    portalApi.scanAttendance(text, studentId).then(
      (r) =>
        setResult({
          ok: true,
          title: r.status === 'late' ? 'Điểm danh thành công — đi muộn' : 'Điểm danh thành công',
          description: `${r.classCode.replace(/^GV[VN]*_/, '')} · Buổi ${r.no || '(học thêm)'}${r.checkInAt ? ` · vào lớp lúc ${r.checkInAt}` : ''}`,
        }),
      (e: Error) => setResult({ ok: false, title: 'Không điểm danh được', description: e.message }),
    );
  };
  const reset = () => {
    setResult(null);
    setCode('');
  };

  return (
    <Modal open={open} onCancel={onClose} footer={null} width={isMobile ? '100%' : 440} title="Quét QR điểm danh" destroyOnHidden centered>
      {result ? (
        <div className="flex flex-col items-center gap-3 py-4 text-center">
          {result.ok ? <CheckCircle2 size={64} className="text-success-600" aria-hidden /> : <XCircle size={64} className="text-danger-600" aria-hidden />}
          <p className={cn('text-lg font-semibold', result.ok ? 'text-success-700' : 'text-danger-700')}>{result.title}</p>
          {result.description && <p className="text-[13px] text-ink-secondary">{result.description}</p>}
          <p className="text-xs text-ink-muted">{dayjs().format('HH:mm · dddd DD/MM/YYYY')}</p>
          <div className="mt-2 flex gap-2">
            {!result.ok && (
              <Button icon={RotateCcw} variant="outline" onClick={reset}>
                Thử lại
              </Button>
            )}
            <Button icon={result.ok ? Check : X} onClick={onClose}>
              {result.ok ? 'Xong' : 'Đóng'}
            </Button>
          </div>
        </div>
      ) : (
        <div className="flex flex-col gap-3">
          <div className="inline-flex self-center rounded-xl border border-line bg-page p-1" role="tablist">
            {(
              [
                ['camera', 'Quét mã', ScanLine],
                ['code', 'Nhập mã', Keyboard],
              ] as const
            ).map(([k, label, Icon]) => (
              <button
                key={k}
                type="button"
                role="tab"
                aria-selected={mode === k}
                onClick={() => setMode(k)}
                className={cn('inline-flex h-9 items-center gap-1.5 rounded-lg px-4 text-[13px] font-medium', mode === k ? 'bg-surface text-primary-700 shadow-sm' : 'text-ink-secondary')}
              >
                <Icon size={15} aria-hidden /> {label}
              </button>
            ))}
          </div>

          {mode === 'camera' ? (
            <>
              <QrCamera onResult={handle} onFallback={() => setMode('code')} />
              <p className="text-center text-[13px] text-ink-muted">Hướng camera vào mã QR giáo viên đang chiếu trên lớp.</p>
            </>
          ) : (
            <form
              className="flex flex-col gap-3 py-2"
              onSubmit={(e) => {
                e.preventDefault();
                if (code.trim().length === 6) handle(code);
              }}
            >
              <p className="text-center text-[13px] text-ink-secondary">Nhập 6 ký tự in dưới mã QR trên màn hình lớp học.</p>
              <TextInput
                autoFocus
                value={code}
                onChange={(e) => setCode(e.target.value.replace(/[^a-z0-9]/gi, '').toUpperCase().slice(0, 6))}
                placeholder="VD: 4F7K2Q"
                className="!text-center !font-mono !text-2xl !tracking-[0.3em]"
                maxLength={6}
                inputMode="text"
                autoComplete="off"
                aria-label="Mã điểm danh"
              />
              <Button icon={CheckCircle2} htmlType="submit" block disabled={code.length !== 6}>
                Điểm danh
              </Button>
            </form>
          )}
        </div>
      )}
    </Modal>
  );
}

/** Khung camera quét QR */
function QrCamera({ onResult, onFallback }: { onResult: (text: string) => void; onFallback: () => void }) {
  const videoRef = useRef<HTMLVideoElement>(null);
  // Giữ callback mới nhất mà không khởi động lại camera
  const resultRef = useRef(onResult);
  resultRef.current = onResult;
  const [error, setError] = useState<string | null>(null);
  const [ready, setReady] = useState(false);

  useEffect(() => {
    let stream: MediaStream | undefined;
    let stopped = false;
    let timer: number | undefined;
    const canvas = document.createElement('canvas');
    const ctx = canvas.getContext('2d', { willReadFrequently: true });

    const start = async () => {
      if (!navigator.mediaDevices?.getUserMedia) {
        setError('Trình duyệt không hỗ trợ camera (cần mở trang bằng https). Hãy dùng “Nhập mã”.');
        return;
      }
      try {
        stream = await navigator.mediaDevices.getUserMedia({ video: { facingMode: { ideal: 'environment' } }, audio: false });
      } catch (e) {
        const name = (e as DOMException).name;
        setError(name === 'NotAllowedError' ? 'Bạn chưa cho phép dùng camera. Bật quyền camera cho trang này hoặc dùng “Nhập mã”.' : 'Không mở được camera. Hãy dùng “Nhập mã”.');
        return;
      }
      if (stopped) return stream.getTracks().forEach((t) => t.stop());
      const video = videoRef.current!;
      video.srcObject = stream;
      await video.play().catch(() => undefined);
      setReady(true);

      // Ưu tiên bộ đọc mã có sẵn của trình duyệt (Chrome Android, Safari macOS...), không có thì dùng jsQR
      type Detector = { detect: (v: HTMLVideoElement) => Promise<{ rawValue: string }[]> };
      const BD = (window as unknown as { BarcodeDetector?: new (o: { formats: string[] }) => Detector }).BarcodeDetector;
      const detector = BD ? new BD({ formats: ['qr_code'] }) : undefined;
      const jsQR = detector ? undefined : (await import('jsqr')).default;

      const tick = async () => {
        if (stopped) return;
        if (video.readyState >= 2) {
          let text: string | undefined;
          if (detector) {
            text = (await detector.detect(video).catch(() => []))[0]?.rawValue;
          } else if (jsQR && ctx) {
            const w = video.videoWidth;
            const h = video.videoHeight;
            // Thu nhỏ khung hình cho nhẹ máy
            const scale = Math.min(1, 640 / Math.max(w, h));
            canvas.width = Math.round(w * scale);
            canvas.height = Math.round(h * scale);
            ctx.drawImage(video, 0, 0, canvas.width, canvas.height);
            const img = ctx.getImageData(0, 0, canvas.width, canvas.height);
            text = jsQR(img.data, img.width, img.height, { inversionAttempts: 'dontInvert' })?.data;
          }
          if (text && !stopped) {
            stopped = true;
            navigator.vibrate?.(80);
            resultRef.current(text);
            return;
          }
        }
        timer = window.setTimeout(tick, 200);
      };
      tick();
    };
    start();

    return () => {
      stopped = true;
      window.clearTimeout(timer);
      stream?.getTracks().forEach((t) => t.stop());
    };
  }, []);

  if (error)
    return (
      <div className="flex aspect-square flex-col items-center justify-center gap-3 rounded-2xl bg-page p-6 text-center">
        <CameraOff size={40} className="text-ink-muted" aria-hidden />
        <p className="text-[13px] text-ink-secondary">{error}</p>
        <Button icon={Keyboard} variant="soft" onClick={onFallback}>
          Nhập mã
        </Button>
      </div>
    );

  return (
    <div className="relative aspect-square overflow-hidden rounded-2xl bg-black">
      <video ref={videoRef} playsInline muted className="h-full w-full object-cover" />
      {/* Khung ngắm */}
      <div className="pointer-events-none absolute inset-[15%]" aria-hidden>
        {['left-0 top-0 border-l-4 border-t-4 rounded-tl-xl', 'right-0 top-0 border-r-4 border-t-4 rounded-tr-xl', 'bottom-0 left-0 border-b-4 border-l-4 rounded-bl-xl', 'bottom-0 right-0 border-b-4 border-r-4 rounded-br-xl'].map((c) => (
          <span key={c} className={cn('absolute size-10 border-white', c)} />
        ))}
        {ready && <span className="absolute inset-x-2 top-1/2 h-0.5 animate-pulse bg-primary-400 shadow-[0_0_12px_2px] shadow-primary-400" />}
      </div>
      {!ready && <p className="absolute inset-0 grid place-items-center text-[13px] text-white/80">Đang mở camera…</p>}
    </div>
  );
}

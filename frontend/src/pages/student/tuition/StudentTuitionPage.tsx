import { PageHeader } from '@/components/common';
import { TuitionView } from '@/features/portal/components/TuitionView';
import { useStudentId } from '@/features/portal/usePortal';

/** Cổng HV › Học phí */
export default function StudentTuitionPage() {
  const studentId = useStudentId();
  return (
    <>
      <PageHeader title="Học phí" subtitle="Hóa đơn, lịch sử thanh toán. Thanh toán VietQR được đối soát tự động." />
      <TuitionView studentIds={[studentId]} />
    </>
  );
}

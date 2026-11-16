import { PageHeader } from '@/components/common';
import { TuitionView } from '@/features/portal/components/TuitionView';
import { useChildren } from '@/features/portal/usePortal';

/** Cổng PH › Học phí: gộp hóa đơn của TẤT CẢ các con (phụ huynh đóng chung) */
export default function ParentTuitionPage() {
  const children = useChildren();
  return (
    <>
      <PageHeader title="Học phí" subtitle={`Hóa đơn của ${children.map((c) => c.fullName.split(' ').pop()).join(' & ') || 'con'} · sự kiện thanh toán VietQR được mô phỏng và cập nhật số dư ngay.`} />
      <TuitionView studentIds={children.map((c) => c.id)} />
    </>
  );
}

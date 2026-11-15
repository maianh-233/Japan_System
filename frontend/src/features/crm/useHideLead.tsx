import { useConfirm, useNotify } from '@/components/common';
import { useCrmStore } from './crmStore';
import type { Lead } from './types';

/** Ẩn / bỏ ẩn lead có xác nhận. Học viên liên kết KHÔNG bị ẩn theo. */
export function useHideLead() {
  const { confirm, confirmDialog } = useConfirm();
  const notify = useNotify();
  const setHidden = useCrmStore((s) => s.setHidden);
  const links = useCrmStore((s) => s.links);

  const toggleHidden = async (lead: Lead) => {
    if (lead.hidden) {
      setHidden(lead.id, false);
      notify.toast.success(`Đã bỏ ẩn ${lead.fullName}`);
      return;
    }
    const studentCount = links.filter((k) => k.leadId === lead.id).length;
    const ok = await confirm({
      title: `Ẩn lead ${lead.fullName}?`,
      description: 'Lead sẽ không hiện trong danh sách mặc định. Có thể xem lại bằng bộ lọc “Hiện lead đã ẩn” và bỏ ẩn bất cứ lúc nào.',
      consequences: [
        studentCount > 0
          ? `${studentCount} hồ sơ học viên liên kết vẫn hiển thị và học bình thường`
          : 'Lead chưa có học viên liên kết',
        'Lịch sử tư vấn được giữ nguyên',
      ],
      tone: 'warning',
      confirmAction: 'save',
      confirmText: 'Ẩn lead',
    });
    if (!ok) return;
    setHidden(lead.id, true);
    notify.success({ title: 'Đã ẩn lead', description: lead.fullName });
  };

  return { toggleHidden, confirmDialog };
}

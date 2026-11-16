import { useEffect, useRef } from 'react';
import { useSearchParams } from 'react-router-dom';

/**
 * Mở thẳng chi tiết từ nơi khác qua URL: /admin/crm/leads?open=L-12
 * Đọc `?open=` → gọi `onOpen(id)` → xóa tham số khỏi URL (để F5 / quay lại không mở lại).
 * Dùng cho: tìm kiếm toàn hệ thống (Ctrl+K), link trong thông báo, nút "Xem hồ sơ / Xem hóa đơn".
 */
export function useOpenParam(onOpen: (id: string) => void) {
  const [params, setParams] = useSearchParams();
  const handler = useRef(onOpen);
  handler.current = onOpen;

  useEffect(() => {
    const id = params.get('open');
    if (!id) return;
    handler.current(id);
    params.delete('open');
    setParams(params, { replace: true });
  }, [params, setParams]);
}

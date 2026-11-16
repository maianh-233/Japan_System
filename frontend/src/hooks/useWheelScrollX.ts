import { useEffect } from 'react';

const scrollsX = (el: HTMLElement) => {
  const o = getComputedStyle(el).overflowX;
  return (o === 'auto' || o === 'scroll') && el.scrollWidth > el.clientWidth + 1;
};
const scrollsY = (el: HTMLElement) => {
  const o = getComputedStyle(el).overflowY;
  return (o === 'auto' || o === 'scroll') && el.scrollHeight > el.clientHeight + 1;
};

/**
 * Thanh cuộn đã ẩn toàn hệ thống → chuột thường khó cuộn ngang.
 * Rê chuột lên vùng CHỈ cuộn ngang (bảng rộng, thanh tab, thanh lọc...) thì lăn chuột = cuộn ngang.
 * Cuộn tới mép → trả lại cho trang cuộn dọc như bình thường. Touchpad (có deltaX) / Shift + lăn giữ nguyên.
 */
export function useWheelScrollX() {
  useEffect(() => {
    const onWheel = (e: WheelEvent) => {
      if (e.shiftKey || e.ctrlKey || Math.abs(e.deltaX) >= Math.abs(e.deltaY) || !e.deltaY) return;
      let el = e.target instanceof HTMLElement ? e.target : null;
      while (el && el !== document.body) {
        // Gặp vùng cuộn dọc trước → để nó cuộn dọc
        if (scrollsY(el)) return;
        if (scrollsX(el)) {
          const max = el.scrollWidth - el.clientWidth;
          const canMove = e.deltaY > 0 ? el.scrollLeft < max - 1 : el.scrollLeft > 0;
          if (!canMove) return;
          e.preventDefault();
          el.scrollLeft += e.deltaY;
          return;
        }
        el = el.parentElement;
      }
    };
    window.addEventListener('wheel', onWheel, { passive: false });
    return () => window.removeEventListener('wheel', onWheel);
  }, []);
}

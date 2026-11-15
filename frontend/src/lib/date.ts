import dayjs from 'dayjs';
import relativeTime from 'dayjs/plugin/relativeTime';

dayjs.extend(relativeTime);

/** 15/11/2026 */
export const fmtDate = (v?: string | null) => (v ? dayjs(v).format('DD/MM/YYYY') : '');
/** 15/11/2026 18:30 */
export const fmtDateTime = (v?: string | null) => (v ? dayjs(v).format('DD/MM/YYYY HH:mm') : '');
/** "5 phút trước" */
export const fromNow = (v?: string | null) => (v ? dayjs(v).fromNow() : '');

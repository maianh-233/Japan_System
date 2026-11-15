import dayjs from 'dayjs';
import type { AppNotification, Portal } from '@/types/auth';

const ago = (minutes: number) => dayjs().subtract(minutes, 'minute').toISOString();

/** Dữ liệu giả — thông báo theo cổng */
export const MOCK_NOTIFICATIONS: Record<Portal, AppNotification[]> = {
  staff: [
    { id: 's1', title: 'Đơn xin hủy buổi mới', description: 'GV Nguyễn Thu Hà xin hủy buổi N3-K12 tối thứ 4.', createdAt: ago(5), read: false, tone: 'warning', link: '/admin/hr/requests' },
    { id: 's2', title: '3 lead mới từ Facebook', description: 'Chiến dịch "Khai giảng N5 tháng 12".', createdAt: ago(42), read: false, tone: 'info', link: '/admin/crm/leads' },
    { id: 's3', title: 'Hóa đơn quá hạn', description: '5 học viên quá hạn đóng học phí đợt 2.', createdAt: ago(180), read: false, tone: 'danger', link: '/admin/finance/debts' },
    { id: 's4', title: 'Lớp N4-K15 đủ sĩ số', description: 'Lớp đã đạt 20/20 học viên.', createdAt: ago(60 * 26), read: true, tone: 'success' },
  ],
  teacher: [
    { id: 't1', title: 'Đơn hủy buổi đã được duyệt', description: 'Buổi N3-K12 ngày 19/11 sẽ học bù vào thứ 7.', createdAt: ago(15), read: false, tone: 'success' },
    { id: 't2', title: '4 bài tập chờ chấm', description: 'Lớp N4-K15 · Bài 12 — Ngữ pháp ～ば.', createdAt: ago(120), read: false, tone: 'info', link: '/teacher/assignments' },
    { id: 't3', title: 'Đổi phòng học', description: 'Lớp N3-K12 chuyển sang P.302 từ tuần sau.', createdAt: ago(60 * 30), read: true, tone: 'warning' },
  ],
  student: [
    { id: 'h1', title: 'Bài tập mới', description: 'Bài 12 — hạn nộp 23:59 thứ 6.', createdAt: ago(30), read: false, tone: 'info', link: '/student/assignments' },
    { id: 'h2', title: 'Buổi học tối thứ 4 bị hủy', description: 'Học bù vào 09:00 thứ 7, phòng 205.', createdAt: ago(90), read: false, tone: 'warning', link: '/student/timetable' },
    { id: 'h3', title: 'Đã nhận học phí', description: 'Thanh toán đợt 1: 4.500.000 ₫.', createdAt: ago(60 * 48), read: true, tone: 'success' },
  ],
  parent: [
    { id: 'p1', title: 'Con vắng học tối thứ 4', description: 'Nguyễn Văn An vắng buổi N3-K12 (có phép).', createdAt: ago(40), read: false, tone: 'warning', link: '/parent/attendance' },
    { id: 'p2', title: 'Học phí đợt 2 sắp đến hạn', description: '1.500.000 ₫ — hạn 30/11.', createdAt: ago(60 * 5), read: false, tone: 'info', link: '/parent/tuition' },
  ],
};

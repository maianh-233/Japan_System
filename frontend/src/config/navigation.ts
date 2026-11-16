import {
  ArrowLeftRight,
  Award,
  BadgePercent,
  Banknote,
  BarChart3,
  Briefcase,
  Building2,
  CalendarCheck,
  CalendarClock,
  CalendarDays,
  ClipboardCheck,
  ClipboardList,
  Clock,
  ContactRound,
  DoorOpen,
  FileBadge,
  FileQuestion,
  FileText,
  FolderOpen,
  GraduationCap,
  HandCoins,
  History,
  Inbox,
  KeyRound,
  Layers,
  LayoutDashboard,
  Library,
  LibraryBig,
  ListChecks,
  ListTodo,
  Megaphone,
  NotebookPen,
  Plane,
  Plug,
  QrCode,
  Receipt,
  Route,
  School,
  Settings,
  ShieldCheck,
  Shuffle,
  Tags,
  Target,
  TrendingUp,
  TriangleAlert,
  UserCheck,
  UserCog,
  UserPlus,
  UserRound,
  Users,
  Wallet,
  type LucideIcon,
} from 'lucide-react';
import type { Portal } from '@/types/auth';

export interface NavItem {
  key: string;
  label: string;
  icon: LucideIcon;
  /** Đường dẫn đầy đủ */
  path: string;
  /** Mã quyền cần có (xem lib/permission.ts) */
  permission?: string;
  /** Chức năng dự kiến của trang — hiển thị trên trang "đang xây dựng", dùng làm đặc tả */
  features?: string[];
  /** Mục mới thêm từ danh sách gợi ý */
  isNew?: boolean;
}

export interface NavModule {
  key: string;
  label: string;
  icon: LucideIcon;
  /** Mục con ở sidebar — bấm module sẽ mở mục đầu tiên người dùng có quyền */
  items: NavItem[];
}

const staffItem = (
  module: string,
  key: string,
  label: string,
  icon: LucideIcon,
  features?: string[],
  isNew?: boolean,
): NavItem => ({
  key,
  label,
  icon,
  path: `/admin/${module}/${key}`,
  permission: `${module}.${key}`,
  features,
  isNew,
});

/** Mục CÁ NHÂN: mọi nhân viên đăng nhập đều vào được (không gắn mã quyền) */
const selfItem = (module: string, key: string, label: string, icon: LucideIcon): NavItem => ({
  key,
  label,
  icon,
  path: `/admin/${module}/${key}`,
});

const portalItem = (portal: string, key: string, label: string, icon: LucideIcon, features?: string[], isNew?: boolean): NavItem => ({
  key,
  label,
  icon,
  path: `/${portal}/${key}`,
  features,
  isNew,
});

/**
 * CỔNG NHÂN VIÊN: module trên header → mục con ở sidebar.
 * Cấu trúc chi tiết + quan hệ dữ liệu: docs/modules.md
 */
export const STAFF_MODULES: NavModule[] = [
  {
    key: 'dashboard',
    label: 'Tổng quan',
    icon: LayoutDashboard,
    items: [
      staffItem('dashboard', 'overview', 'Bảng điều khiển', LayoutDashboard),
      staffItem('dashboard', 'reports', 'Báo cáo', BarChart3, [
        'Tỷ lệ chuyển đổi lead theo nguồn, chiến dịch, Sale',
        'Doanh thu, công nợ theo tháng / chi nhánh',
        'Sĩ số, chuyên cần, tỷ lệ lấp đầy phòng học',
        'So sánh giữa các chi nhánh, xuất Excel',
      ]),
    ],
  },
  {
    // Lead = người quan tâm, CHƯA học. Lịch sử tư vấn xem trong chi tiết từng lead.
    key: 'crm',
    label: 'Tuyển sinh',
    icon: Target,
    items: [
      staffItem('crm', 'leads', 'Lead', Target, [
        'Danh sách + Kanban theo giai đoạn: Mới → Đang tư vấn → Hẹn test → Chốt / Mất',
        'Chi tiết lead: thông tin, LỊCH SỬ TƯ VẤN (cuộc gọi, ghi chú, lịch hẹn gọi lại)',
        'Phát hiện trùng theo số điện thoại / email',
        'Cảnh báo SLA: lead mới chưa được gọi sau 15 phút',
        'Chuyển lead → học viên (giữ nguyên lịch sử tư vấn)',
      ]),
      staffItem('crm', 'lead-rules', 'Chia lead & SLA', Shuffle, [
        'Tự chia lead mới cho Sale đang trong ca (xoay vòng / theo tải)',
        'Quy tắc theo nguồn lead, chi nhánh, khóa quan tâm',
        'Đặt thời hạn liên hệ (SLA) và người nhận cảnh báo khi quá hạn',
      ], true),
      staffItem('crm', 'placement-tests', 'Test xếp lớp', ClipboardCheck, [
        'Tạo bài test đầu vào từ Ngân hàng câu hỏi',
        'Gửi link làm bài online cho lead',
        'Tự chấm, gợi ý trình độ & khóa học phù hợp',
      ], true),
      staffItem('crm', 'campaigns', 'Chiến dịch', Megaphone, [
        'Chiến dịch marketing: kênh, ngân sách, thời gian',
        'Số lead, tỷ lệ chốt, chi phí / lead của từng chiến dịch',
      ]),
    ],
  },
  {
    // Học viên = đã đăng ký lớp và đang/đã học tại trung tâm
    key: 'students',
    label: 'Học viên',
    icon: Users,
    items: [
      staffItem('students', 'profiles', 'Hồ sơ học viên', Users),
      staffItem('students', 'contacts', 'Phụ huynh / Người giám hộ', ContactRound, [
        'Một phụ huynh liên kết nhiều học viên (anh chị em)',
        'Người liên hệ chính, người nhận thông báo & hóa đơn',
        'Cấp tài khoản Cổng phụ huynh',
      ]),
      staffItem('students', 'at-risk', 'Cảnh báo bỏ học', TriangleAlert, [
        'Tự phát hiện: vắng ≥ 3 buổi liên tiếp, điểm giảm, nợ học phí quá hạn',
        'Mức độ rủi ro: thấp / trung bình / cao',
        'Giao Giáo vụ gọi chăm sóc, ghi lại kết quả',
      ], true),
      staffItem('students', 'jlpt', 'Thi JLPT', FileBadge, [
        'Theo dõi kỳ thi tháng 7 & 12: ai đăng ký, cấp độ, địa điểm thi',
        'Nhắc hạn nộp hồ sơ, lệ phí',
        'Nhập kết quả, thống kê tỷ lệ đỗ theo lớp / giáo viên',
      ], true),
      staffItem('students', 'certificates', 'Chứng nhận', Award, [
        'Cấp chứng nhận hoàn thành khóa (PDF theo mẫu)',
        'Mã QR xác thực chứng nhận',
      ], true),
      staffItem('students', 'study-abroad', 'Du học / XKLĐ', Plane, [
        'Hồ sơ giấy tờ, trạng thái COE, visa',
        'Trường / đối tác tiếp nhận, lịch phỏng vấn',
        '(Bật nếu trung tâm có mảng này)',
      ], true),
    ],
  },
  {
    key: 'academic',
    label: 'Đào tạo',
    icon: GraduationCap,
    items: [
      staffItem('academic', 'courses', 'Khóa học', Library, [
        'Khóa học: trình độ đầu vào/ra, số buổi, học phí, hình thức online/offline',
        'Gắn lộ trình (curriculum) cho khóa',
      ]),
      staffItem('academic', 'curriculum', 'Lộ trình (Curriculum)', Route, [
        'Lộ trình theo khóa: Lesson → Chapter → tài liệu giảng dạy',
        'Bài kiểm tra nhỏ trong Lesson, exam lớn (giữa kỳ, cuối kỳ, thi thử)',
        'Quản lý phiên bản: lớp đang học giữ bản cũ',
      ]),
      staffItem('academic', 'questions', 'Ngân hàng câu hỏi', FileQuestion, [
        'Từ vựng, ngữ pháp, Kanji, đọc hiểu, nghe hiểu (có audio)',
        'Hỗ trợ furigana, gắn cấp độ N5–N1, độ khó',
        'Dùng chung cho quiz, exam, test xếp lớp',
      ]),
      staffItem('academic', 'classes', 'Lớp học', School, [
        'Mở lớp theo khóa: giáo viên, phòng, lịch lặp, sĩ số',
        'Kiểm tra trùng lịch phòng & giáo viên khi xếp lớp',
        'Ghi danh, chuyển lớp, bảo lưu; trung tâm được hủy cả lớp',
      ]),
      staffItem('academic', 'sessions', 'Buổi học (Session)', CalendarCheck, [
        'Theo dõi buổi nào đã diễn ra / bị hủy / học bù (danh sách + lịch)',
        'Điểm danh, nhật ký buổi, tiến độ bài dạy',
        'Duyệt đơn hủy buổi → gợi ý giáo viên dạy thay hoặc xếp học bù',
        'Xác nhận buổi đã dạy → làm căn cứ tính lương giáo viên',
      ]),
      staffItem('academic', 'requests', 'Đơn chờ xử lý', Inbox, [
        'Đơn hủy buổi (GV) · đơn xin nghỉ (HV/PH) · đơn từ nhân viên — gom một nơi, kèm lịch sử duyệt',
        'Duyệt đơn hủy buổi → tự đánh dấu buổi, mở luôn bước mời GV dạy thay hoặc hủy & xếp học bù',
        'Theo dõi lời mời dạy thay: ai nhận trước thì dạy, lời mời còn lại tự hết hiệu lực',
      ], true),
      staffItem('academic', 'rooms', 'Phòng học', DoorOpen, [
        'Phòng theo chi nhánh: sức chứa, thiết bị',
        'Lịch sử dụng phòng, tỷ lệ lấp đầy',
      ]),
    ],
  },
  {
    // Thư viện điện tử — KHÔNG gắn với bài học trong lộ trình
    key: 'library',
    label: 'Thư viện',
    icon: LibraryBig,
    items: [
      staffItem('library', 'documents', 'Thư viện tài liệu', LibraryBig, [
        'Sách tham khảo, đề JLPT các năm, tài liệu tự học',
        'Lọc theo cấp độ N5–N1, kỹ năng, thẻ',
        'Xem trực tuyến có watermark tên người xem, chặn tải về',
        'Thống kê lượt đọc',
      ]),
    ],
  },
  {
    key: 'finance',
    label: 'Tài chính',
    icon: Wallet,
    items: [
      staffItem('finance', 'tuition', 'Học phí & bảng giá', Tags, [
        'Bảng giá theo khóa / chi nhánh, ưu đãi, combo',
        'Chính sách đóng nhiều đợt',
      ]),
      staffItem('finance', 'invoices', 'Hóa đơn & thu tiền', Receipt, [
        'Hóa đơn sinh từ ghi danh, thu nhiều đợt, hoàn tiền',
        'Thanh toán bằng mã VietQR riêng cho từng hóa đơn',
        'Nhắc học phí qua Zalo / Email',
      ]),
      staffItem('finance', 'reconciliation', 'Đối soát VietQR', QrCode, [
        'Nhận giao dịch ngân hàng, tự khớp với hóa đơn theo nội dung chuyển khoản',
        'Danh sách giao dịch chưa khớp để xử lý tay',
      ], true),
      staffItem('finance', 'debts', 'Công nợ', TrendingUp, [
        'Công nợ theo học viên / lớp / chi nhánh, tuổi nợ',
        'Danh sách cần nhắc, lịch sử nhắc',
      ]),
      staffItem('finance', 'teacher-payroll', 'Lương giáo viên', HandCoins, [
        'Tự tính từ buổi đã dạy (đã xác nhận) × đơn giá theo cấp độ / hình thức',
        'Trừ buổi hủy, cộng buổi dạy thay / học bù',
        'Chốt bảng lương tháng, xuất phiếu lương',
      ]),
      staffItem('finance', 'staff-payroll', 'Lương nhân viên', Banknote, [
        'Lương cứng + công thực tế từ Chấm công / Xếp ca',
        'Phụ cấp, khấu trừ, hoa hồng',
      ]),
      staffItem('finance', 'commissions', 'Hoa hồng & KPI Sale', BadgePercent, [
        'Doanh số chốt theo Sale, theo tháng',
        'Bậc hoa hồng, chỉ tiêu KPI, bảng xếp hạng',
        'Tự đưa vào Lương nhân viên',
      ], true),
    ],
  },
  {
    key: 'hr',
    label: 'Nhân sự',
    icon: Briefcase,
    items: [
      staffItem('hr', 'recruitment', 'Tuyển dụng', UserPlus, [
        'Tin tuyển dụng, ứng viên, lịch phỏng vấn',
        'Pipeline: Ứng tuyển → Phỏng vấn → Offer → Nhận việc',
      ]),
      staffItem('hr', 'onboarding', 'Onboarding', UserCheck, [
        'Ứng viên đạt → tự tạo hồ sơ nhân viên + tài khoản đăng nhập',
        'Checklist nhận việc: giấy tờ, thiết bị, đào tạo hội nhập',
      ], true),
      staffItem('hr', 'employees', 'Nhân viên', UserCog, [
        'Hồ sơ, hợp đồng, chi nhánh, vị trí',
        'Thuộc tính "Làm theo ca" cho vị trí như Sale',
      ]),
      staffItem('hr', 'teachers', 'Giáo viên', UserRound, [
        'Hồ sơ, bằng cấp (JLPT, sư phạm), cấp độ được dạy',
        'Đơn giá dạy, lịch rảnh để xếp lớp / dạy thay',
      ]),
      staffItem('hr', 'timesheets', 'Chấm công', Clock, [
        'Check-in / Check-out, bảng công tháng',
        'So khớp với ca được xếp: đi muộn, về sớm, vắng ca',
      ]),
      staffItem('hr', 'requests', 'Đơn từ & phê duyệt', ClipboardList, [
        'Nghỉ phép, đổi ca, công tác, đề xuất',
        'Duyệt nhiều cấp, thông báo kết quả',
      ]),
    ],
  },
  {
    key: 'work',
    label: 'Công việc',
    icon: ListTodo,
    items: [
      staffItem('work', 'tasks', 'Task toàn hệ thống', ListTodo, [
        'Nhân sự / quản lý giao task cho mọi nhân viên: hạn, ưu tiên, người theo dõi',
        'Xem dạng Kanban / danh sách, task lặp lại',
        'Gắn task vào lead, lớp, học viên; nhắc hạn',
      ]),
      // Ai có nhân viên dưới quyền (theo cây quản lý) đều dùng được — không cần quyền riêng
      selfItem('work', 'team-tasks', 'Giao việc cấp dưới', Users),
      staffItem('work', 'shifts', 'Xếp ca làm việc', CalendarClock, [
        'Quản lý xếp lịch ca cho nhân viên "Làm theo ca" (theo tuần)',
        'Nhân viên đăng ký ca mong muốn, xin đổi ca',
        'Cảnh báo thiếu người trong ca',
      ]),
      staffItem('work', 'handover', 'Bàn giao ca', ArrowLeftRight, [
        'Cuối ca ghi lại: lead đang dở, tiền mặt tại quầy, việc tồn',
        'Ca sau xác nhận đã nhận bàn giao',
      ]),
    ],
  },
  {
    // Trang của riêng người đang đăng nhập — cũng mở được từ menu ảnh đại diện góc phải
    key: 'me',
    label: 'Cá nhân',
    icon: UserRound,
    items: [
      selfItem('me', 'attendance', 'Chấm công của tôi', Clock),
      selfItem('me', 'tasks', 'Việc của tôi', ListChecks),
      selfItem('me', 'availability', 'Lịch rảnh / bận', CalendarCheck),
      selfItem('me', 'profile', 'Thông tin cá nhân', UserRound),
      selfItem('me', 'security', 'Mật khẩu & bảo mật', KeyRound),
    ],
  },
  {
    key: 'system',
    label: 'Hệ thống',
    icon: Settings,
    items: [
      staffItem('system', 'branches', 'Chi nhánh', Building2),
      staffItem('system', 'roles', 'Vai trò & quyền', ShieldCheck, [
        'Role mặc định + tạo role mới',
        'Ma trận quyền: module → nhóm quyền → quyền, kèm phạm vi (của mình / chi nhánh / toàn hệ thống)',
      ]),
      staffItem('system', 'user-permissions', 'Phân quyền người dùng', KeyRound, [
        'Gán role theo chi nhánh cho từng người',
        'Cấp / chặn quyền lẻ có thời hạn (chỉ cấp được quyền mình đang có)',
      ]),
      staffItem('system', 'integrations', 'Tích hợp', Plug, [
        'Zalo OA (thông báo phụ huynh, học viên)',
        'Ngân hàng / VietQR, Zoom / Google Meet, Email',
        'Nguồn lead: Facebook, form website',
      ], true),
      staffItem('system', 'audit', 'Nhật ký hệ thống', History, [
        'Ai làm gì, lúc nào: cấp quyền, xóa dữ liệu, xuất file',
      ]),
    ],
  },
];

/** CỔNG GIÁO VIÊN */
export const TEACHER_MENU: NavItem[] = [
  portalItem('teacher', 'dashboard', 'Tổng quan', LayoutDashboard),
  portalItem('teacher', 'timetable', 'Thời khóa biểu', CalendarDays),
  portalItem('teacher', 'classes', 'Lớp của tôi', School, [
    'Mở buổi học → xem Chapter + tài liệu cần dạy',
    'Điểm danh, nhật ký buổi, cập nhật tiến độ lớp (cả lớp online)',
    'Nhập điểm, nhận xét học viên',
  ]),
  portalItem('teacher', 'assignments', 'Bài tập', NotebookPen, ['Tạo bài tập giao cho lớp, hạn nộp', 'Chấm bài, phản hồi']),
  portalItem('teacher', 'documents', 'Tài liệu của tôi', FileText, ['Tài liệu tự soạn, gửi cho học viên trong lớp (khác tài liệu trung tâm)']),
  portalItem('teacher', 'requests', 'Đơn hủy buổi', ClipboardList, ['Danh sách đơn đã gửi và trạng thái duyệt']),
  portalItem('teacher', 'substitutes', 'Lời mời dạy thay', ArrowLeftRight, [
    'Nhận lời mời dạy thay khi giáo viên khác hủy buổi',
    'Chấp nhận / từ chối, tự cộng vào lương',
  ], true),
  portalItem('teacher', 'timesheet', 'Chấm công', Clock, ['Lịch sử buổi đã dạy, công tháng, lương tạm tính']),
];

/** CỔNG HỌC VIÊN */
export const STUDENT_MENU: NavItem[] = [
  portalItem('student', 'dashboard', 'Tổng quan', LayoutDashboard),
  portalItem('student', 'timetable', 'Thời khóa biểu', CalendarDays),
  portalItem('student', 'assignments', 'Bài tập', NotebookPen, ['Làm và nộp bài, xem nhận xét']),
  portalItem('student', 'exams', 'Bài thi', ListChecks, ['Làm bài kiểm tra / exam có đếm giờ', 'Xem kết quả, đáp án']),
  portalItem('student', 'flashcards', 'Flashcard', Layers, [
    'Từ vựng & Kanji theo từng bài học',
    'Ôn tập lặp lại ngắt quãng, chuỗi ngày học (streak)',
  ], true),
  portalItem('student', 'documents', 'Tài liệu', FolderOpen, ['Tài liệu lớp + thư viện điện tử']),
  portalItem('student', 'tuition', 'Học phí', Wallet, ['Hóa đơn, lịch sử thanh toán, thanh toán VietQR']),
  portalItem('student', 'requests', 'Đơn xin nghỉ', ClipboardList, ['Đơn đã gửi, đăng ký học bù']),
  portalItem('student', 'progress', 'Tiến độ học tập', TrendingUp, ['Tiến độ theo lộ trình, điểm, chuyên cần']),
  portalItem('student', 'certificates', 'Chứng nhận & JLPT', Award, ['Chứng nhận hoàn thành khóa', 'Đăng ký & kết quả thi JLPT'], true),
];

/** CỔNG PHỤ HUYNH (mới) */
export const PARENT_MENU: NavItem[] = [
  portalItem('parent', 'dashboard', 'Tổng quan', LayoutDashboard),
  portalItem('parent', 'timetable', 'Lịch học của con', CalendarDays, ['Thời khóa biểu, buổi hủy / học bù']),
  portalItem('parent', 'attendance', 'Chuyên cần', CalendarCheck, ['Điểm danh từng buổi, số buổi vắng', 'Xin nghỉ thay con']),
  portalItem('parent', 'results', 'Kết quả học tập', TrendingUp, ['Điểm kiểm tra, nhận xét giáo viên']),
  portalItem('parent', 'tuition', 'Học phí', Wallet, ['Hóa đơn, thanh toán VietQR, lịch sử']),
];

/** Trang chủ của từng cổng */
export const PORTAL_HOME: Record<Portal, string> = {
  staff: '/admin/dashboard/overview',
  teacher: '/teacher/dashboard',
  student: '/student/dashboard',
  parent: '/parent/dashboard',
};

/** Tìm mục menu theo đường dẫn (để đặt tiêu đề trang) */
export function findNavItem(pathname: string): { item: NavItem; module?: NavModule } | null {
  for (const m of STAFF_MODULES) {
    const item = m.items.find((i) => pathname === i.path || pathname.startsWith(`${i.path}/`));
    if (item) return { item, module: m };
  }
  const item = [...TEACHER_MENU, ...STUDENT_MENU, ...PARENT_MENU].find(
    (i) => pathname === i.path || pathname.startsWith(`${i.path}/`),
  );
  return item ? { item } : null;
}

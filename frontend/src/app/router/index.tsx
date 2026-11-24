import { Navigate, createBrowserRouter } from 'react-router-dom';
import { RequirePortal, RootRedirect } from '@/app/guards/RequirePortal';
import StaffLayout from '@/layouts/StaffLayout';
import { ParentLayout, StudentLayout, TeacherLayout } from '@/layouts/PortalLayouts';
import LoginPage from '@/pages/auth/login/LoginPage';
import ActivatePage from '@/pages/auth/activate/ActivatePage';
import PlacementTakePage from '@/pages/public/placement/PlacementTakePage';
import VerifyCertificatePage from '@/pages/public/verify/VerifyCertificatePage';
import ComingSoon from '@/pages/common/ComingSoon';
import NotFound from '@/pages/errors/NotFound';
import StaffDashboard from '@/pages/staff/dashboard/StaffDashboard';
import StudentsPage from '@/pages/staff/students/students/StudentsPage';
import LeadsPage from '@/pages/staff/crm/leads/LeadsPage';
import LeadRulesPage from '@/pages/staff/crm/lead-rules/LeadRulesPage';
import PlacementTestsPage from '@/pages/staff/crm/placement-tests/PlacementTestsPage';
import AppointmentsPage from '@/pages/staff/crm/appointments/AppointmentsPage';
import FunnelPage from '@/pages/staff/crm/funnel/FunnelPage';
import PromotionsPage from '@/pages/staff/crm/promotions/PromotionsPage';
import MessageTemplatesPage from '@/pages/staff/crm/message-templates/MessageTemplatesPage';
import CampaignsPage from '@/pages/staff/crm/campaigns/CampaignsPage';
import BranchesPage from '@/pages/staff/system/branches/BranchesPage';
import RolesPage from '@/pages/staff/system/roles/RolesPage';
import UserPermissionsPage from '@/pages/staff/system/user-permissions/UserPermissionsPage';
import IntegrationsPage from '@/pages/staff/system/integrations/IntegrationsPage';
import AuditLogsPage from '@/pages/staff/system/audit-logs/AuditLogsPage';
import ApprovalsPage from '@/pages/staff/system/approvals/ApprovalsPage';
import TrashPage from '@/pages/staff/system/trash/TrashPage';
import SettingsPage from '@/pages/staff/system/settings/SettingsPage';
import CoursesPage from '@/pages/staff/academic/courses/CoursesPage';
import CurriculumPage from '@/pages/staff/academic/curriculum/CurriculumPage';
import ClassesPage from '@/pages/staff/academic/classes/ClassesPage';
import ClassDetailPage from '@/pages/staff/academic/classes/ClassDetailPage';
import AssignmentSubmissionsPage from '@/pages/staff/academic/classes/AssignmentSubmissionsPage';
import TeacherExamResultsPage from '@/pages/teacher/classes/TeacherExamResultsPage';
import ExamResultsPage from '@/pages/staff/academic/classes/ExamResultsPage';
import RoomsPage from '@/pages/staff/academic/rooms/RoomsPage';
import SessionsPage from '@/pages/staff/academic/sessions/SessionsPage';
import WorkflowRequestsPage from '@/pages/staff/workflow/requests/RequestsPage';
import QuestionsPage from '@/pages/staff/academic/questions/QuestionsPage';
import LibraryPage from '@/pages/staff/library/documents/LibraryPage';
import GuardiansPage from '@/pages/staff/students/contacts/GuardiansPage';
import AtRiskPage from '@/pages/staff/students/at-risk/AtRiskPage';
import JlptPage from '@/pages/staff/students/jlpt/JlptPage';
import CertificatesPage from '@/pages/staff/students/certificates/CertificatesPage';
import AbroadPage from '@/pages/staff/students/study-abroad/AbroadPage';
import TasksPage from '@/pages/staff/work/tasks/TasksPage';
import ShiftsPage from '@/pages/staff/work/shifts/ShiftsPage';
import HandoverPage from '@/pages/staff/work/handover/HandoverPage';
import EmployeesPage from '@/pages/staff/hr/employees/PeoplePage';
import TeachersPage from '@/pages/staff/hr/teachers/TeachersPage';
import TimesheetsPage from '@/pages/staff/hr/timesheets/TimesheetsPage';
import RequestsPage from '@/pages/staff/hr/requests/RequestsPage';
import RecruitmentPage from '@/pages/staff/hr/recruitment/RecruitmentPage';
import OnboardingPage from '@/pages/staff/hr/onboarding/OnboardingPage';
import TuitionPage from '@/pages/staff/finance/tuition/TuitionPage';
import InvoicesPage from '@/pages/staff/finance/invoices/InvoicesPage';
import ReconciliationPage from '@/pages/staff/finance/reconciliation/ReconciliationPage';
import DebtsPage from '@/pages/staff/finance/debts/DebtsPage';
import TeacherPayrollPage from '@/pages/staff/finance/teacher-payroll/TeacherPayrollPage';
import StaffPayrollPage from '@/pages/staff/finance/staff-payroll/StaffPayrollPage';
import CommissionsPage from '@/pages/staff/finance/commissions/CommissionsPage';
import ReportsPage from '@/pages/staff/reports/ReportsPage';
import MyAttendancePage from '@/pages/staff/me/MyAttendancePage';
import MyTasksPage from '@/pages/staff/me/MyTasksPage';
import MyShiftAvailabilityPage from '@/pages/staff/me/MyShiftAvailabilityPage';
import TeamTasksPage from '@/pages/staff/work/team-tasks/TeamTasksPage';
import ProfilePage from '@/pages/common/account/ProfilePage';
import SecurityPage from '@/pages/common/account/SecurityPage';
import TeacherDashboard from '@/pages/teacher/dashboard/TeacherDashboard';
import MyClassesPage from '@/pages/teacher/classes/MyClassesPage';
import TeacherClassPage from '@/pages/teacher/classes/TeacherClassPage';
import TeacherAssignmentsPage from '@/pages/teacher/assignments/TeacherAssignmentsPage';
import TeacherDocumentsPage from '@/pages/teacher/documents/TeacherDocumentsPage';
import TeacherRequestsPage from '@/pages/teacher/requests/TeacherRequestsPage';
import SubstitutesPage from '@/pages/teacher/substitutes/SubstitutesPage';
import TeacherTimesheetPage from '@/pages/teacher/timesheet/TeacherTimesheetPage';
import TeachingSessionPage from '@/pages/teacher/teaching/TeachingSessionPage';
import StudentAssignmentsPage from '@/pages/student/assignments/StudentAssignmentsPage';
import StudentExamsPage from '@/pages/student/exams/StudentExamsPage';
import FlashcardsPage from '@/pages/student/flashcards/FlashcardsPage';
import StudentDocumentsPage from '@/pages/student/documents/StudentDocumentsPage';
import StudentTuitionPage from '@/pages/student/tuition/StudentTuitionPage';
import StudentRequestsPage from '@/pages/student/requests/StudentRequestsPage';
import StudentProgressPage from '@/pages/student/progress/StudentProgressPage';
import StudentCertificatesPage from '@/pages/student/certificates/StudentCertificatesPage';
import StudentJlptPage from '@/pages/student/jlpt/StudentJlptPage';
import ParentAttendancePage from '@/pages/parent/attendance/ParentAttendancePage';
import ParentWeeklyReportPage from '@/pages/parent/weekly/ParentWeeklyReportPage';
import ParentResultsPage from '@/pages/parent/results/ParentResultsPage';
import ParentTuitionPage from '@/pages/parent/tuition/ParentTuitionPage';
import StudentDashboard from '@/pages/student/dashboard/StudentDashboard';
import ParentDashboard from '@/pages/parent/dashboard/ParentDashboard';
import { SessionTimetablePage } from '@/features/academic/SessionTimetablePage';
import ComponentShowcase from '@/pages/dev/showcase/ComponentShowcase';
import { STAFF_MODULES } from '@/config/navigation';

/**
 * /login                       Đăng nhập
 * /admin/:module/:page         Cổng nhân viên (Super Admin, Admin, QL chi nhánh, Giáo vụ, Sale, Marketing, Kế toán)
 * /teacher/:page               Cổng giáo viên
 * /student/:page               Cổng học viên
 * /parent/:page                Cổng phụ huynh
 * /dev/components              Trang xem thử component (chỉ khi phát triển)
 * Trang chưa xây → ComingSoon. Quyền từng trang được kiểm tra trong AppShell.
 */
export const router = createBrowserRouter([
  { path: '/', element: <RootRedirect /> },
  { path: '/login', element: <LoginPage /> },
  { path: '/activate', element: <ActivatePage /> },
  // Lead làm test xếp lớp qua link Sale gửi — công khai, không cần đăng nhập (CRM-13)
  { path: '/t/:token', element: <PlacementTakePage /> },
  { path: '/verify', element: <VerifyCertificatePage /> },
  { path: '/verify/:code', element: <VerifyCertificatePage /> },
  {
    path: '/admin',
    element: (
      <RequirePortal portal="staff">
        <StaffLayout />
      </RequirePortal>
    ),
    children: [
      { index: true, element: <Navigate to="dashboard/overview" replace /> },
      { path: 'dashboard/overview', element: <StaffDashboard /> },
      { path: 'dashboard/reports', element: <ReportsPage /> },
      { path: 'crm/leads', element: <LeadsPage /> },
      { path: 'crm/lead-rules', element: <LeadRulesPage /> },
      { path: 'crm/placement-tests', element: <PlacementTestsPage /> },
      { path: 'crm/appointments', element: <AppointmentsPage /> },
      { path: 'crm/promotions', element: <PromotionsPage /> },
      { path: 'crm/funnel', element: <FunnelPage /> },
      { path: 'crm/message-templates', element: <MessageTemplatesPage /> },
      { path: 'crm/campaigns', element: <CampaignsPage /> },
      { path: 'students/profiles', element: <StudentsPage /> },
      { path: 'academic/courses', element: <CoursesPage /> },
      { path: 'academic/curriculum', element: <CurriculumPage /> },
      { path: 'academic/classes', element: <ClassesPage /> },
      { path: 'academic/classes/:id', element: <ClassDetailPage /> },
      { path: 'academic/classes/:id/assignments/:assignmentId', element: <AssignmentSubmissionsPage /> },
      { path: 'academic/classes/:id/exams/:examId', element: <ExamResultsPage /> },
      { path: 'academic/rooms', element: <RoomsPage /> },
      { path: 'academic/sessions', element: <SessionsPage /> },
      { path: 'academic/requests', element: <WorkflowRequestsPage /> },
      { path: 'academic/questions', element: <QuestionsPage /> },
      { path: 'library/documents', element: <LibraryPage /> },
      { path: 'students/contacts', element: <GuardiansPage /> },
      { path: 'students/at-risk', element: <AtRiskPage /> },
      { path: 'students/jlpt', element: <JlptPage /> },
      { path: 'students/certificates', element: <CertificatesPage /> },
      { path: 'students/study-abroad', element: <AbroadPage /> },
      { path: 'work/tasks', element: <TasksPage /> },
      { path: 'work/team-tasks', element: <TeamTasksPage /> },
      { path: 'me/attendance', element: <MyAttendancePage /> },
      { path: 'me/tasks', element: <MyTasksPage /> },
      { path: 'me/availability', element: <MyShiftAvailabilityPage /> },
      { path: 'me/profile', element: <ProfilePage /> },
      { path: 'me/security', element: <SecurityPage /> },
      { path: 'work/shifts', element: <ShiftsPage /> },
      { path: 'work/handover', element: <HandoverPage /> },
      { path: 'hr/employees', element: <EmployeesPage /> },
      { path: 'hr/teachers', element: <TeachersPage /> },
      { path: 'hr/timesheets', element: <TimesheetsPage /> },
      { path: 'hr/requests', element: <RequestsPage /> },
      { path: 'hr/recruitment', element: <RecruitmentPage /> },
      { path: 'hr/onboarding', element: <OnboardingPage /> },
      { path: 'finance/tuition', element: <TuitionPage /> },
      { path: 'finance/invoices', element: <InvoicesPage /> },
      { path: 'finance/reconciliation', element: <ReconciliationPage /> },
      { path: 'finance/debts', element: <DebtsPage /> },
      { path: 'finance/teacher-payroll', element: <TeacherPayrollPage /> },
      { path: 'finance/staff-payroll', element: <StaffPayrollPage /> },
      { path: 'finance/commissions', element: <CommissionsPage /> },
      { path: 'system/branches', element: <BranchesPage /> },
      { path: 'system/roles', element: <RolesPage /> },
      { path: 'system/user-permissions', element: <UserPermissionsPage /> },
      { path: 'system/integrations', element: <IntegrationsPage /> },
      { path: 'system/audit', element: <AuditLogsPage /> },
      { path: 'system/approvals', element: <ApprovalsPage /> },
      { path: 'system/trash', element: <TrashPage /> },
      { path: 'system/settings', element: <SettingsPage /> },
      // /admin/crm → mục đầu tiên của module
      ...STAFF_MODULES.map((m) => ({ path: m.key, element: <Navigate to={m.items[0].path} replace /> })),
      { path: ':module/:page', element: <ComingSoon /> },
    ],
  },
  {
    path: '/teacher',
    element: (
      <RequirePortal portal="teacher">
        <TeacherLayout />
      </RequirePortal>
    ),
    children: [
      { index: true, element: <Navigate to="dashboard" replace /> },
      { path: 'dashboard', element: <TeacherDashboard /> },
      { path: 'timetable', element: <SessionTimetablePage role="teacher" /> },
      { path: 'teaching/:sessionId', element: <TeachingSessionPage /> },
      { path: 'classes', element: <MyClassesPage /> },
      { path: 'classes/:id', element: <TeacherClassPage /> },
      { path: 'assignments', element: <TeacherAssignmentsPage /> },
      { path: 'assignments/:id/:assignmentId', element: <AssignmentSubmissionsPage portal="teacher" /> },
      { path: 'classes/:id/exams/:examId', element: <TeacherExamResultsPage /> },
      { path: 'documents', element: <TeacherDocumentsPage /> },
      { path: 'requests', element: <TeacherRequestsPage /> },
      { path: 'substitutes', element: <SubstitutesPage /> },
      { path: 'timesheet', element: <TeacherTimesheetPage /> },
      { path: 'profile', element: <ProfilePage /> },
      { path: 'security', element: <SecurityPage /> },
      { path: ':page', element: <ComingSoon /> },
    ],
  },
  {
    path: '/student',
    element: (
      <RequirePortal portal="student">
        <StudentLayout />
      </RequirePortal>
    ),
    children: [
      { index: true, element: <Navigate to="dashboard" replace /> },
      { path: 'dashboard', element: <StudentDashboard /> },
      { path: 'timetable', element: <SessionTimetablePage role="student" /> },
      { path: 'assignments', element: <StudentAssignmentsPage /> },
      { path: 'exams', element: <StudentExamsPage /> },
      { path: 'flashcards', element: <FlashcardsPage /> },
      { path: 'documents', element: <StudentDocumentsPage /> },
      { path: 'tuition', element: <StudentTuitionPage /> },
      { path: 'requests', element: <StudentRequestsPage /> },
      { path: 'progress', element: <StudentProgressPage /> },
      { path: 'certificates', element: <StudentCertificatesPage /> },
      { path: 'jlpt', element: <StudentJlptPage /> },
      { path: 'profile', element: <ProfilePage /> },
      { path: 'security', element: <SecurityPage /> },
      { path: ':page', element: <ComingSoon /> },
    ],
  },
  {
    path: '/parent',
    element: (
      <RequirePortal portal="parent">
        <ParentLayout />
      </RequirePortal>
    ),
    children: [
      { index: true, element: <Navigate to="dashboard" replace /> },
      { path: 'dashboard', element: <ParentDashboard /> },
      { path: 'timetable', element: <SessionTimetablePage role="parent" /> },
      { path: 'attendance', element: <ParentAttendancePage /> },
      { path: 'weekly', element: <ParentWeeklyReportPage /> },
      { path: 'results', element: <ParentResultsPage /> },
      { path: 'tuition', element: <ParentTuitionPage /> },
      { path: 'profile', element: <ProfilePage /> },
      { path: 'security', element: <SecurityPage /> },
      { path: ':page', element: <ComingSoon /> },
    ],
  },
  ...(import.meta.env.DEV ? [{ path: '/dev/components', element: <ComponentShowcase /> }] : []),
  { path: '*', element: <NotFound /> },
], {
  future: { v7_relativeSplatPath: true, v7_fetcherPersist: true, v7_normalizeFormMethod: true, v7_partialHydration: true, v7_skipActionErrorRevalidation: true },
});

import { Navigate, createBrowserRouter } from 'react-router-dom';
import { RequirePortal, RootRedirect } from '@/app/guards/RequirePortal';
import StaffLayout from '@/layouts/StaffLayout';
import { ParentLayout, StudentLayout, TeacherLayout } from '@/layouts/PortalLayouts';
import LoginPage from '@/pages/auth/login/LoginPage';
import ComingSoon from '@/pages/common/ComingSoon';
import NotFound from '@/pages/errors/NotFound';
import StaffDashboard from '@/pages/staff/dashboard/StaffDashboard';
import StudentsPage from '@/pages/staff/students/students/StudentsPage';
import LeadsPage from '@/pages/staff/crm/leads/LeadsPage';
import CoursesPage from '@/pages/staff/academic/courses/CoursesPage';
import CurriculumPage from '@/pages/staff/academic/curriculum/CurriculumPage';
import ClassesPage from '@/pages/staff/academic/classes/ClassesPage';
import ClassDetailPage from '@/pages/staff/academic/classes/ClassDetailPage';
import AssignmentSubmissionsPage from '@/pages/staff/academic/classes/AssignmentSubmissionsPage';
import ExamResultsPage from '@/pages/staff/academic/classes/ExamResultsPage';
import TeacherDashboard from '@/pages/teacher/dashboard/TeacherDashboard';
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
      { path: 'crm/leads', element: <LeadsPage /> },
      { path: 'students/profiles', element: <StudentsPage /> },
      { path: 'academic/courses', element: <CoursesPage /> },
      { path: 'academic/curriculum', element: <CurriculumPage /> },
      { path: 'academic/classes', element: <ClassesPage /> },
      { path: 'academic/classes/:id', element: <ClassDetailPage /> },
      { path: 'academic/classes/:id/assignments/:assignmentId', element: <AssignmentSubmissionsPage /> },
      { path: 'academic/classes/:id/exams/:examId', element: <ExamResultsPage /> },
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
      { path: ':page', element: <ComingSoon /> },
    ],
  },
  ...(import.meta.env.DEV ? [{ path: '/dev/components', element: <ComponentShowcase /> }] : []),
  { path: '*', element: <NotFound /> },
], {
  future: { v7_relativeSplatPath: true, v7_fetcherPersist: true, v7_normalizeFormMethod: true, v7_partialHydration: true, v7_skipActionErrorRevalidation: true },
});

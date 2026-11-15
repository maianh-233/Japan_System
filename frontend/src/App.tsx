import { RouterProvider } from 'react-router-dom';
import { AppProvider } from '@/app/providers/AppProvider';
import { router } from '@/app/router';

export default function App() {
  return (
    <AppProvider>
      <RouterProvider router={router} future={{ v7_startTransition: true }} />
    </AppProvider>
  );
}

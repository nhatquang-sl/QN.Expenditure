import ConsecutiveSnackBars from 'components/snackbar';
import { Outlet } from 'react-router-dom';

function Main() {
  return (
    <main className="max-w-4xl mx-auto main-min-height">
      {/* <RouterProvider router={router} /> */}
      <Outlet />
      <ConsecutiveSnackBars />
    </main>
  );
}

export default Main;

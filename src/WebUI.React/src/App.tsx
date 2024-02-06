import { createBrowserRouter, RouterProvider } from 'react-router-dom';

import NotFound from 'components/errors/not-found';
import Footer from 'components/footer';
import Header from 'components/header';
import Main from 'components/main';
import Login from 'features/auth/login';
import RegisterConfirm from 'features/auth/register-confirm';
import RequestActivateEmail from 'features/auth/request-activate-email';
import Landing from 'features/landing';
import './App.css';

const router = createBrowserRouter([
  {
    path: '/login',
    element: <Login />,
  },
  {
    path: '/request-activate-email',
    element: <RequestActivateEmail />,
  },
  {
    path: '/register-confirm',
    element: <RegisterConfirm />,
  },
  {
    path: '/',
    element: (
      <>
        <Header />
        <Main />
        <Footer />
      </>
    ),
    errorElement: <NotFound />,
    children: [{ index: true, element: <Landing /> }],
  },
]);

function App() {
  return <RouterProvider router={router} />;
}

export default App;

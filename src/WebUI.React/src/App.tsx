import { createBrowserRouter, RouterProvider } from 'react-router-dom';

import Header from 'components/header';
import Main from 'components/main';
import Footer from 'components/footer';
import './App.css';
import NotFound from 'components/errors/not-found';
import Landing from 'features/landing';
import Login from 'features/auth/login';

const router = createBrowserRouter([
  {
    path: '/login',
    element: <Login />,
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

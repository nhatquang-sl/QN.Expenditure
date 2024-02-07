import { selectAuth } from 'features/auth/slice';
import { useSelector } from 'react-redux';
import { Link } from 'react-router-dom';

function Header() {
  const auth = useSelector(selectAuth);
  console.log({ auth });
  return (
    <header className="bg-teal-700 text-white sticky top-0 z-10">
      <section className="max-w-4xl mx-auto p-4 flex justify-between items-center">
        <h1 className="text-3xl font-medium">
          <a href="#hero">🚀 Acme Rockets</a>
        </h1>
        <div>
          <button id="mobile-open-button" className="text-3xl sm:hidden focus:outline-none">
            &#9776;
          </button>
          <nav className="hidden sm:block space-x-8 text-xl" aria-label="main">
            <a href="#rockets" className="hover:opacity-90">
              Rockets
            </a>
            <a href="#testimonials" className="hover:opacity-90">
              Testimonials
            </a>
            <a href="#contact" className="hover:opacity-90">
              Contact Us
            </a>
            {/* <Link to={'login'}>Login</Link> */}
            {auth.id ? '' : <Link to={'login'}>Login</Link>}
          </nav>
        </div>
      </section>
    </header>
  );
}

export default Header;

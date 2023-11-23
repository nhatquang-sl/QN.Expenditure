import './index.css';

function Login() {
  return (
    <main className="relative flex flex-1 flex-col lg:flex-row overflow-hidden">
      <input type="checkbox" id="chk" aria-hidden="true" className="hidden" />
      <div className="form-container login-form-container">
        <form className="form">
          <label htmlFor="chk" aria-hidden="true" className="form-header">
            Log in
          </label>
          <div className="mb-6 w-full">
            <label htmlFor="email" className="input-label">
              Email address
            </label>
            <input type="email" id="email" className="input" value="" required />
          </div>
          <div className="mb-6 w-full">
            <div className="flex justify-between">
              <label htmlFor="password" className="input-label">
                Password
              </label>
              {/* <div className="mt-8 text-center"> */}
              <a href="#" className="font-bold text-sm txt-primary">
                Forgot password?
              </a>
              {/* </div> */}
            </div>

            <input type="password" id="password" className="input" value="" required />
          </div>
          <button type="submit" className="btn-primary">
            <span>Sign in to account</span>
          </button>
        </form>
      </div>

      <div className="form-container register-form-container">
        <form className="form translate-y-[-46px] lg:translate-y-0">
          <label
            htmlFor="chk"
            aria-hidden="true"
            className="form-header mt-12 scale-75 lg:scale-100 lg:mt-24"
          >
            Register
          </label>
          <div className="mb-6 w-full">
            <label htmlFor="email" className="input-label">
              Email address
            </label>
            <input type="email" id="email" className="input" value="" required />
          </div>
          <div className="mb-6 w-full">
            <label htmlFor="password" className="input-label">
              Password
            </label>
            <input type="password" id="password" className="input" value="" required />
          </div>
          <button type="submit" className="btn-primary">
            <span>Sign in to account</span>
          </button>
        </form>
      </div>

      <div className="form-container overlay-container">
        <div className="overlay">
          <div className="overlay-panel overlay-login">
            <label
              htmlFor="chk"
              aria-hidden="true"
              className="text-4xl mb-12 transition ease-in-out duration-500 text-slate-100"
            >
              Register
            </label>
            {/* <p>Sign in here if you already have an account </p>
            <button className="ghost mt-5" id="signIn">
              Sign In
            </button> */}
          </div>
          <div className="overlay-panel overlay-register">
            <label
              htmlFor="chk"
              aria-hidden="true"
              className="text-4xl mb-12 transition ease-in-out duration-500"
            >
              Log in
            </label>
          </div>
        </div>
      </div>
    </main>
  );
}
export default Login;

// import './index.css';

function Register() {
  return (
    <section>
      <div className="container-app" id="container">
        <div className="form-container sign-up-container">
          <form action="#">
            <h1>Sign Up</h1>
            <div className="social-container">
              <a href="https://Github.com/YasinDehfuli" target="_blank" className="social">
                <i className="fab fa-github"></i>
              </a>
              <a href="https://Codepen.io/YasinDehfuli" target="_blank" className="social">
                <i className="fab fa-codepen"></i>
              </a>
              <a href="mailto:Ydehfuli@gmail.com" target="_blank" className="social">
                <i className="fab fa-google"></i>
              </a>
            </div>
            <span>Or use your Email for registration</span>
            <label>
              <input type="text" placeholder="Name" />
            </label>
            <label>
              <input type="email" placeholder="Email" />
            </label>
            <label>
              <input type="password" placeholder="Password" />
            </label>
            <button style={{ marginTop: 9 }}>Sign Up</button>
          </form>
        </div>
        <div className="form-container sign-in-container">
          <form action="#">
            <h1>Sign in</h1>
            <div className="social-container">
              <a href="https://Github.com/YasinDehfuli" target="_blank" className="social">
                <i className="fab fa-github"></i>
              </a>
              <a href="https://Codepen.io/YasinDehfuli" target="_blank" className="social">
                <i className="fab fa-codepen"></i>
              </a>
              <a href="mailto:Ydehfuli@gmail.com" target="_blank" className="social">
                <i className="fab fa-google"></i>
              </a>
            </div>
            <span> Or sign in using E-Mail Address</span>
            <label>
              <input type="email" placeholder="Email" />
            </label>
            <label>
              <input type="password" placeholder="Password" />
            </label>
            <a href="#">Forgot your password?</a>
            <button>Sign In</button>
          </form>
        </div>
        <div className="overlay-container">
          <div className="overlay">
            <div className="overlay-panel overlay-left">
              <h1>Log in</h1>
              <p>Sign in here if you already have an account </p>
              <button className="ghost mt-5" id="signIn">
                Sign In
              </button>
            </div>
            <div className="overlay-panel overlay-right">
              <h1>Create, Account!</h1>
              <p>Sign up if you still don't have an account ... </p>
              <button className="ghost" id="signUp">
                Sign Up
              </button>
            </div>
          </div>
        </div>
      </div>
    </section>
  );
}

export default Register;

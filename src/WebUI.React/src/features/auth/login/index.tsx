import {
  Button,
  ThemeProvider,
  Typography,
  createTheme,
  useMediaQuery,
  useTheme,
} from '@mui/material';
import LoginForm from './login-form';
import RegisterForm from './register-form';
import './index.css';

const darkTheme = createTheme({
  palette: {
    mode: 'dark',
  },
});
const lightTheme = createTheme({
  palette: {
    mode: 'light',
  },
});

function Login() {
  const matches = useMediaQuery(useTheme().breakpoints.down('md'));

  return (
    <main className="relative flex flex-1 flex-col lg:flex-row overflow-hidden">
      <input type="checkbox" id="chk" aria-hidden="true" className="hidden" />
      <LoginForm />
      <ThemeProvider theme={matches ? darkTheme : lightTheme}>
        <RegisterForm />
      </ThemeProvider>
      <ThemeProvider theme={darkTheme}>
        <div className="form-container overlay-container">
          <div
            className="overlay"
            style={{ backgroundColor: darkTheme.palette.background.default }}
          >
            <div className="overlay-panel overlay-login">
              <Typography variant="h1">Hello, Friend!</Typography>
              <Typography sx={{ marginY: 2 }}>
                Enter your personal info and start journey with us
              </Typography>
              <Button variant="outlined">
                <label htmlFor="chk" aria-hidden="true">
                  Register
                </label>
              </Button>
            </div>
            <div className="overlay-panel overlay-register">
              <Typography variant="h1">Welcome Back!</Typography>
              <Typography sx={{ mb: 2 }}>
                Keep connect with us please login with your personal info
              </Typography>
              <Button variant="outlined">
                <label htmlFor="chk" aria-hidden="true">
                  Log in
                </label>
              </Button>
            </div>
          </div>
        </div>
      </ThemeProvider>
    </main>
  );
}

export default Login;

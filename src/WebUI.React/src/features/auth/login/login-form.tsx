import { zodResolver } from '@hookform/resolvers/zod';
import LockOutlinedIcon from '@mui/icons-material/LockOutlined';
import { LoadingButton } from '@mui/lab';
import {
  Avatar,
  Checkbox,
  FormControlLabel,
  Grid,
  Link,
  TextField,
  Typography,
} from '@mui/material';
import { showSnackbar } from 'components/snackbar/slice';
import { useEffect, useState } from 'react';
import { Controller, SubmitHandler, useForm } from 'react-hook-form';
import { useDispatch, useSelector } from 'react-redux';
import { useLocation, useNavigate } from 'react-router-dom';
import { authClient } from 'store';
import { BadRequest, LoginCommand } from 'store/api-client';
import { TokenType, selectAuthType, setAuth } from '../slice';
import { LoginData, LoginDataSchema } from './types';

function LoginForm() {
  const {
    handleSubmit,
    trigger,
    control,
    formState: { errors },
  } = useForm<LoginData>({
    resolver: zodResolver(LoginDataSchema),
  });
  const navigate = useNavigate();
  const dispatch = useDispatch();
  const location = useLocation();

  const authType = useSelector(selectAuthType);
  const [loading, setLoading] = useState(false);

  useEffect(() => {
    console.log({ authType });
    switch (authType) {
      case TokenType.Login:
        const from = location.state?.from?.pathname ?? '/login-history';
        navigate(from, { replace: true });
        break;
      case TokenType.NeedActivate:
        navigate('/request-activate-email', { replace: true });
        break;
    }
  }, [authType, location, navigate]);

  const onSubmit: SubmitHandler<LoginData> = async (data) => {
    setLoading(true);
    try {
      var res = await authClient.login(new LoginCommand(data));

      dispatch(setAuth(res.accessToken ?? ''));
    } catch (err: any) {
      if (err instanceof BadRequest) {
        dispatch(showSnackbar(err.message, 'error', 'top', 'left'));
      }
    }

    setLoading(false);
  };

  return (
    <div className="form-container login-form-container">
      <form className="form" onSubmit={handleSubmit(onSubmit)} noValidate>
        <Avatar sx={{ bgcolor: 'secondary.main' }} className="form-icon">
          <LockOutlinedIcon />
        </Avatar>
        <Typography component="label" variant="h5" htmlFor="chk" className="form-header">
          Sign in
        </Typography>
        <Controller
          control={control}
          name="email"
          defaultValue=""
          render={({ field }) => (
            <TextField
              required
              fullWidth
              {...field}
              id="email"
              type="email"
              label="Email Address"
              autoComplete="email"
              margin="normal"
              autoFocus
              error={!!errors.email}
              helperText={errors.email ? errors.email?.message : ''}
            />
          )}
        />
        <Controller
          control={control}
          name="password"
          defaultValue=""
          render={({ field }) => (
            <TextField
              required
              fullWidth
              {...field}
              id="password"
              type="password"
              label="Password"
              margin="normal"
              autoComplete="current-password"
              error={!!errors.password}
              helperText={errors.password ? errors.password?.message : ''}
            />
          )}
        />
        <FormControlLabel
          control={<Checkbox value="remember" color="primary" />}
          label="Remember me"
        />

        <LoadingButton
          type="submit"
          fullWidth
          variant="contained"
          sx={{ mt: 3, mb: 2 }}
          onClick={() => trigger()}
          loading={loading}
        >
          Sign In
        </LoadingButton>
        <Grid container>
          <Grid item xs>
            <Link href="#" variant="body2">
              Forgot password?
            </Link>
          </Grid>
        </Grid>
      </form>
    </div>
  );
}

export default LoginForm;

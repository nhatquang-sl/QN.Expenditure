import {
  Avatar,
  Button,
  Checkbox,
  FormControlLabel,
  Grid,
  Link,
  TextField,
  Typography,
} from '@mui/material';
import LockOutlinedIcon from '@mui/icons-material/LockOutlined';
import { LoginData, LoginDataSchema } from './types';
import { zodResolver } from '@hookform/resolvers/zod';
import { Controller, SubmitHandler, useForm } from 'react-hook-form';

function LoginForm() {
  const {
    handleSubmit,
    trigger,
    control,
    formState: { errors },
  } = useForm<LoginData>({
    resolver: zodResolver(LoginDataSchema),
  });

  console.log(errors);
  const onSubmit: SubmitHandler<LoginData> = (data) => {
    console.log(data);
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
        <Button
          type="submit"
          fullWidth
          variant="contained"
          sx={{ mt: 3, mb: 2 }}
          onClick={() => trigger()}
        >
          Sign In
        </Button>
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

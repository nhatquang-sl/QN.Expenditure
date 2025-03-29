import { zodResolver } from '@hookform/resolvers/zod';
import LockOutlinedIcon from '@mui/icons-material/LockOutlined';
import { LoadingButton } from '@mui/lab';
import { Avatar, Box, Grid, TextField, Typography } from '@mui/material';
import { showSnackbar } from 'components/snackbar/slice';
import { useState } from 'react';
import { Controller, SubmitHandler, useForm } from 'react-hook-form';
import { useDispatch } from 'react-redux';
import { useNavigate } from 'react-router-dom';
import { authClient } from 'store';
import { Conflict, RegisterCommand } from 'store/api-client';
import { RegisterData, RegisterDataSchema } from './types';

function RegisterForm() {
  const {
    handleSubmit,
    trigger,
    control,
    formState: { errors },
  } = useForm<RegisterData>({
    resolver: zodResolver(RegisterDataSchema),
  });
  const dispatch = useDispatch();
  const navigate = useNavigate();
  const [loading, setLoading] = useState(false);

  console.log(errors);
  const onSubmit: SubmitHandler<RegisterData> = async (data) => {
    setLoading(true);
    try {
      await authClient.register(data as unknown as RegisterCommand);
      navigate('/request-activate-email', { replace: true });
    } catch (err: any) {
      if (err instanceof Conflict) {
        dispatch(showSnackbar(err.message, 'error', 'top', 'left'));
      }
    }
    // console.log(result);
    console.log({ data });
    setLoading(false);
  };

  return (
    <Box className="form-container register-form-container" sx={{ bgcolor: 'background.default' }}>
      <form className="form" onSubmit={handleSubmit(onSubmit)} noValidate>
        <Avatar sx={{ bgcolor: 'secondary.main' }} className="form-icon">
          <LockOutlinedIcon />
        </Avatar>
        <Typography
          component="label"
          variant="h5"
          htmlFor="chk"
          className="form-header"
          sx={{ color: 'text.primary' }}
        >
          Sign up
        </Typography>
        <Grid container spacing={2} sx={{ marginTop: 0 }}>
          <Grid item xs={12} sm={6}>
            <Controller
              control={control}
              name="firstName"
              defaultValue=""
              render={({ field }) => (
                <TextField
                  required
                  fullWidth
                  {...field}
                  id="firstName"
                  label="First Name"
                  autoComplete="given-name"
                  error={!!errors.firstName}
                  helperText={errors.firstName ? errors.firstName?.message : ''}
                />
              )}
            />
          </Grid>
          <Grid item xs={12} sm={6}>
            <Controller
              control={control}
              name="lastName"
              defaultValue=""
              render={({ field }) => (
                <TextField
                  required
                  fullWidth
                  {...field}
                  id="lastName"
                  label="Last Name"
                  autoComplete="family-name"
                  error={!!errors.lastName}
                  helperText={errors.lastName ? errors.lastName?.message : ''}
                />
              )}
            />
          </Grid>
          <Grid item xs={12}>
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
                  error={!!errors.email}
                  helperText={errors.email ? errors.email?.message : ''}
                />
              )}
            />
          </Grid>
          <Grid item xs={12}>
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
                  autoComplete="new-password"
                  error={!!errors.password}
                  helperText={errors.password ? errors.password?.message : ''}
                />
              )}
            />
          </Grid>
          <Grid item xs={12}>
            <Controller
              control={control}
              name="confirmPassword"
              defaultValue=""
              render={({ field }) => (
                <TextField
                  required
                  fullWidth
                  {...field}
                  id="confirmPassword"
                  type="password"
                  label="Confirm Password"
                  autoComplete="new-password"
                  error={!!errors.confirmPassword}
                  helperText={errors.confirmPassword ? errors.confirmPassword?.message : ''}
                />
              )}
            />
          </Grid>
        </Grid>
        <LoadingButton
          type="submit"
          fullWidth
          variant="contained"
          sx={{ mt: 3, mb: 2 }}
          onClick={() => trigger()}
          loading={loading}
        >
          Sign Up
        </LoadingButton>
      </form>
    </Box>
  );
}

export default RegisterForm;

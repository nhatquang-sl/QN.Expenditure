import { Avatar, Box, Button, Grid, TextField, Typography } from '@mui/material';
import LockOutlinedIcon from '@mui/icons-material/LockOutlined';
import { SubmitHandler, useForm, Controller } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { RegisterDataSchema, RegisterData } from './types';

function RegisterForm() {
  const {
    handleSubmit,
    trigger,
    control,
    formState: { errors },
  } = useForm<RegisterData>({
    resolver: zodResolver(RegisterDataSchema),
  });

  console.log(errors);
  const onSubmit: SubmitHandler<RegisterData> = (data) => {
    console.log(data);
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
        </Grid>
        <Button
          type="submit"
          fullWidth
          variant="contained"
          sx={{ mt: 3, mb: 2 }}
          onClick={() => trigger()}
        >
          Sign Up
        </Button>
      </form>
    </Box>
  );
}

export default RegisterForm;

import LockOutlinedIcon from '@mui/icons-material/LockOutlined';
import { Avatar, Box, Button, Grid, TextField, Typography } from '@mui/material';
import { createTheme } from '@mui/material/styles';
import * as React from 'react';

const darkTheme = createTheme({
  palette: {
    mode: 'dark',
  },
});
console.log(darkTheme);

function RegisterForm() {
  const handleSubmit = (event: React.FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    const data = new FormData(event.currentTarget);
    console.log({
      email: data.get('email'),
      password: data.get('password'),
    });
  };

  return (
    // <ThemeProvider theme={darkTheme}>
    // <CssBaseline />
    <Box className="form-container register-form-container" sx={{ bgcolor: 'background.default' }}>
      <form className="form">
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
            <TextField
              autoComplete="given-name"
              name="firstName"
              required
              fullWidth
              id="firstName"
              label="First Name"
            />
          </Grid>
          <Grid item xs={12} sm={6}>
            <TextField
              required
              fullWidth
              id="lastName"
              label="Last Name"
              name="lastName"
              autoComplete="family-name"
            />
          </Grid>
          <Grid item xs={12}>
            <TextField
              required
              fullWidth
              id="email"
              label="Email Address"
              name="email"
              autoComplete="email"
            />
          </Grid>
          <Grid item xs={12}>
            <TextField
              required
              fullWidth
              name="password"
              label="Password"
              type="password"
              id="password"
              autoComplete="new-password"
            />
          </Grid>
        </Grid>
        <Button type="submit" fullWidth variant="contained" sx={{ mt: 3, mb: 2 }}>
          Sign Up
        </Button>
      </form>
    </Box>
    // </ThemeProvider>
  );
}

export default RegisterForm;

import { Box, CircularProgress, Container } from '@mui/material';
import { showSnackbar } from 'components/snackbar/slice';
import { useEffect } from 'react';
import { useDispatch } from 'react-redux';
import { useLocation, useNavigate } from 'react-router-dom';
import { authClient } from 'store';

const RegisterConfirm = () => {
  const { search } = useLocation();
  const navigate = useNavigate();
  const dispatch = useDispatch();

  useEffect(() => {
    const registerConfirm = async () => {
      const params = new URLSearchParams(search);

      try {
        await authClient.confirmEmail(params.get('userId') ?? '', params.get('code') ?? '');

        dispatch(showSnackbar('Activate your account success', 'success'));
      } catch (err) {
        const { message } = err as { message: string };
        dispatch(showSnackbar(message, 'error'));
      }
      navigate('/login', { replace: true });
    };

    registerConfirm();
  }, [search, navigate, dispatch]);

  return (
    <Container component="main" maxWidth="xs">
      <Box sx={{ display: 'flex', justifyContent: 'center' }}>
        <CircularProgress />
      </Box>
    </Container>
  );
};

export default RegisterConfirm;

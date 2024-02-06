import LoadingButton from '@mui/lab/LoadingButton';
import { Box, Container } from '@mui/material';
import { useEffect, useState } from 'react';
import { useSelector } from 'react-redux';
import { useNavigate } from 'react-router-dom';
import { TokenType, selectAuthType } from '../slice';

import { authClient } from 'store';

const RequestActivateEmail = () => {
  const navigate = useNavigate();
  const [loading, setLoading] = useState(false);
  const authType = useSelector(selectAuthType);

  useEffect(() => {
    switch (authType) {
      case TokenType.Login:
        navigate('/', { replace: true });
        break;
      case TokenType.NeedActivate:
        navigate('/request-activate-email', { replace: true });
        break;
    }
  }, [authType, navigate]);

  const handleSendActivateEmail = async () => {
    setLoading(true);
    try {
      await authClient.resendEmailConfirmation();
    } catch (err: any) {
      console.log(err);
    }
    setLoading(false);
  };

  return (
    <Container component="main" maxWidth="xs">
      <Box
        sx={{
          display: 'flex',
          flexDirection: 'column',
          alignItems: 'center',
        }}
      >
        <LoadingButton loading={loading} onClick={handleSendActivateEmail}>
          Send active link to my email
        </LoadingButton>
      </Box>
    </Container>
  );
};

export default RequestActivateEmail;

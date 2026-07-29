import Box from '@mui/material/Box';
import Container from '@mui/material/Container';
import Toolbar from '@mui/material/Toolbar';
import ConsecutiveSnackBars from 'components/snackbar';
import { Outlet } from 'react-router-dom';

function Main() {
  return (
    <Box
      component="main"
      sx={{
        backgroundColor: (theme) =>
          theme.palette.mode === 'light' ? theme.palette.grey[100] : theme.palette.grey[900],
        flexGrow: 1,
        height: '100vh',
        display: 'flex',
        flexDirection: 'column',
        overflow: 'hidden',
      }}
    >
      <Toolbar />
      <Container maxWidth={false} sx={{ pt: 4, pb: 4, margin: 0, flex: 1, overflow: 'auto' }}>
        <Outlet />
        <ConsecutiveSnackBars />
        {/* <Copyright sx={{ pt: 4 }} /> */}
      </Container>
    </Box>
  );
}

export default Main;

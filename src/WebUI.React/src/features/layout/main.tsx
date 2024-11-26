import Box from '@mui/material/Box';
import Container from '@mui/material/Container';
import Toolbar from '@mui/material/Toolbar';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import ConsecutiveSnackBars from 'components/snackbar';
import { Outlet } from 'react-router-dom';
const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      refetchOnWindowFocus: false,
      retry: false,
      networkMode: 'online',
    },
  },
});
function Main() {
  return (
    <QueryClientProvider client={queryClient}>
      <Box
        component="main"
        sx={{
          backgroundColor: (theme) =>
            theme.palette.mode === 'light' ? theme.palette.grey[100] : theme.palette.grey[900],
          flexGrow: 1,
          height: '100vh',
          overflow: 'auto',
        }}
      >
        <Toolbar />
        <Container maxWidth={false} sx={{ mt: 4, mb: 4 }}>
          <Outlet />
          <ConsecutiveSnackBars />
          {/* <Copyright sx={{ pt: 4 }} /> */}
        </Container>
      </Box>
    </QueryClientProvider>
  );
}

export default Main;

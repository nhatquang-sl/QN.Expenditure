import { Backdrop, CircularProgress } from '@mui/material';

export const BackdropLoading = (props: { loading: boolean }) => {
  const { loading } = props;
  return (
    <Backdrop
      sx={{ position: 'absolute', color: '#fff', zIndex: (theme) => theme.zIndex.drawer + 1 }}
      open={loading}
    >
      <CircularProgress color="inherit" />
    </Backdrop>
  );
};

import ChevronLeftIcon from '@mui/icons-material/ChevronLeft';
import LogoutIcon from '@mui/icons-material/Logout';
import {
  Divider,
  IconButton,
  List,
  ListItemButton,
  ListItemIcon,
  ListItemText,
  Toolbar,
  styled,
} from '@mui/material';
import MuiDrawer from '@mui/material/Drawer';
import { logout } from 'features/auth/slice';
import { useDispatch, useSelector } from 'react-redux';
import { useNavigate } from 'react-router-dom';
import { RootState } from 'store';
import { BnbMenuItems, mainListItems } from './listItems';
import { drawerWidth, toggleDrawer } from './slice';

const Drawer = styled(MuiDrawer, { shouldForwardProp: (prop) => prop !== 'open' })(
  ({ theme, open }) => ({
    '& .MuiDrawer-paper': {
      position: 'relative',
      whiteSpace: 'nowrap',
      width: drawerWidth,
      transition: theme.transitions.create('width', {
        easing: theme.transitions.easing.sharp,
        duration: theme.transitions.duration.enteringScreen,
      }),
      boxSizing: 'border-box',
      ...(!open && {
        overflowX: 'hidden',
        transition: theme.transitions.create('width', {
          easing: theme.transitions.easing.sharp,
          duration: theme.transitions.duration.leavingScreen,
        }),
        width: theme.spacing(7),
        [theme.breakpoints.up('sm')]: {
          width: theme.spacing(9),
        },
      }),
    },
  })
);
function Sidebar() {
  const open = useSelector((state: RootState) => state.layout.open);
  const dispatch = useDispatch();
  const navigate = useNavigate();

  const toggle = () => {
    dispatch(toggleDrawer());
  };
  return (
    <Drawer variant="permanent" open={open}>
      <Toolbar
        sx={{
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'flex-end',
          px: [1],
        }}
      >
        <IconButton onClick={toggle}>
          <ChevronLeftIcon />
        </IconButton>
      </Toolbar>
      <Divider />
      <List component="nav" sx={{ flex: 1 }}>
        {mainListItems}
        <Divider sx={{ my: 1 }} />
        {/* {secondaryListItems} */}
        <BnbMenuItems />
        <Divider sx={{ my: 1 }} />
        <ListItemButton
          sx={{ position: 'absolute', bottom: 0, width: '100%' }}
          onClick={() => {
            dispatch(logout());
            navigate('/login', { replace: true });
          }}
        >
          <ListItemIcon>
            <LogoutIcon />
          </ListItemIcon>
          <ListItemText primary="Logout" />
        </ListItemButton>
      </List>
    </Drawer>
  );
}

export default Sidebar;

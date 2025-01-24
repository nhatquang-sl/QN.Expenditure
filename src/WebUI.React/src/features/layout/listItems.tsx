import { ExpandLess, ExpandMore } from '@mui/icons-material';
import BarChartIcon from '@mui/icons-material/BarChart';
import DashboardIcon from '@mui/icons-material/Dashboard';
import LayersIcon from '@mui/icons-material/Layers';
import ManageAccountsIcon from '@mui/icons-material/ManageAccounts';
import ManageHistoryIcon from '@mui/icons-material/ManageHistory';
import PeopleIcon from '@mui/icons-material/People';
import ShoppingCartIcon from '@mui/icons-material/ShoppingCart';
import { Collapse, Icon, List } from '@mui/material';
import ListItemButton from '@mui/material/ListItemButton';
import ListItemIcon from '@mui/material/ListItemIcon';
import ListItemText from '@mui/material/ListItemText';
import ListSubheader from '@mui/material/ListSubheader';
import * as React from 'react';
import { Link } from 'react-router-dom';

export const mainListItems = (
  <React.Fragment>
    <ListItemButton>
      <ListItemIcon>
        <DashboardIcon />
      </ListItemIcon>
      <ListItemText primary="Dashboard" />
    </ListItemButton>
    <ListItemButton>
      <ListItemIcon>
        <ShoppingCartIcon />
      </ListItemIcon>
      <ListItemText primary="Orders" />
    </ListItemButton>
    <ListItemButton>
      <ListItemIcon>
        <PeopleIcon />
      </ListItemIcon>
      <ListItemText primary="Customers" />
    </ListItemButton>
    <ListItemButton>
      <ListItemIcon>
        <BarChartIcon />
      </ListItemIcon>
      <ListItemText primary="Reports" />
    </ListItemButton>
    <ListItemButton>
      <ListItemIcon>
        <LayersIcon />
      </ListItemIcon>
      <ListItemText primary="Integrations" />
    </ListItemButton>
  </React.Fragment>
);

export const secondaryListItems = (
  <React.Fragment>
    <ListSubheader component="div" inset>
      Binance
    </ListSubheader>
    <ListItemButton component={Link} to="bnb/setting">
      <ListItemIcon>
        <ManageAccountsIcon />
      </ListItemIcon>
      <ListItemText primary="Setting" />
    </ListItemButton>
    <ListItemButton component={Link} to="bnb/sync-settings">
      <ListItemIcon>
        <ManageHistoryIcon />
      </ListItemIcon>
      <ListItemText primary="Sync Settings" />
    </ListItemButton>
    <ListItemButton component={Link} to="bnb/spot-orders">
      <ListItemIcon>
        <Icon>history</Icon>
      </ListItemIcon>
      <ListItemText primary="Histories" />
    </ListItemButton>
    <ListItemButton component={Link} to="bnb/spot-grids">
      <ListItemIcon>
        <Icon>assignment</Icon>
      </ListItemIcon>
      <ListItemText primary="Spot Grids" />
    </ListItemButton>
  </React.Fragment>
);

export function BnbMenuItems() {
  const [openSpotGrids, setOpenSpotGrids] = React.useState(true);
  const handleClickSpotGrids = () => {
    setOpenSpotGrids(!openSpotGrids);
  };
  return (
    <React.Fragment>
      <ListSubheader component="div" inset>
        Binance
      </ListSubheader>
      <ListItemButton component={Link} to="bnb/setting">
        <ListItemIcon>
          <ManageAccountsIcon />
        </ListItemIcon>
        <ListItemText primary="Setting" />
      </ListItemButton>
      <ListItemButton component={Link} to="bnb/sync-settings">
        <ListItemIcon>
          <ManageHistoryIcon />
        </ListItemIcon>
        <ListItemText primary="Sync Settings" />
      </ListItemButton>
      <ListItemButton component={Link} to="bnb/spot-orders">
        <ListItemIcon>
          <Icon>history</Icon>
        </ListItemIcon>
        <ListItemText primary="Histories" />
      </ListItemButton>
      {/* <ListItemButton component={Link} to="bnb/spot-grids"> */}
      <ListItemButton onClick={handleClickSpotGrids}>
        <ListItemIcon>
          <Icon>assignment</Icon>
        </ListItemIcon>
        <ListItemText primary="Spot Grids" />
        {openSpotGrids ? <ExpandLess /> : <ExpandMore />}
      </ListItemButton>
      <Collapse in={openSpotGrids} timeout="auto" unmountOnExit>
        <List component="div" disablePadding>
          <ListItemButton sx={{ pl: 12 }} component={Link} to="bnb/spot-grids">
            <ListItemText primary="List" />
          </ListItemButton>
          <ListItemButton sx={{ pl: 12 }} component={Link} to="bnb/spot-grids/create">
            <ListItemText primary="Create" />
          </ListItemButton>
        </List>
      </Collapse>
    </React.Fragment>
  );
}

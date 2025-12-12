import { Icon, IconButton, Menu, MenuItem, TableCell, TableRow } from '@mui/material';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { BackdropLoading } from 'components/backdrop-loading';
import React, { useEffect } from 'react';
import { useDispatch } from 'react-redux';
import { useNavigate } from 'react-router-dom';
import { spotGridClient } from 'store';
import { SpotGridDto } from 'store/api-client';
import CurrentPrice from '../components/current-price';
import TotalProfit from '../components/total-profit';
import { setPrice } from '../slice';
import { fixedNumber } from '../utils';

const SpotGridItem = (props: { spotGrid: SpotGridDto }) => {
  const { id, symbol, investment, triggerPrice, lowerPrice, upperPrice, profit } = props.spotGrid;
  const navigate = useNavigate();
  const dispatch = useDispatch();
  // const [curPrice, setCurPrice] = useState(0);
  const queryClient = useQueryClient();
  const mutation = useMutation({
    mutationFn: () => spotGridClient.delete(id),
    onSuccess: (result) => {
      // Replace optimistic todo in the todos list with the result
      queryClient.setQueryData(['SpotGrids'], (spotGrids: SpotGridDto[]) =>
        spotGrids.filter((g) => g.id != result.id)
      );
    },
  });

  useEffect(() => {
    // WS: get market price
    const markPriceWS = new WebSocket(
      `wss://stream.binance.com:9443/ws/${symbol.replace('-', '').toLowerCase()}@kline_1h`
    );
    markPriceWS.onmessage = function (event) {
      try {
        const json = JSON.parse(event.data);
        const curPrice = fixedNumber(Number(json.k.c));
        dispatch(setPrice([symbol, curPrice]));
      } catch (err) {
        console.log(err);
      }
    };

    return () => markPriceWS.close();
  }, [symbol, dispatch]);

  return (
    <TableRow hover tabIndex={-1} key={symbol} sx={{ position: 'relative' }}>
      <TableCell>{symbol}</TableCell>
      <TableCell align="right">{investment}</TableCell>
      <TableCell align="right">
        <TotalProfit {...props} />
      </TableCell>
      <TableCell align="right">{profit}</TableCell>
      <TableCell align="right">{symbol}</TableCell>
      <TableCell align="right">
        <CurrentPrice symbol={symbol} />
      </TableCell>
      <TableCell align="right">{triggerPrice}</TableCell>
      <TableCell align="right">
        {lowerPrice} ~ {upperPrice}
      </TableCell>
      <TableCell align="right">
        <MoreActions
          onDelete={async () => mutation.mutate()}
          onUpdate={() => {
            navigate(`/bnb/spot-grids/${id}`, { replace: true });
          }}
        />
        <BackdropLoading loading={mutation.isPending} />
      </TableCell>
    </TableRow>
  );
};

const MoreActions = (props: { onDelete: () => Promise<void>; onUpdate: () => void }) => {
  const [anchorEl, setAnchorEl] = React.useState<null | HTMLElement>(null);
  const open = Boolean(anchorEl);

  const handleClick = (event: React.MouseEvent<HTMLButtonElement>) => {
    setAnchorEl(event.currentTarget);
  };

  const handleClose = () => {
    setAnchorEl(null);
  };

  const handleAction = (action: string) => {
    console.log(`Selected action: ${action}`);
    handleClose();
  };

  return (
    <>
      <IconButton
        aria-label="more actions"
        aria-controls="more-actions-menu"
        aria-haspopup="true"
        onClick={handleClick}
      >
        <Icon>more_vert</Icon>
      </IconButton>
      <Menu
        id="more-actions-menu"
        anchorEl={anchorEl}
        open={open}
        onClose={handleClose}
        MenuListProps={{
          'aria-labelledby': 'more-actions-button',
        }}
      >
        <MenuItem onClick={props.onUpdate}>Edit</MenuItem>
        <MenuItem onClick={props.onDelete}>Delete</MenuItem>
        <MenuItem onClick={() => handleAction('Share')}>Share</MenuItem>
      </Menu>
    </>
  );
};

export default SpotGridItem;

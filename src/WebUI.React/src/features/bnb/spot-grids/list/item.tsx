import { Icon, IconButton, Menu, MenuItem, TableCell, TableRow } from '@mui/material';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { BackdropLoading } from 'components/backdrop-loading';
import React, { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { bnbSpotGridClient } from 'store';
import { SpotGridDto } from 'store/api-client';

const SpotGridItem = (props: { spotGrid: SpotGridDto }) => {
  const { spotGrid } = props;
  const navigate = useNavigate();
  const [curPrice, setCurPrice] = useState(0);
  const queryClient = useQueryClient();
  const mutation = useMutation({
    mutationFn: () => bnbSpotGridClient.delete(spotGrid.id),
    onSuccess: (result) => {
      // Replace optimistic todo in the todos list with the result
      queryClient.setQueryData(['SpotGrids'], (spotGrids: SpotGridDto[]) =>
        spotGrids.filter((spotGrid) => spotGrid.id != result.id)
      );
    },
  });

  useEffect(() => {
    // WS: get market price
    const markPriceWS = new WebSocket(
      `wss://stream.binance.com:9443/ws/${spotGrid.symbol.toLowerCase()}@kline_1h`
    );
    markPriceWS.onmessage = function (event) {
      try {
        const json = JSON.parse(event.data);

        setCurPrice(Number(json.k.c));
        // console.log(json.k.c);
      } catch (err) {
        console.log(err);
      }
    };

    return () => markPriceWS.close();
  }, [spotGrid.symbol]);

  return (
    <TableRow hover tabIndex={-1} key={spotGrid.symbol} sx={{ position: 'relative' }}>
      <TableCell>{spotGrid.symbol}</TableCell>
      <TableCell align="right">{spotGrid.investment}</TableCell>
      <TableCell align="right">{spotGrid.symbol}</TableCell>
      <TableCell align="right">{spotGrid.symbol}</TableCell>
      <TableCell align="right">{spotGrid.symbol}</TableCell>
      <TableCell align="right">{curPrice.toLocaleString('en-US')}</TableCell>
      <TableCell align="right">{spotGrid.triggerPrice}</TableCell>
      <TableCell align="right">
        {spotGrid.lowerPrice} ~ {spotGrid.upperPrice}
      </TableCell>
      <TableCell align="right">
        <MoreActions
          onDelete={async () => mutation.mutate()}
          onUpdate={() => {
            navigate(`/bnb/spot-grids/${spotGrid.id}`, { replace: true });
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

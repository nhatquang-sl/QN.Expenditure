import { Typography } from '@mui/material';
import { useEffect, useState } from 'react';

export default function Header() {
  const [curPrice, setCurPrice] = useState(0);
  useEffect(() => {
    // WS: get market price
    const markPriceWS = new WebSocket(`wss://stream.binance.com:9443/ws/BTCUSDT@kline_1m`);
    markPriceWS.onmessage = function (event) {
      try {
        const json = JSON.parse(event.data);
        console.log(json);
      } catch (err) {
        console.log(err);
      }
    };

    return () => markPriceWS.close();
  }, []);
  return (
    <>
      <Typography component="h2" variant="h6" color="primary" gutterBottom>
        Grid
      </Typography>
      <Typography>{curPrice}</Typography>
    </>
  );
}

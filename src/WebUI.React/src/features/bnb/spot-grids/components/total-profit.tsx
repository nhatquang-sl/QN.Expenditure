import { Typography } from '@mui/material';
import { useSelector } from 'react-redux';
import { SpotGridDto } from 'store/api-client';
import { selectPrice } from '../slice';

export default function TotalProfit(props: { spotGrid: SpotGridDto }) {
  const { profit, baseBalance, quoteBalance, investment, symbol } = props.spotGrid;
  const curPrice = useSelector(selectPrice(symbol));

  return <Typography>{baseBalance * curPrice + quoteBalance + profit - investment}</Typography>;
}

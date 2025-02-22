import { Typography } from '@mui/material';
import { useSelector } from 'react-redux';
import { selectPrice } from '../slice';

export default function CurrentPrice(props: { symbol: string }) {
  const curPrice = useSelector(selectPrice(props.symbol));
  return <Typography>{curPrice}</Typography>;
}

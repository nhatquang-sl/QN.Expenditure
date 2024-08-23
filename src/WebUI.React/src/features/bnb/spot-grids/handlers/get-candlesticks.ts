import Kline from '../dtos/kline';

const getCandlesticks = async (symbol = 'BTCUSDT', interval = '5m') => {
  const url = `https://api.binance.com/api/v3/klines?symbol=${symbol}&interval=${interval}`;
  const result = await fetch(url);
  const data = await result.json();
  return data.map((d: any[]) => new Kline(d));
};

export default getCandlesticks;

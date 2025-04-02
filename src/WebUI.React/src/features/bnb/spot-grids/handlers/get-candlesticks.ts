import Kline from '../dtos/kline';

const getCandlesticks = async (symbol = 'BTCUSDT', interval = '5m'): Promise<Kline[]> => {
  const url = `https://api.binance.com/api/v3/klines?symbol=${symbol}&interval=${interval}`;
  const result = await fetch(url);
  const data = await result.json();
  return data.map(
    (d: [number, string, string, string, string, string, number, string, number, string, string]) =>
      new Kline(d)
  );
};

export default getCandlesticks;

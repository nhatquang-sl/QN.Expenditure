import Kline from '../dtos/kline';

const getCandlesticks = async (
  symbol = 'BTCUSDT',
  interval = '5m',
  startTime?: number,
  endTime?: number
): Promise<Kline[]> => {
  const params = new URLSearchParams({ symbol, interval });
  if (startTime != null) params.append('startTime', String(startTime));
  if (endTime != null) params.append('endTime', String(endTime));

  const url = `https://api.binance.com/api/v3/klines?${params}`;
  const result = await fetch(url);
  const data = await result.json();
  return data.map(
    (d: [number, string, string, string, string, string, number, string, number, string, string]) =>
      new Kline(d)
  );
};

export default getCandlesticks;

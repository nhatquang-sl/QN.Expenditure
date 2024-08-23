import { CandlestickData, IChartApi } from 'lightweight-charts';
import Kline from '../dtos/kline';

export const candleStickDefaultConfig = {
  upColor: '#00c176',
  downColor: '#cf304a',
  borderDownColor: '#cf304a',
  borderUpColor: '#00c176',
  wickDownColor: '#838ca1',
  wickUpColor: '#838ca1',
};

// https://tradingview.github.io/lightweight-charts/docs/api/interfaces/IChartApiBase#addcandlestickseries
const addCandlesticks = (chart: IChartApi, candles: Kline[]) => {
  const candleSeries = chart.addCandlestickSeries(candleStickDefaultConfig);
  candleSeries.applyOptions({
    priceFormat: {
      type: 'price',
      precision: 2,
      minMove: 0.01,
    },
  });
  candleSeries.setData(
    candles.map((c: Kline) => {
      return {
        time: c.openTime / 1000,
        open: c.open,
        high: c.high,
        low: c.low,
        close: c.close,
      } as CandlestickData;
    })
  );

  return candleSeries;
};

export default addCandlesticks;

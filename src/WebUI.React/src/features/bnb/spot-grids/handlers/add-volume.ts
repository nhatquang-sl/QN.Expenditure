import { HistogramData, IChartApi } from 'lightweight-charts';
import Kline from '../dtos/kline';

// https://tradingview.github.io/lightweight-charts/docs/api/interfaces/IChartApiBase#addhistogramseries
// https://tradingview.github.io/lightweight-charts/tutorials/how_to/price-and-volume
const addVolume = (chart: IChartApi, candles: Kline[]) => {
  const volumeSeries = chart.addHistogramSeries({
    color: '#26a69a',
    priceFormat: {
      type: 'volume',
    },
    priceScaleId: '', // set as an overlay by setting a blank priceScaleId
    priceLineVisible: false,
    lastValueVisible: false,
  });

  volumeSeries.priceScale().applyOptions({
    scaleMargins: {
      top: 0.7, // highest point of the series will be 70% away from the top
      bottom: 0,
    },
  });

  volumeSeries.setData(
    candles.map((c: Kline) => {
      return {
        time: c.openTime / 1000,
        value: c.volume,
        color: c.open < c.close ? '#005a40' : '#82112b',
      } as HistogramData;
    })
  );

  return volumeSeries;
};

export default addVolume;

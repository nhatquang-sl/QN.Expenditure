import {
  CreatePriceLineOptions,
  HistogramData,
  IChartApi,
  ISeriesApi,
  LineData,
  LineStyle,
} from 'lightweight-charts';
import _ from 'lodash';
import { round2Dec, round4Dec } from 'store/constants';
import Kline from '../dtos/kline';
const findIntersection = (array: { low: number; high: number }[]) => {
  const intersections: { low: number; high: number; countLow: number; countHigh: number }[] = [];
  let maxCount = 0,
    result = 0;
  for (let i = 0; i < array.length; i++) {
    const x = array[i];
    intersections.push({ low: x.low, high: x.high, countLow: 0, countHigh: 0 });
    for (const a of array) {
      if (x.low >= a.low && x.low <= a.high) intersections[i].countLow++;
      else if (x.high >= a.low && x.high <= a.high) intersections[i].countHigh++;
    }

    if (Math.max(intersections[i].countHigh, intersections[i].countLow) < maxCount) continue;

    maxCount = Math.max(intersections[i].countHigh, intersections[i].countLow);
    if (intersections[i].countHigh > intersections[i].countLow) result = intersections[i].high;
    else result = intersections[i].low;
  }

  // console.log({ result, maxCount });
  if (maxCount > 14) return result;
  return 0;
};

const addBollingerBands = (chart: IChartApi, rsiChart: IChartApi, candles: Kline[]) => {
  let avgGain = -1,
    avgLoss = -1;
  const klines: Kline[] = JSON.parse(JSON.stringify(candles));
  const supportSeries: ISeriesApi<'Line'>[] = [];
  const supportSet = new Set();

  for (let i = 1; i < klines.length; i++) {
    const kline = klines[i];
    const closeChange = parseFloat((kline.close - klines[i - 1].close).toFixed(4));
    closeChange > 0 ? (kline.gain = closeChange) : (kline.loss = Math.abs(closeChange));

    if (i > 20) {
      const sma20 = _.sumBy(_.slice(klines, i - 19, i + 1), 'close') / 20;
      const stdDev = standardDeviation(_.slice(klines, i - 19, i + 1));
      const bolu = sma20 + 2 * stdDev;
      const bold = sma20 - 2 * stdDev;
      const [rsi, gain, loss] = relativeStrengthIndex(
        _.slice(klines, i - 13, i + 1),
        avgGain,
        avgLoss
      );

      avgGain = gain;
      avgLoss = loss;

      kline.bold = round2Dec(bold);
      kline.bolu = round2Dec(bolu);
      kline.rsi = rsi;
      kline.sma20 = round2Dec(sma20);

      const line = findIntersection(_.slice(klines, i - 19, i + 1));
      if (line) {
        // console.log(line);
        if (!supportSet.has(line)) {
          supportSet.add(line);
          const support = chart.addLineSeries({
            color: 'blue',
            lineWidth: 1,
            crosshairMarkerVisible: false,
            lastValueVisible: false,
          });
          support.setData(
            klines.map((k) => ({ time: k.openTime / 1000, value: line } as LineData))
          );

          supportSeries.push(support);
        }
      }
    }
  }

  const sma20Series = chart.addLineSeries({
    color: 'red',
    lineWidth: 1,
    priceLineVisible: false,
    lastValueVisible: false,
  });
  const boluSeries = chart.addLineSeries({
    color: 'blue',
    lineWidth: 1,
    priceLineVisible: false,
    lastValueVisible: false,
  });
  const boldSeries = chart.addLineSeries({
    color: 'blue',
    lineWidth: 1,
    priceLineVisible: false,
    lastValueVisible: false,
  });

  sma20Series.setData(klines.map((k) => ({ time: k.openTime / 1000, value: k.sma20 } as LineData)));
  boluSeries.setData(klines.map((k) => ({ time: k.openTime / 1000, value: k.bolu } as LineData)));
  boldSeries.setData(klines.map((k) => ({ time: k.openTime / 1000, value: k.bold } as LineData)));

  const rsiSeries = rsiChart.addLineSeries({
    color: 'purple',
    lineWidth: 1,
    priceFormat: {
      type: 'percent',
    },
  });

  rsiSeries.setData(
    klines.map((c: Kline) => {
      return {
        time: c.openTime / 1000,
        value: c.rsi,
      } as HistogramData;
    })
  );

  const maxPriceLine = {
    price: 70,
    color: 'purple',
    lineWidth: 1,
    lineStyle: LineStyle.Solid,
    axisLabelVisible: true,
    title: '',
  } as CreatePriceLineOptions;

  rsiSeries.createPriceLine(maxPriceLine);
  const minPriceLine = {
    price: 30,
    color: 'purple',
    lineWidth: 1,
    lineStyle: LineStyle.Solid,
    axisLabelVisible: true,
    title: '',
  } as CreatePriceLineOptions;

  rsiSeries.createPriceLine(minPriceLine);

  return { boluSeries, sma20Series, boldSeries, rsiSeries, supportSeries };
};

// https://www.wikihow.vn/T%C3%ADnh-%C4%90%E1%BB%99-l%E1%BB%87ch-Chu%E1%BA%A9n
const standardDeviation = (candles: Kline[]) => {
  const period = candles.length;
  const total = _.sumBy(candles, 'close');
  const mean = total / period;
  let variance = 0;
  candles.forEach((kline) => {
    variance += Math.pow(kline.close - mean, 2);
  });
  variance = variance / (period - 1);
  return round2Dec(Math.sqrt(variance));
};

const relativeStrengthIndex = (
  klines: Kline[],
  avgGain: number = -1,
  avgLoss: number = -1
): [number, number, number] => {
  const period = klines.length;
  const kline = klines[period - 1];

  if (avgGain === -1 && avgLoss === -1) {
    avgGain = round4Dec(_.sumBy(klines, 'gain') / period);
    avgLoss = round4Dec(_.sumBy(klines, 'loss') / period);
  } else {
    avgGain = round4Dec((avgGain * (period - 1) + kline.gain) / period);
    avgLoss = round4Dec((avgLoss * (period - 1) + kline.loss) / period);
  }
  const rs = avgGain / avgLoss;
  const rsi = round2Dec(100 - 100 / (1 + rs));

  return [rsi, avgGain, avgLoss];
};

export default addBollingerBands;

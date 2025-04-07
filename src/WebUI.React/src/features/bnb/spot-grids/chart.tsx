/* eslint-disable @typescript-eslint/no-explicit-any */
import { Paper } from '@mui/material';
import { CandlestickData, IChartApi, ISeriesApi, LineData, createChart } from 'lightweight-charts';
import { memo, useCallback, useEffect, useRef } from 'react';
import { useSelector } from 'react-redux';
import { RootState } from 'store';
import addBollingerBands from './handlers/add-bollinger-bands';
import addCandlesticks from './handlers/add-candlesticks';
import addVolume from './handlers/add-volume';
import getCandlesticks from './handlers/get-candlesticks';
import { defaultChartLayout } from './utils/constants';

// https://github.com/tradingview/lightweight-charts/issues/50
// https://github.com/justinkx/react-crypto-chart/blob/main/src/TradeView.tsx
let candleSeries: ISeriesApi<'Candlestick'> | null = null,
  volumeSeries: ISeriesApi<'Histogram'> | null = null;
let boluSeries: ISeriesApi<'Line'>,
  sma20Series: ISeriesApi<'Line'> | null = null,
  boldSeries: ISeriesApi<'Line'> | null = null,
  rsiSeries: ISeriesApi<'Line'>,
  supportSeries: ISeriesApi<'Line'>[] = [];
const gridSeries: ISeriesApi<'Line'>[] = [];
let mainChart: IChartApi;
let rsiChart: IChartApi;
let markPriceWS: WebSocket | null = null;
function Chart(props: { pair: string; interval: string }) {
  const { pair, interval } = props;
  const { gridDetails, triggerPrice } = useSelector((state: RootState) => state.spotGridDetails);
  const resizeObserver = useRef<any>();

  const chartContainerRef = useRef<string | any>();
  const rsiContainerRef = useRef<string | any>();

  const initialChart = useCallback(async () => {
    if (mainChart == null) {
      mainChart = createChart(chartContainerRef.current, {
        width: chartContainerRef.current.clientWidth,
        height: chartContainerRef.current.clientHeight,
        timeScale: { visible: false },
        ...defaultChartLayout,
      });
    }

    if (rsiChart == null) {
      rsiChart = createChart(rsiContainerRef.current, {
        width: rsiContainerRef.current.clientWidth,
        height: rsiContainerRef.current.clientHeight,
        rightPriceScale: {
          minimumWidth: 72,
        },
        timeScale: {
          borderColor: '#485c7b',
          timeVisible: true,
          secondsVisible: false,
        },
        ...defaultChartLayout,
      });
    }

    const mainChartVisibleLogicalRangeChangeHandler = (timeRange: any) => {
      timeRange && rsiChart?.timeScale().setVisibleLogicalRange(timeRange);
    };

    const rsiChartVisibleLogicalRangeChangeHandler = (timeRange: any) => {
      timeRange && mainChart?.timeScale().setVisibleLogicalRange(timeRange);
    };
    const getCrosshairDataPoint = (series: ISeriesApi<'Line'>, param: any) => {
      if (!param.time) {
        return null;
      }
      const dataPoint = param.seriesData.get(series);
      return dataPoint || null;
    };

    const syncCrosshair = (chart: IChartApi, series: ISeriesApi<'Line'>, dataPoint: any) => {
      if (dataPoint) {
        chart.setCrosshairPosition(dataPoint.value, dataPoint.time, series);
        return;
      }
      chart.clearCrosshairPosition();
    };

    mainChart
      .timeScale()
      .unsubscribeVisibleLogicalRangeChange(mainChartVisibleLogicalRangeChangeHandler);

    rsiChart
      .timeScale()
      .unsubscribeVisibleLogicalRangeChange(rsiChartVisibleLogicalRangeChangeHandler);

    const klines = await getCandlesticks(pair, interval);

    candleSeries && mainChart.removeSeries(candleSeries);
    volumeSeries && mainChart.removeSeries(volumeSeries);
    boluSeries && mainChart.removeSeries(boluSeries);
    sma20Series && mainChart.removeSeries(sma20Series);
    boldSeries && mainChart.removeSeries(boldSeries);
    rsiSeries && rsiChart.removeSeries(rsiSeries);

    if (supportSeries.length)
      for (const s of supportSeries) {
        mainChart.removeSeries(s);
      }
    console.log({ gridSeries });
    if (gridSeries.length)
      for (const s of gridSeries) {
        try {
          mainChart.removeSeries(s);
        } catch {
          // Ignore errors while removing grid series
        }
      }

    candleSeries = addCandlesticks(mainChart, klines);
    volumeSeries = addVolume(mainChart, klines);
    const bb = addBollingerBands(mainChart, rsiChart, klines);
    boluSeries = bb.boluSeries;
    sma20Series = bb.sma20Series;
    boldSeries = bb.boldSeries;
    rsiSeries = bb.rsiSeries;
    supportSeries = bb.supportSeries;

    mainChart
      .timeScale()
      .subscribeVisibleLogicalRangeChange(mainChartVisibleLogicalRangeChangeHandler);

    rsiChart
      .timeScale()
      .subscribeVisibleLogicalRangeChange(rsiChartVisibleLogicalRangeChangeHandler);

    rsiChart.subscribeCrosshairMove((param) => {
      const dataPoint = getCrosshairDataPoint(rsiSeries, param);
      syncCrosshair(mainChart, boluSeries, dataPoint);
    });
    mainChart.subscribeCrosshairMove((param) => {
      const dataPoint = getCrosshairDataPoint(boluSeries, param);
      syncCrosshair(rsiChart, rsiSeries, dataPoint);
    });
    console.log({ gridDetails });
    gridDetails.forEach((item) => {
      const grid = mainChart.addLineSeries({
        color: item.buyPrice < triggerPrice ? 'green' : 'red',
        lineWidth: 1,
        crosshairMarkerVisible: false,
        // lastValueVisible: false,
      });
      grid.setData(
        klines.map((k) => ({ time: k.openTime / 1000, value: item.buyPrice } as LineData))
      );

      gridSeries.push(grid);
    });
  }, [pair, interval, gridDetails, triggerPrice]);

  useEffect(() => {
    initialChart();
  }, [initialChart]);

  // Resize chart on container resizes.
  useEffect(() => {
    if (resizeObserver.current) return;
    resizeObserver.current = new ResizeObserver((entries) => {
      const { width, height } = entries[0].contentRect;

      mainChart && mainChart.applyOptions({ width, height });
      rsiChart && rsiChart.applyOptions({ width });
    });

    resizeObserver.current.observe(chartContainerRef.current);

    return () => resizeObserver.current.disconnect();
  }, []);

  useEffect(() => {
    // WS: get market price
    markPriceWS != null && markPriceWS.close();
    markPriceWS = new WebSocket(
      `wss://stream.binance.com:9443/ws/${pair}@kline_${interval}`.toLowerCase()
    );
    markPriceWS.onmessage = function (event) {
      try {
        const json = JSON.parse(event.data);
        // console.log(json);
        const {
          k: { t, o, c, h, l },
        } = json;

        candleSeries &&
          candleSeries.update({
            time: t / 1000,
            open: parseFloat(o),
            high: parseFloat(h),
            low: parseFloat(l),
            close: parseFloat(c),
          } as CandlestickData);
      } catch (err) {
        console.log(err);
      }
    };

    return () => markPriceWS?.close();
  }, [pair, interval]);

  return (
    <Paper elevation={0}>
      <div
        ref={chartContainerRef}
        style={{ position: 'relative', minHeight: '500px', minWidth: '400px' }}
      ></div>
      <div ref={rsiContainerRef} style={{ minHeight: '250px' }}></div>
    </Paper>
  );
}

const MemoizedChart = memo(Chart);
export default MemoizedChart;

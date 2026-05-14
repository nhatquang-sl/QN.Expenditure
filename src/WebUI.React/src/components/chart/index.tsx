/* eslint-disable @typescript-eslint/no-explicit-any */
import { Paper } from '@mui/material';
import { CandlestickData, IChartApi, ISeriesApi, LineData, createChart } from 'lightweight-charts';
import { memo, useCallback, useEffect, useRef } from 'react';
import addBollingerBands from './handlers/add-bollinger-bands';
import addCandlesticks from './handlers/add-candlesticks';
import addVolume from './handlers/add-volume';
import getCandlesticks from './handlers/get-candlesticks';
import Kline from './dtos/kline';
import { defaultChartLayout } from './utils/constants';
import { TZ_OFFSET_SECONDS } from './utils';

// https://github.com/tradingview/lightweight-charts/issues/50
// https://github.com/justinkx/react-crypto-chart/blob/main/src/TradeView.tsx

export interface GridLine {
  price: number;
  color: string;
}

interface ChartProps {
  pair: string;
  interval: string;
  gridLines?: GridLine[];
  startTime?: number;
  endTime?: number;
}

function Chart(props: ChartProps) {
  const { pair, interval, gridLines = [], startTime, endTime } = props;

  const chartContainerRef = useRef<HTMLDivElement>(null);
  const rsiContainerRef = useRef<HTMLDivElement>(null);
  const resizeObserver = useRef<ResizeObserver | null>(null);
  const markPriceWS = useRef<WebSocket | null>(null);

  const mainChart = useRef<IChartApi | null>(null);
  const rsiChart = useRef<IChartApi | null>(null);
  const candleSeries = useRef<ISeriesApi<'Candlestick'> | null>(null);
  const volumeSeries = useRef<ISeriesApi<'Histogram'> | null>(null);
  const boluSeries = useRef<ISeriesApi<'Line'> | null>(null);
  const sma20Series = useRef<ISeriesApi<'Line'> | null>(null);
  const boldSeries = useRef<ISeriesApi<'Line'> | null>(null);
  const rsiSeries = useRef<ISeriesApi<'Line'> | null>(null);
  const supportSeries = useRef<ISeriesApi<'Line'>[]>([]);
  const gridSeries = useRef<ISeriesApi<'Line'>[]>([]);

  // Store handler refs so the same function instance is used for unsubscribe
  const mainRangeHandler = useRef<((r: any) => void) | null>(null);
  const rsiRangeHandler = useRef<((r: any) => void) | null>(null);
  const mainCrosshairHandler = useRef<((p: any) => void) | null>(null);
  const rsiCrosshairHandler = useRef<((p: any) => void) | null>(null);

  const initialChart = useCallback(async () => {
    if (!chartContainerRef.current || !rsiContainerRef.current) return;

    if (mainChart.current == null) {
      mainChart.current = createChart(chartContainerRef.current, {
        width: chartContainerRef.current.clientWidth,
        height: chartContainerRef.current.clientHeight,
        timeScale: { visible: false },
        ...defaultChartLayout,
      });
    }

    if (rsiChart.current == null) {
      rsiChart.current = createChart(rsiContainerRef.current, {
        width: rsiContainerRef.current.clientWidth,
        height: rsiContainerRef.current.clientHeight,
        rightPriceScale: { minimumWidth: 72 },
        timeScale: {
          borderColor: '#485c7b',
          timeVisible: true,
          secondsVisible: false,
        },
        ...defaultChartLayout,
      });
    }

    const mc = mainChart.current;
    const rc = rsiChart.current;

    // Unsubscribe previous handler instances before re-subscribing
    if (mainRangeHandler.current) mc.timeScale().unsubscribeVisibleLogicalRangeChange(mainRangeHandler.current);
    if (rsiRangeHandler.current) rc.timeScale().unsubscribeVisibleLogicalRangeChange(rsiRangeHandler.current);
    if (mainCrosshairHandler.current) mc.unsubscribeCrosshairMove(mainCrosshairHandler.current);
    if (rsiCrosshairHandler.current) rc.unsubscribeCrosshairMove(rsiCrosshairHandler.current);

    let klines: Kline[];
    try {
      klines = await getCandlesticks(pair, interval, startTime, endTime);
    } catch (err) {
      console.error('Failed to fetch candlesticks:', err);
      return;
    }

    if (candleSeries.current) { mc.removeSeries(candleSeries.current); candleSeries.current = null; }
    if (volumeSeries.current) { mc.removeSeries(volumeSeries.current); volumeSeries.current = null; }
    if (boluSeries.current) { mc.removeSeries(boluSeries.current); boluSeries.current = null; }
    if (sma20Series.current) { mc.removeSeries(sma20Series.current); sma20Series.current = null; }
    if (boldSeries.current) { mc.removeSeries(boldSeries.current); boldSeries.current = null; }
    if (rsiSeries.current) { rc.removeSeries(rsiSeries.current); rsiSeries.current = null; }

    for (const s of supportSeries.current) mc.removeSeries(s);
    supportSeries.current = [];

    for (const s of gridSeries.current) {
      try { mc.removeSeries(s); } catch { /* ignore */ }
    }
    gridSeries.current = [];

    candleSeries.current = addCandlesticks(mc, klines);
    volumeSeries.current = addVolume(mc, klines);
    const bb = addBollingerBands(mc, rc, klines);
    boluSeries.current = bb.boluSeries;
    sma20Series.current = bb.sma20Series;
    boldSeries.current = bb.boldSeries;
    rsiSeries.current = bb.rsiSeries;
    supportSeries.current = bb.supportSeries;

    const getCrosshairDataPoint = (series: ISeriesApi<'Line'>, param: any) => {
      if (!param.time) return null;
      return param.seriesData.get(series) || null;
    };

    const syncCrosshair = (chart: IChartApi, series: ISeriesApi<'Line'>, dataPoint: any) => {
      if (dataPoint) {
        chart.setCrosshairPosition(dataPoint.value, dataPoint.time, series);
      } else {
        chart.clearCrosshairPosition();
      }
    };

    // Define and store new handler instances
    mainRangeHandler.current = (timeRange: any) => {
      timeRange && rc.timeScale().setVisibleLogicalRange(timeRange);
    };
    rsiRangeHandler.current = (timeRange: any) => {
      timeRange && mc.timeScale().setVisibleLogicalRange(timeRange);
    };
    rsiCrosshairHandler.current = (param: any) => {
      const dataPoint = getCrosshairDataPoint(rsiSeries.current!, param);
      syncCrosshair(mc, boluSeries.current!, dataPoint);
    };
    mainCrosshairHandler.current = (param: any) => {
      const dataPoint = getCrosshairDataPoint(boluSeries.current!, param);
      syncCrosshair(rc, rsiSeries.current!, dataPoint);
    };

    mc.timeScale().subscribeVisibleLogicalRangeChange(mainRangeHandler.current);
    rc.timeScale().subscribeVisibleLogicalRangeChange(rsiRangeHandler.current);
    rc.subscribeCrosshairMove(rsiCrosshairHandler.current);
    mc.subscribeCrosshairMove(mainCrosshairHandler.current);

    gridLines.forEach((item) => {
      const grid = mc.addLineSeries({
        color: item.color,
        lineWidth: 1,
        crosshairMarkerVisible: false,
      });
      grid.setData(
        klines.map((k) => ({ time: k.openTime / 1000 + TZ_OFFSET_SECONDS, value: item.price } as LineData))
      );
      gridSeries.current.push(grid);
    });
  }, [pair, interval, gridLines, startTime, endTime]);

  useEffect(() => {
    initialChart();
  }, [initialChart]);

  // Destroy charts on unmount
  useEffect(() => {
    return () => {
      mainChart.current?.remove();
      rsiChart.current?.remove();
      mainChart.current = null;
      rsiChart.current = null;
    };
  }, []);

  // Resize chart on container resizes.
  useEffect(() => {
    if (resizeObserver.current || !chartContainerRef.current) return;
    resizeObserver.current = new ResizeObserver((entries) => {
      const { width, height } = entries[0].contentRect;
      mainChart.current?.applyOptions({ width, height });
      rsiChart.current?.applyOptions({ width });
    });

    resizeObserver.current.observe(chartContainerRef.current);

    return () => resizeObserver.current?.disconnect();
  }, []);

  useEffect(() => {
    // WS: get market price — skip when viewing a historical date range
    if (startTime != null || endTime != null) return;

    markPriceWS.current?.close();
    markPriceWS.current = new WebSocket(
      `wss://stream.binance.com:9443/ws/${pair}@kline_${interval}`.toLowerCase()
    );
    markPriceWS.current.onmessage = function (event) {
      try {
        const json = JSON.parse(event.data);
        const { k: { t, o, c, h, l } } = json;

        candleSeries.current?.update({
          time: t / 1000 + TZ_OFFSET_SECONDS,
          open: parseFloat(o),
          high: parseFloat(h),
          low: parseFloat(l),
          close: parseFloat(c),
        } as CandlestickData);
      } catch (err) {
        console.log(err);
      }
    };

    return () => markPriceWS.current?.close();
  }, [pair, interval, startTime, endTime]);

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

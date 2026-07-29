import { Grid, Paper, Stack, Typography } from '@mui/material';
import { useQuery } from '@tanstack/react-query';
import { useMemo } from 'react';
import { candlesClient } from 'store';
import { IntervalType, Kline } from 'store/api-client';
import { round2Dec } from 'store/constants';
import MonthCalendar from './components/MonthCalendar';
import { ColorScale, DayCell, MonthGroup } from './types';

// Monday = 0, ..., Sunday = 6
function mondayIndex(date: Date): number {
  const day = date.getDay(); // 0=Sun
  return day === 0 ? 6 : day - 1;
}

function buildMonthGroups(candles: Kline[]): {
  monthGroups: MonthGroup[];
  greenScale: ColorScale;
  redScale: ColorScale;
} {
  const greenPercentages: number[] = [];
  const redPercentages: number[] = [];
  const monthMap = new Map<string, DayCell[]>();

  for (const candle of candles) {
    const date = new Date(candle.openTime);
    const year = date.getFullYear();
    const month = date.getMonth();
    const key = `${year}-${String(month).padStart(2, '0')}`;

    const pct = round2Dec(((candle.highestPrice - candle.lowestPrice) / candle.openPrice) * 100);
    const direction: DayCell['direction'] =
      candle.closePrice > candle.openPrice ? 'up' : candle.closePrice < candle.openPrice ? 'down' : 'flat';

    if (direction === 'up') greenPercentages.push(pct);
    else if (direction === 'down') redPercentages.push(pct);

    if (!monthMap.has(key)) monthMap.set(key, []);
    monthMap.get(key)!.push({ date, percentage: pct, direction });
  }

  const greenMin = Math.min(...greenPercentages);
  const greenMax = Math.max(...greenPercentages);
  const redMin = Math.min(...redPercentages);
  const redMax = Math.max(...redPercentages);

  const greenScale: ColorScale = { min: greenMin, bucketSize: (greenMax - greenMin) / 10 };
  const redScale: ColorScale = { min: redMin, bucketSize: (redMax - redMin) / 10 };

  const monthGroups: MonthGroup[] = [];

  for (const [key, days] of [...monthMap.entries()].toSorted().toReversed()) {
    const [yearStr, monthStr] = key.split('-');
    const year = Number(yearStr);
    const month = Number(monthStr);

    days.sort((a, b) => a.date.getTime() - b.date.getTime());

    const leading = mondayIndex(days[0].date);
    const cells: (DayCell | null)[] = [...Array(leading).fill(null), ...days];
    const remainder = cells.length % 7;
    if (remainder !== 0) cells.push(...Array(7 - remainder).fill(null));

    monthGroups.push({ year, month, cells });
  }

  return { monthGroups, greenScale, redScale };
}

export default function CandleAnalytics() {
  const { data, isPending } = useQuery({
    queryKey: ['candle-analytics-btcusdt'],
    queryFn: () => candlesClient.get('BTCUSDT', IntervalType.OneDay),
  });

  const { monthGroups, greenScale, redScale } = useMemo(() => {
    if (!data || data.length === 0) {
      return {
        monthGroups: [],
        greenScale: { min: 0, bucketSize: 1 },
        redScale: { min: 0, bucketSize: 1 },
      };
    }
    return buildMonthGroups(data);
  }, [data]);

  return (
    <Grid container spacing={3}>
      <Grid item xs={12}>
        <Paper sx={{ p: 2, display: 'flex', flexDirection: 'column' }}>
          <Typography component="h2" variant="h6" color="primary" gutterBottom>
            Candle Analytics — BTCUSDT
          </Typography>
        </Paper>
      </Grid>
      {isPending && (
        <Grid item xs={12}>
          <Typography>Loading...</Typography>
        </Grid>
      )}
      <Grid item xs={12}>
        <Stack direction="row" gap={0} flexWrap="wrap" sx={{  justifyContent: "space-around" }}>
          {monthGroups.map((group) => (
            <Stack
              key={`${group.year}-${group.month}`}
              sx={{  flexShrink: 0}}
            >
              <MonthCalendar group={group} greenScale={greenScale} redScale={redScale} />
            </Stack>
          ))}
        </Stack>
      </Grid>
    </Grid>
  );
}

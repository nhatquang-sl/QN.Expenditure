import { Box, Grid, Paper, Skeleton, Typography } from '@mui/material';
import { SignalStatisticInfo, SignalStatistics } from '../types';
import { useGetSignalStatistics } from '../hooks/use-get-signal-statistics';

interface PeriodCardProps {
  label: string;
  info: SignalStatisticInfo;
}

function PeriodCard({ label, info }: PeriodCardProps) {
  return (
    <Paper variant="outlined" sx={{ p: 1.5 }}>
      <Typography variant="subtitle2" fontWeight="bold" gutterBottom>
        {label}
      </Typography>
      <Grid container rowSpacing={0.25}>
        <Grid item xs={7}>
          <Typography variant="caption" color="text.secondary">Signals</Typography>
        </Grid>
        <Grid item xs={5}>
          <Typography variant="caption" align="right" display="block">{info.totalSignals}</Typography>
        </Grid>
        <Grid item xs={7}>
          <Typography variant="caption" color="text.secondary">Entries</Typography>
        </Grid>
        <Grid item xs={5}>
          <Typography variant="caption" align="right" display="block" color="success.main">{info.totalEntries}</Typography>
        </Grid>
        <Grid item xs={7}>
          <Typography variant="caption" color="text.secondary">Max Profit Hits</Typography>
        </Grid>
        <Grid item xs={5}>
          <Typography variant="caption" align="right" display="block" color="success.main">{info.totalMaxProfitHits}</Typography>
        </Grid>
        <Grid item xs={7}>
          <Typography variant="caption" color="text.secondary">Stop Loss Hits</Typography>
        </Grid>
        <Grid item xs={5}>
          <Typography variant="caption" align="right" display="block" color="error.main">{info.totalStopLossHits}</Typography>
        </Grid>
        <Grid item xs={7}>
          <Typography variant="caption" color="text.secondary">Avg Entry $</Typography>
        </Grid>
        <Grid item xs={5}>
          <Typography variant="caption" align="right" display="block">
            {info.avgEntryPrice > 0 ? info.avgEntryPrice.toFixed(2) : '—'}
          </Typography>
        </Grid>
        <Grid item xs={7}>
          <Typography variant="caption" color="text.secondary">Avg Max Profit %</Typography>
        </Grid>
        <Grid item xs={5}>
          <Typography variant="caption" align="right" display="block" color={info.avgMaxProfit > 0 ? 'success.main' : 'text.primary'}>
            {info.avgMaxProfit > 0 ? `+${info.avgMaxProfit.toFixed(2)}%` : '—'}
          </Typography>
        </Grid>
      </Grid>
    </Paper>
  );
}

interface SignalStatisticsPanelProps {
  interval: string;
  signalType: string;
}

const PERIODS: { key: keyof SignalStatistics; label: string }[] = [
  { key: 'today', label: 'Today' },
  { key: 'yesterday', label: 'Yesterday' },
  { key: 'thisWeek', label: 'This Week' },
  { key: 'lastWeek', label: 'Last Week' },
  { key: 'thisMonth', label: 'This Month' },
  { key: 'lastMonth', label: 'Last Month' },
];

export default function SignalStatisticsPanel({ interval, signalType }: SignalStatisticsPanelProps) {
  const { data, isLoading } = useGetSignalStatistics({ interval, signalType });

  if (isLoading) {
    return (
      <Box sx={{ display: 'grid', gridTemplateColumns: 'repeat(6, 1fr)', gap: 1, mb: 2 }}>
        {PERIODS.map(({ key }) => (
          <Skeleton key={key} variant="rectangular" height={140} />
        ))}
      </Box>
    );
  }

  if (!data) return null;

  return (
    <Box sx={{ display: 'grid', gridTemplateColumns: 'repeat(6, 1fr)', gap: 1, mb: 2 }}>
      {PERIODS.map(({ key, label }) => (
        <PeriodCard key={key} label={label} info={data[key]} />
      ))}
    </Box>
  );
}

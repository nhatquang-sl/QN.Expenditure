import { Box, Chip, Grid, Paper, Typography } from '@mui/material';

interface StatisticsCardProps {
  type: 'BUY' | 'SELL';
  stats: {
    totalFunds: string;
    totalFee: string;
    totalSize: string;
    avgPrice: string;
  };
}

export function StatisticsCard({ type, stats }: StatisticsCardProps) {
  return (
    <Paper variant="outlined" sx={{ p: 2 }}>
      <Box sx={{ display: 'flex', alignItems: 'center', gap: 1, mb: 1 }}>
        <Chip label={type} color={type === 'BUY' ? 'success' : 'error'} size="small" />
        <Typography variant="subtitle2" color="text.secondary">
          Statistics
        </Typography>
      </Box>
      <Grid container>
        <Grid item xs={6}>
          <Typography variant="body2" color="text.secondary">
            Total Funds
          </Typography>
        </Grid>
        <Grid item xs={6}>
          <Typography align="right" variant="body2">
            {stats.totalFunds}
          </Typography>
        </Grid>
        <Grid item xs={6}>
          <Typography variant="body2" color="text.secondary">
            Total Fee
          </Typography>
        </Grid>
        <Grid item xs={6}>
          <Typography align="right" variant="body2">
            {stats.totalFee}
          </Typography>
        </Grid>
        <Grid item xs={6}>
          <Typography variant="body2" color="text.secondary">
            Total Size
          </Typography>
        </Grid>
        <Grid item xs={6}>
          <Typography align="right" variant="body2">
            {stats.totalSize}
          </Typography>
        </Grid>
        <Grid item xs={6}>
          <Typography variant="body2" color="text.secondary">
            Avg Price
          </Typography>
        </Grid>
        <Grid item xs={6}>
          <Typography align="right" variant="body2">
            {stats.avgPrice}
          </Typography>
        </Grid>
      </Grid>
    </Paper>
  );
}

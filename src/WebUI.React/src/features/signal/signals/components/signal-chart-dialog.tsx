import {
  Dialog,
  DialogContent,
  DialogTitle,
  FormControl,
  IconButton,
  MenuItem,
  Select,
  Typography,
} from '@mui/material';
import CloseIcon from '@mui/icons-material/Close';
import Chart from 'components/chart';
import { SignalDto } from '../types';

const BINANCE_INTERVALS = ['1m', '5m', '15m', '30m', '1h', '4h', '1d'];

const BINANCE_INTERVAL_MS: Record<string, number> = {
  '1m': 60_000,
  '5m': 300_000,
  '15m': 900_000,
  '30m': 1_800_000,
  '1h': 3_600_000,
  '4h': 14_400_000,
  '1d': 86_400_000,
};

const DATE_FORMAT_OPTIONS: Intl.DateTimeFormatOptions = {
  year: 'numeric',
  month: '2-digit',
  day: '2-digit',
  hour: '2-digit',
  minute: '2-digit',
  second: '2-digit',
  hour12: false,
};

interface SignalChartDialogProps {
  signal: SignalDto;
  interval: string;
  onClose: () => void;
  onIntervalChange: (interval: string) => void;
}

export default function SignalChartDialog({
  signal,
  interval,
  onClose,
  onIntervalChange,
}: SignalChartDialogProps) {
  const intervalMs = BINANCE_INTERVAL_MS[interval] ?? 60_000;
  const startTime = signal.detectedAt - 400 * intervalMs;
  const endTime = signal.detectedAt + 10 * intervalMs;

  return (
    <Dialog
      open
      onClose={onClose}
      fullWidth
      maxWidth="xl"
      PaperProps={{ sx: { height: '90vh' } }}
    >
      <DialogTitle sx={{ display: 'flex', alignItems: 'center', gap: 2 }}>
        <Typography variant="h6" component="span" sx={{ flex: 1 }}>
          {signal.symbol} · {new Date(signal.detectedAt).toLocaleString('en-US', DATE_FORMAT_OPTIONS)}
        </Typography>
        <FormControl size="small" sx={{ minWidth: 80 }}>
          <Select value={interval} onChange={(e) => onIntervalChange(e.target.value)}>
            {BINANCE_INTERVALS.map((iv) => (
              <MenuItem key={iv} value={iv}>{iv}</MenuItem>
            ))}
          </Select>
        </FormControl>
        <IconButton onClick={onClose} size="small">
          <CloseIcon />
        </IconButton>
      </DialogTitle>
      <DialogContent sx={{ p: 0, overflow: 'hidden' }}>
        <Chart
          pair={signal.symbol}
          interval={interval}
          startTime={startTime}
          endTime={endTime}
        />
      </DialogContent>
    </Dialog>
  );
}

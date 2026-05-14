import { Box, Button, FormControl, InputLabel, MenuItem, Select } from '@mui/material';
import { DatePicker } from '@mui/x-date-pickers';
import dayjs, { Dayjs } from 'dayjs';
import { useState } from 'react';
import { INTERVALS, SIGNAL_TYPES } from '../types';

const DEFAULT_FROM = () => dayjs().subtract(1, 'month').startOf('month');
const DEFAULT_TO = () => dayjs().endOf('day');

interface SignalSearchBarProps {
  onSearch: (params: { from: string; to: string; interval: string; signalType: string }) => void;
}

export default function SignalSearchBar({ onSearch }: SignalSearchBarProps) {
  const [from, setFrom] = useState<Dayjs>(DEFAULT_FROM);
  const [to, setTo] = useState<Dayjs>(DEFAULT_TO);
  const [interval, setInterval] = useState('');
  const [signalType, setSignalType] = useState('');

  return (
    <Box sx={{ display: 'flex', flexWrap: 'wrap', gap: 2, mb: 3, alignItems: 'center' }}>
      <DatePicker
        label="From"
        value={from}
        onChange={(v) => v && setFrom(v)}
        slotProps={{ textField: { size: 'small' } }}
      />
      <DatePicker
        label="To"
        value={to}
        onChange={(v) => v && setTo(v)}
        slotProps={{ textField: { size: 'small' } }}
      />
      <FormControl size="small" sx={{ minWidth: 130 }}>
        <InputLabel>Interval</InputLabel>
        <Select value={interval} label="Interval" onChange={(e) => setInterval(e.target.value)}>
          <MenuItem value="">
            <em>All</em>
          </MenuItem>
          {INTERVALS.map((iv) => (
            <MenuItem key={iv} value={iv}>
              {iv}
            </MenuItem>
          ))}
        </Select>
      </FormControl>
      <FormControl size="small" sx={{ minWidth: 130 }}>
        <InputLabel>Signal Type</InputLabel>
        <Select value={signalType} label="Signal Type" onChange={(e) => setSignalType(e.target.value)}>
          <MenuItem value="">
            <em>All</em>
          </MenuItem>
          {SIGNAL_TYPES.map((st) => (
            <MenuItem key={st} value={st}>
              {st}
            </MenuItem>
          ))}
        </Select>
      </FormControl>
      <Button variant="contained" onClick={() => onSearch({ from: from.toISOString(), to: to.toISOString(), interval, signalType })}>
        Search
      </Button>
    </Box>
  );
}

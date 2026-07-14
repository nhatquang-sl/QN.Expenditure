import {
  Paper,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  Typography,
} from '@mui/material';
import { ColorScale, DayCell, MonthGroup } from '../types';

const WEEK_DAYS = ['Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday', 'Sunday'];

const MONTH_NAMES = [
  'January', 'February', 'March', 'April', 'May', 'June',
  'July', 'August', 'September', 'October', 'November', 'December',
];

const GREEN_COLORS = [
  '#e8f5e9', '#c8e6c9', '#a5d6a7', '#81c784', '#66bb6a',
  '#4caf50', '#43a047', '#388e3c', '#2e7d32', '#1b5e20',
];

const RED_COLORS = [
  '#ffebee', '#ffcdd2', '#ef9a9a', '#e57373', '#ef5350',
  '#f44336', '#e53935', '#d32f2f', '#c62828', '#b71c1c',
];

function getLevel(pct: number, scale: ColorScale): number {
  if (scale.bucketSize === 0) return 0;
  return Math.min(9, Math.floor((pct - scale.min) / scale.bucketSize));
}

function getCellSx(cell: DayCell | null, greenScale: ColorScale, redScale: ColorScale) {
  if (!cell || cell.direction === 'flat') return { backgroundColor: '#fff' };
  if (cell.direction === 'up') {
    const level = getLevel(cell.percentage, greenScale);
    return { backgroundColor: GREEN_COLORS[level], color: level >= 5 ? '#fff' : 'inherit' };
  }
  const level = getLevel(cell.percentage, redScale);
  return { backgroundColor: RED_COLORS[level], color: level >= 5 ? '#fff' : 'inherit' };
}

type Props = {
  group: MonthGroup;
  greenScale: ColorScale;
  redScale: ColorScale;
};

export default function MonthCalendar({ group, greenScale, redScale }: Props) {
  const rows: (DayCell | null)[][] = [];
  for (let i = 0; i < group.cells.length; i += 7) {
    rows.push(group.cells.slice(i, i + 7));
  }

  return (
    <TableContainer component={Paper}>
      <Typography variant="subtitle1" sx={{ px: 2, pt: 1.5, fontWeight: 'bold' }}>
        {MONTH_NAMES[group.month]} {group.year}
      </Typography>
      <Table size="small" aria-label={`${MONTH_NAMES[group.month]} ${group.year}`}>
        <TableHead>
          <TableRow>
            {WEEK_DAYS.map((day) => (
              <TableCell key={day} sx={{ textAlign: 'center', fontWeight: 'bold', width: '14.28%' }}>
                {day}
              </TableCell>
            ))}
          </TableRow>
        </TableHead>
        <TableBody>
          {rows.map((row, rowIdx) => (
            <TableRow key={rowIdx}>
              {row.map((cell, colIdx) => (
                <TableCell
                  key={colIdx}
                  sx={{ textAlign: 'center', width: '14.28%', ...getCellSx(cell, greenScale, redScale) }}
                >
                  {cell && (
                    <>
                      {cell.date.getDate()}/{cell.date.getMonth() + 1}
                      <br />
                      {cell.percentage}%
                    </>
                  )}
                </TableCell>
              ))}
            </TableRow>
          ))}
        </TableBody>
      </Table>
    </TableContainer>
  );
}

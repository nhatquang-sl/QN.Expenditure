import { TableCell, TableRow } from '@mui/material';
import { useState } from 'react';

import { UserLoginHistory } from 'store/api-client';
import { columns } from './types';

const SessionRow = (props: { session: UserLoginHistory }) => {
  const { session } = props;
  const [openDetail, setOpenDetail] = useState(false);

  const handleSelectSession = () => {
    setOpenDetail(true);
  };

  const handleDeselectSession = () => {
    setOpenDetail(false);
  };

  return (
    <>
      <TableRow
        hover
        role="checkbox"
        tabIndex={-1}
        key={session.id}
        onClick={() => handleSelectSession()}
      >
        {session &&
          columns.map((column) => {
            const value = session[column.id];
            return (
              <TableCell key={column.id} align={column.align} sx={{ cursor: 'pointer' }}>
                {column.format ? column.format((value ?? '').toString()) : value?.toString()}
              </TableCell>
            );
          })}
      </TableRow>
      {/* <SessionDetail session={session} open={openDetail} onClose={handleDeselectSession} /> */}
    </>
  );
};

export default SessionRow;

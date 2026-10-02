import { Chip } from '@mui/material';
import { makeStyles } from 'tss-react/mui';
import React from 'react';
import { HealthStatus } from './utils';

const useStyles = makeStyles()((theme) => ({
   statusChip: {
      cursor: 'pointer',
      maxWidth: 200,
   },
   statusChipOk: {
      backgroundColor: '#27ae60',
   },
   statusChipError: {
      backgroundColor: theme.palette.error.main,
   },
}));

type Props = React.ComponentProps<typeof Chip> & {
   status: HealthStatus;
};

export default function StatusChip({ status, className, ...props }: Props) {
   const { classes, cx } = useStyles();

   return (
      <Chip
         className={cx(className, classes.statusChip, {
            [classes.statusChipOk]: status === 'ok',
            [classes.statusChipError]: status === 'error',
         })}
         {...props}
      />
   );
}

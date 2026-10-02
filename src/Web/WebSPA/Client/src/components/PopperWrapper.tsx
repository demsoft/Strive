import { ClickAwayListener, Grow, Paper, Popper, PopperProps } from '@mui/material';
import { makeStyles } from 'tss-react/mui';
import React from 'react';

const useStyles = makeStyles()((theme) => ({
   includePadding: {
      padding: theme.spacing(2),
   },
}));

type Props = {
   open: boolean;
   anchorEl: PopperProps['anchorEl'];
   onClose: () => void;
   children: React.ReactElement;

   placement: PopperProps['placement'];
   transformOrigin: string;

   padding?: boolean;
};

export default function PopperWrapper({
   open,
   anchorEl,
   onClose,
   children,
   placement,
   transformOrigin,
   padding,
}: Props) {
   const { classes } = useStyles();

   return (
      <Popper open={open} anchorEl={anchorEl} transition placement={placement}>
         {({ TransitionProps }) => (
            <Grow {...TransitionProps} style={{ transformOrigin }}>
               <Paper className={padding ? classes.includePadding : undefined}>
                  <ClickAwayListener onClickAway={onClose}>{children}</ClickAwayListener>
               </Paper>
            </Grow>
         )}
      </Popper>
   );
}

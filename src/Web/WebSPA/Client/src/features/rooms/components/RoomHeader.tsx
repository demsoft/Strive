import { ButtonBase, Typography } from '@mui/material';
import { makeStyles } from 'tss-react/mui';
import { alpha } from '@mui/material/styles';
import CompassIcon from 'mdi-material-ui/CompassRose';
import PoundIcon from 'mdi-material-ui/Pound';
import React from 'react';
import { RoomViewModel } from '../types';

const useStyles = makeStyles<void, 'selected'>()((theme, _params, classes) => ({
   root: {
      borderRadius: theme.shape.borderRadius,
      padding: theme.spacing(0, 1),
      paddingTop: 6,
      paddingBottom: 6,
      width: '100%',
      display: 'flex',
      flexDirection: 'row',
      justifyContent: 'flex-start',
      // transition: theme.transitions.create('background-color'),
      '&:hover': {
         textDecoration: 'none',
         backgroundColor: alpha(theme.palette.text.primary, 0.05),
      },
      [`&.${classes.selected}`]: {
         color: theme.palette.action.active,
         backgroundColor: alpha(theme.palette.action.active, 0.06),
         '&:hover': {
            backgroundColor: alpha(theme.palette.action.active, 0.15),
         },
         '& + &': {
            borderLeft: 0,
            marginLeft: 0,
         },
      },
   },
   /* Pseudo-class applied to the root element if `selected={true}`. */
   selected: {},
   rootSelected: {
      backgroundColor: alpha(theme.palette.text.primary, 0.1),
   },
   icon: {
      marginRight: 8,
      color: theme.palette.text.secondary,
   },
   starIcon: {
      marginRight: 8,
      color: theme.palette.primary.light,
   },
   nameContainer: {
      display: 'flex',
      flexDirection: 'row',
      alignItems: 'center',
   },
}));

type Props = {
   room: RoomViewModel;
   selected?: boolean;
   onClick: () => void;
   className?: string;
};

export default function RoomHeader({ room: { displayName, isDefaultRoom }, selected, onClick, className }: Props) {
   const { classes, cx } = useStyles();
   return (
      <ButtonBase
         className={cx([classes.root, className, { [classes.selected]: selected }])}
         onClick={selected ? undefined : onClick}
         aria-pressed={selected}
      >
         <div className={classes.nameContainer}>
            {isDefaultRoom ? (
               <CompassIcon fontSize="small" className={classes.starIcon} />
            ) : (
               <PoundIcon fontSize="small" className={classes.icon} />
            )}
            <Typography variant="subtitle2" color={selected ? 'textPrimary' : 'textSecondary'}>
               {displayName}
            </Typography>
         </div>
      </ButtonBase>
   );
}

import { IconButton } from '@mui/material';
import { makeStyles } from 'tss-react/mui';
import { motion, Variants } from 'framer-motion';
import React from 'react';

const useStyles = makeStyles()((theme) => ({
   root: {
      position: 'relative',
   },
   iconButton: {
      marginTop: -4,
      marginBottom: -4,
   },
   indicatorContainer: {
      position: 'absolute',
      left: 0,
      top: 0,
      bottom: 0,

      display: 'flex',
      alignItems: 'center',
   },
   label: {
      display: 'flex',
      transition: theme.transitions.create('transform', {
         duration: theme.transitions.duration.shorter,
         easing: theme.transitions.easing.easeOut,
      }),
      transform: 'translate(0px, 0px)',
   },
   labelSelected: {
      transform: 'translate(4px, 0px)',
   },
}));

const indicatorVariants: Variants = {
   selected: {
      d: 'M 0 0 A 16 40 270 0 1 0 16',
   },
   default: { d: 'M 0 0 A 16 0 270 0 1 0 16' },
};

type Props = Omit<React.ComponentProps<typeof IconButton>, 'children'> & {
   className?: string;
   icon: React.ReactNode;
   selected?: boolean;
};

export default React.forwardRef<HTMLButtonElement, Props>(function ToolIcon(
   { className, icon, selected, onClick, ...props },
   ref,
) {
   const { classes, cx } = useStyles();

   return (
      <div className={cx(classes.root, className)}>
         <IconButton className={classes.iconButton} onClick={onClick} ref={ref} {...props} size="large">
            <span className={cx(classes.label, selected && classes.labelSelected)}>{icon}</span>
         </IconButton>
         <div className={classes.indicatorContainer}>
            <svg width={8} height={16}>
               <motion.path
                  variants={indicatorVariants}
                  initial="default"
                  animate={selected ? 'selected' : 'default'}
                  d="M 0 0 A 16 40 270 0 1 0 16"
                  fill="white"
                  transition={{ type: 'tween', duration: 0.1 }}
               />
            </svg>
         </div>
      </div>
   );
});

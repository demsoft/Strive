import { IconButton } from '@mui/material';
import { makeStyles } from 'tss-react/mui';
import { Delete } from 'mdi-material-ui';
import React from 'react';
import { useTranslation } from 'react-i18next';

const useStyles = makeStyles()((theme) => ({
   iconButtonRipple: {
      color: theme.palette.error.main,
   },
   iconButton: {
      transition: theme.transitions.create('color', {
         duration: theme.transitions.duration.shorter,
         easing: theme.transitions.easing.easeOut,
      }),
      '&:hover': {
         color: theme.palette.error.main,
      },
   },
}));

export default function ClearWhiteboardButton({
   TouchRippleProps,
   className,
   ...props
}: React.ComponentProps<typeof IconButton>) {
   const { classes, cx } = useStyles();
   const { t } = useTranslation();

   return (
      <IconButton
         className={cx(className, classes.iconButton)}
         TouchRippleProps={{ ...TouchRippleProps, className: classes.iconButtonRipple }}
         {...props}
         title={t('conference.whiteboard.toolbar.clear')}
         size="large"
      >
         <Delete fontSize="small" />
      </IconButton>
   );
}

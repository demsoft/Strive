import { alpha, InputBase, Theme } from '@mui/material';
import { withStyles } from 'tss-react/mui';

export default withStyles(InputBase, (theme: Theme) => ({
   root: {
      'label + &': {
         marginTop: theme.spacing(3),
      },
   },
   input: {
      color: theme.palette.text.secondary,
      borderRadius: theme.shape.borderRadius,
      position: 'relative' as const,
      backgroundColor: theme.palette.background.paper,
      fontSize: 16,
      padding: '8px 16px 8px 0px',
      transition: theme.transitions.create(['background-color']),
      '&:hover': {
         backgroundColor: alpha(theme.palette.text.primary, 0.2),
      },
   },
}));

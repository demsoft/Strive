import { Box } from '@mui/material';
import { makeStyles } from 'tss-react/mui';
import { Skeleton } from '@mui/material';
import React from 'react';

const useStyles = makeStyles()((theme) => ({
   controlSkeleton: {
      marginTop: theme.spacing(2),
      marginLeft: theme.spacing(2),
      marginRight: theme.spacing(2),
      height: 32,
   },
}));

export default function CreateConferenceFormSkeleton() {
   const { classes } = useStyles();
   return (
      <div>
         <Box
            sx={{
               mb: 2,
               px: 3,
               mt: 2,
            }}
         >
            <Skeleton variant="rectangular" height={40} />
         </Box>
         <Skeleton variant="rectangular" height={45} />

         <Box
            sx={{
               mt: 4,
            }}
         >
            {Array.from({ length: 5 }).map((_, i) => (
               <Skeleton key={i} className={classes.controlSkeleton} variant="rectangular" />
            ))}
         </Box>
      </div>
   );
}

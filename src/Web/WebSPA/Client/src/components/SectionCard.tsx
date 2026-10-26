import { Box, Paper, SxProps, Theme, Typography } from '@mui/material';
import React from 'react';
import { brand } from 'src/theme';

type Props = {
   id?: string;
   title?: string;
   hint?: string;
   children: React.ReactNode;
   sx?: SxProps<Theme>;
};

/** A rounded card with an optional heading, the building block of the pages of a creator. */
export default function SectionCard({ id, title, hint, children, sx }: Props) {
   return (
      <Paper
         id={id}
         sx={{
            p: { xs: 2, sm: 2.75 },
            mb: 3,
            borderRadius: '20px',
            border: `1px solid ${brand.border}`,
            ...(Array.isArray(sx) ? {} : (sx as Record<string, unknown>)),
         }}
      >
         {title && (
            <Box sx={{ mb: 1.5 }}>
               <Typography variant="h6" sx={{ fontWeight: 700 }}>
                  {title}
               </Typography>
               {hint && (
                  <Typography variant="body2" color="text.secondary">
                     {hint}
                  </Typography>
               )}
            </Box>
         )}
         {children}
      </Paper>
   );
}

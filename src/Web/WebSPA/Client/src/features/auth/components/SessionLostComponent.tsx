import { Box, Button } from '@mui/material';
import React from 'react';
import { useTranslation } from 'react-i18next';
import BaseAuthComponent from './BaseAuthComponent';

export default function SessionLostComponent() {
   const { t } = useTranslation();

   return (
      <BaseAuthComponent componentName="SessionLostComponent">
         <Box
            sx={{
               mt: 2,
            }}
         >
            <Button href="/" variant="contained">
               {t('common:back_to_start')}
            </Button>
         </Box>
      </BaseAuthComponent>
   );
}

import { Button } from '@mui/material';
import ArrowBackIcon from '@mui/icons-material/ArrowBack';
import React from 'react';
import { useTranslation } from 'react-i18next';
import { useHistory } from 'react-router-dom';

/**
 * Goes back to where the person came from. When there is nothing to go back to (the page was opened by its address in
 * a new tab) it goes to `fallback`.
 */
export default function BackButton({ fallback }: { fallback: string }) {
   const { t } = useTranslation();
   const history = useHistory();

   return (
      <Button
         id="back-button"
         color="inherit"
         startIcon={<ArrowBackIcon />}
         onClick={() => (window.history.length > 1 ? history.go(-1) : history.push(fallback))}
         sx={{ borderRadius: '999px', px: 1.75, backgroundColor: 'rgba(255,255,255,0.06)' }}
      >
         {t('common:back')}
      </Button>
   );
}

import { Box, Button, Typography } from '@mui/material';
import ConfirmationNumberOutlinedIcon from '@mui/icons-material/ConfirmationNumberOutlined';
import LiveTvOutlinedIcon from '@mui/icons-material/LiveTvOutlined';
import React from 'react';
import { useTranslation } from 'react-i18next';
import { Link as RouterLink } from 'react-router-dom';
import { brand } from 'src/theme';

const card = {
   flex: 1,
   minWidth: 220,
   p: 2.5,
   borderRadius: '20px',
   backgroundColor: 'rgba(20, 24, 41, 0.6)',
   border: `1px solid ${brand.border}`,
   textAlign: 'left',
} as const;

/** The two ways to use Gbedu, on the home page of a signed in person: as a fan (tickets) or as a creator. */
export default function GbeduRoles() {
   const { t } = useTranslation();

   return (
      <Box sx={{ display: 'flex', gap: 2, flexWrap: 'wrap', mt: 4 }} id="gbedu-roles">
         <Box sx={card}>
            <ConfirmationNumberOutlinedIcon color="primary" />
            <Typography variant="subtitle1" sx={{ fontWeight: 700, mt: 1 }}>
               {t('roles.fan_title')}
            </Typography>
            <Typography variant="body2" color="text.secondary" sx={{ mb: 2 }}>
               {t('roles.fan_text')}
            </Typography>
            <Button id="roles-fan" variant="outlined" component={RouterLink} to="/tickets">
               {t('roles.fan_button')}
            </Button>
         </Box>
         <Box sx={card}>
            <LiveTvOutlinedIcon color="primary" />
            <Typography variant="subtitle1" sx={{ fontWeight: 700, mt: 1 }}>
               {t('roles.creator_title')}
            </Typography>
            <Typography variant="body2" color="text.secondary" sx={{ mb: 2 }}>
               {t('roles.creator_text')}
            </Typography>
            <Button id="roles-creator" variant="contained" component={RouterLink} to="/creator">
               {t('roles.creator_button')}
            </Button>
         </Box>
      </Box>
   );
}

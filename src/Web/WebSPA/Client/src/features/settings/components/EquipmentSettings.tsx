import { Box, Button, Chip, Grid, TextField, Typography } from '@mui/material';
import { makeStyles } from 'tss-react/mui';
import { Skeleton } from '@mui/material';
import { QRCodeSVG } from 'qrcode.react';
import React, { useEffect } from 'react';
import { useDispatch, useSelector } from 'react-redux';
import { useRouteMatch } from 'react-router-dom';
import { getEquipmentToken } from 'src/core-hub';
import { ConferenceRouteParams } from 'src/routes/types';
import { RootState } from 'src/store';
import CheckIcon from '@mui/icons-material/Check';
import { selectEquipmentConnections } from '../selectors';
import { selectMyParticipantId } from 'src/features/auth/selectors';
import { useTranslation } from 'react-i18next';

const QR_CODE_PADDING = 8;

const useStyles = makeStyles()({
   qrCodeContainer: {
      backgroundColor: 'white',
      padding: QR_CODE_PADDING,
      height: 200 + 2 * QR_CODE_PADDING,
      width: 200 + 2 * QR_CODE_PADDING,
   },
});

export default function EquipmentSettings() {
   const dispatch = useDispatch();
   const { t } = useTranslation();
   const { classes } = useStyles();

   const token = useSelector((state: RootState) => state.settings.equipmentToken);
   const error = useSelector((state: RootState) => state.settings.equipmentTokenError);
   const equipment = useSelector(selectEquipmentConnections);
   const participantId = useSelector(selectMyParticipantId);

   const {
      params: { id },
   } = useRouteMatch<ConferenceRouteParams>();

   const handleFetchEquipment = () => dispatch(getEquipmentToken());

   useEffect(() => {
      if (!token) {
         handleFetchEquipment();
      }
   }, [token, dispatch]);

   const url = new URL(`/c/${id}/as-equipment?participantId=${participantId}&token=${token}`, document.baseURI).href;

   return (
      <Box
         sx={{
            p: 2,
            pt: 0,
         }}
      >
         <Typography variant="subtitle1">{t('conference.settings.equipment.description')}</Typography>
         {error ? (
            <Box
               sx={{
                  mt: 2,
               }}
            >
               <Typography color="error" gutterBottom>
                  {t('conference.settings.equipment.error_fetch_token', { error })}
               </Typography>
               <Button variant="contained" onClick={handleFetchEquipment}>
                  {t('common:retry')}
               </Button>
            </Box>
         ) : (
            <Box
               sx={{
                  display: 'flex',
                  mt: 4,
               }}
            >
               {token ? (
                  <div className={classes.qrCodeContainer}>
                     <QRCodeSVG value={url} size={200} />
                  </div>
               ) : (
                  <Skeleton variant="rectangular" width={200} height={200} />
               )}
               <Box
                  sx={{
                     flex: 1,
                     ml: 3,
                  }}
               >
                  <Typography>{token ? t('conference.settings.equipment.step_1') : <Skeleton />}</Typography>
                  <Box
                     sx={{
                        mt: 1,
                        mb: 1,
                     }}
                  >
                     {token ? (
                        <TextField
                           fullWidth
                           variant="outlined"
                           value={url}
                           slotProps={{
                              input: { readOnly: true },
                           }}
                        />
                     ) : (
                        <Skeleton height={50} />
                     )}
                  </Box>
                  <Typography gutterBottom>
                     {token ? t('conference.settings.equipment.step_2') : <Skeleton />}
                  </Typography>
                  <Typography>{token ? t('conference.settings.equipment.step_3') : <Skeleton />}</Typography>
               </Box>
            </Box>
         )}
         {equipment && (
            <Box
               sx={{
                  mt: 2,
               }}
            >
               <Grid container>
                  {Object.values(equipment).map((x) => (
                     <Grid key={x.connectionId}>
                        <Chip color="secondary" label={x.name} icon={<CheckIcon />} />
                     </Grid>
                  ))}
               </Grid>
            </Box>
         )}
      </Box>
   );
}

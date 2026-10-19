import { AppBar, Box, Divider, Toolbar, Typography } from '@mui/material';
import React from 'react';
import { useSelector } from 'react-redux';
import BrandLogo from 'src/components/BrandLogo';
import ConferenceTools from 'src/features/conference/components/ConferenceTools';
import RecordingIndicator from 'src/features/recording/components/RecordingIndicator';
import RecordingsDialog from 'src/features/recording/components/RecordingsDialog';
import useIsMobile from 'src/hooks/useIsMobile';
import { RootState } from 'src/store';
import WebRtcStatusChip from 'src/features/conference/components/appbar/WebRtcStatusChip';
import LiveBadge from './LiveBadge';
import ViewersChip from './ViewersChip';

/**
 * The top of a Gbedu session: the name of the session, that it is live and how many people watch, and the tools of the
 * person. It has no link to invite people: the ticket is the way in.
 */
export default function GbeduLiveHeader() {
   const isMobile = useIsMobile();
   const title = useSelector((state: RootState) => state.conference.conferenceState?.name);

   return (
      <AppBar
         position="static"
         elevation={0}
         sx={{ background: 'rgba(18, 13, 10, 0.72)', backdropFilter: 'blur(16px)' }}
      >
         <Toolbar variant="dense" sx={{ gap: { xs: 0.75, sm: 1.5 }, minHeight: 60 }}>
            <BrandLogo size={28} showName={false} />
            <LiveBadge />
            {title && (
               <>
                  <Divider orientation="vertical" flexItem sx={{ my: 1.75, display: { xs: 'none', sm: 'block' } }} />
                  <Typography
                     id="conference-name"
                     variant="subtitle1"
                     noWrap
                     sx={{ fontWeight: 700, minWidth: 0, maxWidth: { xs: 120, sm: 220, md: 340, lg: 460 } }}
                  >
                     {title}
                  </Typography>
               </>
            )}
            <Box sx={{ display: 'flex', alignItems: 'center', gap: 1, flex: 1, minWidth: 0 }}>
               <ViewersChip />
               <RecordingIndicator />
               {!isMobile && <WebRtcStatusChip />}
            </Box>
            <ConferenceTools showInvite={false} />
            <RecordingsDialog />
         </Toolbar>
      </AppBar>
   );
}

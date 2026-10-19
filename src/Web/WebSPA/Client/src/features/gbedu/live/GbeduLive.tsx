import { Grid, Typography } from '@mui/material';
import { makeStyles } from 'tss-react/mui';
import React, { useMemo, useRef } from 'react';
import { useTranslation } from 'react-i18next';
import { useSelector } from 'react-redux';
import AnnouncementOverlay from 'src/features/chat/components/AnnouncementOverlay';
import ChatBar from 'src/features/chat/components/ChatBar';
import { selectShowChat } from 'src/features/chat/selectors';
import ConferenceLayoutContext, {
   ConferenceLayoutContextType,
} from 'src/features/conference/conference-layout-context';
import PermissionDialog from 'src/features/conference/components/PermissionDialog';
import ParticipantMicManager from 'src/features/media/components/ParticipantMicManager';
import CurrentPollsBar from 'src/features/poll/components/CurrentPollsBar';
import SceneView from 'src/features/scenes/components/SceneView';
import { brand } from 'src/theme';
import GbeduLiveHeader from './GbeduLiveHeader';

const CHAT_WIDTH = 340;

const useStyles = makeStyles()((theme) => ({
   root: { height: '100%', display: 'flex', flexDirection: 'column' },
   main: {
      flex: 1,
      display: 'flex',
      minHeight: 0,
      gap: theme.spacing(1.5),
      padding: theme.spacing(0, 1.5, 1.5),
   },
   // the stage: what the host shows, in a frame of its own
   stage: {
      flex: 1,
      minWidth: 0,
      display: 'flex',
      flexDirection: 'column',
      borderRadius: 24,
      overflow: 'hidden',
      backgroundColor: 'rgba(0, 0, 0, 0.35)',
      border: `1px solid ${brand.border}`,
      boxShadow: '0 20px 60px rgba(0, 0, 0, 0.45)',
   },
   stageInner: { flex: 1, display: 'flex', minHeight: 0 },
   chat: {
      width: CHAT_WIDTH,
      flexShrink: 0,
      display: 'flex',
      flexDirection: 'column',
      minHeight: 0,
      borderRadius: 24,
      overflow: 'hidden',
      backgroundColor: brand.surface,
      border: `1px solid ${brand.border}`,
      padding: theme.spacing(0, 1.5, 1.5),
   },
   chatTitle: { padding: theme.spacing(1.75, 0.5, 1), fontWeight: 800 },
}));

/**
 * The room of a Gbedu session on a computer: a stage with a rounded frame for what the host shows, a live chat beside
 * it, and no sidebar of meeting tools. People who are not the host see the stage and take part through the chat, hand
 * raising and, if the host allows it, their microphone and camera.
 */
export default function GbeduLive() {
   const { classes } = useStyles();
   const { t } = useTranslation();
   const showChat = useSelector(selectShowChat);
   const chatContainer = useRef<HTMLDivElement>(null);
   const sceneBarContainer = useRef<HTMLDivElement>(null);

   const context = useMemo<ConferenceLayoutContextType>(
      () => ({
         chatContainer: chatContainer.current,
         chatWidth: CHAT_WIDTH,
         sceneBarContainer: sceneBarContainer.current,
      }),
      [chatContainer.current],
   );

   return (
      <ParticipantMicManager>
         <ConferenceLayoutContext.Provider value={context}>
            <div className={classes.root} id="gbedu-live">
               <AnnouncementOverlay />
               <GbeduLiveHeader />
               <div className={classes.main}>
                  <div className={classes.stage} id="gbedu-stage">
                     <div ref={sceneBarContainer} />
                     <div className={classes.stageInner}>
                        <SceneView />
                     </div>
                  </div>
                  {showChat && (
                     <aside className={classes.chat} id="gbedu-chat">
                        <Typography className={classes.chatTitle}>{t('gbedu_live.chat')}</Typography>
                        <Grid ref={chatContainer} />
                        <CurrentPollsBar />
                        <ChatBar />
                     </aside>
                  )}
               </div>
               <PermissionDialog />
            </div>
         </ConferenceLayoutContext.Provider>
      </ParticipantMicManager>
   );
}

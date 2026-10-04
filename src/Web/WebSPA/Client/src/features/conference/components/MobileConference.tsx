import { BottomNavigation, BottomNavigationAction, Badge, Grid, Paper } from '@mui/material';
import { makeStyles } from 'tss-react/mui';
import ChatIcon from '@mui/icons-material/Chat';
import PeopleIcon from '@mui/icons-material/People';
import VideocamIcon from '@mui/icons-material/Videocam';
import React, { useMemo, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { useSelector } from 'react-redux';
import AnnouncementOverlay from 'src/features/chat/components/AnnouncementOverlay';
import ChatBar from 'src/features/chat/components/ChatBar';
import { selectHasNewMessages, selectShowChat } from 'src/features/chat/selectors';
import ParticipantMicManager from 'src/features/media/components/ParticipantMicManager';
import CurrentPollsBar from 'src/features/poll/components/CurrentPollsBar';
import RoomsList from 'src/features/rooms/components/RoomsList';
import SceneManagement from 'src/features/scenes/components/SceneManagement';
import useThrottledResizeObserver from 'src/hooks/useThrottledResizeObserver';
import SceneView from '../../scenes/components/SceneView';
import ConferenceLayoutContext, { ConferenceLayoutContextType } from '../conference-layout-context';
import ConferenceAppBar from './ConferenceAppBar';
import PermissionDialog from './PermissionDialog';

type Tab = 'meeting' | 'chat' | 'people';

const useStyles = makeStyles()((theme) => ({
   root: {
      height: '100%',
      display: 'flex',
      flexDirection: 'column',
   },
   main: {
      flex: 1,
      position: 'relative',
      minHeight: 0,
   },
   scene: {
      position: 'absolute',
      inset: 0,
      display: 'flex',
      flexDirection: 'column',
   },
   sceneLayout: {
      flex: 1,
      display: 'flex',
      minHeight: 0,
   },
   sceneInner: {
      flex: 1,
      minWidth: 0,
   },
   // panels cover the scene, which stays mounted so that video and audio keep running
   panel: {
      position: 'absolute',
      inset: 0,
      display: 'flex',
      flexDirection: 'column',
      backgroundColor: theme.palette.background.default,
      zIndex: 2,
      padding: theme.spacing(1),
   },
   hidden: {
      display: 'none',
   },
   people: {
      display: 'flex',
      flexDirection: 'column',
      flex: 1,
      minHeight: 0,
      overflowY: 'auto',
   },
   nav: {
      flexShrink: 0,
      paddingBottom: 'env(safe-area-inset-bottom)',
   },
}));

/** The conference layout for small screens: the scene fills the screen, chat and people open as panels. */
export default function MobileConference() {
   const { classes, cx } = useStyles();
   const { t } = useTranslation();

   const showChat = useSelector(selectShowChat);
   const hasNewMessages = useSelector(selectHasNewMessages);

   const [tab, setTab] = useState<Tab>('meeting');
   // never stay on the chat tab if the chat gets unavailable
   const activeTab: Tab = tab === 'chat' && !showChat ? 'meeting' : tab;

   const [contentRef, dimensions] = useThrottledResizeObserver(100);
   const chatContainer = useRef<HTMLDivElement>(null);
   const sceneBarContainer = useRef<HTMLDivElement>(null);

   const context = useMemo<ConferenceLayoutContextType>(
      () => ({
         chatContainer: chatContainer.current,
         chatWidth: dimensions?.width ?? 0,
         sceneBarContainer: sceneBarContainer.current,
         sceneBarWidth: dimensions?.width,
      }),
      [chatContainer.current, dimensions?.width],
   );

   return (
      <ParticipantMicManager>
         <ConferenceLayoutContext.Provider value={context}>
            <div className={classes.root} id="mobile-conference">
               <AnnouncementOverlay />
               <ConferenceAppBar />
               <div className={classes.main} ref={contentRef}>
                  <div className={classes.scene}>
                     <div ref={sceneBarContainer} />
                     <div className={classes.sceneLayout}>
                        <div className={classes.sceneInner}>
                           <SceneView />
                        </div>
                     </div>
                  </div>

                  {/* always mounted: the chat bar subscribes to the messages */}
                  <div
                     className={cx(classes.panel, activeTab !== 'chat' && classes.hidden)}
                     id="mobile-panel-chat"
                  >
                     <Grid ref={chatContainer} />
                     <CurrentPollsBar />
                     {showChat && <ChatBar />}
                  </div>

                  <div
                     className={cx(classes.panel, activeTab !== 'people' && classes.hidden)}
                     id="mobile-panel-people"
                  >
                     <div className={classes.people}>
                        <RoomsList />
                        <SceneManagement />
                     </div>
                  </div>
               </div>
               <Paper elevation={4} square className={classes.nav}>
                  <BottomNavigation showLabels value={activeTab} onChange={(_event, value: Tab) => setTab(value)}>
                     <BottomNavigationAction
                        id="mobile-nav-meeting"
                        value="meeting"
                        label={t('conference.mobile.meeting')}
                        icon={<VideocamIcon />}
                     />
                     {showChat && (
                        <BottomNavigationAction
                           id="mobile-nav-chat"
                           value="chat"
                           label={t('conference.mobile.chat')}
                           icon={
                              <Badge color="secondary" variant="dot" invisible={!hasNewMessages || activeTab === 'chat'}>
                                 <ChatIcon />
                              </Badge>
                           }
                        />
                     )}
                     <BottomNavigationAction
                        id="mobile-nav-people"
                        value="people"
                        label={t('conference.mobile.people')}
                        icon={<PeopleIcon />}
                     />
                  </BottomNavigation>
               </Paper>
               <PermissionDialog />
            </div>
         </ConferenceLayoutContext.Provider>
      </ParticipantMicManager>
   );
}

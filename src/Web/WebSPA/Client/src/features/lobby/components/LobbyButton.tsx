import {
   Badge,
   Button,
   IconButton,
   List,
   ListItem,
   ListItemText,
   Popover,
   Tooltip,
   Typography,
} from '@mui/material';
import { makeStyles } from 'tss-react/mui';
import MeetingRoomIcon from '@mui/icons-material/MeetingRoom';
import React, { useEffect, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { useDispatch, useSelector } from 'react-redux';
import * as coreHub from 'src/core-hub';
import useStriveSound from 'src/hooks/useStriveSound';
import usePermission from 'src/hooks/usePermission';
import { LOBBY_CAN_ADMIT } from 'src/permissions';
import { showMessage } from 'src/store/notifier/actions';
import { selectIsLobbyEnabled, selectWaitingParticipants } from '../selectors';

const useStyles = makeStyles()((theme) => ({
   popover: {
      width: 320,
      padding: theme.spacing(1, 2),
   },
   header: {
      display: 'flex',
      alignItems: 'center',
      justifyContent: 'space-between',
   },
   actions: {
      display: 'flex',
      gap: theme.spacing(1),
   },
}));

/** App bar button for participants that may admit others: shows who waits in the lobby. */
export default function LobbyButton() {
   const { classes } = useStyles();
   const { t } = useTranslation();
   const dispatch = useDispatch();

   const canAdmit = usePermission(LOBBY_CAN_ADMIT);
   const enabled = useSelector(selectIsLobbyEnabled);
   const waiting = useSelector(selectWaitingParticipants);

   const [playWaitingSound] = useStriveSound('striveLobbyWaiting');

   const buttonRef = useRef<HTMLButtonElement>(null);
   const [open, setOpen] = useState(false);

   // tell the moderator when somebody new starts waiting
   const knownIds = useRef<Set<string> | null>(null);
   useEffect(() => {
      const known = knownIds.current;
      if (known) {
         const newlyWaiting = waiting.filter((x) => !known.has(x.id));
         if (newlyWaiting.length > 0) playWaitingSound();

         for (const participant of newlyWaiting) {
            dispatch(
               showMessage({
                  type: 'info',
                  message: t('conference.lobby.someone_waiting', { name: participant.displayName }),
                  icon: '🚪',
               }),
            );
         }
      }
      knownIds.current = new Set(waiting.map((x) => x.id));
   }, [waiting]);

   if (!canAdmit || (!enabled && waiting.length === 0)) return null;

   return (
      <>
         <Tooltip title={t('conference.lobby.title')}>
            <IconButton
               id="lobby-button"
               ref={buttonRef}
               color="inherit"
               aria-label={t('conference.lobby.title')}
               onClick={() => setOpen(true)}
               size="large"
            >
               <Badge badgeContent={waiting.length} color="secondary">
                  <MeetingRoomIcon />
               </Badge>
            </IconButton>
         </Tooltip>
         <Popover
            open={open}
            anchorEl={buttonRef.current}
            onClose={() => setOpen(false)}
            anchorOrigin={{ vertical: 'bottom', horizontal: 'right' }}
            transformOrigin={{ vertical: 'top', horizontal: 'right' }}
         >
            <div className={classes.popover} id="lobby-popover">
               <div className={classes.header}>
                  <Typography variant="subtitle1">{t('conference.lobby.title')}</Typography>
                  {waiting.length > 1 && (
                     <Button id="lobby-admit-all" size="small" onClick={() => dispatch(coreHub.admitAllParticipants())}>
                        {t('conference.lobby.admit_all')}
                     </Button>
                  )}
               </div>
               {waiting.length === 0 ? (
                  <Typography color="textSecondary" variant="body2">
                     {t('conference.lobby.nobody_waiting')}
                  </Typography>
               ) : (
                  <List dense>
                     {waiting.map((participant) => (
                        <ListItem key={participant.id} disableGutters className="lobby-waiting-participant">
                           <ListItemText primary={participant.displayName} />
                           <div className={classes.actions}>
                              <Button
                                 size="small"
                                 variant="contained"
                                 onClick={() => dispatch(coreHub.admitParticipant({ participantId: participant.id }))}
                              >
                                 {t('conference.lobby.admit')}
                              </Button>
                              <Button
                                 size="small"
                                 onClick={() => dispatch(coreHub.denyParticipant({ participantId: participant.id }))}
                              >
                                 {t('conference.lobby.deny')}
                              </Button>
                           </div>
                        </ListItem>
                     ))}
                  </List>
               )}
            </div>
         </Popover>
      </>
   );
}

import { Box, Chip, List, ListItem, ListItemText, Popover, Typography } from '@mui/material';
import PeopleAltOutlinedIcon from '@mui/icons-material/PeopleAltOutlined';
import React, { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { useSelector } from 'react-redux';
import HostAvatar from 'src/features/gbedu/ui/HostAvatar';
import { isRecorderParticipant } from 'src/features/recording/recorder';
import { RootState } from 'src/store';
import { selectParticipantList } from 'src/features/conference/selectors';

/** People in the room, without the recorder, hosts first. */
export function whoIsHere<T extends { id: string; displayName: string }>(participants: T[], hosts: string[]) {
   return participants
      .filter((x) => !isRecorderParticipant(x.id))
      .sort(
         (a, b) =>
            Number(hosts.includes(b.id)) - Number(hosts.includes(a.id)) || a.displayName.localeCompare(b.displayName),
      );
}

/** How many people are watching; a click shows who they are. */
export default function ViewersChip() {
   const { t } = useTranslation();
   const participants = useSelector(selectParticipantList);
   const hosts = useSelector((state: RootState) => state.conference.conferenceState?.moderators ?? []);
   const [anchor, setAnchor] = useState<HTMLElement | null>(null);

   const people = whoIsHere(participants, hosts);

   return (
      <>
         <Chip
            id="viewers-chip"
            size="small"
            icon={<PeopleAltOutlinedIcon />}
            label={t('gbedu_live.viewers', { count: people.length })}
            onClick={(e) => setAnchor(e.currentTarget)}
            sx={{ backgroundColor: 'rgba(255, 255, 255, 0.08)', fontWeight: 600 }}
         />
         <Popover
            open={Boolean(anchor)}
            anchorEl={anchor}
            onClose={() => setAnchor(null)}
            anchorOrigin={{ vertical: 'bottom', horizontal: 'left' }}
         >
            <Box sx={{ width: 280, maxHeight: 360, overflowY: 'auto', p: 1 }} id="viewers-list">
               <Typography variant="overline" color="text.secondary" sx={{ px: 1.5 }}>
                  {t('gbedu_live.in_the_room')}
               </Typography>
               <List dense disablePadding>
                  {people.map((person) => (
                     <ListItem key={person.id} className="viewer">
                        <Box sx={{ mr: 1.5 }}>
                           <HostAvatar name={person.displayName} size={30} />
                        </Box>
                        <ListItemText
                           primary={person.displayName}
                           secondary={hosts.includes(person.id) ? t('gbedu_live.host') : undefined}
                        />
                     </ListItem>
                  ))}
               </List>
            </Box>
         </Popover>
      </>
   );
}

import { List, ListItemText, Typography } from '@mui/material';
import React from 'react';
import { useSelector } from 'react-redux';
import { selectParticipants } from 'src/features/conference/selectors';
import { Participant } from 'src/features/conference/types';
import { selectTalkingStickQueue } from '../../selectors';

import ListItemButton from '@mui/material/ListItemButton';

type Props = {
   onPassStick: (participantId: string) => void;
};
export default function PassStickList({ onPassStick }: Props) {
   const queue = useSelector(selectTalkingStickQueue);
   const participants = useSelector(selectParticipants);

   if (queue.length === 0) {
      return (
         <Typography variant="body2" align="center" style={{ marginTop: 8 }}>
            At the moment, no participants want to say something...
         </Typography>
      );
   }

   return (
      <List dense>
         {queue
            .map((id) => participants[id])
            .filter((x): x is Participant => !!x)
            .map(({ id, displayName }) => (
               <ListItemButton key={id} onClick={() => onPassStick(id)}>
                  <ListItemText primary={displayName} />
               </ListItemButton>
            ))}
      </List>
   );
}

import { Chip, IconButton, List, ListItemSecondaryAction, ListItemText } from '@mui/material';
import { makeStyles } from 'tss-react/mui';
import MoreVertIcon from '@mui/icons-material/MoreVert';
import StarIcon from '@mui/icons-material/Star';
import StarBorder from '@mui/icons-material/StarBorder';
import _ from 'lodash';
import React from 'react';
import to from 'src/utils/to';
import { ConferenceLink } from '../types';

import ListItemButton from '@mui/material/ListItemButton';

const useStyles = makeStyles()(() => ({
   chipsRoot: {
      display: 'flex',
      '& > *': {
         cursor: 'pointer',
      },
   },
   chipActive: {
      borderColor: '#2ecc71',
   },
}));

type Props = {
   links: ConferenceLink[];
};

export default function MyConferencesList({ links }: Props) {
   const { classes } = useStyles();

   return (
      <List>
         {_.orderBy(links, [(x) => x.starred, (x) => x.lastJoin], ['asc', 'desc']).map((x) => (
            <ListItemButton key={x.conferenceId} {...to(`/c/${x.conferenceId}`)}>
               <ListItemText
                  primary={x.conferenceName || 'Unnamed conference'}
                  secondary={
                     <div className={classes.chipsRoot}>
                        <Chip
                           className={x.isActive ? classes.chipActive : undefined}
                           size="small"
                           variant="outlined"
                           label={x.isActive ? 'Active' : 'Inactive'}
                        />
                        {x.isModerator && (
                           <Chip
                              size="small"
                              color="primary"
                              variant="outlined"
                              label="Moderator"
                              style={{ marginLeft: 8 }}
                           />
                        )}
                     </div>
                  }
               />
               <ListItemSecondaryAction>
                  <IconButton edge="start" aria-label="star" size="large">
                     {x.starred ? <StarIcon /> : <StarBorder />}
                  </IconButton>
                  <IconButton edge="end" aria-label="options" size="large">
                     <MoreVertIcon />
                  </IconButton>
               </ListItemSecondaryAction>
            </ListItemButton>
         ))}
      </List>
   );
}

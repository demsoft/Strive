import { Collapse, List, ListItemIcon, ListItemText } from '@mui/material';
import { makeStyles } from 'tss-react/mui';
import ExpandLess from '@mui/icons-material/ExpandLess';
import ExpandMore from '@mui/icons-material/ExpandMore';
import { AccountArrowRight, AccountConvert, AutoFix, HumanQueue, RunFast } from 'mdi-material-ui';
import React from 'react';
import { useTranslation } from 'react-i18next';
import { ModeSceneListItemProps, TalkingStickMode } from '../../types';

import ListItemButton from '@mui/material/ListItemButton';

const useStyles = makeStyles()((theme) => ({
   nested: {
      paddingLeft: theme.spacing(4),
   },
}));

export default function ModeSceneListItem({ selectedScene, onChangeScene }: ModeSceneListItemProps) {
   const { classes } = useStyles();
   const { t } = useTranslation();

   const isSelected = selectedScene.type === 'talkingStick';
   const handleSetScene = (mode: TalkingStickMode) => () => onChangeScene({ type: 'talkingStick', mode });

   const [open, setOpen] = React.useState(false);

   const handleClick = () => {
      setOpen(!open);
   };

   return (
      <>
         <ListItemButton selected={isSelected} onClick={handleClick}>
            <ListItemIcon>
               <AccountConvert />
            </ListItemIcon>
            <ListItemText primary={t('conference.scenes.talking_stick')} />
            {open ? <ExpandLess /> : <ExpandMore />}
         </ListItemButton>
         <Collapse in={open} timeout="auto" unmountOnExit>
            <List component="div" disablePadding>
               <ListItemButton className={classes.nested} onClick={handleSetScene('queue')}>
                  <ListItemIcon>
                     <HumanQueue />
                  </ListItemIcon>
                  <ListItemText
                     primary={t('conference.scenes.talking_stick_modes.queue')}
                     secondary={t('conference.scenes.talking_stick_modes.queue_description')}
                  />
               </ListItemButton>
               <ListItemButton className={classes.nested} onClick={handleSetScene('race')}>
                  <ListItemIcon>
                     <RunFast />
                  </ListItemIcon>
                  <ListItemText
                     primary={t('conference.scenes.talking_stick_modes.race')}
                     secondary={t('conference.scenes.talking_stick_modes.race_description')}
                  />
               </ListItemButton>
               <ListItemButton className={classes.nested} onClick={handleSetScene('moderated')}>
                  <ListItemIcon>
                     <AutoFix />
                  </ListItemIcon>
                  <ListItemText
                     primary={t('conference.scenes.talking_stick_modes.moderated')}
                     secondary={t('conference.scenes.talking_stick_modes.moderated_description')}
                  />
               </ListItemButton>
               <ListItemButton className={classes.nested} onClick={handleSetScene('speakerPassStick')}>
                  <ListItemIcon>
                     <AccountArrowRight />
                  </ListItemIcon>
                  <ListItemText
                     primary={t('conference.scenes.talking_stick_modes.speakerPassStick')}
                     secondary={t('conference.scenes.talking_stick_modes.speakerPassStick_description')}
                  />
               </ListItemButton>
            </List>
         </Collapse>
      </>
   );
}

import GroupWorkIcon from '@mui/icons-material/GroupWork';
import React from 'react';
import { useTranslation } from 'react-i18next';
import { AvailableSceneListItemProps } from '../../types';
import SceneListItemWithPopper from '../SceneListItemWithPopper';
import BreakoutRoomsPopper from './BreakoutRoomsPopper';
import SettingsIcon from '@mui/icons-material/Settings';

export default function BreakoutRoomListItem(props: AvailableSceneListItemProps) {
   const { t } = useTranslation();

   return (
      <SceneListItemWithPopper
         {...props}
         icon={<GroupWorkIcon />}
         listItemIcon={<SettingsIcon />}
         title={t('conference.scenes.breakout_rooms.label')}
         PopperComponent={BreakoutRoomsPopper}
      />
   );
}

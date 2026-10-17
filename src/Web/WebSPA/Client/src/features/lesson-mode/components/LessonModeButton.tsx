import SchoolOutlinedIcon from '@mui/icons-material/SchoolOutlined';
import { Chip, IconButton, Tooltip } from '@mui/material';
import React from 'react';
import { useTranslation } from 'react-i18next';
import { useDispatch, useSelector } from 'react-redux';
import * as coreHub from 'src/core-hub';
import usePermission from 'src/hooks/usePermission';
import { LESSON_MODE_CAN_CONTROL } from 'src/permissions';
import { selectLessonModeActive } from '../selectors';

/**
 * Lesson mode, for the teachers: everybody else is muted until the teacher allows somebody to speak (in the menu of the
 * participant) or turns the mode off. The others only see that the mode is on.
 */
export default function LessonModeButton() {
   const { t } = useTranslation();
   const dispatch = useDispatch();
   const canControl = usePermission(LESSON_MODE_CAN_CONTROL);
   const active = useSelector(selectLessonModeActive);

   if (canControl) {
      const label = active ? t('conference.lesson_mode.turn_off') : t('conference.lesson_mode.turn_on');
      return (
         <Tooltip title={`${label}. ${t('conference.lesson_mode.description')}`}>
            <IconButton
               id="lesson-mode-button"
               color={active ? 'primary' : 'inherit'}
               aria-label={label}
               aria-pressed={active}
               onClick={() => dispatch(coreHub.setLessonMode({ active: !active }))}
            >
               <SchoolOutlinedIcon />
            </IconButton>
         </Tooltip>
      );
   }

   if (!active) return null;

   return (
      <Tooltip title={t('conference.lesson_mode.active_info')}>
         <Chip
            id="lesson-mode-chip"
            size="small"
            color="primary"
            icon={<SchoolOutlinedIcon />}
            label={t('conference.lesson_mode.title')}
         />
      </Tooltip>
   );
}

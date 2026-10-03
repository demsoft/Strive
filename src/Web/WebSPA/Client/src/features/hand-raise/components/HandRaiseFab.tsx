import { Fab, Tooltip } from '@mui/material';
import PanToolIcon from '@mui/icons-material/PanTool';
import PanToolOutlinedIcon from '@mui/icons-material/PanToolOutlined';
import { motion } from 'framer-motion';
import React from 'react';
import { useTranslation } from 'react-i18next';
import { useDispatch, useSelector } from 'react-redux';
import * as coreHub from 'src/core-hub';
import { RootState } from 'src/store';
import { selectIsMyHandRaised } from '../selectors';

type Props = {
   className?: string;
   variants?: any;
};

export default function HandRaiseFab({ className, variants }: Props) {
   const { t } = useTranslation();
   const dispatch = useDispatch();
   const raised = useSelector((state: RootState) => selectIsMyHandRaised(state));

   const label = raised ? t('conference.hand_raise.lower') : t('conference.hand_raise.raise');

   const handleClick = () => {
      dispatch(raised ? coreHub.lowerHand() : coreHub.raiseHand());
   };

   return (
      <Tooltip title={label} arrow>
         <Fab
            id="hand-raise-toggle"
            color={raised ? 'primary' : 'default'}
            className={className}
            onClick={handleClick}
            component={motion.button}
            variants={variants}
            aria-label={label}
            aria-pressed={raised}
         >
            {raised ? <PanToolIcon /> : <PanToolOutlinedIcon />}
         </Fab>
      </Tooltip>
   );
}

import { FormHelperText, Select } from '@mui/material';
import React from 'react';
import { useTranslation } from 'react-i18next';
import MobileAwareSelect from 'src/components/MobileAwareSelect';

export default function SceneLayoutSelect(props: React.ComponentProps<typeof Select>) {
   const { t } = useTranslation();

   return (
      <>
         <MobileAwareSelect {...props} fullWidth>
            {[
               {
                  value: 'auto',
                  label: t('dialog_create_conference.tabs.common.scene_layout_auto'),
               },
               {
                  value: 'chips',
                  label: t('dialog_create_conference.tabs.common.scene_layout_chips'),
               },
               {
                  value: 'chipsWithPresenter',
                  label: t('dialog_create_conference.tabs.common.scene_layout_chips_with_presenter'),
               },
               {
                  value: 'tiles',
                  label: t('dialog_create_conference.tabs.common.scene_layout_tiles'),
               },
            ]}
         </MobileAwareSelect>
         <FormHelperText>{t(`dialog_create_conference.tabs.common.scene_layout_${props.value}_desc`)}</FormHelperText>
      </>
   );
}

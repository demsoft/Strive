import { IconButton, Tooltip } from '@mui/material';
import PersonAddAlt1Icon from '@mui/icons-material/PersonAddAlt1';
import React from 'react';
import { useTranslation } from 'react-i18next';
import useInviteLink from 'src/hooks/useInviteLink';

/** Copies the link of the current conference to invite other people. */
export default function InviteLinkButton() {
   const { t } = useTranslation();
   const { copy } = useInviteLink();

   return (
      <Tooltip title={t('conference.invite.copy')}>
         <IconButton id="copy-invite-link" color="inherit" onClick={copy} aria-label={t('conference.invite.copy')}>
            <PersonAddAlt1Icon />
         </IconButton>
      </Tooltip>
   );
}

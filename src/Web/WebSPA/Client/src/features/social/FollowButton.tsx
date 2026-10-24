import { Button } from '@mui/material';
import CheckIcon from '@mui/icons-material/Check';
import React from 'react';
import { useTranslation } from 'react-i18next';
import { brand } from 'src/theme';

type Props = {
   following: boolean;
   onClick: () => void;
   size?: 'small' | 'medium';
   className?: string;
   fullWidth?: boolean;
};

/** "Follow" before, "Following" after (pressing it again stops following). */
export default function FollowButton({ following, onClick, size = 'medium', className, fullWidth }: Props) {
   const { t } = useTranslation();
   return (
      <Button
         className={className ?? 'follow-button'}
         size={size}
         fullWidth={fullWidth}
         variant={following ? 'outlined' : 'contained'}
         color={following ? 'inherit' : 'primary'}
         startIcon={following ? <CheckIcon /> : undefined}
         aria-pressed={following}
         onClick={onClick}
         sx={following ? undefined : { background: brand.gradient }}
      >
         {following ? t('social.following') : t('social.follow')}
      </Button>
   );
}

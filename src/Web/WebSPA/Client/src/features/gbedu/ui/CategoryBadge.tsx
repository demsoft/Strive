import { Box } from '@mui/material';
import React from 'react';
import { useTranslation } from 'react-i18next';
import { categoryGradient, categoryStyle } from './categories';

type Props = { category: string; size?: 'small' | 'medium' };

/** The category of a session as a small coloured label with its icon. */
export default function CategoryBadge({ category, size = 'small' }: Props) {
   const { t } = useTranslation();
   const { Icon } = categoryStyle(category);

   return (
      <Box
         className="category-badge"
         sx={{
            display: 'inline-flex',
            alignItems: 'center',
            gap: 0.75,
            px: size === 'small' ? 1.25 : 1.75,
            py: size === 'small' ? 0.5 : 0.75,
            borderRadius: 99,
            color: '#fff',
            fontSize: size === 'small' ? 12 : 14,
            fontWeight: 700,
            letterSpacing: 0.2,
            background: categoryGradient(category),
         }}
      >
         <Icon sx={{ fontSize: size === 'small' ? 15 : 18 }} />
         {t(`sessions.category_${category}`)}
      </Box>
   );
}

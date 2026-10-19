import { Avatar } from '@mui/material';
import React from 'react';

/** Up to two initials of a name: "Ada Lovelace" is AL, "kofi" is K. */
export function initialsOf(name: string): string {
   const parts = name.trim().split(/\s+/).filter(Boolean);
   if (parts.length === 0) return '?';
   const first = Array.from(parts[0])[0] ?? '';
   const second = parts.length > 1 ? Array.from(parts[parts.length - 1])[0] ?? '' : '';
   return (first + second).toUpperCase();
}

type Props = {
   name: string;
   size?: number;
   /** the colour of the circle, for example the gradient of the category */
   background?: string;
};

/** A round badge with the initials of the host (there are no profile photos yet). */
export default function HostAvatar({ name, size = 36, background }: Props) {
   return (
      <Avatar
         aria-label={name}
         sx={{
            width: size,
            height: size,
            fontSize: size * 0.4,
            fontWeight: 700,
            color: '#fff',
            background: background ?? 'rgba(255, 255, 255, 0.14)',
         }}
      >
         {initialsOf(name)}
      </Avatar>
   );
}

import React, { useContext } from 'react';
import { Typography } from '@mui/material';
import BrandLogo from 'src/components/BrandLogo';
import { MeetingPortalContext } from 'src/features/hrm-call/MeetingPortalContext';

export default function AppBarLogo() {
   const portal = useContext(MeetingPortalContext);
   return portal ? <Typography variant="h6" component="span" sx={{ fontWeight: 800 }}>GOS Meet</Typography> : <BrandLogo size={28} />;
}

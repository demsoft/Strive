import { useMediaQuery, useTheme } from '@mui/material';

/**
 * True on phones and small tablets, where the conference uses the mobile layout (full screen scene with bottom
 * navigation) instead of the sidebar/scene/chat columns.
 */
export default function useIsMobile() {
   const theme = useTheme();
   return useMediaQuery(theme.breakpoints.down('md'));
}

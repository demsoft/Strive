import { alpha, createTheme, responsiveFontSizes } from '@mui/material';
import { isGbedu } from './brand-id';

/**
 * The Strive design tokens. The Identity server pages (Services/Identity/Identity.API/wwwroot/css/site.css) use the same
 * values, keep them in sync so that the sign in feels like part of the app.
 */
const strive = {
   violet: '#7C5CFF',
   violetLight: '#A18BFF',
   cyan: '#22D3EE',
   background: '#0B0D17',
   surface: '#141829',
   surfaceRaised: '#1B2036',
   border: 'rgba(255, 255, 255, 0.08)',
   gradient: 'linear-gradient(135deg, #7C5CFF 0%, #22D3EE 100%)',
   gradientSoft: 'linear-gradient(135deg, rgba(124, 92, 255, 0.18) 0%, rgba(34, 211, 238, 0.12) 100%)',
   glowA: 'rgba(124, 92, 255, 0.16)',
   glowB: 'rgba(34, 211, 238, 0.10)',
   accentShadow: 'rgba(124, 92, 255, 0.45)',
   pulse: 'rgba(34, 211, 238, 0.6)',
   pulseFade: 'rgba(34, 211, 238, 0)',
};

/** Gbedu: warm amber and coral on a warm dark base. The names stay the same, the values are the brand's own. */
const gbedu: typeof strive = {
   violet: '#FF9F1C',
   violetLight: '#FFC266',
   cyan: '#FF4D6D',
   background: '#120D0A',
   surface: '#1D1511',
   surfaceRaised: '#281D17',
   border: 'rgba(255, 255, 255, 0.08)',
   gradient: 'linear-gradient(135deg, #FF9F1C 0%, #FF4D6D 100%)',
   gradientSoft: 'linear-gradient(135deg, rgba(255, 159, 28, 0.18) 0%, rgba(255, 77, 109, 0.12) 100%)',
   glowA: 'rgba(255, 159, 28, 0.14)',
   glowB: 'rgba(255, 77, 109, 0.09)',
   accentShadow: 'rgba(255, 159, 28, 0.40)',
   pulse: 'rgba(255, 77, 109, 0.6)',
   pulseFade: 'rgba(255, 77, 109, 0)',
};

export const brand = isGbedu ? gbedu : strive;

const fontFamily = '"Inter", "Roboto", -apple-system, BlinkMacSystemFont, "Segoe UI", "Helvetica Neue", Arial, sans-serif';

const baseTheme = createTheme({
   palette: {
      mode: 'dark',
      primary: { main: brand.violet, light: brand.violetLight, contrastText: '#ffffff' },
      secondary: { main: brand.cyan, contrastText: isGbedu ? '#2a0610' : '#06202a' },
      error: { main: '#F87171' },
      success: { main: '#34D399' },
      warning: { main: '#FBBF24' },
      background: { default: brand.background, paper: brand.surface },
      divider: brand.border,
      text: { primary: '#F3F4FA', secondary: 'rgba(243, 244, 250, 0.62)' },
   },
   shape: { borderRadius: 14 },
   typography: {
      fontFamily,
      h1: { fontWeight: 800, letterSpacing: '-0.02em' },
      h2: { fontWeight: 800, letterSpacing: '-0.02em' },
      h3: { fontWeight: 700, letterSpacing: '-0.02em' },
      h4: { fontWeight: 700, letterSpacing: '-0.02em' },
      h5: { fontWeight: 700, letterSpacing: '-0.01em' },
      h6: { fontWeight: 600 },
      subtitle1: { fontWeight: 500 },
      button: { textTransform: 'none', fontWeight: 600, letterSpacing: 0 },
   },
});

const theme = responsiveFontSizes(
   createTheme(baseTheme, {
      components: {
         MuiCssBaseline: {
            styleOverrides: {
               html: { colorScheme: 'dark' },
               body: {
                  backgroundColor: brand.background,
                  backgroundImage:
                     `radial-gradient(1200px 600px at 10% -10%, ${brand.glowA}, transparent 60%), radial-gradient(900px 500px at 100% 0%, ${brand.glowB}, transparent 55%)`,
                  backgroundAttachment: 'fixed',
               },
               '::-webkit-scrollbar': { width: 10, height: 10 },
               '::-webkit-scrollbar-thumb': { backgroundColor: 'rgba(255,255,255,0.14)', borderRadius: 8 },
            },
         },
         // outlined fields look more modern than the underlined MUI v4 default
         MuiTextField: { defaultProps: { variant: 'outlined' } },
         MuiSelect: { defaultProps: { variant: 'outlined' } },
         MuiFormControl: { defaultProps: { variant: 'outlined' } },
         MuiButton: {
            defaultProps: { disableElevation: true },
            styleOverrides: {
               root: { borderRadius: 12, padding: '8px 18px' },
               containedPrimary: {
                  backgroundImage: brand.gradient,
                  transition: 'filter 150ms ease, transform 150ms ease',
                  '&:hover': { filter: 'brightness(1.1)', backgroundImage: brand.gradient },
               },
               sizeLarge: { padding: '12px 28px', fontSize: '1rem' },
            },
         },
         MuiFab: {
            styleOverrides: {
               root: { boxShadow: '0 8px 24px rgba(0, 0, 0, 0.35)' },
               primary: { backgroundImage: brand.gradient },
               default: {
                  backgroundColor: 'rgba(255, 255, 255, 0.10)',
                  color: '#fff',
                  backdropFilter: 'blur(14px)',
                  '&:hover': { backgroundColor: 'rgba(255, 255, 255, 0.18)' },
               },
            },
         },
         MuiPaper: {
            styleOverrides: {
               root: { backgroundImage: 'none' },
            },
         },
         MuiDialog: {
            styleOverrides: {
               paper: {
                  backgroundColor: brand.surfaceRaised,
                  border: `1px solid ${brand.border}`,
                  borderRadius: 20,
                  boxShadow: '0 24px 80px rgba(0, 0, 0, 0.55)',
               },
            },
         },
         MuiAppBar: {
            defaultProps: { elevation: 0, color: 'transparent' },
            styleOverrides: {
               root: {
                  backgroundColor: alpha(brand.surface, 0.72),
                  backdropFilter: 'blur(18px)',
                  borderBottom: `1px solid ${brand.border}`,
               },
               colorDefault: { backgroundColor: alpha(brand.surface, 0.72), color: '#fff' },
            },
         },
         MuiChip: { styleOverrides: { root: { borderRadius: 10, fontWeight: 500 } } },
         MuiTabs: { styleOverrides: { indicator: { height: 3, borderRadius: 3, backgroundImage: brand.gradient } } },
         MuiTab: { styleOverrides: { root: { textTransform: 'none', fontWeight: 600 } } },
         MuiTooltip: {
            styleOverrides: {
               tooltip: { backgroundColor: brand.surfaceRaised, border: `1px solid ${brand.border}`, borderRadius: 8 },
               arrow: { color: brand.surfaceRaised },
            },
         },
         MuiPopover: {
            styleOverrides: {
               paper: { backgroundColor: brand.surfaceRaised, border: `1px solid ${brand.border}`, borderRadius: 16 },
            },
         },
         MuiMenu: {
            styleOverrides: { paper: { backgroundColor: brand.surfaceRaised, border: `1px solid ${brand.border}` } },
         },
         MuiBottomNavigation: {
            styleOverrides: { root: { backgroundColor: alpha(brand.surface, 0.9), backdropFilter: 'blur(18px)' } },
         },
         MuiBottomNavigationAction: {
            styleOverrides: { root: { '&.Mui-selected': { color: brand.violetLight } } },
         },
      },
   }),
);

export default theme;

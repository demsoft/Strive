import 'i18next';

declare module 'i18next' {
   interface CustomTypeOptions {
      // allows <Trans>...{{ count }}...</Trans> interpolation objects as JSX children
      allowObjectInHTMLChildren: true;
   }
}

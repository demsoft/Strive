import i18next from 'i18next';
import LanguageDetector from 'i18next-browser-languagedetector';
import { initReactI18next } from 'react-i18next';
import de from 'src/assets/locales/de';
import en from 'src/assets/locales/en';
import { formatErrorMessage } from 'src/utils/error-utils';

type LanguageInfo = {
   id: string;
   name: string;
};

const resources = {
   en,
   de,
};

export const supportedLanguages: LanguageInfo[] = [
   { id: 'en', name: 'English' },
   { id: 'de', name: 'Deutsch' },
];

i18next
   .use(initReactI18next)
   .use(LanguageDetector)
   .init({
      resources,
      fallbackLng: 'en',
      supportedLngs: supportedLanguages.map((x) => x.id),
      ns: ['common', 'glossary', 'main'],
      defaultNS: 'main',
      nonExplicitSupportedLngs: true,
      interpolation: {
         escapeValue: false,
      },
   });

// used in translations as {{error, error}}
i18next.services.formatter?.add('error', (value) => formatErrorMessage(value));

export default i18next;

import { useEffect } from 'react';
import { brandName } from 'src/brand-id';

export default function usePageTitle(title?: string | null) {
   useEffect(() => {
      const previousTitle = document.title;

      return () => {
         document.title = previousTitle;
      };
   }, []);

   useEffect(() => {
      if (title) {
         document.title = `${brandName} | ${title}`;
      } else {
         document.title = brandName;
      }
   }, [title]);
}

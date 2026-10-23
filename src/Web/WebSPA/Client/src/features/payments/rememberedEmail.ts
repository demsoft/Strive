const EMAIL_KEY = 'gbedu.email';

/** The email the person paid with last time (their receipt goes there): saved in the browser, if it allows. */
export const rememberedEmail = () => {
   try {
      return window.localStorage.getItem(EMAIL_KEY) ?? '';
   } catch {
      return '';
   }
};

export const rememberEmail = (email: string) => {
   try {
      window.localStorage.setItem(EMAIL_KEY, email);
   } catch {
      // storage can be blocked
   }
};

export const isPlausibleEmail = (email: string) => /^[^@\s]+@[^@\s]+\.[^@\s]+$/.test(email.trim());

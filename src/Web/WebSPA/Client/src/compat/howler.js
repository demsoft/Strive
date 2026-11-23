// use-sound dynamically imports named exports; Howler's UMD package exposes a default object in Vite.
import library from 'howler/dist/howler.js';
export const Howl = library.Howl;
export const Howler = library.Howler;
export default library;

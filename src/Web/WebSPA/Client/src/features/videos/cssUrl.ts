/** A picture for `background-image`: quoted, so that any address (with brackets, spaces or quotes in it) works. */
export const cssUrl = (url: string) => `url("${url.replace(/"/g, '%22')}")`;

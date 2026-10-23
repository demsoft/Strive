/** Photos from a phone are many megabytes: nobody needs more than this to see a face in a circle. */
export const PHOTO_SIZE = 400;
export const MAX_SOURCE_BYTES = 15 * 1024 * 1024;
const ACCEPTED = ['image/jpeg', 'image/png', 'image/webp'];

export const isAcceptedPhoto = (file: { type: string; size: number }) =>
   ACCEPTED.includes(file.type) && file.size > 0 && file.size <= MAX_SOURCE_BYTES;

/** The biggest square in the middle of a picture: where to cut, so that a face is not squeezed. */
export function squareCrop(width: number, height: number) {
   const side = Math.min(width, height);
   return { sx: Math.floor((width - side) / 2), sy: Math.floor((height - side) / 2), side };
}

/**
 * Cuts the middle square of the picture, shrinks it to PHOTO_SIZE and makes a jpeg of it. A jpeg made by the browser
 * has no location or camera data in it, whatever the original had.
 */
export async function shrinkToSquareJpeg(file: Blob, size = PHOTO_SIZE, quality = 0.85): Promise<Blob> {
   const bitmap = await createImageBitmap(file);
   try {
      const { sx, sy, side } = squareCrop(bitmap.width, bitmap.height);
      const target = Math.min(size, side);
      const canvas = document.createElement('canvas');
      canvas.width = target;
      canvas.height = target;
      const context = canvas.getContext('2d');
      if (!context) throw new Error('canvas');

      // a png with see-through parts would turn black as a jpeg
      context.fillStyle = '#ffffff';
      context.fillRect(0, 0, target, target);
      context.drawImage(bitmap, sx, sy, side, side, 0, 0, target, target);

      return await new Promise<Blob>((resolve, reject) =>
         canvas.toBlob((blob) => (blob ? resolve(blob) : reject(new Error('toBlob'))), 'image/jpeg', quality),
      );
   } finally {
      bitmap.close?.();
   }
}

import { createReadStream, statSync } from 'node:fs';
import { S3Client } from '@aws-sdk/client-s3';
import { Upload } from '@aws-sdk/lib-storage';
import { Config } from './config';
import { Logger } from './logger';
import { Uploader } from './types';

/** Uploads to an S3 compatible store (Cloudflare R2, MinIO, AWS S3) as a multipart upload. */
export class S3Uploader implements Uploader {
   private readonly client: S3Client;

   constructor(
      private readonly config: Config['storage'],
      private readonly log: Logger,
   ) {
      this.client = new S3Client({
         region: config.region,
         endpoint: config.endpoint,
         forcePathStyle: config.forcePathStyle,
         credentials: { accessKeyId: config.accessKeyId, secretAccessKey: config.secretAccessKey },
      });
   }

   async upload(file: string, storageKey: string): Promise<number> {
      const size = statSync(file).size;

      const upload = new Upload({
         client: this.client,
         params: {
            Bucket: this.config.bucket,
            Key: storageKey,
            Body: createReadStream(file),
            ContentType: 'video/mp4',
         },
         // parts of equal size are required by R2, the last one may be smaller
         partSize: 16 * 1024 * 1024,
         queueSize: 3,
         leavePartsOnError: false,
      });

      let lastLogged = 0;
      upload.on('httpUploadProgress', (progress) => {
         const percent = progress.loaded && size ? Math.floor((progress.loaded / size) * 100) : 0;
         if (percent >= lastLogged + 20) {
            lastLogged = percent;
            this.log.info('Upload progress', { storageKey, percent });
         }
      });

      await upload.done();
      return size;
   }
}

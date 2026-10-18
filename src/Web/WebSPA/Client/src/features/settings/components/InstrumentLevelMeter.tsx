import { Box, LinearProgress, Typography } from '@mui/material';
import React, { useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { MUSIC_AUDIO_CONSTRAINTS } from 'src/store/webrtc/hooks/useMicrophone';

/** The loudest sample of a block of audio, 0 (silence) to 1 (full scale) */
export function peakLevel(samples: Float32Array): number {
   let peak = 0;
   for (let i = 0; i < samples.length; i++) {
      const value = Math.abs(samples[i]);
      if (value > peak) peak = value;
   }
   return peak;
}

/** Loudness shown on the meter: decibels from -60 (empty) to 0 (full), as a percentage */
export function levelToPercent(peak: number): number {
   if (peak <= 0) return 0;
   const decibels = 20 * Math.log10(peak);
   return Math.max(0, Math.min(100, ((decibels + 60) / 60) * 100));
}

type Props = {
   deviceId: string;
};

/**
 * Shows what the chosen input of the sound card delivers right now, in the same raw stereo format that is sent when the
 * instrument is turned on. It tells at once if the sound card gives any signal at all.
 */
export default function InstrumentLevelMeter({ deviceId }: Props) {
   const { t } = useTranslation();
   const [percent, setPercent] = useState(0);
   const [channels, setChannels] = useState<number | null>(null);
   const [failed, setFailed] = useState(false);

   useEffect(() => {
      let cancelled = false;
      let frame = 0;
      let stream: MediaStream | undefined;
      let context: AudioContext | undefined;
      setFailed(false);
      setChannels(null);
      setPercent(0);

      (async () => {
         try {
            stream = await navigator.mediaDevices.getUserMedia({
               audio: { ...MUSIC_AUDIO_CONSTRAINTS, deviceId: { exact: deviceId } },
            });
            if (cancelled) {
               stream.getTracks().forEach((x) => x.stop());
               return;
            }

            setChannels(stream.getAudioTracks()[0]?.getSettings().channelCount ?? null);
            context = new AudioContext();
            await context.resume();
            const analyser = context.createAnalyser();
            analyser.fftSize = 2048;
            context.createMediaStreamSource(stream).connect(analyser);
            const samples = new Float32Array(analyser.fftSize);
            let shown = 0;

            const tick = () => {
               analyser.getFloatTimeDomainData(samples);
               const value = levelToPercent(peakLevel(samples));
               // rises at once, falls slowly: easier to read
               shown = value > shown ? value : shown * 0.9 + value * 0.1;
               setPercent(shown);
               frame = requestAnimationFrame(tick);
            };
            tick();
         } catch {
            if (!cancelled) setFailed(true);
         }
      })();

      return () => {
         cancelled = true;
         cancelAnimationFrame(frame);
         stream?.getTracks().forEach((x) => x.stop());
         context?.close().catch(() => undefined);
      };
   }, [deviceId]);

   return (
      <Box id="instrument-level-meter" sx={{ mt: 1, maxWidth: 400 }}>
         <LinearProgress
            variant="determinate"
            value={percent}
            color={percent > 95 ? 'error' : 'primary'}
            sx={{ height: 8, borderRadius: 4 }}
         />
         <Typography variant="caption" color={failed ? 'error' : 'text.secondary'}>
            {failed
               ? t('conference.settings.audio.instrument_meter_failed')
               : percent < 2
               ? t('conference.settings.audio.instrument_meter_silent')
               : t('conference.settings.audio.instrument_meter_ok', { channels: channels ?? '?' })}
         </Typography>
      </Box>
   );
}

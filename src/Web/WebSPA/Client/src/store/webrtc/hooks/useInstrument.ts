import { useEffect, useRef } from 'react';
import useMedia, { UseMediaState } from './useMedia';
import { MUSIC_AUDIO_CONSTRAINTS, MUSIC_MIC_OPTIONS } from './useMicrophone';

/**
 * A second audio input for a musician (an instrument through a sound card): a producer of its own next to the
 * microphone, so the voice and the instrument can be heard (and recorded) as two tracks. Always raw stereo.
 *
 * @param monitor play the instrument to the musician in the headphones, straight from the sound card input (not through
 * the server, so without the delay of the call). Only with headphones: with speakers it feeds back.
 */
export default function useInstrument(monitor = false): UseMediaState {
   const monitorGain = useRef<GainNode | null>(null);

   useEffect(() => {
      if (monitorGain.current) monitorGain.current.gain.value = monitor ? 1 : 0;
   }, [monitor]);

   const getInstrument = async (deviceId?: string) => {
      // woken up while the tap is still being handled (see useMicrophone)
      const audioContext = new AudioContext({ latencyHint: 'interactive' });
      audioContext.resume().catch(() => undefined);

      let stream: MediaStream;
      try {
         stream = await navigator.mediaDevices.getUserMedia({
            audio: { ...MUSIC_AUDIO_CONSTRAINTS, deviceId: deviceId ? { exact: deviceId } : undefined },
         });
      } catch (error) {
         audioContext.close().catch(() => undefined);
         throw error;
      }

      // the monitor path always exists and is switched with its volume, so the switch works while playing
      const gain = audioContext.createGain();
      gain.gain.value = monitor ? 1 : 0;
      audioContext.createMediaStreamSource(stream).connect(gain);
      gain.connect(audioContext.destination);
      monitorGain.current = gain;

      // stopping the track also releases the input (it is the track of the stream) and ends the monitor
      const track = stream.getAudioTracks()[0];
      const stopTrack = track.stop.bind(track);
      track.stop = () => {
         stopTrack();
         if (monitorGain.current === gain) monitorGain.current = null;
         audioContext.close().catch(() => undefined);
      };
      return track;
   };

   return useMedia('instrument', getInstrument, MUSIC_MIC_OPTIONS);
}

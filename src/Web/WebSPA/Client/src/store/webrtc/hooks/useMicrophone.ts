import { ProducerOptions } from 'mediasoup-client/lib/types';
import { useEffect, useRef } from 'react';
import useMedia, { UseMediaState } from './useMedia';

/**
 * Music mode: the processing meant for speech (echo cancellation, noise suppression, automatic gain) would damage an
 * instrument and make the volume pump, so it is switched off. Stereo from the sound card, no discontinuous
 * transmission, a higher bitrate, short packets to keep the delay low.
 */
export const MUSIC_AUDIO_CONSTRAINTS: MediaTrackConstraints = {
   echoCancellation: false,
   noiseSuppression: false,
   autoGainControl: false,
   channelCount: 2,
   sampleRate: 48000,
};

export const MUSIC_MIC_OPTIONS: Partial<ProducerOptions> = {
   codecOptions: { opusStereo: true, opusFec: true, opusDtx: false, opusMaxPlaybackRate: 48000, opusPtime: 10 },
   encodings: [{ maxBitrate: 192000 }],
};

export default function useMicrophone(gain?: number, loopback = false, musicMode = false): UseMediaState {
   const audioGainNode = useRef<GainNode | null>(null);

   useEffect(() => {
      if (audioGainNode.current) {
         audioGainNode.current.gain.value = gain ?? 1;
      }
   }, [gain]);

   const getMic = async (deviceId?: string) => {
      const audioContext = new AudioContext(musicMode ? { latencyHint: 'interactive' } : undefined);
      // Safari on the iPhone creates the context suspended: nothing would come out of it, the microphone would be silent.
      // It has to be woken up while the tap of the person is still being handled, so before anything is awaited.
      audioContext.resume().catch(() => undefined);
      const gainNode = audioContext.createGain();

      let stream: MediaStream;
      try {
         stream = await navigator.mediaDevices.getUserMedia({
            audio: musicMode ? { ...MUSIC_AUDIO_CONSTRAINTS, deviceId } : { deviceId },
         });
      } catch (error) {
         audioContext.close().catch(() => undefined);
         throw error;
      }

      const audioSource = audioContext.createMediaStreamSource(stream);
      const audioDestination = audioContext.createMediaStreamDestination();

      audioSource.connect(gainNode);
      gainNode.connect(audioDestination);
      gainNode.gain.value = gain ?? 1;

      audioGainNode.current = gainNode;
      // make sure it runs (a second chance if the first resume came too early)
      audioContext.resume().catch(() => undefined);

      const track = audioDestination.stream.getAudioTracks()[0];
      // Stopping the track of the audio graph must also release the real microphone and the audio context: else the
      // microphone stays on (the indicator of the phone stays lit) and the browser runs out of audio contexts.
      const stopTrack = track.stop.bind(track);
      track.stop = () => {
         stopTrack();
         stream.getTracks().forEach((source) => source.stop());
         audioContext.close().catch(() => undefined);
      };

      return track;
   };

   return useMedia(loopback ? 'loopback-mic' : 'mic', getMic, musicMode ? MUSIC_MIC_OPTIONS : undefined);
}

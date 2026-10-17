import useMedia, { UseMediaState } from './useMedia';
import { MUSIC_AUDIO_CONSTRAINTS, MUSIC_MIC_OPTIONS } from './useMicrophone';

/**
 * A second audio input for a musician (an instrument through a sound card): a producer of its own next to the
 * microphone, so the voice and the instrument can be heard (and recorded) as two tracks. Always raw stereo.
 */
export default function useInstrument(): UseMediaState {
   const getInstrument = async (deviceId?: string) => {
      const stream = await navigator.mediaDevices.getUserMedia({
         audio: { ...MUSIC_AUDIO_CONSTRAINTS, deviceId: deviceId ? { exact: deviceId } : undefined },
      });
      // stopping the track releases the input
      const track = stream.getAudioTracks()[0];
      return track;
   };

   return useMedia('instrument', getInstrument, MUSIC_MIC_OPTIONS);
}

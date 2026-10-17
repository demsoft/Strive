import PianoIcon from '@mui/icons-material/Piano';
import React from 'react';
import { AnimatedIconProps } from './AnimatedIconBase';

/** The button of the instrument input. It has no animation; it only follows the color of the animated icons. */
export default function InstrumentIcon({ color }: AnimatedIconProps) {
   return <PianoIcon sx={{ color: color as string, width: 24, height: 24 }} />;
}

import LightbulbOutlinedIcon from '@mui/icons-material/LightbulbOutlined';
import GroupsOutlinedIcon from '@mui/icons-material/GroupsOutlined';
import MusicNoteIcon from '@mui/icons-material/MusicNote';
import SchoolOutlinedIcon from '@mui/icons-material/SchoolOutlined';
import TheatersOutlinedIcon from '@mui/icons-material/TheatersOutlined';
import { SvgIconComponent } from '@mui/icons-material';

export type CategoryStyle = {
   Icon: SvgIconComponent;
   /** the two colours of the band of the ticket and of the badge */
   from: string;
   to: string;
};

const CATEGORY_STYLES: Record<string, CategoryStyle> = {
   teach: { Icon: SchoolOutlinedIcon, from: '#FFB02E', to: '#FF7A1A' },
   perform: { Icon: TheatersOutlinedIcon, from: '#FF5D73', to: '#E8326F' },
   expertise: { Icon: LightbulbOutlinedIcon, from: '#2DD4BF', to: '#0EA5A5' },
   community: { Icon: GroupsOutlinedIcon, from: '#A78BFA', to: '#7C5CFF' },
   music: { Icon: MusicNoteIcon, from: '#FF9F1C', to: '#FF4D6D' },
};

/** Every category has its own colour and icon, so that sessions can be told apart at a glance. Unknown: like music. */
export const categoryStyle = (category: string): CategoryStyle => CATEGORY_STYLES[category] ?? CATEGORY_STYLES.music;

export const categoryGradient = (category: string) => {
   const { from, to } = categoryStyle(category);
   return `linear-gradient(135deg, ${from} 0%, ${to} 100%)`;
};

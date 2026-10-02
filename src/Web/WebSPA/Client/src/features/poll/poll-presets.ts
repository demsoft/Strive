import { TFunction } from 'i18next';
import { CreatePollDto } from 'src/core-hub.types';

type PollPreset = {
   label: string;
   data: CreatePollDto;
};

const getPresets: (t: TFunction) => PollPreset[] = (t) => [
   {
      label: t('conference.poll.create_dialog.presets.yes_no.label'),
      data: {
         config: { isAnonymous: true, isAnswerFinal: true },
         initialState: { isOpen: true, resultsPublished: false },
         instruction: {
            type: 'singleChoice',
            options: [
               t('conference.poll.create_dialog.presets.yes_no.yes'),
               t('conference.poll.create_dialog.presets.yes_no.no'),
            ],
         },
      },
   },
   {
      label: t('conference.poll.create_dialog.presets.a_b_c.label'),
      data: {
         config: { isAnonymous: true, isAnswerFinal: true },
         initialState: { isOpen: true, resultsPublished: false },
         instruction: {
            type: 'singleChoice',
            options: ['A', 'B', 'C'],
         },
      },
   },
   {
      label: t('conference.poll.create_dialog.presets.true_false.label'),
      data: {
         config: { isAnonymous: true, isAnswerFinal: true },
         initialState: { isOpen: true, resultsPublished: false },
         instruction: {
            type: 'singleChoice',
            options: [
               t('conference.poll.create_dialog.presets.true_false.true'),
               t('conference.poll.create_dialog.presets.true_false.false'),
            ],
         },
      },
   },
   {
      label: t('conference.poll.create_dialog.presets.task_status.label'),
      data: {
         config: { isAnonymous: true, isAnswerFinal: false },
         initialState: { isOpen: true, resultsPublished: false },
         instruction: {
            type: 'singleChoice',
            options: [
               t('conference.poll.create_dialog.presets.task_status.finished'),
               t('conference.poll.create_dialog.presets.task_status.surrendered'),
               t('conference.poll.create_dialog.presets.task_status.need_time'),
            ],
         },
      },
   },
];

export default getPresets;

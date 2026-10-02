import { TextField } from '@mui/material';
import React from 'react';
import { useTranslation } from 'react-i18next';
import { wrapForInputRef } from 'src/utils/reat-hook-form-utils';
import { InstructionFormProps } from '../types';

export default function SingleChoiceInstructionForm({
   form: {
      register,
      formState: { errors },
      watch,
   },
}: InstructionFormProps) {
   const { t } = useTranslation();

   const validateOptionsText = (s: string) => {
      if (!s) return false;
      return s.split(/\r?\n/).filter((x) => x.length > 0).length > 1;
   };

   const options = watch('instruction.options');

   return (
      <TextField
         autoFocus
         label={t('conference.poll.create_dialog.choices_label')}
         fullWidth
         {...wrapForInputRef(
            register('instruction.options', { validate: (value) => validateOptionsText(value as unknown as string) }),
         )}
         rows={4}
         multiline
         error={Boolean((errors.instruction as any)?.options)}
         helperText={
            (errors.instruction as any)?.options
               ? t('conference.poll.create_dialog.choices_error_at_least_two')
               : t('conference.poll.create_dialog.choices_helper_text')
         }
         slotProps={{
            inputLabel: { shrink: Boolean(options) },
         }}
      />
   );
}

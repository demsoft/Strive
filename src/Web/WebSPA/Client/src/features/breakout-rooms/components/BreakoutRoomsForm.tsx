import { Box, TextField } from '@mui/material';
import React from 'react';
import { Controller, UseFormReturn } from 'react-hook-form';
import { useTranslation } from 'react-i18next';
import { OpenBreakoutRoomsDto } from 'src/core-hub.types';
import { Participant } from 'src/features/conference/types';
import { wrapForInputRef } from 'src/utils/reat-hook-form-utils';
import OpenBreakoutRoomsAssignments from './BreakoutRoomsAssignments';

const validateNumberInteger = (val: unknown) => {
   const x = Number(val);
   if (Number.isInteger(x)) return true;

   return 'The amount must be an integer.';
};

type Props = {
   form: UseFormReturn<OpenBreakoutRoomsDto>;
   participants: Participant[] | null;
};

export default function BreakoutRoomsForm({
   form: {
      register,
      formState: { errors },
      watch,
      control,
   },
   participants,
}: Props) {
   const amount = watch('amount');
   const { t } = useTranslation();

   return (
      <div>
         <Box
            sx={{
               mt: 4,
            }}
         >
            <Box
               sx={{
                  display: 'flex',
               }}
            >
               <TextField
                  {...wrapForInputRef(
                     register('amount', {
                        min: {
                           value: 1,
                           message: t('conference.dialog_breakout_rooms.validation_must_create_at_least_one'),
                        },
                        required: true,
                        validate: validateNumberInteger,
                     }),
                  )}
                  required
                  label={t('common:amount')}
                  autoFocus
                  type="number"
                  error={!!errors.amount}
                  style={{ maxWidth: 80 }}
                  helperText={errors.amount?.message}
                  fullWidth
                  slotProps={{
                     htmlInput: { min: 1, step: 1 },
                  }}
               />
               <TextField
                  {...wrapForInputRef(
                     register('deadline', {
                        min: {
                           value: 1,
                           message: t('conference.dialog_breakout_rooms.validation_must_at_least_one_minute'),
                        },
                        validate: validateNumberInteger,
                     }),
                  )}
                  label={t('conference.dialog_breakout_rooms.duration_in_minutes')}
                  type="number"
                  error={!!errors.deadline}
                  style={{ maxWidth: 160, marginLeft: 16 }}
                  helperText={errors.deadline?.message}
                  fullWidth
                  slotProps={{
                     htmlInput: { min: 1, step: 1 },
                  }}
               />
               <TextField
                  label={t('common:description')}
                  {...wrapForInputRef(register('description'))}
                  placeholder={t('conference.dialog_breakout_rooms.description_description')}
                  fullWidth
                  style={{ marginLeft: 16 }}
               />
            </Box>
         </Box>
         {participants && (
            <Box
               sx={{
                  mt: 3,
                  height: 300,
               }}
            >
               <Controller
                  control={control}
                  name="assignedRooms"
                  render={({ field: { value, onChange } }) => (
                     <OpenBreakoutRoomsAssignments
                        data={value ?? []}
                        participants={participants}
                        createdRooms={amount}
                        onChange={(data) => onChange(data)}
                     />
                  )}
               />
            </Box>
         )}
      </div>
   );
}

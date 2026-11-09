import {
   Alert,
   Box,
   Button,
   Checkbox,
   CircularProgress,
   FormControlLabel,
   MenuItem,
   TextField,
   Typography,
} from '@mui/material';
import React, { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { requestErrorMessage } from 'src/features/creator/errors';
import * as api from 'src/services/api/sessions';
import { RightsBasis, Session } from 'src/services/api/sessions';
import { isPriceInRange, MATCH_CATEGORY, parsePriceToMinor } from '../money';

const BASES: RightsBasis[] = ['own', 'licensed', 'permission'];

type Props = { onCreated: (session: Session) => void };

/**
 * An organizer sets up a match (Arena): the teams, when it kicks off, the price of a ticket, and the declaration that
 * the organizer has the right to stream it. Nothing is published without the declaration.
 */
export default function NewMatchForm({ onCreated }: Props) {
   const { t } = useTranslation();
   const [home, setHome] = useState('');
   const [away, setAway] = useState('');
   const [competition, setCompetition] = useState('');
   const [description, setDescription] = useState('');
   const [price, setPrice] = useState('');
   const [startsAt, setStartsAt] = useState('');
   const [basis, setBasis] = useState<RightsBasis>('own');
   const [note, setNote] = useState('');
   const [accepted, setAccepted] = useState(false);
   const [saving, setSaving] = useState(false);
   const [error, setError] = useState<string | null>(null);

   const priceMinor = parsePriceToMinor(price);
   const priceOk = priceMinor !== null && isPriceInRange(priceMinor);
   const needsNote = basis !== 'own';
   const canPublish =
      home.trim().length >= 2 &&
      away.trim().length >= 2 &&
      priceOk &&
      accepted &&
      (!needsNote || note.trim().length >= 3) &&
      !saving;

   const publish = async () => {
      if (priceMinor === null) return;
      setSaving(true);
      setError(null);
      try {
         const session = await api.createSession({
            title: '',
            description: description.trim() || undefined,
            category: MATCH_CATEGORY,
            priceMinor,
            startsAt: startsAt ? new Date(startsAt).toISOString() : undefined,
            durationMinutes: 120,
            match: {
               homeTeam: home.trim(),
               awayTeam: away.trim(),
               competition: competition.trim() || undefined,
               rightsBasis: basis,
               rightsNote: needsNote ? note.trim() : undefined,
               rightsAccepted: accepted,
            },
         });
         setHome('');
         setAway('');
         setCompetition('');
         setDescription('');
         setPrice('');
         setStartsAt('');
         setNote('');
         setAccepted(false);
         onCreated(session);
      } catch (e) {
         setError(requestErrorMessage(e));
      } finally {
         setSaving(false);
      }
   };

   return (
      <Box id="new-match-form">
         <Box sx={{ display: 'flex', gap: 2, flexWrap: 'wrap', mb: 2 }}>
            <TextField
               id="match-home"
               label={t('matches.home')}
               value={home}
               onChange={(e) => setHome(e.target.value)}
               slotProps={{ htmlInput: { maxLength: 40 } }}
               sx={{ flex: '1 1 160px' }}
            />
            <TextField
               id="match-away"
               label={t('matches.away')}
               value={away}
               onChange={(e) => setAway(e.target.value)}
               slotProps={{ htmlInput: { maxLength: 40 } }}
               sx={{ flex: '1 1 160px' }}
            />
         </Box>
         <TextField
            id="match-competition"
            fullWidth
            label={t('matches.competition')}
            value={competition}
            onChange={(e) => setCompetition(e.target.value)}
            slotProps={{ htmlInput: { maxLength: 60 } }}
            sx={{ mb: 2 }}
         />
         <TextField
            id="match-starts"
            fullWidth
            type="datetime-local"
            label={t('matches.kickoff')}
            helperText={t('matches.kickoff_hint')}
            value={startsAt}
            onChange={(e) => setStartsAt(e.target.value)}
            slotProps={{ inputLabel: { shrink: true } }}
            sx={{ mb: 2 }}
         />
         <TextField
            id="match-price"
            fullWidth
            label={t('matches.price')}
            value={price}
            onChange={(e) => setPrice(e.target.value)}
            error={price !== '' && !priceOk}
            helperText={price !== '' && !priceOk ? t('sessions.price_range') : t('matches.price_hint')}
            slotProps={{ htmlInput: { inputMode: 'decimal' } }}
            sx={{ mb: 2 }}
         />
         <TextField
            id="match-description"
            fullWidth
            multiline
            minRows={2}
            label={t('matches.description')}
            value={description}
            onChange={(e) => setDescription(e.target.value)}
            slotProps={{ htmlInput: { maxLength: 2000 } }}
            sx={{ mb: 2 }}
         />

         <Box sx={{ p: 2, mb: 2, borderRadius: '16px', border: '1px solid', borderColor: 'divider' }} id="match-rights">
            <Typography sx={{ fontWeight: 700 }}>{t('matches.rights_title')}</Typography>
            <Typography variant="body2" color="text.secondary" sx={{ mb: 2 }}>
               {t('matches.rights_text')}
            </Typography>
            <TextField
               id="match-rights-basis"
               select
               fullWidth
               label={t('matches.rights_basis')}
               value={basis}
               onChange={(e) => setBasis(e.target.value as RightsBasis)}
               sx={{ mb: 2 }}
            >
               {BASES.map((b) => (
                  <MenuItem key={b} value={b}>
                     {t(`matches.basis_${b}`)}
                  </MenuItem>
               ))}
            </TextField>
            {needsNote && (
               <TextField
                  id="match-rights-note"
                  fullWidth
                  label={t('matches.rights_note')}
                  helperText={t('matches.rights_note_hint')}
                  value={note}
                  onChange={(e) => setNote(e.target.value)}
                  slotProps={{ htmlInput: { maxLength: 300 } }}
                  sx={{ mb: 1 }}
               />
            )}
            <FormControlLabel
               control={
                  <Checkbox
                     id="match-rights-accept"
                     checked={accepted}
                     onChange={(e) => setAccepted(e.target.checked)}
                  />
               }
               label={t('matches.rights_accept')}
            />
         </Box>

         {error && (
            <Alert severity="error" sx={{ mb: 2 }} id="match-form-error">
               {error}
            </Alert>
         )}
         <Button id="match-publish" variant="contained" size="large" fullWidth disabled={!canPublish} onClick={publish}>
            {saving ? <CircularProgress size={22} color="inherit" /> : t('matches.publish')}
         </Button>
      </Box>
   );
}

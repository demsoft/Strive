import { Box, Button, LinearProgress, Paper, Typography } from '@mui/material';
import CheckCircleIcon from '@mui/icons-material/CheckCircle';
import RadioButtonUncheckedIcon from '@mui/icons-material/RadioButtonUnchecked';
import React from 'react';
import { useTranslation } from 'react-i18next';
import { Link as RouterLink } from 'react-router-dom';
import { brand } from 'src/theme';

export type SetupStep = {
   id: string;
   title: string;
   text: string;
   done: boolean;
   /** the button of an open step: leads to a page, or scrolls to the part of this page where it is done */
   to?: string;
   scrollTo?: string;
   action: string;
   /** not counted: nice to have */
   optional?: boolean;
};

/** What is left to do to be ready: a bar, and the steps with a button for each one that is open. */
export default function SetupChecklist({ steps }: { steps: SetupStep[] }) {
   const { t } = useTranslation();
   const counted = steps.filter((x) => !x.optional);
   const done = counted.filter((x) => x.done).length;
   const percent = Math.round((done / counted.length) * 100);

   if (done === counted.length && steps.every((x) => x.done || x.optional)) return null;

   return (
      <Paper
         id="setup-checklist"
         sx={{ p: { xs: 2.5, md: 3 }, borderRadius: '24px', border: `1px solid ${brand.border}` }}
      >
         <Box sx={{ display: 'flex', alignItems: 'baseline', justifyContent: 'space-between', gap: 2 }}>
            <Typography variant="h6" sx={{ fontWeight: 800 }}>
               {t('dashboard.setup_title')}
            </Typography>
            <Typography className="setup-progress-text" variant="body2" color="text.secondary">
               {t('dashboard.setup_progress', { done, total: counted.length })}
            </Typography>
         </Box>
         <LinearProgress
            variant="determinate"
            value={percent}
            sx={{
               my: 1.5,
               height: 8,
               borderRadius: 4,
               backgroundColor: 'rgba(255,255,255,0.08)',
               '& .MuiLinearProgress-bar': { background: brand.gradient, borderRadius: 4 },
            }}
         />
         {steps.map((step, index) => (
            <Box
               key={step.id}
               className={`setup-step ${step.done ? 'done' : 'open'}`}
               sx={{
                  display: 'flex',
                  alignItems: 'center',
                  gap: 1.5,
                  py: 1.25,
                  borderTop: index ? `1px solid ${brand.border}` : 'none',
               }}
            >
               {step.done ? (
                  <CheckCircleIcon sx={{ color: 'success.main' }} />
               ) : (
                  <RadioButtonUncheckedIcon sx={{ opacity: 0.45 }} />
               )}
               <Box sx={{ minWidth: 0, flex: 1 }}>
                  <Typography
                     sx={{
                        fontWeight: 700,
                        textDecoration: step.done ? 'line-through' : 'none',
                        opacity: step.done ? 0.6 : 1,
                     }}
                  >
                     {step.title}
                     {step.optional && (
                        <Typography component="span" variant="caption" color="text.secondary" sx={{ ml: 1 }}>
                           {t('dashboard.optional')}
                        </Typography>
                     )}
                  </Typography>
                  {!step.done && (
                     <Typography variant="body2" color="text.secondary">
                        {step.text}
                     </Typography>
                  )}
               </Box>
               {!step.done &&
                  (step.to ? (
                     <Button
                        className="setup-action"
                        size="small"
                        variant="outlined"
                        component={RouterLink}
                        to={step.to}
                     >
                        {step.action}
                     </Button>
                  ) : (
                     <Button
                        className="setup-action"
                        size="small"
                        variant="outlined"
                        onClick={() =>
                           document
                              .getElementById(step.scrollTo ?? '')
                              ?.scrollIntoView?.({ behavior: 'smooth', block: 'center' })
                        }
                     >
                        {step.action}
                     </Button>
                  ))}
            </Box>
         ))}
      </Paper>
   );
}

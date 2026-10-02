import { useTheme } from '@mui/material';
import { ResponsiveBar } from '@nivo/bar';
import React from 'react';
import { useTranslation } from 'react-i18next';
import { NivoTooltipContent } from '../NivoTooltip';
import { PollResultsProps } from '../types';

export default function SelectionPollResults({ viewModel: { results, poll } }: PollResultsProps) {
   const theme = useTheme();
   const { t } = useTranslation();

   if (results?.results.type !== 'selection') return null;

   const maxAnswers = Math.ceil(Math.max(results.participantsAnswered, 5) / 5) * 5;
   // nivo bar data only allows strings and numbers, so the participant tokens are looked up for the tooltip
   const tokensByOption = results.results.options;

   return (
      <div style={{ height: '100%' }}>
         <ResponsiveBar
            data={Object.entries(results.results.options).map(([option, answers]) => ({
               option,
               count: answers.length,
            }))}
            keys={['count']}
            indexBy="option"
            animate={true}
            margin={{ bottom: 50, left: 50, top: 20 }}
            motionConfig={{ tension: 90, friction: 15 }}
            valueScale={{ type: 'linear', max: maxAnswers }}
            indexScale={{ type: 'band', round: true }}
            colors={{ scheme: 'nivo' }}
            labelTextColor={{ from: 'color', modifiers: [['darker', 1.6]] }}
            borderColor={{ from: 'color', modifiers: [['darker', 1.6]] }}
            axisTop={null}
            axisRight={null}
            tooltip={({ data: { option, count } }) => (
               <NivoTooltipContent
                  header={
                     <span>
                        {option}: <strong>{count}</strong>
                     </span>
                  }
                  participantTokens={tokensByOption[option] as any}
                  pollId={poll.id}
               />
            )}
            padding={0.3}
            theme={{
               text: { fill: theme.palette.text.secondary },
               grid: { line: { stroke: theme.palette.divider } },
               axis: {
                  ticks: {
                     line: {
                        stroke: theme.palette.text.secondary,
                     },
                     text: {
                        fill: theme.palette.text.primary,
                     },
                  },
               },
               tooltip: {
                  container: {
                     backgroundColor: theme.palette.background.paper,
                  },
               },
            }}
            axisBottom={{
               tickSize: 5,
               tickPadding: 5,
               tickRotation: 0,
               legend: t('conference.poll.chart_legend', { count: results.participantsAnswered }),
               legendPosition: 'middle',
               legendOffset: 40,
            }}
            axisLeft={{
               tickSize: 5,
               tickPadding: 5,
               tickRotation: 0,
               legend: t('conference.poll.types.single_choice.axis_legend'),
               legendPosition: 'middle',
               legendOffset: -40,
               tickValues: 5,
            }}
            gridYValues={5}
         />
      </div>
   );
}

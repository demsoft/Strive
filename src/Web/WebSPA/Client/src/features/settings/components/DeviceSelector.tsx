import { Box, Button, FormControl, InputLabel, SelectChangeEvent } from '@mui/material';
import { makeStyles } from 'tss-react/mui';
import _, { Collection } from 'lodash';
import React from 'react';
import { useTranslation } from 'react-i18next';
import { useDispatch } from 'react-redux';
import MobileAwareSelect from 'src/components/MobileAwareSelect';
import { showMessage } from 'src/store/notifier/actions';
import { DeviceGroup, EquipmentDeviceGroup } from '../selectors';
import { fetchDevices } from '../thunks';
import { AnyInputDevice } from '../types';

const useStyles = makeStyles()({
   control: {
      minWidth: 240,
      width: '100%',
      maxWidth: 400,
   },
});

type Props = {
   devices: DeviceGroup[];
   defaultName: string;
   className?: string;
   label: string;

   onChange: (device: AnyInputDevice) => void;

   selectedDevice?: AnyInputDevice;
};

const parseDeviceId: (deviceId: string) => [string, string] = (deviceId) => {
   const i = deviceId.indexOf('/');
   return [deviceId.slice(0, i), deviceId.slice(i + 1)];
};

const getId = (device: AnyInputDevice) =>
   device.type === 'local' ? `local/${device.deviceId}` : `${device.connectionId}/${device.deviceId}`;

export default function DeviceSelector({ devices, defaultName, className, selectedDevice, onChange, label }: Props) {
   const selectId = defaultName.toLowerCase() + '-select';
   const { classes, cx } = useStyles();
   const dispatch = useDispatch();
   const { t } = useTranslation();

   const handleChange = (event: SelectChangeEvent<unknown>) => {
      const value = event.target.value as string;
      if (!value) return;

      let possibleDevices: Collection<DeviceGroup>;

      const [type, deviceId] = parseDeviceId(value);
      if (type === 'local') {
         possibleDevices = _(devices).filter((x) => x.type === 'local') as Collection<DeviceGroup>;
      } else {
         possibleDevices = _(devices).filter(
            (x) => x.type === 'equipment' && x.connectionId === type,
         ) as Collection<DeviceGroup>;
      }

      const viewModel = possibleDevices.flatMap((x) => x.devices).find((x) => x.device.deviceId === deviceId);

      if (viewModel) {
         onChange(viewModel.device);
      }
   };

   const handleRefresh = () => {
      dispatch(
         showMessage({
            type: 'action',
            message: t('conference.notifications.fetch_devices.loading'),
            failedOn: { type: fetchDevices.rejected.type, message: t('conference.notifications.fetch_devices.error') },
            succeededOn: {
               type: fetchDevices.fulfilled.type,
               message: t('conference.notifications.fetch_devices.success'),
            },
         }),
      );
      dispatch(fetchDevices());
   };

   const defaultDevice = devices[0]?.devices[0]?.device;

   return (
      <Box
         sx={{
            display: 'flex',
            alignItems: 'flex-end',
         }}
      >
         <FormControl className={cx(className, classes.control)}>
            <InputLabel htmlFor={selectId}>{label}</InputLabel>
            <MobileAwareSelect
               id={selectId}
               value={selectedDevice ? getId(selectedDevice) : defaultDevice ? getId(defaultDevice) : ''}
               onChange={handleChange}
            >
               {[
                  ...(devices
                     .find((x) => x.type === 'local')
                     ?.devices.map((x, i) => ({ value: getId(x.device), label: x.label || `${defaultName} #${i}` })) ||
                     []),
                  ...devices
                     .filter((x) => x.type !== 'local')
                     .map((x) => x as EquipmentDeviceGroup)
                     .map((x, i) => ({
                        label: x.equipmentName || t('conference.settings.unnamed_device'),
                        key: x.connectionId,
                        children: x.devices.map((x) => ({
                           value: getId(x.device),
                           label: x.label || `${defaultName} #${i}`,
                        })),
                     })),
               ]}
            </MobileAwareSelect>
         </FormControl>
         <Box
            onClick={handleRefresh}
            sx={{
               ml: 1,
            }}
         >
            <Button>{t('common:refresh')}</Button>
         </Box>
      </Box>
   );
}

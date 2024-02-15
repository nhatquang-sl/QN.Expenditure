import { zodResolver } from '@hookform/resolvers/zod';
import { LoadingButton } from '@mui/lab';
import { TextField } from '@mui/material';
import { MobileDateTimePicker } from '@mui/x-date-pickers';
import { showSnackbar } from 'components/snackbar/slice';
import dayjs from 'dayjs';
import { useState } from 'react';
import { Controller, SubmitHandler, useForm } from 'react-hook-form';
import { useDispatch } from 'react-redux';
import { bnbSpotClient } from 'store';
import { Conflict, CreateSyncSettingCommand } from 'store/api-client';
import { CreateSpotOrderData, CreateSpotOrderSchema, OnChangeCallback } from './types';

const AddSyncSetting = (props: { onAddNew: OnChangeCallback }) => {
  const { onAddNew } = props;
  const dispatch = useDispatch();
  const [loading, setLoading] = useState(false);
  const {
    handleSubmit,
    trigger,
    control,
    formState: { errors },
  } = useForm<CreateSpotOrderData>({
    resolver: zodResolver(CreateSpotOrderSchema),
  });

  const onSubmit: SubmitHandler<CreateSpotOrderData> = async (data) => {
    setLoading(true);
    console.log({ data });
    console.log(data.lastSyncAt.unix() * 1000);
    try {
      var syncSetting = await bnbSpotClient.createSyncSetting(
        new CreateSyncSettingCommand({
          symbol: data.symbol,
          lastSyncAt: data.lastSyncAt.unix() * 1000,
        })
      );
      onAddNew(syncSetting);
    } catch (err: any) {
      if (err instanceof Conflict) {
        dispatch(showSnackbar(err.message, 'error', 'top', 'right'));
      }
    }

    setLoading(false);
  };

  return (
    <form onSubmit={handleSubmit(onSubmit)} noValidate>
      <Controller
        control={control}
        name="symbol"
        defaultValue=""
        render={({ field }) => (
          <TextField
            required
            {...field}
            id="symbol"
            type="text"
            label="Symbol"
            sx={{ margin: 1 }}
            error={!!errors.symbol}
            helperText={errors.symbol ? errors.symbol?.message : ''}
          />
        )}
      />

      <Controller
        control={control}
        name="lastSyncAt"
        defaultValue={dayjs(new Date())}
        render={({ field }) => (
          <MobileDateTimePicker {...field} sx={{ verticalAlign: 'middle', margin: 1 }} />
        )}
      />

      <LoadingButton
        type="submit"
        variant="contained"
        sx={{ margin: 1 }}
        onClick={() => trigger()}
        loading={loading}
      >
        Add
      </LoadingButton>
    </form>
  );
};

export default AddSyncSetting;

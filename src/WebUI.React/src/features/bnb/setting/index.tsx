import { zodResolver } from '@hookform/resolvers/zod';
import { LoadingButton } from '@mui/lab';
import { Grid, Paper, Typography } from '@mui/material';
import { useCallback, useEffect, useState } from 'react';
import { SubmitHandler, useForm } from 'react-hook-form';
import { bnbSettingClient } from 'store';
import { UpdateBnbSettingCommand } from 'store/api-client';
import TextPassword from './text-password';
import { UpdateSettingData, UpdateSettingSchema } from './types';

// function ControlledTextPassword(props: {
//   name: 'apiKey' | 'secretKey';
//   label: string;
//   loading: boolean;
//   control: Control<UpdateSettingData>;
//   errors: FieldErrors<UpdateSettingData>;
// }) {
//   const { name, label, loading, control, errors } = props;
//   return (
//     <Controller
//       control={control}
//       name={name}
//       defaultValue=""
//       render={({ field }) => (
//         <TextPassword
//           required
//           {...field}
//           fullWidth
//           id={name}
//           label={label}
//           disabled={loading}
//           sx={{ margin: 1 }}
//           error={!!errors[name]}
//           helperText={errors[name] ? errors[name]?.message : ''}
//         />
//       )}
//     />
//   );
// }

export default function BnbSetting() {
  const [loading, setLoading] = useState(false);

  const {
    handleSubmit,
    trigger,
    setValue,
    control,
    formState: { errors },
  } = useForm<UpdateSettingData>({
    resolver: zodResolver(UpdateSettingSchema),
  });

  const fetchSessions = useCallback(async () => {
    setLoading(true);
    const setting = await bnbSettingClient.getSetting();

    setValue('apiKey', setting.apiKey);
    setValue('secretKey', setting.secretKey);

    setLoading(false);
  }, []);

  useEffect(() => {
    console.log('Bnb Setting');

    fetchSessions();
  }, [fetchSessions]);

  const onSubmit: SubmitHandler<UpdateSettingData> = async (data) => {
    setLoading(true);
    console.log({ data });

    await bnbSettingClient.updateSetting(new UpdateBnbSettingCommand(data));

    setLoading(false);
  };

  return (
    <Grid container spacing={3}>
      <Grid item xs={12}>
        <Paper sx={{ p: 2, display: 'flex', flexDirection: 'column' }}>
          <Typography component="h2" variant="h6" color="primary" gutterBottom>
            Bnb Setting
          </Typography>
          <form onSubmit={handleSubmit(onSubmit)} noValidate>
            <TextPassword
              name="apiKey"
              label="API Key"
              control={control}
              errors={errors}
              loading={loading}
            />

            <TextPassword
              name="secretKey"
              label="Secret Key"
              control={control}
              errors={errors}
              loading={loading}
            />
            <LoadingButton
              type="submit"
              variant="contained"
              sx={{ margin: 1 }}
              loading={loading}
              onClick={() => trigger()}
            >
              Add
            </LoadingButton>
          </form>
        </Paper>
      </Grid>
    </Grid>
  );
}

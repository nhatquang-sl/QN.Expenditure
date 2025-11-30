import { Visibility, VisibilityOff } from '@mui/icons-material';
import { IconButton, InputAdornment, TextField } from '@mui/material';
import { useState } from 'react';
import { Control, Controller, FieldErrors } from 'react-hook-form';
import { UpdateSettingData } from './types';

export default function TextPassword(props: {
  name: 'apiKey' | 'secret';
  label: string;
  loading?: boolean;
  control: Control<UpdateSettingData>;
  errors: FieldErrors<UpdateSettingData>;
}) {
  const [showPassword, setShowPassword] = useState(false);
  const { name, label, loading, control, errors } = props;
  const handleClickShowPassword = () => setShowPassword((show) => !show);

  const handleMouseDownPassword = (event: React.MouseEvent<HTMLButtonElement>) => {
    event.preventDefault();
  };
  return (
    <Controller
      control={control}
      name={name}
      defaultValue=""
      render={({ field }) => (
        <TextField
          required
          fullWidth
          id={name}
          label={label}
          disabled={loading}
          sx={{ margin: 1 }}
          {...field}
          error={!!errors.apiKey}
          helperText={errors.apiKey ? errors.apiKey?.message : ''}
          type={showPassword ? 'text' : 'password'}
          InputProps={{
            endAdornment: (
              <InputAdornment position="end">
                <IconButton
                  onClick={handleClickShowPassword}
                  onMouseDown={handleMouseDownPassword}
                  edge="end"
                >
                  {showPassword ? <VisibilityOff /> : <Visibility />}
                </IconButton>
              </InputAdornment>
            ),
          }}
        />
      )}
    />
  );
}

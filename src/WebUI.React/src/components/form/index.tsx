import { LoadingButton } from '@mui/lab';
import { Alert, Grid, MenuItem, TextField } from '@mui/material';
import { useState } from 'react';
import { Resolver, useForm } from 'react-hook-form';
import { Block } from './types';

function camelCase(str: string) {
  // Using replace method with regEx
  return str
    .replace(/(?:^\w|[A-Z]|\b\w)/g, function (word, index) {
      return index == 0 ? word.toLowerCase() : word.toUpperCase();
    })
    .replace(/\s+/g, '');
}

// https://www.youtube.com/watch?v=aCRaGQmUiQE
export default function Form(props: {
  blocks: Block[];
  resolver: Resolver;
  onSubmit: (data: any) => Promise<void>;
}) {
  const { blocks } = props;
  const {
    handleSubmit,
    register,
    formState: { errors },
  } = useForm({ resolver: props.resolver });
  const [submitErrors, setSubmitErrors] = useState<Record<string, string>>({});
  const [errorMessage, setErrorMessage] = useState('');
  const [loading, setLoading] = useState(false);
  const onSubmit = async (data: any) => {
    setLoading(true);
    try {
      await props.onSubmit(data);

      setSubmitErrors({});
      setErrorMessage('');
    } catch (err: any) {
      setSubmitErrors(err);
      setErrorMessage(err['message']);
    }
    setLoading(false);
  };

  const onCloseErrorMessage = () => {
    setErrorMessage('');
  };
  console.log(errors, props.resolver.name);
  return (
    <Grid
      container
      spacing={2}
      sx={{ marginTop: 0 }}
      component="form"
      onSubmit={handleSubmit(onSubmit)}
      noValidate
    >
      {errorMessage && (
        <Grid item xs={12} sm={12}>
          <Alert severity="error" onClose={onCloseErrorMessage}>
            {errorMessage}
          </Alert>
        </Grid>
      )}
      {blocks.map((b) => (
        <Grid key={b.id} item xs={12} sm={12} sx={{ display: 'flex', columnGap: '16px' }}>
          {b.elements.map((el) => {
            const elId = camelCase(el.label);
            const error = errors[elId];
            if (el.type === 'select')
              return (
                <TextField
                  id={elId}
                  label={el.label}
                  key={elId}
                  {...register(elId, { setValueAs: (v) => (isNaN(v) ? v : parseFloat(v)) })}
                  select
                  defaultValue={el.defaultValue ?? el.options[0].value}
                  error={!!error}
                  helperText={error ? error.message?.toString() : ''}
                  sx={{ minWidth: 120 }}
                >
                  {el.options.map((option) => (
                    <MenuItem key={option.value} value={option.value}>
                      {option.label}
                    </MenuItem>
                  ))}
                </TextField>
              );

            return (
              <TextField
                key={elId}
                {...register(elId, {
                  setValueAs: (v) => {
                    if (v === '') return '';
                    if (el.type === 'number') return parseFloat(v);
                    else return v;
                  },
                })}
                id={elId}
                defaultValue={el.defaultValue ?? ''}
                label={el.label}
                type={el.type}
                error={!!error || !!submitErrors[elId]}
                helperText={
                  error ? error.message?.toString() : !!submitErrors[elId] ? submitErrors[elId] : ''
                }
                sx={{ flex: el.flex }}
              />
            );
          })}
          {b.actions.map((a) =>
            a.onClickButton === undefined ? (
              <LoadingButton type={a.type} key={a.label} variant="contained" loading={loading}>
                {a.label}
              </LoadingButton>
            ) : (
              <LoadingButton type={a.type} key={a.label} variant="contained" loading={loading}>
                {a.label}
              </LoadingButton>
            )
          )}
        </Grid>
      ))}
    </Grid>
  );
}

import { createSlice, PayloadAction } from '@reduxjs/toolkit';
import { RootState } from 'store';
import { UserAuthDto } from 'store/api-client';

type AuthState = {
  id: string;
  firstName: string;
  lastName: string;
  email: string;
  emailConfirmed: boolean;
};

const defaultState: AuthState = {
  id: '',
  firstName: '',
  lastName: '',
  email: '',
  emailConfirmed: false,
};

export const authSlice = createSlice({
  name: 'auth',
  initialState: defaultState,
  reducers: {
    setAuth: (state: AuthState, action: PayloadAction<UserAuthDto>) => {
      state.id = action.payload.id ?? '';
      state.firstName = action.payload.firstName ?? '';
      state.lastName = action.payload.lastName ?? '';
      state.email = action.payload.email ?? '';
      state.emailConfirmed = action.payload.emailConfirmed ?? false;
    },
    logout: (state) => {
      Object.assign(state, defaultState);
    },
  },
});

export const { setAuth, logout } = authSlice.actions;
export const selectAuth = (state: RootState) => state.auth;
export const selectIsAuthenticated = (state: RootState) => !!state.auth.id;
export const selectNeedsEmailConfirmation = (state: RootState) =>
  !!state.auth.id && !state.auth.emailConfirmed;

export default authSlice.reducer;

import { createSlice, PayloadAction } from '@reduxjs/toolkit';
import { jwtDecode } from 'jwt-decode';
import { RootState } from 'store';

export enum TokenType {
  Login = 'LOGIN',
  NeedActivate = 'NEED_ACTIVATE',
  ResetPassword = 'RESET_PASSWORD',
}

export class TokenData {
  id: string = '';
  firstName: string = '';
  lastName: string = '';
  emailAddress: string = '';
  roles: string[] = [];
  type: TokenType = TokenType.NeedActivate;
  exp: number = 0;
  iat: number = 0;
}

type AuthState = {
  id?: string;
  accessToken: string;
  firstName: string;
  lastName: string;
  emailAddress: string;
  roles: string[];
  type: TokenType | null;
  exp: number;
  iat: number;
};

const initialState: AuthState = JSON.parse(localStorage.getItem('AUTH') ?? '{}') as AuthState;
// {
//   accessToken: '',
//   firstName: '',
//   lastName: '',
//   emailAddress: '',
//   roles: [],
//   type: null,
//   exp: 0,
//   iat: 0,
// };
console.log({ initialState });

export const authSlice = createSlice({
  name: 'auth',
  initialState,
  reducers: {
    setAuth: (state: AuthState, action: PayloadAction<string>) => {
      const accessToken = action.payload;
      const tokenData = (accessToken ? jwtDecode(accessToken) : {}) as TokenData;

      state.id = tokenData?.id;
      state.accessToken = accessToken ?? '';
      state.firstName = tokenData?.firstName ?? '';
      state.lastName = tokenData?.lastName ?? '';
      state.emailAddress = tokenData?.emailAddress ?? '';
      state.type = tokenData?.type ?? '';
      state.roles = tokenData?.roles ?? [];
      state.exp = tokenData?.exp ?? 0;
      state.iat = tokenData?.iat ?? 0;
      localStorage.setItem('AUTH', JSON.stringify(state));
      console.log(tokenData);
    },
    logout: (state) => {
      localStorage.clear();
      state.accessToken = '';
      state.type = null;
    },
  },
});

export const { setAuth, logout } = authSlice.actions;
export const selectAuthType = (state: RootState) => state.auth.type;
export const selectAuth = (state: RootState) => state.auth;

export default authSlice.reducer;

import { z } from 'zod';

export const LoginDataSchema = z.object({
  email: z
    .string()
    .email()
    .trim()
    .max(255, { message: 'Email has reached a maximum of 255 characters.' }),
  password: z
    .string()
    .trim()
    .min(6, { message: 'Password must be at least 6 characters.' })
    .regex(/[a-z]/g, { message: "Password must have at least one lowercase ('a'-'z')." })
    .regex(/[A-Z]/g, { message: "Password must have at least one uppercase ('A'-'Z')." })
    .regex(/[0-9]/g, { message: 'Password must contain at least one number.' })
    .regex(/[!@#$%^&*()_+=\[{\]};:<>|./?,-]/g, {
      message: 'Password must have at least one non alphanumeric character.',
    })
    .max(50, { message: 'Password has reached a maximum of 50 characters.' }),
});

export const RegisterDataSchema = LoginDataSchema.extend({
  firstName: z
    .string()
    .trim()
    .min(2, { message: 'First Name must be at least 2 characters.' })
    .max(50, { message: 'First Name has reached a maximum of 50 characters.' }),
  lastName: z
    .string()
    .trim()
    .min(2, { message: 'Last Name must be at least 2 characters.' })
    .max(50, { message: 'Last Name has reached a maximum of 50 characters.' }),
  confirmPassword: z.string().min(1, { message: "Confirm Password don't match" }).trim(),
}).refine((data) => data.password === data.confirmPassword, {
  message: "Confirm Password don't match",
  path: ['confirmPassword'],
});

export type LoginData = z.infer<typeof LoginDataSchema>;
export type RegisterData = z.infer<typeof RegisterDataSchema>;

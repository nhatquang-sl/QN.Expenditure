INSERT INTO "EmailType" ("Id", "Subject", "HtmlTemplate")
VALUES (
    'activate_account',
    'Activate your account',
    '<!DOCTYPE html>
<html>
<head><meta charset="UTF-8"><title>Activate your account</title></head>
<body style="font-family:sans-serif;max-width:600px;margin:0 auto;padding:24px;color:#333">
  <h2>Hello, {{.FirstName}}</h2>
  <p>Thank you for registering. Please confirm your email address by clicking the button below:</p>
  <p>
    <a href="{{.ConfirmURL}}"
       style="background:#1976d2;color:#fff;padding:12px 24px;text-decoration:none;border-radius:4px;display:inline-block">
      Activate Account
    </a>
  </p>
  <p>If the button does not work, copy and paste this link into your browser:</p>
  <p style="word-break:break-all">{{.ConfirmURL}}</p>
  <p style="color:#888;font-size:12px">This link will expire in 24 hours. If you did not create an account, you can ignore this email.</p>
</body>
</html>'
);

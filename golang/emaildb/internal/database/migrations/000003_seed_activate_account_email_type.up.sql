INSERT INTO "EmailType" ("Id", "Subject", "HtmlTemplate")
VALUES (
    'activate_account',
    'Activate your account',
    '<html><body><p>Hello {{.FirstName}},</p><p>Activate your account using this link: <a href="{{.ConfirmURL}}">Confirm</a></p></body></html>'
)
ON CONFLICT ("Id") DO NOTHING;

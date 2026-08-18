package port

import "context"

type EmailService interface {
	SendEmailConfirmation(ctx context.Context, toEmail, firstName, confirmURL string) error
}

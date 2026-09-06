package shared

import "context"

type EmailService interface {
	Send(ctx context.Context, userId string, emailType EmailType, data any) error
}

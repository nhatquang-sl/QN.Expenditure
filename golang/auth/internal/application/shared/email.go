package shared

import "context"

type EmailService interface {
	Send(ctx context.Context, msg EmailMessage) error
}

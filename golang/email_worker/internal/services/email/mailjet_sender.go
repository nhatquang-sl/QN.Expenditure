package email

import (
	"context"
	"fmt"
	"log"

	"email_worker/internal/worker"

	mailjet "github.com/mailjet/mailjet-apiv3-go/v4"
)

type MailjetSender struct {
	client    *mailjet.Client
	fromEmail string
}

var _ worker.EmailSender = (*MailjetSender)(nil)

func NewMailjetSender(apiKeyPublic, apiKeyPrivate, fromEmail string) *MailjetSender {
	return &MailjetSender{
		client:    mailjet.NewMailjetClient(apiKeyPublic, apiKeyPrivate),
		fromEmail: fromEmail,
	}
}

func (s *MailjetSender) Send(_ context.Context, to, subject, htmlBody string) error {
	messages := mailjet.MessagesV31{
		Info: []mailjet.InfoMessagesV31{
			{
				From: &mailjet.RecipientV31{
					Email: s.fromEmail,
				},
				To: &mailjet.RecipientsV31{
					{Email: to},
				},
				Subject:  subject,
				HTMLPart: htmlBody,
			},
		},
	}

	res, err := s.client.SendMailV31(&messages)
	if err != nil {
		return fmt.Errorf("mailjet send: %w", err)
	}
	if len(res.ResultsV31) > 0 && res.ResultsV31[0].Status != "success" {
		return fmt.Errorf("mailjet send status: %s", res.ResultsV31[0].Status)
	}
	log.Println("Mailjet send result:", res.ResultsV31)
	return nil
}

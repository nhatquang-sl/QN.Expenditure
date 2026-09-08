package email

import (
	"context"
	"encoding/json"
	"fmt"
	"sync"

	"auth/internal/application/shared"

	amqp "github.com/rabbitmq/amqp091-go"
)

const (
	exchangeName = "email"
	queueName    = "email.queue"
	routingKey   = "email.notify"
)

type emailMessage struct {
	UserId    string          `json:"userId"`
	ToEmail   string          `json:"toEmail"`
	EmailType string          `json:"emailType"`
	Data      json.RawMessage `json:"data"`
}

// RabbitMQService publishes email jobs to a RabbitMQ exchange.
// It is safe for concurrent use.
type RabbitMQService struct {
	conn    *amqp.Connection
	channel *amqp.Channel
	mu      sync.Mutex
}

func NewRabbitMQService(host, username, password string) (*RabbitMQService, error) {
	url := fmt.Sprintf("amqp://%s:%s@%s/", username, password, host)
	conn, err := amqp.Dial(url)
	if err != nil {
		return nil, fmt.Errorf("rabbitmq connect: %w", err)
	}

	ch, err := conn.Channel()
	if err != nil {
		conn.Close()
		return nil, fmt.Errorf("rabbitmq open channel: %w", err)
	}

	if err := ch.ExchangeDeclare(exchangeName, "direct", true, false, false, false, nil); err != nil {
		ch.Close()
		conn.Close()
		return nil, fmt.Errorf("rabbitmq declare exchange: %w", err)
	}

	if _, err := ch.QueueDeclare(queueName, true, false, false, false, nil); err != nil {
		ch.Close()
		conn.Close()
		return nil, fmt.Errorf("rabbitmq declare queue: %w", err)
	}

	if err := ch.QueueBind(queueName, routingKey, exchangeName, false, nil); err != nil {
		ch.Close()
		conn.Close()
		return nil, fmt.Errorf("rabbitmq bind queue: %w", err)
	}

	return &RabbitMQService{conn: conn, channel: ch}, nil
}

func (s *RabbitMQService) Close() error {
	s.channel.Close()
	return s.conn.Close()
}

func (s *RabbitMQService) Send(ctx context.Context, msg shared.EmailMessage) error {
	rawData, err := json.Marshal(msg.Data)
	if err != nil {
		return fmt.Errorf("marshal email data: %w", err)
	}

	payload, err := json.Marshal(emailMessage{
		UserId:    msg.UserId,
		ToEmail:   msg.ToEmail,
		EmailType: string(msg.EmailType),
		Data:      rawData,
	})
	if err != nil {
		return fmt.Errorf("marshal email message: %w", err)
	}

	s.mu.Lock()
	defer s.mu.Unlock()

	return s.channel.PublishWithContext(ctx, exchangeName, routingKey, false, false, amqp.Publishing{
		ContentType:  "application/json",
		DeliveryMode: amqp.Persistent,
		Body:         payload,
	})
}

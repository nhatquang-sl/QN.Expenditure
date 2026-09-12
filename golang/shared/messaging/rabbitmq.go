package messaging

import (
	"context"
	"encoding/json"
	"fmt"
	"reflect"
	"sync"

	amqp "github.com/rabbitmq/amqp091-go"
)

// RabbitMQService publishes messages to a RabbitMQ exchange.
// It is safe for concurrent use.
type RabbitMQService struct {
	conn      *amqp.Connection
	channel   *amqp.Channel
	queueName string
	mu        sync.Mutex
}

func ConnectAmqp[T any](cfg RabbitMqConfig) (*RabbitMQService, error) {
	queueName := reflect.TypeFor[T]().String()
	address := fmt.Sprintf("amqp://%s:%s@%s:%d/", cfg.Username, cfg.Password, cfg.Host, cfg.Port)
	conn, err := amqp.Dial(address)
	if err != nil {
		return nil, fmt.Errorf("rabbitmq connect: %w", err)
	}

	channel, err := conn.Channel()
	if err != nil {
		conn.Close()
		return nil, fmt.Errorf("rabbitmq open channel: %w", err)
	}

	err = channel.ExchangeDeclare(
		queueName, // exchange name (same as queue)
		"fanout",  // type
		true,      // durable: survives broker restart
		false,     // autoDeleted: don't delete when no bindings
		false,     // internal: accessible from outside (normal)
		false,     // noWait: wait for server confirm
		nil,       // arguments
	)
	if err != nil {
		channel.Close()
		conn.Close()
		return nil, fmt.Errorf("rabbitmq declare exchange: %w", err)
	}

	_, err = channel.QueueDeclare(queueName,
		true,  // durable: survives broker restart
		false, // autoDelete: don't delete when no consumers
		false, // exclusive: shared across connections
		false, // noWait: wait for server confirm
		nil,   // arguments
	)
	if err != nil {
		channel.Close()
		conn.Close()
		return nil, fmt.Errorf("rabbitmq declare queue: %w", err)
	}

	err = channel.QueueBind(
		queueName, // queue name
		"",        // routing key (ignored for fanout)
		queueName, // exchange name
		false,     // no-wait
		nil,       // arguments
	)
	if err != nil {
		channel.Close()
		conn.Close()
		return nil, fmt.Errorf("rabbitmq bind queue: %w", err)
	}

	return &RabbitMQService{
		conn:      conn,
		channel:   channel,
		queueName: queueName,
	}, nil
}

func (s *RabbitMQService) Close() error {
	s.channel.Close()
	return s.conn.Close()
}

func (s *RabbitMQService) Consume() (<-chan amqp.Delivery, error) {
	return s.channel.Consume(
		s.queueName, // queue
		"",          // consumer tag
		true,        // auto-ack
		false,       // exclusive
		false,       // no-local
		false,       // no-wait
		nil,         // arguments
	)
}

func (s *RabbitMQService) Send(ctx context.Context, msg any) error {
	payload, err := json.Marshal(msg)
	if err != nil {
		return fmt.Errorf("marshal message: %w", err)
	}

	s.mu.Lock()
	defer s.mu.Unlock()

	return s.channel.PublishWithContext(ctx, s.queueName, "", false, false, amqp.Publishing{
		ContentType:  "application/json",
		DeliveryMode: amqp.Persistent,
		Body:         payload,
	})
}

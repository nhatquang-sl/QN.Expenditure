package messaging

import (
	"context"
	"encoding/json"
	"fmt"
	"reflect"
	"sync"
	"time"

	amqp "github.com/rabbitmq/amqp091-go"
)

// RabbitMQService publishes messages to a RabbitMQ exchange.
// It is safe for concurrent use.
type RabbitMQService struct {
	conn      *amqp.Connection
	channel   *amqp.Channel
	queueName string
	cfg       RabbitMqConfig
	mu        sync.Mutex
}

func ConnectAmqp[T any](cfg RabbitMqConfig) (*RabbitMQService, error) {
	queueName := reflect.TypeFor[T]().String()
	address := dialAddress(cfg)
	conn, err := amqp.Dial(address)
	if err != nil {
		return nil, fmt.Errorf("rabbitmq connect: %w", err)
	}

	channel, err := setupChannel(conn, queueName)
	if err != nil {
		conn.Close()
		return nil, err
	}

	return &RabbitMQService{
		conn:      conn,
		channel:   channel,
		queueName: queueName,
		cfg:       cfg,
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

	if err := s.publish(ctx, payload); err == nil {
		return nil
	}

	// publish failed — reconnect with backoff and retry once
	if err := s.reconnect(); err != nil {
		return fmt.Errorf("rabbitmq reconnect: %w", err)
	}
	return s.publish(ctx, payload)
}

func (s *RabbitMQService) publish(ctx context.Context, payload []byte) error {
	return s.channel.PublishWithContext(ctx, s.queueName, "", false, false, amqp.Publishing{
		ContentType:  "application/json",
		DeliveryMode: amqp.Persistent,
		Body:         payload,
	})
}

// reconnect must be called with s.mu held.
func (s *RabbitMQService) reconnect() error {
	s.channel.Close()
	s.conn.Close()

	address := dialAddress(s.cfg)
	delays := []time.Duration{1, 2, 4, 8, 16}
	var (
		conn *amqp.Connection
		err  error
	)
	for _, d := range delays {
		conn, err = amqp.Dial(address)
		if err == nil {
			break
		}
		time.Sleep(d * time.Second)
	}
	if err != nil {
		return fmt.Errorf("rabbitmq dial: %w", err)
	}

	ch, err := setupChannel(conn, s.queueName)
	if err != nil {
		conn.Close()
		return err
	}

	s.conn = conn
	s.channel = ch
	return nil
}

func dialAddress(cfg RabbitMqConfig) string {
	port := cfg.Port
	if port == 0 {
		port = 5672
	}
	return fmt.Sprintf("amqp://%s:%s@%s:%d/", cfg.Username, cfg.Password, cfg.Host, port)
}

func setupChannel(conn *amqp.Connection, queueName string) (*amqp.Channel, error) {
	channel, err := conn.Channel()
	if err != nil {
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
		return nil, fmt.Errorf("rabbitmq bind queue: %w", err)
	}

	return channel, nil
}

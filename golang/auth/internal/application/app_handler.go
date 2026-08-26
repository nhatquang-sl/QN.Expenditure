package application

import "context"

type Handler[C, R any] interface {
	Handle(ctx context.Context, cmd C) (R, error)
}

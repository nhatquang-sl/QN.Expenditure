package app

import (
	"context"
	"errors"
	"testing"

	sharedapperror "qn.expenditure/shared/apperror"
)

type testCommand struct {
	Name string `json:"name" validate:"required,min=3"`
}

type testResult string

type testHandler struct{}

func (testHandler) Handle(ctx context.Context, cmd testCommand) (testResult, error) {
	return testResult("ok"), nil
}

func TestValidatorRejectsInvalidCommand(t *testing.T) {
	validator := NewValidator[testCommand, testResult](testHandler{})

	_, err := validator.Handle(context.Background(), testCommand{})
	if err == nil {
		t.Fatal("expected validation error")
	}

	var validationErr *sharedapperror.ValidationError
	if !errors.As(err, &validationErr) {
		t.Fatalf("expected ValidationError, got %T", err)
	}
	if len(validationErr.Fields) == 0 {
		t.Fatal("expected validation fields to be populated")
	}
}

func TestValidatorDelegatesOnSuccess(t *testing.T) {
	validator := NewValidator[testCommand, testResult](testHandler{})

	result, err := validator.Handle(context.Background(), testCommand{Name: "abc"})
	if err != nil {
		t.Fatalf("unexpected error: %v", err)
	}
	if result != "ok" {
		t.Fatalf("expected ok result, got %q", result)
	}
}

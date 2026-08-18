package apperror

import (
	"errors"

	"github.com/go-playground/validator/v10"
)

type AppError struct {
	Code    int
	Message string
}

func (e *AppError) Error() string { return e.Message }

func NewBadRequest(msg string) *AppError    { return &AppError{Code: 400, Message: msg} }
func NewUnauthorized(msg string) *AppError  { return &AppError{Code: 401, Message: msg} }
func NewNotFound(msg string) *AppError      { return &AppError{Code: 404, Message: msg} }
func NewConflict(msg string) *AppError      { return &AppError{Code: 409, Message: msg} }

type ValidationErrors struct {
	Errors []FieldError `json:"errors"`
}

type FieldError struct {
	Name   string   `json:"name"`
	Errors []string `json:"errors"`
}

func (e *ValidationErrors) Error() string { return "validation failed" }

func NewValidationErrors(err validator.ValidationErrors) *ValidationErrors {
	grouped := make(map[string][]string)
	for _, fe := range err {
		grouped[fe.Field()] = append(grouped[fe.Field()], fe.Tag())
	}
	result := &ValidationErrors{}
	for name, errs := range grouped {
		result.Errors = append(result.Errors, FieldError{Name: name, Errors: errs})
	}
	return result
}

func AsValidationErrors(err error) (*ValidationErrors, bool) {
	var ve validator.ValidationErrors
	if errors.As(err, &ve) {
		return NewValidationErrors(ve), true
	}
	return nil, false
}

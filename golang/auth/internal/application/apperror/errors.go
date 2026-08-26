package apperror

import (
	"sort"

	ut "github.com/go-playground/universal-translator"
	"github.com/go-playground/validator/v10"
)

type AppError struct {
	Code    int
	Message string
}

// Error implements the error interface.
func (e *AppError) Error() string { return e.Message }

func NewBadRequest(msg string) *AppError   { return &AppError{Code: 400, Message: msg} }
func NewUnauthorized(msg string) *AppError { return &AppError{Code: 401, Message: msg} }
func NewNotFound(msg string) *AppError     { return &AppError{Code: 404, Message: msg} }
func NewConflict(msg string) *AppError     { return &AppError{Code: 409, Message: msg} }

type FieldError struct {
	Name   string   `json:"name"`
	Errors []string `json:"errors"`
}

type ValidationError struct {
	Fields []FieldError
}

// Error implements the error interface.
func (e *ValidationError) Error() string { return "validation failed" }

func NewValidationErrors(err validator.ValidationErrors, trans ut.Translator) *ValidationError {
	grouped := make(map[string][]string)
	for _, fe := range err {
		grouped[fe.Field()] = append(grouped[fe.Field()], fe.Translate(trans))
	}
	fields := make([]FieldError, 0, len(grouped))
	for name, errs := range grouped {
		fields = append(fields, FieldError{Name: name, Errors: errs})
	}
	sort.Slice(fields, func(i, j int) bool { return fields[i].Name < fields[j].Name })
	return &ValidationError{Fields: fields}
}

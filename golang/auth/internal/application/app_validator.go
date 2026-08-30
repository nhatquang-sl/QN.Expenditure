package application

import (
	"context"
	"errors"
	"log/slog"
	"reflect"
	"strings"

	"auth/internal/application/apperror"

	"github.com/go-playground/locales/en"
	ut "github.com/go-playground/universal-translator"
	v10 "github.com/go-playground/validator/v10"
	en_translations "github.com/go-playground/validator/v10/translations/en"
)

type Validator[C, R any] struct {
	inner    Handler[C, R]
	validate *v10.Validate
	trans    ut.Translator
}

func NewValidator[C, R any](inner Handler[C, R], configure ...func(*v10.Validate, ut.Translator)) *Validator[C, R] {
	enLocale := en.New()
	uni := ut.New(enLocale, enLocale)
	trans, _ := uni.GetTranslator("en")

	v := v10.New()
	v.RegisterTagNameFunc(func(fld reflect.StructField) string {
		name := strings.SplitN(fld.Tag.Get("json"), ",", 2)[0]
		if name == "-" {
			return ""
		}
		return name
	})
	en_translations.RegisterDefaultTranslations(v, trans)
	for _, fn := range configure {
		fn(v, trans)
	}
	return &Validator[C, R]{inner: inner, validate: v, trans: trans}
}

func (vl *Validator[C, R]) Handle(ctx context.Context, cmd C) (R, error) {
	cmdType := reflect.TypeOf(cmd).Name()
	if err := vl.validate.Struct(cmd); err != nil {
		var zero R
		var ve v10.ValidationErrors
		if errors.As(err, &ve) {
			slog.Default().WarnContext(ctx, "validation failed", slog.String("command", cmdType), slog.Any("errors", ve.Translate(vl.trans)))
			return zero, apperror.NewValidationErrors(ve, vl.trans)
		}
		return zero, err
	}
	slog.Default().InfoContext(ctx, "validation passed", slog.String("command", cmdType))
	return vl.inner.Handle(ctx, cmd)
}

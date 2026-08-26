package register

import (
	"unicode"

	"auth/internal/application"

	ut "github.com/go-playground/universal-translator"
	v10 "github.com/go-playground/validator/v10"
)

func newValidator(h handler) *application.Validator[Command, Result] {
	return application.NewValidator(&h, func(v *v10.Validate, trans ut.Translator) {
		v.RegisterValidation("password_strength", func(fl v10.FieldLevel) bool {
			p := fl.Field().String()
			if len(p) < 8 {
				return false
			}
			var hasUpper, hasLower, hasDigit bool
			for _, r := range p {
				switch {
				case unicode.IsUpper(r):
					hasUpper = true
				case unicode.IsLower(r):
					hasLower = true
				case unicode.IsDigit(r):
					hasDigit = true
				}
			}
			return hasUpper && hasLower && hasDigit
		})
		v.RegisterTranslation("password_strength", trans,
			func(ut ut.Translator) error {
				return ut.Add("password_strength", "{0} must be at least 8 characters with an uppercase letter, a lowercase letter, and a digit", true)
			},
			func(ut ut.Translator, fe v10.FieldError) string {
				t, _ := ut.T("password_strength", fe.Field())
				return t
			},
		)
	})
}

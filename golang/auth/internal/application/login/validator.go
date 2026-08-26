package login

import "auth/internal/application"

func newValidator(h handler) *application.Validator[Command, Result] {
	return application.NewValidator(&h)
}

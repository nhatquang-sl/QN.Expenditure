package login

import (
	. "qn.expenditure/shared/app"
)

func newValidator(h handler) *Validator[Command, Result] {
	return NewValidator(&h)
}

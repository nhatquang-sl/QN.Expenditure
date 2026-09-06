package shared

type EmailType string

const EmailTypeActivateAccount EmailType = "activate_account"

// ActivateAccountData holds the template variables for the activate_account email type.
type ActivateAccountData struct {
	FirstName  string
	ConfirmURL string
}

module auth_bot

go 1.27.0

replace qn.expenditure/shared => ../shared

require (
	github.com/stretchr/testify v1.12.1
	qn.expenditure/shared v0.0.0
)

require go.yaml.in/yaml/v3 v3.0.5 // indirect

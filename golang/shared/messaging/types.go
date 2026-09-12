package messaging

type Item struct {
	Id string `json:"id"`
	Quantity int	`json:"quantity"`
}

type Order struct {
	Id string `json:"id"`
	Items []Item `json:"items"`
}
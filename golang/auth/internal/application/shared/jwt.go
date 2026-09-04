package shared

import "time"

type TokenPair struct {
	AccessToken         string
	RefreshToken        string
	AccessTokenExpires  time.Time
	RefreshTokenExpires time.Time
}

type UserClaims struct {
	Id             string
	Email          string
	FirstName      string
	LastName       string
	EmailConfirmed bool
	TokenId        int64
	ExpiresAt      time.Time
}

type JwtService interface {
	GenerateTokens(user UserClaims, rememberMe bool) (TokenPair, error)
	ValidateAccessToken(token string) (*UserClaims, error)
	ValidateRefreshToken(token string) (*UserClaims, error)
}

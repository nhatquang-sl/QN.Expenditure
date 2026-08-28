package jwt

import (
	"fmt"
	"time"

	. "auth/internal/application/shared"
	. "auth/internal/config"

	. "github.com/golang-jwt/jwt/v5"
)

const (
	accessTokenExpiry      = 5 * time.Minute
	refreshTokenExpiry     = 5 * time.Hour
	refreshTokenRememberMe = 30 * 24 * time.Hour
)

type Service struct {
	cfg JwtConfig
}

func NewService(cfg JwtConfig) *Service {
	return &Service{cfg: cfg}
}

type authClaims struct {
	RegisteredClaims
	Id             string `json:"id"`
	Email          string `json:"email"`
	FirstName      string `json:"firstName"`
	LastName       string `json:"lastName"`
	EmailConfirmed bool   `json:"emailConfirmed"`
	Type           string `json:"type"`
}

func (s *Service) GenerateTokens(user UserClaims, rememberMe bool) (TokenPair, error) {
	now := time.Now().UTC()
	accessExpires := now.Add(accessTokenExpiry)

	refreshDuration := refreshTokenExpiry
	if rememberMe {
		refreshDuration = refreshTokenRememberMe
	}
	refreshExpires := now.Add(refreshDuration)

	tokenType := "LOGIN"
	if !user.EmailConfirmed {
		tokenType = "NEED_ACTIVATE"
	}

	accessToken, err := s.sign(user, s.cfg.AccessTokenSecretKey, accessExpires, refreshExpires.UnixMilli(), tokenType)
	if err != nil {
		return TokenPair{}, err
	}
	refreshToken, err := s.sign(user, s.cfg.RefreshTokenSecretKey, refreshExpires, 0, tokenType)
	if err != nil {
		return TokenPair{}, err
	}

	return TokenPair{
		AccessToken:         accessToken,
		RefreshToken:        refreshToken,
		AccessTokenExpires:  accessExpires,
		RefreshTokenExpires: refreshExpires,
	}, nil
}

func (s *Service) ValidateAccessToken(tokenStr string) (*UserClaims, error) {
	return s.validate(tokenStr, s.cfg.AccessTokenSecretKey)
}

func (s *Service) ValidateRefreshToken(tokenStr string) (*UserClaims, error) {
	return s.validate(tokenStr, s.cfg.RefreshTokenSecretKey)
}

func (s *Service) sign(user UserClaims, secret string, expires time.Time, rte int64, tokenType string) (string, error) {
	claims := authClaims{
		RegisteredClaims: RegisteredClaims{
			Issuer:    s.cfg.Issuer,
			Audience:  ClaimStrings{s.cfg.Audience},
			ExpiresAt: NewNumericDate(expires),
			IssuedAt:  NewNumericDate(time.Now().UTC()),
		},
		Id:             user.Id,
		Email:          user.Email,
		FirstName:      user.FirstName,
		LastName:       user.LastName,
		EmailConfirmed: user.EmailConfirmed,
		Type:           tokenType,
	}
	token := NewWithClaims(SigningMethodHS256, claims)
	return token.SignedString([]byte(secret))
}

func (s *Service) validate(tokenStr, secret string) (*UserClaims, error) {
	token, err := ParseWithClaims(tokenStr, &authClaims{}, func(t *Token) (any, error) {
		if _, ok := t.Method.(*SigningMethodHMAC); !ok {
			return nil, fmt.Errorf("unexpected signing method: %v", t.Header["alg"])
		}
		return []byte(secret), nil
	})
	if err != nil || !token.Valid {
		return nil, nil
	}
	c := token.Claims.(*authClaims)
	return &UserClaims{
		Id:             c.Id,
		Email:          c.Email,
		FirstName:      c.FirstName,
		LastName:       c.LastName,
		EmailConfirmed: c.EmailConfirmed,
	}, nil
}

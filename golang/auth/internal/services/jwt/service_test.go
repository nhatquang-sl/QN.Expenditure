package jwt

import (
	"testing"

	"auth/internal/application/shared"
	"auth/internal/config"

	"github.com/stretchr/testify/assert"
	"github.com/stretchr/testify/require"
)

var testService = NewService(config.JwtConfig{
	Issuer:                "test",
	Audience:              "test",
	AccessTokenSecretKey:  "test-access-secret",
	RefreshTokenSecretKey: "test-refresh-secret",
})

var baseUser = shared.UserClaims{
	Id:             "user-1",
	Email:          "test@example.com",
	FirstName:      "Test",
	LastName:       "User",
	EmailConfirmed: true,
	TokenId:        42,
}

func TestGenerateTokens(t *testing.T) {
	t.Run("RolesRoundTrip", func(t *testing.T) {
		user := baseUser
		user.Roles = []string{"user"}

		tokens, err := testService.GenerateTokens(user, false)
		require.NoError(t, err)

		claims, err := testService.ValidateAccessToken(tokens.AccessToken)
		require.NoError(t, err)
		require.NotNil(t, claims)
		assert.Equal(t, []string{"user"}, claims.Roles)
	})

	t.Run("EmptyRoles", func(t *testing.T) {
		user := baseUser
		user.Roles = []string{}

		tokens, err := testService.GenerateTokens(user, false)
		require.NoError(t, err)

		claims, err := testService.ValidateAccessToken(tokens.AccessToken)
		require.NoError(t, err)
		require.NotNil(t, claims)
		assert.Empty(t, claims.Roles)
	})

	t.Run("MultipleRoles", func(t *testing.T) {
		user := baseUser
		user.Roles = []string{"admin", "user"}

		tokens, err := testService.GenerateTokens(user, false)
		require.NoError(t, err)

		claims, err := testService.ValidateAccessToken(tokens.AccessToken)
		require.NoError(t, err)
		require.NotNil(t, claims)
		assert.Equal(t, []string{"admin", "user"}, claims.Roles)
	})

	t.Run("RolesInRefreshToken", func(t *testing.T) {
		user := baseUser
		user.Roles = []string{"admin"}

		tokens, err := testService.GenerateTokens(user, false)
		require.NoError(t, err)

		claims, err := testService.ValidateRefreshToken(tokens.RefreshToken)
		require.NoError(t, err)
		require.NotNil(t, claims)
		assert.Equal(t, []string{"admin"}, claims.Roles)
	})
}

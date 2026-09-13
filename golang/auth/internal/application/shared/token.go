package shared

import (
	"crypto/hmac"
	"crypto/sha256"
	"encoding/base64"
	"fmt"
	"strconv"
	"strings"
	"time"
)

// GenerateConfirmToken creates a stateless HMAC-SHA256 token with a 24-hour expiry.
// Format: base64url(payload) + "." + base64url(HMAC-SHA256(payload, secret))
// Payload: "<userID>:<expiry_unix>"
func GenerateConfirmToken(userID string, secret []byte) string {
	expiry := time.Now().UTC().Add(24 * time.Hour).Unix()
	payload := fmt.Sprintf("%s:%d", userID, expiry)
	payloadEnc := base64.RawURLEncoding.EncodeToString([]byte(payload))

	mac := hmac.New(sha256.New, secret)
	mac.Write([]byte(payloadEnc))
	sig := base64.RawURLEncoding.EncodeToString(mac.Sum(nil))

	return payloadEnc + "." + sig
}

// ParseConfirmToken verifies the token signature and expiry, returning the userID.
// Returns an error if the token is malformed, has an invalid signature, or has expired.
func ParseConfirmToken(token string, secret []byte) (string, error) {
	parts := strings.SplitN(token, ".", 2)
	if len(parts) != 2 {
		return "", fmt.Errorf("invalid token format")
	}
	payloadEnc, sig := parts[0], parts[1]

	mac := hmac.New(sha256.New, secret)
	mac.Write([]byte(payloadEnc))
	expectedSig := base64.RawURLEncoding.EncodeToString(mac.Sum(nil))
	if !hmac.Equal([]byte(sig), []byte(expectedSig)) {
		return "", fmt.Errorf("invalid token signature")
	}

	payloadBytes, err := base64.RawURLEncoding.DecodeString(payloadEnc)
	if err != nil {
		return "", fmt.Errorf("invalid token payload")
	}

	payload := string(payloadBytes)
	lastColon := strings.LastIndex(payload, ":")
	if lastColon == -1 {
		return "", fmt.Errorf("invalid token payload format")
	}

	userID := payload[:lastColon]
	expiry, err := strconv.ParseInt(payload[lastColon+1:], 10, 64)
	if err != nil {
		return "", fmt.Errorf("invalid token expiry")
	}

	if time.Now().UTC().Unix() > expiry {
		return "", fmt.Errorf("token expired")
	}

	return userID, nil
}

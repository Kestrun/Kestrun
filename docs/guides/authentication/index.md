---
title: Authentication
parent: Guides
nav_order: 80
has_children: true
---

# Authentication

Authentication-related guides: token-based auth (JWT), third-party login (OAuth 2.0 / OIDC), and mutual TLS (mTLS) client certificates.

## Available

| Guide | Summary |
| --- | --- |
| [Client Certificate Authentication](./clientcertificateauthentication) | Mutual TLS (mTLS) client certificate authentication. Covers TLS handshake vs auth, Kestrel client cert modes, and safe dev validation patterns. |
| [JWT Tokens](./jwt) | Issue and validate JWT bearer tokens, renew/refresh patterns, key material formats, and best practices. |
| [OAuth 2.0](./oauth2) | Provider sign-in / delegated authorization using Authorization Code + PKCE, cookie-backed sessions, scopes, and provider setup checklists. |
| [OpenID Connect (OIDC)](./oidc) | Identity-focused OAuth 2.0 using OIDC discovery, ID tokens, cookie sessions, PKCE, logout patterns, and provider checklists. |

---

Return to the [Guides index](/guides/).

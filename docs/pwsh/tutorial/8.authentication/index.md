---
title: Authentication
parent: Tutorials
nav_order: 8
---

# Authentication

Protect routes with multiple authentication schemes: Basic, API Key, JWT, Cookies, plus claims & policies.

> Prerequisites: review [Endpoints](../7.endpoints/index) & [Logging](../5.logging/1.Simple-Logging) for hosting & diagnostics.

> Conceptual background: see Guides — [OAuth 2.0](/guides/oauth2) and [OpenID Connect](/guides/oidc).

Chapters:

| #   | Chapter                                              | Focus                                     |
| --- | ---------------------------------------------------- | ----------------------------------------- |
| 1   | [Basic (PowerShell)](./1.Basic-PS)                   | Inline credential validation script block |
| 2   | [Basic (C# / VB.NET)](./2.Basic-MultiLang)           | Basic auth with multi-language handlers   |
| 3   | [API Key](./3.Api-Key)                               | Header key (fixed, script, C#/VB)         |
| 4   | [JWT Tokens](./4.Jwt)                                | Issue, renew, validate bearer tokens      |
| 5   | [Cookies](./5.Cookies)                               | Form login + cookie session auth          |
| 6   | [Windows Authentication](./6.Windows-Authentication) | Integrated Windows credentials            |
| 7   | [Claims & Policies](./7.Claims-Policies)             | Claim policy config + route enforcement   |
| 8   | [Multiple Schemes](./10.Multiple-Schemes)            | Combining schemes & route groups          |
| 9   | [OpenID Connect (Okta)](./9.OpenID-Connect-Okta)     | Okta OIDC + cookie session                |
| 10  | [GitHub Authentication](./11.GitHub-Authentication)  | GitHub OAuth + cookie policy              |
| 11  | [OpenID Connect (Duende Demo)](./12.OpenID-Connect)  | OIDC code flow (PKCE, tokens)             |
| 12  | [Full Demo](./13.Full-Demo)                          | Integrated multi-scheme server            |

---

Next: [Basic (PowerShell)](./1.Basic-PS)

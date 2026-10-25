# Trial licensing — how it works (boundary view)

Commercial 14-day trial. The licensing service itself is private; this document
describes the boundary so a customer knows what happens and what never leaves
this repo.

```
USER (structured email, template in REQUEST-TEMPLATE.md)
  │
  ▼
EMAIL PROVIDER (normal mail transport)
  │
  ▼
PRIVATE LICENSING SERVICE
  ├── request validation (template fields present, sane values)
  ├── normalized-email check (case/whitespace/dot-normalized identity)
  ├── one-trial enforcement (one trial per normalized identity)
  ├── trial duration (14 days, recorded in the license)
  ├── license signing (private key, never in this repo)
  └── response email (license file + install notes)
```

Notes:

- MCP may orchestrate the private workflow (queue, checks, notifications).
  MCP is not the email transport itself.
- Never published: signing keys, production issuer identity, license database,
  issued licenses, SMTP credentials, API tokens, internal MCP/Coolify config,
  infrastructure credentials, customer/trial records.
- Only disposable test licensing is used in any public release workflow — never
  production signing keys.

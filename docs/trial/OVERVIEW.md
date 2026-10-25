# Trial licensing — how it works (boundary view)

Commercial 14-day trial. The licensing service itself is private; this document
describes the boundary so a customer knows what happens and what never leaves
this repo.

## Request paths (all open, all manual-send)

- **Installer wizard** (`packaging/windows/razorzip-setup.iss`, trial page —
  source reviewed, installer itself not yet compiled): Request Trial by Email /
  Copy Request Template. Skipped in silent installs.
- **Harness GUI** (Trial tab buttons — wired; interactive click-through not
  yet test-executed): Request 14-Day Trial / Copy Request Template.
- **CLI**: `RazorBenchmark --request-trial` (graphical) with printed fallback
  on headless systems; `--copy-trial-template` prints the template.
- **Linux desktop**: `razorzip-trial.desktop` opens the registered handler
  with the same prefilled message.
- **Manual**: copy `docs/trial/trial-request-template.txt` into an email to
  `razor@mobisec.cloud`.

Every path uses the same template (single source of truth:
`docs/trial/trial-request-template.txt`, also installed as
`trial-request-template.txt` next to the binaries). What happens on click:
your email app opens with To/Subject/body prefilled — you review and press
Send yourself. The app/installer cannot confirm sending and never claims it.
No SMTP credentials, API keys, accounts, or private endpoints are involved.

```
Intended private fulfilment (DESIGN, not an existing service — fulfilment is
currently manual review; no automatic issuance, mailbox polling, or delivery
automation exists in this release):

USER (structured email, template in REQUEST-TEMPLATE.md)
  │
  ▼
EMAIL PROVIDER (normal mail transport)
  │
  ▼
PRIVATE LICENSING SERVICE (intended)
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

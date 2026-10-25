# artifacts/hardened — commercial binaries (allow-list gated)

**This release intentionally contains NO commercial binaries.**

Release policy (§14): binaries are published ONLY when named in an
operator-provided allow-list (`approved-binaries.txt`) identifying the exact
approved artifacts. No such allow-list was provided at staging time, so the gate
held and nothing was copied here.

When an allow-list exists, each listed artifact ships in this directory with:

- the approved binary itself,
- a detached `.integrity` sidecar,
- recorded SHA-256, version, and build date (see ` integrity` record),
- smoke-test and tamper-negative results in the release report.

Until then: the harness works fully with third-party engines and any locally
installed Razor binary you configure — it never requires a vendored binary.

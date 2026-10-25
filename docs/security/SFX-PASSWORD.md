# SFX / password & online gating (where shipped)

## Offline password mode

- Data encryption: **AES-256-GCM**.
- Key derivation: **Argon2id** (parameters recorded by the shipping artifact).
- Wrong password or corrupted payload → authentication-tag failure → no output,
  controlled error. There is no "partial decrypt".

## Online authorization mode

Where the shipped implementation validates token structure/claims locally and
then contacts an authorization endpoint, this repository describes it as:

> Policy-controlled token-gated extraction with optional online authorization.

It is **not** described as local cryptographic JWT signature verification unless
the shipping implementation actually performs signature verification locally.

## Trial artifacts

Trial licenses are data files consumed by the commercial binary. Request format
and workflow are documented in `docs/trial/`; signing, issuance, and enforcement
live in the private licensing service and are never in this repo.

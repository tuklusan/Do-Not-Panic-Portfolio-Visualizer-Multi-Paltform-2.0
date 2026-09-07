<!--
============================================================================
Copyright (c) 2026 Supratim Sanyal of SANYALnet Labs.
Proprietary rights reserved except as expressly licensed herein.

DO NOT PANIC PORTFOLIO VISUALIZER
This file is governed by the SANYALnet Labs Non-Commercial License in the
root LICENSE file. Non-Commercial use is permitted; Commercial Use and use
for AI/ML model training are prohibited unless separately authorized.

Attribution is required: "Based on original work by Supratim Sanyal of
SANYALnet Labs." See LICENSE for full terms, warranty disclaimer, termination,
patent, trademark, and governing-law provisions.
============================================================================
-->

# VERIFIED NVIDIA NEMOTRON EXTERNAL REVIEW GATE WORKFLOW

Status: IMPLEMENTATION SPECIFICATION
Verification date: 2026-09-07
Encoding: ASCII / UTF-8 compatible

## 1. Purpose

This document defines a portable external code-review gate that preserves the
semantic, evidentiary, verdict, and fail-closed behavior of the Warajevo ZX
Spectrum Next evidence-bound review gate while changing the NVIDIA NIM model
policy to:

- PRIMARY: `nvidia/nemotron-3-super-120b-a12b`
- FALLBACK: `nvidia/nemotron-3.5-lightning-30b-a3b`

The primary model is the normal reviewer. The fallback is an authorized
availability fallback only. It MUST NOT be used to seek a different semantic
verdict after the primary has produced a valid semantic result. This workflow
also contains an explicit, reviewed transport-timing override for hosted NIM
availability; that override is isolated in sections 3.4, 6, 7.3, and 9.

The gate is fail-closed. A test, remote execution, hosted execution, or other
protected validation stage MUST NOT start until the exact immutable candidate
has a complete CODE `PASS` receipt.

The objective is correctness and evidentiary integrity, not API-call economy.

## 2. Verified reference baseline

The behavioral reference for this workflow is the live Warajevo repository at
the following immutable revision:

- Repository: `https://github.com/tuklusan/warajevo-zx-spectrum-next`
- Branch inspected: `main`
- Commit: `0d29d8c8d5391f7c82df1237d88686ea5bb846fa`
- Commit message: `Link fullscreen boundary test implementation`
- Reference gate: `tools/reviewer/review_gate.py`
- Reference gate Git blob SHA: `686cea7085fd450cd97bb64eff8387a2a4bd7536`
- Reference gate size: 100496 bytes
- Reference authority document: `design/review-gate.md`
- Reference authority-document Git blob SHA: `a035b5fa109c0f847d413d0a7663e03c3789a499`

Semantic/evidentiary behavioral parity is taken from the executable
`review_gate.py` at the pinned blob above. The design document is supporting
authority, but it is not allowed to override observed executable behavior when
the two differ. This workflow now contains two explicit reviewed protocol
divergences from that executable baseline: the hosted-NIM availability timing
defined in sections 3.4, 6, 7.3, and 9, and the NVIDIA-documented reasoning
policy defined in sections 2.1, 3.3, 6, 7.2, 18.3, 19, 22, 24, 29, and 29A.

### 2.1 Verified implementation/documentation discrepancy and reviewed reasoning revision

The pinned `design/review-gate.md` says normal falsification enables thinking
and uses a reasoning budget. The pinned executable gate actually invokes:

- discovery with thinking disabled;
- cross-unit integration discovery with thinking disabled;
- falsification with thinking disabled;
- compact single-candidate falsification with thinking disabled; and
- adjudication with thinking disabled.

Earlier revisions of this portable workflow intentionally followed that
executable behavior. This revision deliberately changes it. Accuracy is the
primary objective, and both authorized NVIDIA models now document supported
reasoning controls. Therefore all normal semantic review calls use bounded
reasoning, including discovery, cross-unit integration discovery,
falsification, compact falsification retry, and adjudication. The bootstrap
maintenance audit uses the same bounded reasoning policy.

This is an explicit reviewed semantic-protocol revision, not an accidental
parity drift. The immutable-evidence rules, candidate filtering, hostile
falsification contract, host-side verdict synthesis, and fail-closed behavior
remain unchanged. Reasoning output is never authority and MUST NOT be logged,
stored in receipts, or substituted for the required structured JSON result.

## 3. Verified NVIDIA model facts

As verified against NVIDIA's public NIM model pages and API references on
2026-09-07:

### 3.1 Primary

`nvidia/nemotron-3-super-120b-a12b`

- Hosted NIM model identifier exists.
- OpenAI-compatible chat-completions endpoint:
  `https://integrate.api.nvidia.com/v1/chat/completions`
- Published context length: up to 1M tokens.
- Published `max_tokens` range: 1 through 32768.
- Published default temperature: 1.
- Published default top_p: 0.95.
- Reasoning can be configured; the hosted API documents
  `reasoning_effort` values `none`, `low`, and `high`, with `high` as the
  default, plus `reasoning_budget` from -1 through 32768 tokens.
- NVIDIA says `reasoning_effort="high"` enables full reasoning and that an
  explicit reasoning budget is typically most useful with high effort.

NVIDIA sources:

- `https://build.nvidia.com/nvidia/nemotron-3-super-120b-a12b`
- `https://docs.api.nvidia.com/nim/reference/nvidia-nemotron-3-super-120b-a12b-infer`

### 3.2 Fallback

`nvidia/nemotron-3.5-lightning-30b-a3b`

- Hosted NIM model identifier exists.
- OpenAI-compatible chat-completions endpoint:
  `https://integrate.api.nvidia.com/v1/chat/completions`
- Published context length: up to 1M tokens.
- Published `max_tokens` range: 1 through 32768.
- Published default temperature: 1.
- Published default top_p: 0.95.
- The hosted API documents `reasoning_budget` from -1 through 32768 tokens.
- NVIDIA's hosted Build prototype explicitly enables reasoning with
  `chat_template_kwargs.enable_thinking=true` and sends
  `reasoning_budget=16384`.

NVIDIA sources:

- `https://build.nvidia.com/nvidia/nemotron-3.5-lightning-30b-a3b`
- `https://docs.api.nvidia.com/nim/reference/nvidia-nemotron-3-5-lightning-30b-a3b-infer`

### 3.3 Consequence for this gate: bounded high/full reasoning with JSON reserve

Both authorized hosted models allow up to 32768 generated tokens per call.
NVIDIA documents a 16384-token default reasoning budget for both models. For
Super, the hosted API explicitly supports `reasoning_effort="high"`; for
Lightning, the hosted Build prototype explicitly enables thinking with
`chat_template_kwargs.enable_thinking=true` and `reasoning_budget=16384`.

NVIDIA's Nemotron 3.5 Lightning guide also states that `max_tokens` covers the
combined reasoning trace plus final answer: if reasoning consumes the allowance
first, the call can end with `finish_reason="length"` and no final answer. For
structured JSON, NVIDIA recommends either disabling reasoning or raising
`max_tokens` enough to leave room for both reasoning and the JSON result. This
workflow intentionally chooses the latter because review accuracy is more
important than latency.

Accordingly, preserve the reference gate's visible structured-result allowances
as FINAL-JSON RESERVES, add a bounded 16384-token reasoning allowance, and set
per-phase `max_tokens` to their sum:

```text
phase                     reasoning cap   final JSON reserve   max_tokens
DISCOVERY / INTEGRATION       16384             8192              24576
FALSIFICATION                 16384            12288              28672
COMPACT FALSIFICATION         16384             4096              20480
ADJUDICATION                  16384            12288              28672
BOOTSTRAP                     16384             8192              24576
```

All totals remain below the hosted 32768-token maximum. The reasoning budget is
a cap, not evidence authority. Only the final schema-valid JSON in
`choices[0].message.content` is parsed by the gate. Any separate reasoning field
returned by the service is ignored and MUST NOT be persisted.

Both models publish a hosted context limit up to 1M tokens, so the reference
gate's much smaller input byte budgets remain conservative and unchanged.
Section 3.4 documents the separate availability-timing override.

### 3.4 Timing-policy evidence and deliberate transport override

The pinned reference gate used one universal 180-second request timeout and the
retry delays `(1, 2, 4, 8, 16, 32)` for all retryable infrastructure failures.
Those transport timings are not semantic-evidence authority. This workflow
intentionally replaces only that availability timing policy while preserving
the reference gate's immutable-evidence, review, verdict, and fail-closed
semantics.

Current NVIDIA documentation provides the following useful operational anchors:

- NeMo Curator documents `max_retries=3`, `base_delay=1.0`, and a 120-second
  request timeout as an example for NVIDIA API endpoints. It says 429 handling
  uses automatic backoff with jitter and recommends increasing the timeout to
  300 seconds for slow networks or high-latency endpoints.
- NeMo Platform documents short exponential retry behavior for connection
  errors, 408, 429, and >=500 responses rather than a long universal retry
  ladder.

NVIDIA sources:

- `https://docs.nvidia.com/nemo/curator/v26.02/curate-text/synthetic/llm-client`
- `https://docs.nvidia.com/nemo-platform/documentation/reference/python-sdk`

Recent NVIDIA Developer Forum reports are operational evidence, not API
contracts, but they materially inform hosted-NIM availability policy:

- a Nemotron 3 Super 120B user reported continuous 429 responses for more than
  three days while another model on the same account/key continued working;
- low-request-rate users have reported near-immediate repeated 429 responses;
- multiple users have reported hosted chat-completions 404 `Function ... not
  found for account` failures even when `/v1/models` still listed the model or
  the same model worked elsewhere.

Operational-evidence sources:

- `https://forums.developer.nvidia.com/t/nemotron-3-super-nemotron-3-super-120b-a12b-returning-429-continuously-for-3-days-new-api-key-didnt-help/377670`
- `https://forums.developer.nvidia.com/t/api-key-rate-limited-429-on-every-request-despite-low-rpm-account-level-issue/377111`
- `https://forums.developer.nvidia.com/t/request-to-enable-public-api-endpoints-for-my-account-404-function-not-found/376712`
- `https://forums.developer.nvidia.com/t/404-function-not-found-for-account-when-calling-aisingapore-sea-lion-7b-instruct-via-integrate-api-nvidia-com/361577`

Consequently, this workflow gives a real inference request a generous
300-second completion window but gives explicit availability failures much
shorter class-specific recovery windows. HTTP 404 is intentionally treated as
a hosted-route/model-availability condition for one quick retry only. A
persistent 404 is not retried indefinitely and is never treated as semantic
review evidence.

## 4. Non-negotiable gate invariants

An implementation conforms to this workflow only if all of the following are
true.

1. CODE review occurs before protected tests or protected execution.
2. The candidate is committed and the working tree is clean before CODE review.
3. The reviewed `head` is exactly current `HEAD`.
4. The selected `base` is an ancestor of `head`.
5. A CODE attempt invalidates any older CODE PASS receipt before review begins.
6. The review packet is constructed from immutable committed Git objects, not
   mutable working-tree copies.
7. Every changed item is represented in a deterministic NUL-safe change
   manifest.
8. Complete text content is supplied for changed text files, subject only to
   the deterministic bounded sharding protocol below.
9. Binary, image, symlink, and gitlink data is never hallucinated into text
   semantics.
10. Exact requirement sources, their bytes, and their SHA-256 identities are
    authority. Model summaries are not authority.
11. A candidate is only an allegation. It cannot block execution until the
    independent falsification path positively confirms a serious defect.
12. A genuine issue below HIGH is `NON_BLOCKING`; it is not mislabeled
    `REJECTED` merely to make the ledger tidy.
13. Missing material evidence is `UNRESOLVED`/`INCONCLUSIVE`; it is never
    silently promoted to HIGH and never silently ignored.
14. Authoritative-source conflict produces `HUMAN_DECISION_REQUIRED`.
15. Malformed output, transport exhaustion, deadline exhaustion, missing
    mandatory evidence, source mutation, incomplete mandatory passes, and
    irreducible truncation all fail closed.
16. The model cannot add or remove gate authority. Python/host code validates
    schemas, exact quotes, paths, hashes, candidate IDs, and final blocker sets.
17. Correcting a confirmed defect creates a new immutable candidate and starts
    a complete fresh CODE review. There is no arbitrary maximum number of
    correction rounds.
18. The exact reviewed candidate is revalidated immediately before a CODE PASS
    receipt is written.
19. Only `PASS` plus `review_complete=true` may authorize protected testing.
20. The primary/fallback mechanism MUST NOT permit semantic verdict shopping.

## 5. Required workflow order

The project workflow MUST be:

1. Define the active change request and exact authoritative requirement sources.
2. Implement the change without running protected target-system tests.
3. Commit the candidate.
4. Require a clean working tree.
5. Invalidate any stale CODE PASS receipt.
6. Build an immutable CODE review packet from `base..head`.
7. Run the complete external CODE review described in this document.
8. If the verdict is `FAIL`, correct every confirmed BLOCKER/HIGH issue, commit
   a new candidate, and return to step 4.
9. If the verdict is `INCONCLUSIVE`, `REVIEW_UNAVAILABLE`, or
   `HUMAN_DECISION_REQUIRED`, do not test. Resolve the cause and rerun the
   complete CODE review on an immutable candidate.
10. Only after CODE `PASS`, run any required DOCUMENTATION review.
11. Publish the exact reviewed commit to the project's authoritative remote if
    remote-test policy requires publication first.
12. Verify that the PASS receipt still names the exact candidate, requirements,
    scope, and packet identity.
13. Run the approved protected tests.
14. Collect immutable test artifacts.
15. Run `TEST_ARTIFACT` review where required.
16. If test-artifact analysis implies a code correction, invalidate the CODE
    PASS and return to step 2.

No test result can retroactively authorize code that never passed CODE review.

## 6. Reference constants and reviewed availability-timing overrides

Preserve the immutable-evidence and input-budget constants from the pinned
reference gate unless a later reviewed protocol revision deliberately changes
them. This revision contains two explicit constant-level overrides:

- the reasoning/generation constants implement the reviewed reasoning policy in
  sections 2.1 and 3.3 while preserving the reference gate's original final-JSON
  allowances; and
- the transport-availability constants implement section 3.4 and supersede the
  reference gate's 180-second request timeout and universal
  `(1, 2, 4, 8, 16, 32)` retry ladder.

```python
API_URL = "https://integrate.api.nvidia.com/v1/chat/completions"
PRIMARY_MODEL = "nvidia/nemotron-3-super-120b-a12b"
FALLBACK_MODEL = "nvidia/nemotron-3.5-lightning-30b-a3b"
KEY_NAME = "NVIDIA_API_KEY_CODING"
PROTOCOL_VERSION = 2

MAX_CONTEXT_TOKENS = 1_000_000
INPUT_BUDGET_BYTES = 520_000
TARGET_UNIT_BYTES = 340_000
MIN_UNIT_BYTES = 64_000
PROTOCOL_OVERHEAD_BYTES = 16_384
SAFETY_MARGIN_BYTES = 32_768
FALSIFICATION_CONTEXT_BYTES = 48_000
CONTEXT_BYTES = 340_000

REASONING_BUDGET_TOKENS = 16_384

DISCOVERY_FINAL_JSON_RESERVE_TOKENS = 8_192
FALSIFICATION_FINAL_JSON_RESERVE_TOKENS = 12_288
COMPACT_FALSIFICATION_FINAL_JSON_RESERVE_TOKENS = 4_096
ADJUDICATION_FINAL_JSON_RESERVE_TOKENS = 12_288
BOOTSTRAP_FINAL_JSON_RESERVE_TOKENS = 8_192

DISCOVERY_MAX_TOKENS = 24_576
FALSIFICATION_MAX_TOKENS = 28_672
COMPACT_FALSIFICATION_MAX_TOKENS = 20_480
ADJUDICATION_MAX_TOKENS = 28_672
BOOTSTRAP_MAX_TOKENS = 24_576

DEFAULT_REVIEW_DEADLINE_SECONDS = 3_600.0
STALE_LOCK_SECONDS = 900.0

CONNECT_TIMEOUT_SECONDS = 10
REQUEST_TIMEOUT_SECONDS = 300
MODEL_429_RECOVERY_BUDGET_SECONDS = 90
MAX_INFRASTRUCTURE_ATTEMPTS_PER_MODEL = 4

RETRY_DELAY_404_SECONDS = 2
RETRY_DELAYS_429 = (5, 15, 45)
RETRY_DELAYS_502_503 = (2, 8)
RETRY_DELAYS_RESOURCE = (2, 8)
RETRY_DELAY_500_SECONDS = 5
RETRY_DELAY_408_504_SECONDS = 5
RETRY_DELAYS_TRANSPORT = (1, 4)
RETRYABLE_HTTP_STATUS = {404, 408, 429, 500, 502, 503, 504}

MAX_CONTEXT_CYCLES = 2
MAX_NEW_CANDIDATE_CYCLES = 1
```

The overall review deadline is shared across retries and model failover. A
fallback transition MUST NOT reset the 3600-second deadline.

## 7. Model adapter and fallback policy

### 7.1 Authorized model set

The only normal-gate model identifiers are:

```python
PRIMARY_MODEL = "nvidia/nemotron-3-super-120b-a12b"
FALLBACK_MODEL = "nvidia/nemotron-3.5-lightning-30b-a3b"
```

No alias, abbreviated ID, dynamically selected model, or third model may be
accepted silently.

### 7.2 Model-specific reasoning request payloads

Every normal semantic phase uses bounded reasoning, structured JSON output, and
the phase-specific total generation allowance from section 6. The two hosted
models do NOT use an identical reasoning-control surface, so the adapter MUST
construct model-specific payloads.

#### 7.2.1 Primary: Nemotron 3 Super

For `nvidia/nemotron-3-super-120b-a12b`, use the hosted API's explicit full
reasoning controls:

```python
payload = {
    "model": PRIMARY_MODEL,
    "messages": [
        {"role": "system", "content": system_prompt},
        {"role": "user", "content": user_prompt},
    ],
    "stream": False,
    "response_format": {"type": "json_object"},
    "temperature": 1.0,
    "top_p": 0.95,
    "max_tokens": phase_max_tokens,
    "reasoning_effort": "high",
    "reasoning_budget": REASONING_BUDGET_TOKENS,
}
```

Do not also add `chat_template_kwargs.reasoning_budget` for Super. NVIDIA states
that the chat-template value takes precedence when both are supplied; sending
both would create two authorities for the same control. Likewise, do not rely
on the model's default high effort: send `reasoning_effort="high"` explicitly
so the reviewed protocol is auditable.

#### 7.2.2 Fallback: Nemotron 3.5 Lightning

For `nvidia/nemotron-3.5-lightning-30b-a3b`, do NOT send `reasoning_effort`;
its hosted API reference does not document that field. Use NVIDIA's hosted Build
prototype contract instead:

```python
payload = {
    "model": FALLBACK_MODEL,
    "messages": [
        {"role": "system", "content": system_prompt},
        {"role": "user", "content": user_prompt},
    ],
    "stream": False,
    "response_format": {"type": "json_object"},
    "temperature": 1.0,
    "top_p": 0.95,
    "max_tokens": phase_max_tokens,
    "reasoning_budget": REASONING_BUDGET_TOKENS,
    "chat_template_kwargs": {"enable_thinking": True},
}
```

The hosted Build prototype currently demonstrates exactly
`enable_thinking=true` plus `reasoning_budget=16384` for Lightning. The separate
self-hosted NIM guide also documents a `thinking_token_budget` control, but this
workflow targets NVIDIA's hosted `integrate.api.nvidia.com` endpoint and MUST
use the hosted API/Build contract above unless a later reviewed protocol
revision changes providers.

#### 7.2.3 Structured JSON and reasoning separation

`response_format={"type":"json_object"}` remains mandatory. NVIDIA's Lightning
guide explicitly supports JSON mode and warns that reasoning and final content
share the generation allowance. This workflow therefore reserves the original
visible JSON budgets by adding the 16384-token reasoning cap to `max_tokens`.

A successful response MUST still satisfy `finish_reason == "stop"` and contain
non-empty schema-valid JSON in `choices[0].message.content`. If NVIDIA returns a
separate reasoning field such as `reasoning`, `reasoning_content`, or an
equivalent provider field, host code MUST ignore it for verdict purposes and
MUST NOT log, persist, or copy it into receipts or findings. If reasoning leaks
into `message.content` and makes the required JSON invalid, use the existing
bounded schema-repair discipline; do not scrape or reinterpret chain-of-thought
as evidence.

Pin `temperature=1.0` and `top_p=0.95` explicitly on both authorized models.
NVIDIA's Super model card recommends those values across reasoning and general
tasks, and NVIDIA's hosted Lightning prototype uses the same pair. They also
match the hosted API defaults, so this makes the reviewed protocol explicit
rather than relying on a mutable service default. Any later sampling change
requires a separate reviewed protocol revision.

NVIDIA sources for this reasoning contract:

- `https://docs.api.nvidia.com/nim/reference/nvidia-nemotron-3-super-120b-a12b-infer`
- `https://docs.api.nvidia.com/nim/reference/nvidia-nemotron-3-super-120b-a12b`
- `https://build.nvidia.com/nvidia/nemotron-3.5-lightning-30b-a3b`
- `https://docs.api.nvidia.com/nim/reference/nvidia-nemotron-3-5-lightning-30b-a3b-infer`
- `https://docs.nvidia.com/nim/large-language-models/2.0.10/get-started/advanced/get-started-nemotron-3.5-lightning.html`

### 7.3 Availability-only failover

Fallback is permitted only when the active primary has not produced a valid
semantic response for the current logical API call and the primary has
exhausted the applicable bounded infrastructure-recovery path below.

Infrastructure failures eligible for failover are:

- HTTP status 404, 408, 429, 500, 502, 503, or 504;
- URL/transport failure, including connection reset, DNS/TLS transport failure,
  or remote disconnect;
- connection timeout;
- hard request/inference timeout; or
- `finish_reason == "insufficient_system_resource"`, which the reference gate
  treats as resource unavailability.

Every retry repeats the exact same immutable system prompt, user prompt, phase,
output budget, and review deadline. Retry/failover may not alter evidence or
semantic instructions.

The timing policy is failure-class-specific. Do not replace it with one generic
retry loop.

#### 7.3.1 HTTP 404: hosted route/model not found

A 404 receives exactly one quick same-model retry after 2 seconds. If that retry
also returns 404, fail over the current logical call immediately.

This exception is intentional because hosted NIM has produced real-world 404
`Function ... not found for account` failures for models that are otherwise
listed/available. However, a persistent 404 can also indicate entitlement or
configuration failure, so repeated long backoff is not justified. If both
authorized models exhaust their 404 policy, return `REVIEW_UNAVAILABLE` and
surface the sanitized 404 class for operator action.

#### 7.3.2 HTTP 429: rate limiting

When a valid `Retry-After` header is present, honor it only when its delay fits
inside both:

- the remaining `MODEL_429_RECOVERY_BUDGET_SECONDS`; and
- the remaining overall review deadline.

Support normal HTTP `Retry-After` delta-seconds and HTTP-date forms. If the
header is malformed, treat it as absent. If the requested wait exceeds either
remaining budget, do not sleep to the limit; fail over immediately.

When `Retry-After` is absent, retry after 5, 15, and 45 seconds. There are at
most four same-model attempts total for that logical call: the initial attempt
plus those three retries.

The 429 recovery window is capped at 90 seconds from receipt of the first 429.
A later 429 that arrives after the budget is exhausted triggers failover
immediately. If a retry receives normal success rather than another 429, it has
left rate-limit recovery and the ordinary request completion rules apply.

#### 7.3.3 HTTP 502/503 and resource exhaustion

For 502 or 503, retry after 2 seconds and then 8 seconds. If the condition
persists, fail over.

Treat `finish_reason == "insufficient_system_resource"` with the same 2-second
and 8-second recovery schedule, then fail over.

#### 7.3.4 HTTP 500

Retry once after 5 seconds. A second 500 fails over immediately.

#### 7.3.5 HTTP 408 or 504

Retry once after 5 seconds. A second 408/504 fails over immediately. These
statuses already represent upstream timeout behavior, so a long retry ladder is
not useful.

#### 7.3.6 Transport/connectivity failures

Connection reset, DNS/TLS transport failure, remote disconnect, and equivalent
retryable transport failures use delays of 1 second and then 4 seconds. If the
transport path still fails, fail over.

Connection establishment itself is bounded by 10 seconds, subject to any
smaller remaining overall review deadline.

#### 7.3.7 Hard request/inference timeout

A genuine semantic inference call may consume up to 300 seconds total from
request dispatch, subject to the smaller remaining overall review deadline.
The 10-second connection ceiling is a sub-limit of that 300-second request
window, not an additional 10 seconds.

If a request consumes the full 300-second request window without a valid
response, do not repeat another 300-second same-model attempt. Fail over
immediately. This deliberately gives the stronger primary a generous chance to
finish real work while preventing repeated multi-minute stalls.

#### 7.3.8 Mixed infrastructure failures and global attempt bound

Infrastructure failure classes may change between retries. Apply the policy for
the newly observed class, but never exceed
`MAX_INFRASTRUCTURE_ATTEMPTS_PER_MODEL = 4` attempts for one identical logical
API call on one model. Reaching that cap without a valid semantic response
causes primary-to-fallback transition, or `REVIEW_UNAVAILABLE` when already on
the fallback.

Schema-repair prompts are distinct logical API calls for this purpose. A
schema-invalid semantic response does not itself trigger failover; if a later
schema-repair API call independently encounters an eligible infrastructure
failure, that repair call uses the same availability policy.

#### 7.3.9 Fallback exhaustion

The fallback model uses the same class-specific infrastructure timing rules.
There is no third model. If the fallback exhausts the applicable policy or the
overall review deadline, return `REVIEW_UNAVAILABLE` and issue no CODE PASS
receipt.

The overall 3600-second review deadline spans primary retries, failover,
fallback retries, schema repairs, and all semantic phases. Model transition
never resets it.

### 7.4 Failover latch and primary restoration

After the first successful availability failover in a review attempt, latch the
active model to `FALLBACK_MODEL` for the remainder of that immutable review
attempt. Do not probe or oscillate back to the primary inside the same review.

Previously completed primary calls remain evidence for the same immutable
packet. The logical call that failed over had no accepted primary semantic
output, so there is no competing verdict to choose between.

A new complete review attempt always starts with `PRIMARY_MODEL` again. This is
the only normal failback mechanism. The fallback is therefore temporary at the
review-attempt boundary, not a persistent project/model preference.

### 7.5 Failover MUST NOT occur for semantic outcomes

Do not invoke the fallback merely because the primary produced:

- a valid `CONFIRMED` decision;
- a valid `REJECTED` decision;
- a valid `NON_BLOCKING` decision;
- a valid `UNRESOLVED` decision;
- authoritative-source conflict;
- a valid `FAIL` verdict;
- output truncation;
- schema-invalid semantic output after the reference repair discipline;
- a material missing-context result; or
- any other valid but inconvenient review result.

A valid semantic result is final for that stage under the gate rules. Asking a
second model because the first answer is unwelcome is verdict shopping and
invalidates the gate.

### 7.6 Permanent failures

A non-retryable HTTP response is a configuration or permanent failure. In
particular, authentication, authorization, malformed request, and
billing/configuration failures MUST NOT trigger model fallback. They produce
`REVIEW_UNAVAILABLE` or the corresponding fail-closed result.

HTTP 404 is the deliberate exception to the usual "Not Found is permanent"
assumption because hosted NIM model/function routing has produced transient or
model-specific 404s in practice. It receives only the bounded one-retry policy
in section 7.3.1. Persistent 404 across both authorized models is still a
fail-closed availability/configuration problem, not permission to keep cycling
or to weaken the request.

### 7.7 Telemetry additions required by two-model operation

Every API call record MUST additionally contain:

```json
{
  "model": "exact-model-id",
  "model_role": "PRIMARY|FALLBACK",
  "failover_generation": 0,
  "failover_reason": null
}
```

After failover, `failover_generation` is 1 and `failover_reason` contains only a
sanitized failure class/status. Never store API keys, Authorization headers,
prompts, source text, or hidden reasoning in telemetry.

The final review telemetry MUST contain:

```json
{
  "primary_model": "nvidia/nemotron-3-super-120b-a12b",
  "fallback_model": "nvidia/nemotron-3.5-lightning-30b-a3b",
  "fallback_used": false,
  "final_active_model": "nvidia/nemotron-3-super-120b-a12b"
}
```

If fallback is used, set the last two fields accordingly.

## 8. API response discipline

For every call:

1. Apply the failure-class-specific transport/status policy in section 7.3.
2. On 429, parse and bound `Retry-After` before sleeping.
3. Require HTTP/transport success before semantic parsing.
4. Parse the outer NIM response as JSON.
5. Require `choices[0]`.
6. Treat `finish_reason == "insufficient_system_resource"` as retryable
   infrastructure unavailability under section 7.3.3.
7. Treat `finish_reason == "length"` as truncation, not success.
8. Require `finish_reason == "stop"` for normal success.
9. Require non-empty `choices[0].message.content`.
10. Parse the message content as JSON.
11. Apply the phase-specific host-side schema validator.
12. Never infer a missing field from prose.

The reference `request_validated` behavior allows up to two fresh schema-repair
prompts after the initial schema-invalid structured response. Preserve that
bounded repair behavior. A third schema-invalid result fails closed.

Schema invalidity itself is not model failover. An independently occurring
eligible infrastructure failure while transmitting a schema-repair prompt may
use the availability policy because that is an infrastructure failure, not a
second opinion on the invalid semantic response.

## 9. Deadline and timeout enforcement

The default overall review deadline is 3600 seconds.

For a normal semantic HTTP request:

- total request wall-clock time from dispatch is bounded by the smaller of
  300 seconds or the remaining review deadline;
- connection establishment is bounded by the smaller of 10 seconds, the
  remaining request window, or the remaining review deadline; and
- a full 300-second request timeout causes immediate primary-to-fallback
  transition under section 7.3.7, or `REVIEW_UNAVAILABLE` when already on the
  fallback, rather than another same-model 300-second retry.

Rate-limit waits are additionally bounded by the 90-second 429 recovery budget.
Every retry sleep is clipped by the remaining overall review deadline; if the
required retry cannot legally begin inside its applicable budget, transition
immediately instead of sleeping past the budget.

The reference implementation enforces the overall deadline outside the HTTP
socket operation by executing the transport in a separate process and
terminating that worker if the remaining review deadline expires. Preserve
equivalent behavior. A stuck socket cannot extend authorization authority
beyond the gate deadline.

Every mandatory phase checks the deadline before starting.

Deadline exhaustion yields a fail-closed unavailable/incomplete result and no
CODE PASS receipt.

## 10. Duplicate-review exclusion

Normal review packets MUST NOT be processed in parallel.

Use a private active-review status/lock keyed by at least:

- review type;
- current CR identity; and
- immutable snapshot identity.

The reference stale-lock threshold is 900 seconds, with process-liveness checks.
Completed, abandoned, or demonstrably stale locks may be removed. A live
parallel review causes `ACTIVE_REVIEW_ALREADY_RUNNING` and fails closed.

## 11. CODE snapshot construction

### 11.1 Preconditions

For CODE review:

- resolve `base^{commit}`;
- resolve `head^{commit}`;
- require `base` to be an ancestor of `head`;
- require resolved `head` to equal current `HEAD`;
- require `git status --porcelain=v1 -z --untracked-files=all` to be empty; and
- require `base..head` to contain a non-empty diff.

Any violation is `SNAPSHOT_MISMATCH` or an equivalent fail-closed error.

### 11.2 Public snapshot identity

Generate the review diff from committed objects with:

```text
git diff --no-ext-diff --unified=80 <base-sha> <head-sha>
```

Define:

```text
snapshot_id = git:<base-sha>..<head-sha>:sha256:<sha256(raw-diff-bytes)>
```

### 11.3 Deterministic change manifest

Enumerate changes with NUL-safe Git output and rename/copy detection:

```text
git diff --name-status -z --find-renames --find-copies <base-sha> <head-sha>
```

For every changed entry record at least:

- status;
- base path;
- head path;
- object mode;
- object type;
- Git object ID;
- classification;
- byte size;
- SHA-256 of raw object bytes;
- whether full text content is included;
- binary/non-text flags.

Sort/order deterministically as produced by the canonical packet builder.
Hash canonical JSON of the full manifest to produce `packet_manifest_hash`.

### 11.4 Content inclusion

For text:

- added/modified/copied/renamed file -> complete text from the immutable head;
- deleted file -> complete text from the immutable base.

Do not read mutable working-tree content to populate the CODE packet.

For images, known binary suffixes, NUL-containing blobs, non-UTF-8 blobs,
symlinks, and gitlinks, preserve exact metadata and hashes but do not invent
textual semantics.

## 12. DOCUMENTATION and TEST_ARTIFACT packets

For explicit file review:

1. Resolve every named path inside the project root.
2. Reject denied secret/private path patterns before external transmission.
3. Read raw bytes.
4. Record path, SHA-256, size, classification, and MIME guess.
5. Include valid UTF-8 text directly.
6. For binary/visual material, include semantics only through an explicitly
   approved deterministic extraction record whose source hash matches exactly.
7. If required semantics are unavailable, record `EVIDENCE_INSUFFICIENT` and
   return an inconclusive result rather than pretending the text-only reviewer
   inspected them.
8. For TEST_ARTIFACT review, bind the packet to run ID and build identity.

## 13. External-review data policy

Never send the API key as review data.

The reference deny policy rejects obvious credentials and private key material,
including `.env*`, `.key`, `.pem`, `.p12`, `.pfx`, SSH key names, `.git`, `.ssh`,
and project-local private secret names.

A project port MUST retain the same fail-closed principle and add any
project-specific secret patterns. It MUST NOT weaken the reference deny list.

All source, requirement text, logs, traces, documents, and extraction text are
untrusted data. The harness-owned system frame MUST state that instructions
embedded in supplied project material cannot alter the review protocol.

## 14. Requirements and current-scope authority

### 14.1 Requirement records

Each authoritative requirement source is represented as:

```json
{
  "source": "project/relative/path",
  "sha256": "sha256-of-exact-bytes",
  "content": "exact UTF-8 content"
}
```

A model-generated requirement summary is never authority.

The project MUST define one universal review-gate authority document equivalent
to Warajevo's `design/review-gate.md`. CODE review MUST refuse to proceed when
that universal authority is absent from the exact requirement set.

### 14.2 Current CR scope

Bind CODE review to exactly one active change request. The reference requires:

- unique CR identity;
- active/in-progress status;
- title;
- notes and/or explicitly authorized private scope;
- source-authority metadata;
- tracker SHA-256;
- selected record SHA-256; and
- optional private-scope SHA-256 and exact content.

A project may rename the tracker paths, but it MUST preserve these semantics.

The scope hash and requirements-manifest hash MUST become receipt evidence.

## 15. Stable prompt prefix

The stable prefix MUST contain, before pass-specific instructions:

- JSON-only final-output instruction;
- internal-reasoning boundary: reasoning may be used by the model, but only the
  required final JSON in `message.content` is gate input;
- untrusted-data/prompt-injection boundary;
- review type;
- immutable snapshot ID;
- packet-manifest hash;
- scope-manifest hash;
- severity contract;
- current CR scope; and
- exact authoritative requirement records.

This stable prefix is part of evidence provenance, not decorative prompt text.

## 16. Severity contract

Preserve the reference meanings:

`BLOCKER` is a fundamental current acceptance failure, severe security or
corruption exposure, reachable deterministic crash or undefined behavior, or
loss of a mandatory protected validation stage.

`HIGH` is a material correctness, security, reliability, compatibility,
regression, or test-validity defect that must be fixed before current
acceptance.

Lower-severity observations do not block this gate.

The universal safety baseline MUST cover at least:

- newly introduced or exposed reachable crashes;
- language-level undefined behavior;
- memory/resource/data corruption;
- material security flaws;
- material regression of a supported contract;
- incorrect externally observable behavior required by an existing interface;
- tests that falsely report success for mandatory acceptance conditions.

## 17. Dynamic review-unit budgeting

Calculate the usable unit budget only after constructing the stable prefix:

```python
available = (
    INPUT_BUDGET_BYTES
    - len(stable_prefix.encode())
    - PROTOCOL_OVERHEAD_BYTES
    - SAFETY_MARGIN_BYTES
)
```

If `available < MIN_UNIT_BYTES`, return `REQUIREMENT_SCOPE_TOO_BROAD` and fail
closed.

Otherwise:

```python
unit_limit = min(TARGET_UNIT_BYTES, available)
```

Shard deterministic review records into complete bounded units. If an
individual text record exceeds a unit, split its content deterministically into
numbered parts that fit the byte limit. Do not skip small files. Do not skip
large files. Do not summarize away source bytes.

## 18. Discovery phase

### 18.1 One combined discovery pass per unit

This point is easy to get wrong and is mandatory.

For each review unit, make ONE combined discovery call covering all lenses for
that review type. Do NOT make one model call per lens.

For CODE the combined call covers:

1. requirements and functional correctness;
2. runtime, failure paths, safety, hostile input, lifecycle, ownership,
   concurrency, and recovery; and
3. integration, regression, compatibility, and test adequacy.

For DOCUMENTATION it covers:

1. technical and factual correctness;
2. current-scope consistency and completeness; and
3. implementation and test readiness.

For TEST_ARTIFACT it covers:

1. direct textual evidence and explicit failure signals;
2. masked, contradictory, or misinterpreted signals; and
3. current acceptance-criteria correlation and proof sufficiency.

### 18.2 Discovery semantics

Discovery returns candidates, not findings.

Every candidate must provide:

```text
candidate_id
proposed_severity
category
requirement_source
requirement_quote
scope_link
location
claim
failure_scenario
causal_path
evidence
assumptions[]
context_requests[]
```

A candidate must cite an exact requirement source and an exact quote from that
source. It must explain why the requirement applies to the current scope and
identify a concrete failure scenario and causal path.

Missing context is a bounded PATH/SYMBOL request. It is not evidence for HIGH.

Discovery excludes style, cleanup, speculative redesign, unrelated backlog,
and future work.

### 18.3 Discovery execution settings

Discovery uses full bounded reasoning because omissions in discovery cannot be
recovered reliably by later falsification if the candidate is never raised.

For Super:

- `reasoning_effort="high"`
- `reasoning_budget=16384`
- `max_tokens=24576`

For Lightning:

- `chat_template_kwargs.enable_thinking=true`
- `reasoning_budget=16384`
- no `reasoning_effort` field
- `max_tokens=24576`

For both:

- final JSON reserve: 8192 tokens
- `response_format={"type":"json_object"}` required
- `finish_reason="stop"` required
- schema-repair discipline preserved
- separate reasoning output ignored and never persisted

### 18.4 Output truncation

If discovery output truncates:

- if the review unit is larger than 8192 bytes, split it deterministically into
  smaller immutable units and retry those units;
- if it is already 8192 bytes or smaller, mark irreducible output truncation and
  fail closed.

## 19. Cross-unit CODE integration pass

If CODE review requires more than one discovery unit, set
`cross_unit_integration_required=true` and build a separate bounded integration
unit.

That integration packet contains the immutable change manifest and a source
index. Include full source where it fits; otherwise include deterministic bounded
source excerpts.

Run a separate `CODE-INTEGRATION` discovery pass covering cross-unit
integration, cross-rule behavior, caller/callee interaction, and regression
correctness.

Execution settings are identical to ordinary discovery:

- Super: high reasoning, 16384-token reasoning cap;
- Lightning: thinking enabled, 16384-token reasoning cap;
- 8192-token final JSON reserve;
- `max_tokens=24576`;
- JSON object and `finish_reason="stop"` required.

This pass discovers candidates only; they still enter the same deterministic
filter and falsification pipeline.

## 20. Deterministic candidate filtering

Before another model call, host code MUST validate every discovered candidate.
Reject deterministically when any of the following is true:

- candidate schema malformed;
- cited requirement source is not an authoritative input;
- exact requirement quote does not occur in cited source content;
- current-scope link missing;
- TEST_ARTIFACT category invalid;
- CODE location is not a tracked current-snapshot path;
- location is not an exact line in immutable review material and no valid
  matching PATH request can resolve it; or
- candidate duplicates an already accepted fingerprint.

The reference fingerprint is SHA-256 over canonical JSON containing:

```json
{
  "requirement_source": "...",
  "requirement_quote": "...",
  "location": "...",
  "claim": "..."
}
```

After acceptance, normalize the candidate ID to:

```text
DS-<first-12-uppercase-hex-of-fingerprint>
```

Model-authored IDs never become authoritative merely because the model emitted
them.

## 21. Bounded deterministic context completion

Supported context requests are only:

- `PATH`
- `SYMBOL`

Each candidate is bounded by `MAX_CONTEXT_CYCLES = 2`.

For a CODE candidate whose exact location line ends in `;` and contains a
callable identifier, host code may add one deterministic SYMBOL lookup when
capacity remains and discovery did not already request that symbol. This closes
a common declaration/callee evidence gap without trusting model navigation.

Resolve context from the same immutable reviewed head. Bound path/symbol
content by the reference byte limits. Record exact hashes with resolved content.

Unresolved required context remains unresolved. It is not filled from web
knowledge, memory, or a later mutable checkout.

## 22. Falsification phase

Every candidate that survives deterministic filtering enters a hostile,
independent falsification step.

The falsifier is instructed to assume each allegation is false until exact
current evidence and an exact current requirement positively prove it.

It must inspect, as relevant:

- alternate callers/callees;
- initialization and cleanup;
- invariants;
- reachability;
- language/platform behavior;
- assumptions;
- current CR scope;
- future-work boundaries; and
- severity contract.

For every candidate it must return exactly one:

- `CONFIRMED`
- `REJECTED`
- `NON_BLOCKING`
- `UNRESOLVED`

It must also return evidence conclusion:

- `VIOLATION`
- `COMPLIANCE`
- `INCONCLUSIVE`

`CONFIRMED` requires:

- positive proof;
- `evidence_conclusion=VIOLATION`; and
- independently established `BLOCKER` or `HIGH` severity.

A real issue below HIGH is `NON_BLOCKING`.

Missing evidence is `UNRESOLVED`.

A falsifier may emit a new suspicion only as `new_candidates`; those candidates
must enter the same deterministic validation/context/falsification pipeline.
The reference allows one new-candidate expansion cycle.

### 22.1 Falsification batching

Batch candidates only while the exact falsification prompt remains within
`INPUT_BUDGET_BYTES`.

If one candidate alone exceeds the input budget, fail closed.

### 22.2 Falsification execution settings

Falsification is the highest-value reasoning stage because it decides whether a
candidate allegation is actually proven by immutable evidence. Use full bounded
reasoning.

For Super:

- `reasoning_effort="high"`
- `reasoning_budget=16384`
- `max_tokens=28672`

For Lightning:

- `chat_template_kwargs.enable_thinking=true`
- `reasoning_budget=16384`
- no `reasoning_effort` field
- `max_tokens=28672`

For both, reserve up to 12288 tokens for the final structured JSON result and
require `finish_reason="stop"`.

### 22.3 Falsification truncation behavior

If a multi-candidate falsification output truncates, bisect the batch and retry
smaller batches.

If a single-candidate falsification output truncates, make exactly one compact
retry over the identical immutable evidence. The retry remains a high/full
reasoning call; only the final JSON schema is compact:

- Super: `reasoning_effort="high"`, `reasoning_budget=16384`;
- Lightning: thinking enabled, `reasoning_budget=16384`;
- final JSON reserve: 4096 tokens;
- `max_tokens=20480`;
- compact JSON decision only.

A second single-candidate truncation is unresolved/inconclusive. Do not ask the
other model for a nicer answer; truncation is a semantic/protocol completion
failure, not availability failover.

## 23. Prior findings and disputes

Prior records may be:

- `OPEN`
- `RESOLVED`
- `DISPUTED`

They retain exact evidence and do not bias independent discovery. Supply prior
evidence only to matching candidates during falsification/adjudication.

A prior finding requires a unique ID and non-empty evidence records with exact
source, location, and claim.

## 24. Adjudication

Adjudication is exceptional. It is used for a matching `DISPUTED` prior record
whose candidate remains confirmed or unresolved after falsification.

Resolve every dispute source from the same immutable packet first. If decisive
dispute evidence cannot be resolved, return `INCONCLUSIVE`.

The adjudication packet is bounded to:

- snapshot identity;
- candidate;
- falsifier decision;
- developer dispute;
- resolved dispute evidence; and
- exact authoritative requirement.

Allowed adjudication decisions:

- `CONFIRMED`
- `REJECTED`
- `NON_BLOCKING`
- `HUMAN_DECISION_REQUIRED`

If authoritative evidence conflicts or the supplied evidence cannot decide,
require a human decision. Do not force ambiguity into a model-generated answer.

Adjudication uses the same full bounded reasoning policy as falsification:

- Super: `reasoning_effort="high"`, `reasoning_budget=16384`;
- Lightning: thinking enabled, `reasoning_budget=16384`;
- final JSON reserve: 12288 tokens;
- `max_tokens=28672`;
- `finish_reason="stop"` and schema-valid JSON required.

## 25. Verdict synthesis

Host code, not a final model, synthesizes the gate verdict.

### PASS

Return `PASS` only when:

- every mandatory discovery/integration pass completed;
- every surviving candidate has a terminal proof result;
- no material candidate is unresolved;
- no authority conflict remains;
- no confirmed BLOCKER/HIGH finding remains;
- packet/scope/requirements identities remain valid; and
- `review_complete=true`.

### FAIL

Return `FAIL` when the review completed and at least one independently confirmed
BLOCKER/HIGH defect remains.

### INCONCLUSIVE

Return `INCONCLUSIVE` for incomplete proof, missing material evidence, unresolved
context, schema/protocol incompleteness, source mutation, irreducible truncation,
or similar conditions that do not constitute a completed serious-defect proof.

### REVIEW_UNAVAILABLE

Return `REVIEW_UNAVAILABLE` when required review infrastructure or configuration
cannot perform the review inside the bounded retry/deadline policy.

### HUMAN_DECISION_REQUIRED

Return `HUMAN_DECISION_REQUIRED` when exact authoritative intent conflicts or
cannot be decided from the immutable evidence.

Only PASS authorizes protected testing.

## 26. Confirmed blocker record

A confirmed blocker record should preserve the reference evidence contract:

```text
id
severity
category
requirement_source
requirement_quote
scope_link
location
failure_scenario
causal_path
evidence + falsifier proof
assumptions
negative_check
required_outcome
falsification_decision=CONFIRMED
```

Do not collapse this into a free-form model summary.

## 27. CODE PASS receipt

At the start of every CODE attempt, delete any older CODE PASS receipt.

Write a new receipt only when:

- verdict is exactly `PASS`;
- `review_complete` is true; and
- immediately before writing, current `HEAD` still equals the reviewed head and
  the working tree is still clean.

The receipt MUST include at least:

```json
{
  "schema_version": 2,
  "review_protocol_version": 2,
  "cr_number": "CR-XXXX",
  "snapshot_id": "git:...",
  "packet_manifest_hash": "...",
  "requirements_manifest_hash": "...",
  "scope_manifest_hash": "...",
  "requirement_sources": [
    {"source": "...", "sha256": "..."}
  ],
  "scope_private_source": null,
  "verdict": "PASS",
  "review_complete": true,
  "primary_model": "nvidia/nemotron-3-super-120b-a12b",
  "fallback_model": "nvidia/nemotron-3.5-lightning-30b-a3b",
  "fallback_used": false
}
```

The three model-policy fields are the only receipt extension required by this
two-model port. They make the changed model authority auditable.

Any non-PASS CODE result MUST leave no valid CODE PASS receipt.

## 28. Telemetry

Retain the reference telemetry fields, including:

- review type;
- CR number;
- snapshot ID;
- packet/requirement/scope hashes;
- call count;
- per-call records;
- retry count;
- API status;
- token/cache usage when supplied by the endpoint;
- discovery passes and unit count;
- cross-unit integration flag;
- discovery candidate count;
- deterministic rejects;
- context requests/resolutions;
- falsifier result counts;
- falsification batch count;
- new candidates;
- adjudication count;
- human-decision count;
- confirmed count;
- final result; and
- elapsed time.

Add the model/failover fields from section 7.7.

Never log:

- API key;
- Authorization header;
- prompts;
- source text;
- private requirement content; or
- hidden reasoning.

Keep telemetry private unless project policy explicitly approves publication.

## 29. Health-check contract

Normal review MUST NOT add a separate liveness call before every review. That
would change the reference call graph and creates a new failure surface.

Provide an explicit operator health check that tests the exact production
payload contract separately against BOTH authorized models:

```text
PRIMARY  -> must return exact schema-valid availability JSON
FALLBACK -> must return exact schema-valid availability JSON
```

The health check must verify the exact model-specific reasoning contract, not
a cheaper non-thinking surrogate. It must use:

- the production endpoint;
- the same credential source;
- `stream=false`;
- `response_format={"type":"json_object"}`;
- `temperature=1.0` and `top_p=0.95`;
- Super with `reasoning_effort="high"` and `reasoning_budget=16384`;
- Lightning with `enable_thinking=true` and `reasoning_budget=16384`, without a
  `reasoning_effort` field;
- `max_tokens=24576`, preserving an 8192-token final JSON reserve;
- `finish_reason="stop"`; and
- a non-empty schema-valid final JSON object in `message.content`.

Any separate reasoning field must be ignored and not logged during health
checking just as in production review.

A deployment cannot be certified as having the requested fallback unless both
model health checks pass.

A health-check failure does not authorize weakening the production payload.

## 29A. Bootstrap maintenance gate

The reference repository retains `tools/reviewer/legacy_bootstrap_gate.py` as a
separate one-pass audit path for changes to the normal reviewer harness itself.
A conforming project MUST retain an equivalent bootstrap path so maintenance of
the normal gate does not depend solely on the normal multi-stage verdict path
that is being changed.

The pinned reference bootstrap behavior is retained except for this workflow's
explicit reasoning revision:

- build the same immutable CODE packet from committed `base..head`;
- require the same universal authority source;
- optionally scope the bootstrap packet by exact path and line range;
- use a separate input ceiling of 2,000,000 bytes;
- use one complete independent audit prompt;
- use the same model-specific full reasoning controls as normal review;
- reasoning cap 16384 tokens;
- final JSON reserve 8192 tokens;
- `max_tokens=24576`;
- return at most one highest-confidence decisive BLOCKER/HIGH finding;
- require exact authoritative requirement source and quote;
- require exact immutable location, concrete failure, evidence, negative check,
  and required outcome;
- PASS iff the findings array is empty;
- allow at most two fresh schema-repair attempts after the initial response;
- fail closed as inconclusive when the complete bootstrap packet exceeds the
  independent input ceiling or when the reviewer/protocol is unavailable; and
- write a private bootstrap PASS receipt only for a complete PASS.

The reference bootstrap implementation reuses the normal gate's immutable
packet builder and NVIDIA transport client but has a distinct one-pass review
contract and validator. Preserve that separation. Do not replace it with
"normal gate reviews itself and says it is fine."

In this two-model port the bootstrap path uses the same authorized model policy:

```text
PRIMARY  nvidia/nemotron-3-super-120b-a12b
FALLBACK nvidia/nemotron-3.5-lightning-30b-a3b
```

The same availability-only failover rules apply. A bootstrap semantic FAIL,
inconclusive result, truncation, or schema failure MUST NOT trigger verdict
shopping through the fallback.

For maintenance that changes the model adapter, packet builder, or bootstrap
imports themselves, the project MUST execute the bootstrap review from a
previously trusted immutable gate/tooling revision against the candidate diff,
or use an equivalently isolated trusted bootstrap copy. The candidate under
review must not be allowed to redefine the code that decides whether its own
bootstrap review passed.

The protected maintenance/test launcher MUST verify the bootstrap receipt and
its immutable snapshot/requirement identities before allowing gate-maintenance
validation. A normal CODE PASS receipt is not a substitute for this bootstrap
authority.

## 30. Protected-test admission

The test launcher/orchestrator MUST independently verify the CODE PASS receipt
before protected execution.

At minimum it must verify:

1. receipt exists;
2. schema/protocol version accepted;
3. verdict exactly `PASS`;
4. `review_complete=true`;
5. receipt CR equals active CR;
6. receipt reviewed head equals current required head;
7. current repository state satisfies the project's clean/published policy;
8. packet/diff identity matches the candidate being tested;
9. requirement/scope identities match the current authorities;
10. model IDs in receipt exactly match this workflow's authorized pair; and
11. no later code modification invalidated the receipt.

Any mismatch denies test execution.

The CI system may run cheap repository-policy validation before this point, but
it MUST NOT run the protected target/test workload that the external gate is
intended to authorize.

## 31. Project-specific integration points

A new project may change names and paths, but not semantics. Define explicitly:

```text
UNIVERSAL_REQUIREMENT_SOURCE = <project gate authority document>
CR_TRACKER_PATH = <project active-change tracker>
PRIVATE_REVIEW_DIR = <ignored/private reviewer artifact directory>
CODE_PASS_RECEIPT = <private receipt path>
PROTECTED_TEST_ENTRYPOINT = <test command/orchestrator>
AUTHORITATIVE_REMOTE = <remote/ref policy, if applicable>
```

If the project has no CR tracker yet, create one. Do not replace current-scope
binding with an unconstrained prompt such as "review everything." The gate needs
an exact current acceptance boundary.

## 32. Command interface

A compatible CLI should retain the reference conceptual surface:

```text
python tools/reviewer/review_gate.py review \
  --type CODE \
  --cr CR-XXXX \
  --base <base> \
  --head <head> \
  --requirements <universal-review-authority> \
  [--requirements <additional-authority>] \
  [--scope-file <authorized-private-scope>]

python tools/reviewer/review_gate.py review \
  --type DOCUMENTATION \
  --requirements <authority> \
  --path <document>

python tools/reviewer/review_gate.py review \
  --type TEST_ARTIFACT \
  --requirements <authority> \
  --run-id <test-run-id> \
  --build-id <build-identity> \
  --path <artifact>

python tools/reviewer/review_gate.py health-check \
  --requirements <universal-review-authority> \
  --deadline-seconds 60
```

For the two-model port, `health-check` should report both model results and exit
non-zero if either authorized model fails the exact payload contract.

Normal review must not expose a casual `--model` override. Model authority is a
reviewed protocol constant, not a convenience switch.

## 33. Required implementation tests

The port is not complete until automated tests prove the following behavior.
The test names are illustrative; the assertions are normative.

### 33.1 Configuration/model tests

1. Missing `NVIDIA_API_KEY_CODING` fails closed.
2. Empty `NVIDIA_API_KEY_CODING` fails closed.
3. API key is never present in system/user prompt data.
4. Primary model ID is exact.
5. Fallback model ID is exact.
6. Normal first request always uses primary.
7. No unapproved model alias is accepted.
8. Every Super semantic call sends `reasoning_effort="high"`,
   `reasoning_budget=16384`, `temperature=1.0`, and `top_p=0.95`; it does not
   send a competing `chat_template_kwargs.reasoning_budget`.
9. Every Lightning semantic call sends `enable_thinking=true`,
   `reasoning_budget=16384`, `temperature=1.0`, and `top_p=0.95`; it does not
   send the undocumented `reasoning_effort` field.
10. Phase generation contracts are exact: discovery/integration 24576 with an
    8192-token final-JSON reserve; falsification 28672 with 12288 reserved;
    compact falsification 20480 with 4096 reserved; adjudication 28672 with
    12288 reserved. Only final `message.content` JSON is parsed; separate
    reasoning output is never persisted. The dual-model health check exercises
    these same model-specific reasoning and sampling controls rather than a
    non-thinking surrogate.

### 33.2 Fallback/timing tests

11. Primary 404 receives exactly one retry after 2 seconds, then activates
    fallback if 404 persists.
12. Primary 429 without `Retry-After` uses 5/15/45-second backoff, at most four
    attempts, and a 90-second recovery budget before fallback.
13. Valid 429 `Retry-After` inside the remaining 90-second/review budgets is
    honored; malformed `Retry-After` is treated as absent.
14. `Retry-After` that exceeds either remaining budget triggers immediate
    fallback instead of an over-budget sleep.
15. Primary 502/503 exhaustion uses exactly the 2/8-second retry schedule before
    fallback.
16. Primary `insufficient_system_resource` uses exactly the 2/8-second retry
    schedule before fallback.
17. Primary 500 receives one retry after 5 seconds, then falls back if 500
    persists.
18. Primary 408/504 receives one retry after 5 seconds, then falls back if the
    timeout status persists.
19. Retryable transport/connectivity failure uses 1/4-second delays, enforces a
    10-second connection ceiling, then falls back on exhaustion.
20. A hard request/inference timeout is 300 seconds and triggers immediate
    fallback without another same-model 300-second attempt.
21. Fallback uses the same class-specific policy; its exhaustion returns
    `REVIEW_UNAVAILABLE` because no third model exists.
22. Valid primary semantic results, including `CONFIRMED`, `UNRESOLVED`, and
    `FAIL`, never invoke fallback.
23. Truncation, schema-invalid semantic output/repair exhaustion, and
    non-retryable auth/configuration failure do not verdict-shop through the
    fallback.
24. After successful failover, the fallback remains latched for that immutable
    review; a fresh complete review starts with primary; failover never resets
    the overall deadline.
25. Telemetry records the exact active model, sanitized failure class/status,
    attempt count, retry delay/budget state, and failover reason without secret
    material.

### 33.3 Snapshot tests

26. Non-ancestor base fails.
27. `head != HEAD` fails.
28. Dirty staged tree fails.
29. Dirty unstaged tree fails.
30. Untracked non-ignored file fails.
31. Empty code-review range fails.
32. Candidate mutation during review prevents PASS receipt.
33. Raw diff hash determines snapshot identity exactly.
34. Rename/copy NUL parsing is deterministic.
35. Deleted text comes from base, not head/worktree.
36. Added/modified text comes from head Git object, not worktree.

### 33.4 Data/evidence tests

37. Denied secret paths are rejected before transmission.
38. Binary/image data does not become invented text semantics.
39. Unapproved extractor is rejected.
40. Extraction source-hash mismatch is rejected.
41. Missing required binary semantics yields evidence-insufficient/inconclusive.
42. Requirement records bind exact source bytes and SHA-256.
43. Missing universal CODE authority fails.
44. Missing/ambiguous/inactive CR scope fails.

### 33.5 Discovery/filter tests

45. Exactly one combined discovery call is made per ordinary review unit,
    using the model-specific full reasoning policy and `max_tokens=24576`.
46. CODE combined prompt includes all three CODE lenses.
47. Multi-unit CODE review adds cross-unit integration discovery using the
    same full reasoning policy and `max_tokens=24576`.
48. Tiny files are not skipped.
49. Oversized records are deterministically sharded rather than skipped.
50. Oversized requirement prefix fails `REQUIREMENT_SCOPE_TOO_BROAD`.
51. Candidate with non-authoritative requirement source is rejected locally.
52. Candidate with quote absent from exact requirement is rejected locally.
53. Invalid location is rejected unless valid bounded PATH resolution exists.
54. Duplicate candidate fingerprint is rejected locally.
55. Accepted candidate ID is host-derived from fingerprint.

### 33.6 Context/falsification tests

56. Only PATH/SYMBOL context request types are accepted.
57. Context cycles are bounded to two.
58. Deterministic callable-symbol lookup uses the immutable head.
59. Every surviving candidate receives exactly one terminal falsifier decision
    using full bounded reasoning and `max_tokens=28672`.
60. `CONFIRMED` requires positive proof, VIOLATION, and BLOCKER/HIGH severity.
61. Lower-severity real issue becomes NON_BLOCKING.
62. Missing evidence becomes UNRESOLVED.
63. Multi-candidate truncation bisects the batch.
64. Single-candidate truncation receives exactly one compact retry that retains
    full bounded reasoning, reserves 4096 tokens for final JSON, and uses
    `max_tokens=20480`.
65. Second compact truncation remains unresolved/inconclusive.
66. Falsifier new candidates re-enter deterministic validation.
67. New-candidate expansion is bounded to one cycle.

### 33.7 Adjudication/verdict tests

68. Only matching DISPUTED prior records invoke adjudication, and adjudication
    uses full bounded reasoning with `max_tokens=28672`.
69. Unresolvable dispute evidence yields inconclusive.
70. Authority conflict yields HUMAN_DECISION_REQUIRED.
71. Host code synthesizes final blocker set; no final model is allowed to edit it.
72. Confirmed BLOCKER/HIGH yields FAIL.
73. Material unresolved candidate yields INCONCLUSIVE.
74. Complete clean review yields PASS with `review_complete=true`.
75. Review-unavailable path cannot yield PASS.
76. Human-decision path cannot yield PASS.

### 33.8 Receipt/lock/deadline tests

77. CODE start deletes stale PASS receipt.
78. Non-PASS CODE result leaves no PASS receipt.
79. PASS receipt is written only after final head/clean-tree revalidation.
80. Receipt contains exact snapshot/packet/requirements/scope identities.
81. Receipt contains exact primary/fallback model policy.
82. Active parallel review is rejected.
83. Stale/abandoned lock recovery is bounded and safe.
84. The 10-second connection sub-timeout and 300-second request timeout each
    respect the smaller remaining overall deadline.
85. Overall deadline terminates a stuck transport worker.
86. Protected test launcher refuses missing receipt.
87. Protected test launcher refuses stale/mismatched receipt.
88. Protected test launcher admits only exact valid PASS receipt.

### 33.9 Bootstrap-maintenance tests

89. Bootstrap review uses the immutable committed CODE packet.
90. Bootstrap input above 2,000,000 bytes fails closed.
91. Bootstrap uses the same model-specific full reasoning policy, a 16384-token
    reasoning cap, an 8192-token final-JSON reserve, and `max_tokens=24576`.
92. Bootstrap result validator requires exact requirement quote and immutable
    source location for every serious finding.
93. Bootstrap schema repair is bounded to two fresh repairs after the initial
    response.
94. Bootstrap PASS is valid only with an empty findings array.
95. Bootstrap semantic FAIL/inconclusive/schema failure does not trigger model
    verdict shopping.
96. Gate/model-adapter maintenance is reviewed by a previously trusted isolated
    bootstrap implementation, not by candidate-controlled pass logic.

All 96 tests MUST pass before the port is considered complete.

## 34. Failure matrix

| Condition | Result | Tests allowed? | Fallback allowed? |
|---|---|---:|---:|
| Complete clean review | PASS | YES | N/A |
| Confirmed BLOCKER/HIGH | FAIL | NO | NO |
| Material unresolved candidate | INCONCLUSIVE | NO | NO |
| Authority conflict | HUMAN_DECISION_REQUIRED | NO | NO |
| Missing required evidence | INCONCLUSIVE | NO | NO |
| Dirty/mutated source | INCONCLUSIVE | NO | NO |
| Schema repair exhausted | INCONCLUSIVE/UNAVAILABLE | NO | NO |
| Output truncation exhausted | INCONCLUSIVE | NO | NO |
| Primary 404 persists after one 2-second retry | continue same logical call on fallback | NO until final PASS | YES |
| Primary 429 exceeds 5/15/45 schedule or 90-second budget | continue same logical call on fallback | NO until final PASS | YES |
| Primary 502/503 or resource condition persists after 2/8 retries | continue same logical call on fallback | NO until final PASS | YES |
| Primary 500 persists after one 5-second retry | continue same logical call on fallback | NO until final PASS | YES |
| Primary 408/504 persists after one 5-second retry | continue same logical call on fallback | NO until final PASS | YES |
| Primary transport path persists after 1/4 retries | continue same logical call on fallback | NO until final PASS | YES |
| Primary hard request timeout reaches 300 seconds | continue same logical call on fallback immediately | NO until final PASS | YES |
| Primary reaches four infrastructure attempts without semantic success | continue same logical call on fallback | NO until final PASS | YES |
| Fallback retryable infrastructure policy exhausted | REVIEW_UNAVAILABLE | NO | already exhausted |
| Non-retryable auth/config error | REVIEW_UNAVAILABLE | NO | NO |
| Deadline exhausted | REVIEW_UNAVAILABLE | NO | NO |
| Live duplicate review | INCONCLUSIVE/UNAVAILABLE | NO | NO |

## 35. Reference-parity acceptance checklist

Before declaring implementation complete, verify every item manually against the
on-disk implementation and tests.

- [ ] Exact primary model ID present once as authority constant.
- [ ] Exact fallback model ID present once as authority constant.
- [ ] NVIDIA NIM endpoint exact.
- [ ] Credential comes only from `NVIDIA_API_KEY_CODING` or an explicitly
      reviewed project replacement.
- [ ] No CLI model override.
- [ ] Super semantic calls use explicit high reasoning, 16384-token reasoning
      cap, temperature 1.0, and top_p 0.95.
- [ ] Lightning semantic calls use explicit thinking, 16384-token reasoning cap,
      temperature 1.0, and top_p 0.95, with no `reasoning_effort` field.
- [ ] Discovery/integration, falsification, compact falsification, adjudication,
      and bootstrap max-token values preserve the original final-JSON allowances
      after adding the bounded reasoning reserve.
- [ ] Separate reasoning output is ignored and never logged, persisted, or used
      as gate evidence.
- [ ] 1M advertised context is not used as permission to inflate byte budgets.
- [ ] Retryable HTTP status set remains exactly 404/408/429/500/502/503/504.
- [ ] Connection establishment is capped at 10 seconds.
- [ ] A genuine inference request is capped at 300 seconds and is not repeated
      on the same model after a hard timeout.
- [ ] 404 gets exactly one retry after 2 seconds before model transition.
- [ ] 429 honors bounded valid `Retry-After`; otherwise uses 5/15/45 seconds and
      never exceeds the 90-second rate-limit recovery budget.
- [ ] 502/503 and `insufficient_system_resource` use 2/8-second recovery.
- [ ] 500 and 408/504 get exactly one retry after 5 seconds.
- [ ] Retryable transport/connectivity failures use 1/4-second recovery.
- [ ] No identical logical API call exceeds four infrastructure attempts per
      model.
- [ ] Fallback uses the same class-specific timing and has no third-model escape.
- [ ] Fallback is availability-only and one-way-latched for the review attempt.
- [ ] Every fresh complete review starts with primary again.
- [ ] No semantic verdict triggers fallback.
- [ ] Overall deadline spans primary + fallback and every retry/wait.
- [ ] CODE PASS receipt invalidated at start.
- [ ] Clean committed snapshot required.
- [ ] Base ancestry required.
- [ ] Exact raw diff SHA-256 binds snapshot.
- [ ] NUL-safe deterministic change enumeration.
- [ ] Full changed text from immutable Git objects.
- [ ] Binary/visual semantics fail closed when unavailable.
- [ ] Exact requirements and hashes are authority.
- [ ] Current scope has an immutable identity.
- [ ] Prompt-injection boundary is harness-owned.
- [ ] Stable prefix precedes pass-specific review instructions.
- [ ] One combined discovery call per ordinary unit.
- [ ] All CODE lenses are inside that one call.
- [ ] Multi-unit CODE receives an integration pass.
- [ ] Candidate schema is host-validated.
- [ ] Requirement quote is host-validated against exact authority bytes.
- [ ] Candidate IDs are host-derived fingerprints.
- [ ] PATH/SYMBOL context only, bounded to two cycles.
- [ ] Every surviving candidate is independently falsified.
- [ ] `CONFIRMED` requires positive serious-defect proof.
- [ ] Lower severity is NON_BLOCKING.
- [ ] Missing evidence is UNRESOLVED, not HIGH.
- [ ] Falsifier new suspicions re-enter the same pipeline.
- [ ] Dispute adjudication is bounded and exceptional.
- [ ] Conflicting authority requires human decision.
- [ ] Host synthesizes final verdict.
- [ ] Source/head cleanliness revalidated immediately before PASS receipt.
- [ ] Model/failover provenance recorded.
- [ ] Duplicate reviews prevented.
- [ ] API and overall deadlines enforced.
- [ ] Protected tests verify receipt independently.
- [ ] Independent bootstrap maintenance path is retained and tested.
- [ ] Gate/model-adapter changes cannot redefine their own bootstrap PASS logic.
- [ ] Any code correction reopens complete CODE review.

## 36. Verification procedure for this workflow itself

When this workflow document or its implementation changes:

1. Read the candidate from disk, not from an earlier chat/message copy.
2. Recompute SHA-256.
3. Compare every normative behavior against the pinned reference gate revision,
   treating sections 3.4, 6, 7.3, and 9 as the explicit reviewed transport-
   timing divergence and sections 2.1, 3.3, 6, 7.2, 18.3, 19, 22, 24, 29, and
   29A as the explicit reviewed reasoning divergence rather than accidental
   parity drift.
4. Compare all model capability and NVIDIA timing claims against current NVIDIA
   primary sources, and re-check cited operational reports when they are used to
   justify availability policy.
5. Run the 96 implementation tests.
6. Run a health check against both authorized model IDs with the exact
   production payload contract.
7. Audit the resulting telemetry for model, retry, fallback, deadline, and
   secret-handling correctness.
8. Re-read the final disk copy from byte 0 to EOF.
9. Repeat the full audit from the disk copy.
10. Certification requires two consecutive complete audits with zero gaps.

If a gap is found, fix it and restart the count from zero. The gate does not get
extra credit for being almost immutable.

## 37. Explicit non-goals

This port MUST NOT quietly add any of the following:

- majority voting between models;
- primary/fallback comparative judging;
- routing "easy" findings to Lightning merely to save latency;
- severity promotion by a final summarizer;
- reviewer-generated requirement authority;
- web knowledge as proof of a repository defect;
- mutable working-tree evidence in CODE review;
- arbitrary maximum correction rounds;
- permissive test execution after an inconclusive review;
- silent sampling-control changes;
- silent thinking/reasoning-mode changes; or
- silent output/context budget inflation.

Any such change is a new review protocol and requires its own explicit design,
implementation, tests, and certification.

## 38. Final implementation rule

For a fresh project, copy the architecture, not the Warajevo names.

Preserve:

- immutable evidence;
- exact scope/requirement authority;
- deterministic complete coverage;
- one combined discovery pass per unit;
- hostile independent falsification;
- bounded context completion;
- evidence-bound dispute adjudication;
- host-side verdict synthesis;
- fail-closed error handling;
- class-specific bounded primary/fallback availability timing;
- immutable PASS receipts;
- pre-test admission; and
- audited primary/fallback model provenance.

Change only project-specific paths/identities and the explicitly authorized
model and availability-timing policies in this document.

Under this workflow, the normal reviewer is
`nvidia/nemotron-3-super-120b-a12b`; the only model fallback is
`nvidia/nemotron-3.5-lightning-30b-a3b`; and neither model gets to overrule the
evidence court merely by sounding confident.

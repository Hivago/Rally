# PayU Payment: Same-Tab Checkout + Return Redirect

> Reference for how the PayU hosted-checkout flow works in Rally, how the
> post-payment redirect lands the user back on the SPA, and how to configure /
> debug it. Written after the 2026-07-14 "returns the API URL after payment"
> incident.

## Goal

1. Payment opens in the **same browser tab** (not a new tab).
2. After payment, the user lands on the SPA success page with the order id:
   `https://hivago.vercel.app/payment-success?orderId=<guid>`.

## How the flow works

```
Frontend (SPA)                 Backend (RallyAPI)              PayU
─────────────                  ──────────────────              ────
POST /api/payments/initiate ──►
                               returns { key, txnId, amount,
                               hash, surl, furl, payUBaseUrl }
◄──────────────────────────────
build hidden form, POST it
to payUBaseUrl (SAME TAB) ─────────────────────────────────►  hosted checkout
                                                               user pays
                               ◄── POST (form) to surl ────────  (browser navigates)
   /api/payments/return/success
   - reads POSTed txnid
   - IPaymentRepository.GetByTxnIdAsync → Payment.OrderId (Guid)
   - 302 redirect (GET) ─┐
◄──────────────────────── ┘  to  FrontendSuccessUrl + "?orderId=<guid>"
success page reads orderId,
confirms via GET /api/payments/verify (or GET order)
```

Two facts that dictate the whole design:

- **PayU POSTs to `surl`/`furl`** (form-urlencoded), and a **static SPA cannot read
  a POST body** from JS. So the browser must land on the **backend** first, which
  reads the POST and then GET-redirects to the SPA.
- **PayU does NOT template `{orderId}`.** `surl` is a static string; PayU only echoes
  its own fields (`txnid`, `status`, …). The order id is recovered on the backend by
  mapping `txnid → Payment.OrderId`. `txnid` format: `RALLY-{OrderNumber}-{HHmmss}`.
- **Source of truth is the S2S webhook** (`/api/payments/webhook`), not this redirect.
  The redirect is UX only.

## The TWO variable pairs (do not confuse them)

| Env var | Role | Points at |
|---|---|---|
| `PayU__SuccessUrl` / `PayU__FailureUrl` | **surl/furl** — where PayU returns the browser | the **backend** return endpoints |
| `PayU__FrontendSuccessUrl` / `PayU__FrontendFailureUrl` | where the backend then forwards the browser | the **SPA** pages |

### Correct values (per environment — use that env's OWN backend domain)

```
# surl/furl → this environment's backend return endpoints:
PayU__SuccessUrl          = https://<this-env-backend>/api/payments/return/success
PayU__FailureUrl          = https://<this-env-backend>/api/payments/return/failure

# where the backend forwards the browser (bare SPA URLs — NO ?orderId, backend appends it):
PayU__FrontendSuccessUrl  = https://hivago.vercel.app/payment-success
PayU__FrontendFailureUrl  = https://hivago.vercel.app/payment-failed
```

- Do **not** put `?orderId={orderId}` in these — the backend appends the real
  `?orderId=<guid>` itself.
- On **staging** the backend domain must be the **staging** service, on **prod** the
  **prod** service. A staging `SuccessUrl` pointing at the prod domain sends payments
  back through prod (which may not have the fix).

## Frontend: submit PayU form in the SAME TAB

The new-tab bug is caused by `target="_blank"` or `window.open`. Submit the form in the
current window with **no target**:

```ts
function redirectToPayU(res: InitiatePaymentResponse) {
  const form = document.createElement("form");
  form.method = "POST";
  form.action = res.payUBaseUrl;      // e.g. https://secure.payu.in/_payment
  // NO form.target → stays in the same tab. Do NOT use "_blank" or window.open.

  const fields: Record<string, string> = {
    key: res.key, txnid: res.txnId, amount: res.amount,
    productinfo: res.productInfo, firstname: res.firstName,
    email: res.email, phone: res.phone,
    surl: res.surl, furl: res.furl, hash: res.hash,
  };
  for (const [name, value] of Object.entries(fields)) {
    const i = document.createElement("input");
    i.type = "hidden"; i.name = name; i.value = value;
    form.appendChild(i);
  }
  document.body.appendChild(form);
  form.submit();
}
```

- Field names must be **lowercase** exactly (`txnid`, `productinfo`, `firstname`).
- Use `surl`/`furl` **as returned by the backend** — never override them.
- Same-tab means the SPA **fully reloads** on return. Any in-memory cart/order state is
  gone → rebuild the success page from `orderId` (query string) + a server call. Do not
  rely on React state surviving the round-trip.

## Success / failure pages

- `/payment-success` — read `orderId` from the query string, then confirm with
  `GET /api/payments/verify` (or fetch the order) and render status.
- `/payment-failed` — same pattern; `?orderId=<guid>` is included when known.

## Debugging: read the `/initiate` response

The `surl` in the `/initiate` response tells you **which backend the payment will return
to** — it is `PayU__SuccessUrl` on whichever service answered `/initiate`.

| Symptom (final URL after paying) | Cause | Fix |
|---|---|---|
| `…railway.app/payment-success` (API host, no `?orderId`) | Backend running **old code** (fix not deployed to that env) | Deploy the fix to that env |
| `hivago.vercel.app/payment-success` (no `?orderId`) | Code deployed but `FrontendSuccessUrl` unset/empty → falls back | Set `PayU__FrontendSuccessUrl` on that service |
| `hivago.vercel.app/payment-success?orderId=<guid>` | ✅ Working | — |
| `surl` in `/initiate` = **prod** domain but you meant to test staging | Frontend `VITE_API_URL` points at prod, OR staging `SuccessUrl` misconfigured to prod domain | Point `VITE_API_URL` at staging; set staging `SuccessUrl` to staging backend |

Test-mode tell: `payUBaseUrl = https://test.payu.in/_payment` and a test merchant key
means you're on PayU test config; live is `https://secure.payu.in` with the live key.

## Deploy checklist (per environment)

- [ ] Backend has the return-redirect code (`fix/payu-return-redirect`, merged into the
      branch that env deploys from — staging deploys from `staging`, prod from `master`).
- [ ] `PayU__SuccessUrl` / `PayU__FailureUrl` → **this env's** backend return endpoints.
- [ ] `PayU__FrontendSuccessUrl` / `PayU__FrontendFailureUrl` → the SPA pages (bare, no query).
- [ ] Frontend `VITE_API_URL` points at the backend you intend to test.
- [ ] Frontend submits the PayU form in the same tab (no `target="_blank"`).
- [ ] One real test-mode payment lands on `…/payment-success?orderId=<guid>`.

## Related code

- `src/Modules/Orders/RallyAPI.Orders.Endpoints/PaymentEndpoints.cs` — `/initiate`,
  `/webhook`, `/verify`, `/return/success`, `/return/failure`, `BuildReturnRedirect`.
- `src/Modules/Orders/RallyAPI.Orders.Infrastructure/Services/PayU/PayUOptions.cs` — config.
- `src/Modules/Orders/RallyAPI.Orders.Infrastructure/Services/PayU/PayUService.cs` — hash,
  checkout params, `NormalizePhone` (E.164 → 10-digit; a country-code phone hides UPI).

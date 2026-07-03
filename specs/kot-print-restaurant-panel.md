# Feature Spec: Kitchen Order Ticket (KOT) Printing — Restaurant Panel

> **Status**: In Progress (backend done, frontend pending)
> **Priority**: P1 (High)
> **Estimated Effort**: ~1.5 days frontend
> **Module(s)**: Orders (backend done), Restaurant Dashboard (frontend)
> **Owner**: Backend — done. Frontend — Restaurant Dashboard dev.
> **Date**: 2026-07-03

---

## 1. Problem Statement

Restaurants need to print a Kitchen Order Ticket (KOT) — a paper slip listing what to
cook for each order — so kitchen staff aren't reading orders off a screen. The ticket
must be able to **print automatically the moment a new (paid) order arrives**, and be
**re-printable** on demand.

## 2. User Stories

- As a **restaurant**, I want a KOT to auto-print when a new order is paid, so the kitchen starts immediately.
- As a **restaurant**, I want a "Print KOT" button on any order, so I can reprint a lost/smudged ticket.

## 3. Acceptance Criteria

- [ ] New paid order → KOT prints automatically (if auto-print is enabled).
- [ ] "Print KOT" button on the order detail/card reprints the ticket.
- [ ] KOT shows: order number, DELIVERY/PICKUP, placed time, item lines (qty × name), per-item + order-level notes, item count.
- [ ] KOT shows **no pricing** (kitchen doesn't need money).
- [ ] Auto-print can be toggled off (per-device setting) and doesn't fire twice for the same order.
- [ ] Layout targets **80mm thermal** (the launch standard — not A4).

## 4. Technical Design

### API Endpoint (BACKEND — DONE)

| Method | Endpoint | Auth | Description |
|--------|----------|------|-------------|
| GET | `/api/orders/{orderId}/kot` | `AdminOrRestaurant` | Returns `KitchenTicketDto` (kitchen-only, no pricing). 404 if not found or not the owning restaurant. |

**Response shape (`KitchenTicketDto`):**

```jsonc
{
  "orderId": "0f9c...",
  "orderNumber": "RA-000123",
  "fulfillmentType": "Pickup",        // enum: "Delivery" | "Pickup"
  "fulfillmentDisplay": "PICKUP",     // pre-uppercased for the ticket header
  "customerName": "Aditi",
  "statusDisplay": "Paid",            // for reprint context
  "placedAt": "2026-07-03T12:41:09Z",
  "totalItems": 3,
  "specialInstructions": "No onions in anything",   // nullable (order-level)
  "items": [
    { "itemName": "Paneer Tikka",   "quantity": 2, "specialInstructions": "Extra spicy" },
    { "itemName": "Butter Naan",    "quantity": 1, "specialInstructions": null }
  ]
}
```

> Auth note: the restaurant's JWT `sub` == its `restaurantId`, and the endpoint only
> returns tickets for that restaurant. No `restaurantId` needs to be passed by the client.

### SignalR — Auto-print trigger (ALREADY EXISTS, no backend change)

The dashboard already (or should) connect to the shared **`NotificationHub`** and, on
login as a restaurant, is auto-joined to group `restaurant_{restaurantId}`.

| Event name | Payload | Fires when |
|------------|---------|-----------|
| `NewOrderReceived` | `{ orderId, orderNumber, customerId, itemCount, totalAmount, placedAt }` | Customer **pays** (OrderPaidEvent) — this is the incoming-order alert |
| `OrderStatusUpdate` | `{ orderId, orderNumber, status, message }` | Every later lifecycle change |

**Auto-print = listen for `NewOrderReceived` → fetch `/api/orders/{orderId}/kot` → print.**

SignalR connection (JWT goes on the query string for WebSocket upgrade):

```ts
import { HubConnectionBuilder } from "@microsoft/signalr";

const connection = new HubConnectionBuilder()
  .withUrl(`${API_URL}/hubs/notifications?access_token=${getAccessToken()}`)
  .withAutomaticReconnect()
  .build();
```

> Hub is mapped at **`/hubs/notifications`** (confirmed in Program.cs).
> The dashboard likely already has this connection for the live orders feed — reuse it,
> don't open a second one.

### Frontend Components

| Component | Page | Description |
|-----------|------|-------------|
| `KitchenTicket` | (print-only) | Renders `KitchenTicketDto` into the 80mm print layout |
| `useKotPrint()` hook | shared | Fetches KOT + triggers print for a given orderId |
| `useAutoPrintKot()` hook | Orders page | Subscribes to `NewOrderReceived`, dedupes, calls `useKotPrint` |
| Auto-print toggle | Settings / Orders header | Per-device on/off, persisted to `localStorage` |

### Print approach

Use **`react-to-print`**. It clones the referenced component into its own isolated print
document, so there's **no `body * { visibility:hidden }` hack and no separate `.css` file** —
everything is Tailwind, per the house rule. The one thing Tailwind can't express — the 80mm
**paper size** (`@page`) — is passed as the hook's `pageStyle` string.

```tsx
import { useReactToPrint } from "react-to-print";

// 80mm thermal paper. `@page` can't be a Tailwind utility, so it lives here only.
const KOT_PAGE_STYLE = `@page { size: 80mm auto; margin: 3mm; }`;

function useKotPrint() {
  const ref = useRef<HTMLDivElement>(null);
  const [ticket, setTicket] = useState<KitchenTicketDto | null>(null);

  const doPrint = useReactToPrint({
    content: () => ref.current,
    pageStyle: KOT_PAGE_STYLE,
  });

  async function printKot(orderId: string) {
    const data = await api.get<KitchenTicketDto>(`/api/orders/${orderId}/kot`);
    setTicket(data);
    requestAnimationFrame(() => doPrint()); // let the ticket render, then print
  }

  return { ref, ticket, printKot };
}
```

All styling is Tailwind. The component is rendered **hidden on screen** (`hidden`) but
`react-to-print` still clones and prints it. Sized for a ~72mm printable width inside 80mm
paper, monospace, pure black for thermal legibility.

```tsx
// KitchenTicket.tsx — Tailwind-only, no stylesheet. Kept in the DOM but visually hidden.
interface KitchenTicketProps { ticket: KitchenTicketDto; }

export const KitchenTicket = forwardRef<HTMLDivElement, KitchenTicketProps>(
  ({ ticket }, ref) => (
    // `hidden` keeps it off-screen; react-to-print unhides its clone in the print doc.
    <div className="hidden">
      <div
        ref={ref}
        className="w-[72mm] font-mono text-black leading-tight"
      >
        <div className="text-center">
          <div className="text-sm font-bold tracking-widest">
            {ticket.fulfillmentDisplay}
          </div>
          <h1 className="text-2xl font-extrabold my-1">#{ticket.orderNumber}</h1>
          <div className="text-xs">{new Date(ticket.placedAt).toLocaleString()}</div>
          <div className="text-sm font-semibold">{ticket.customerName}</div>
        </div>

        <hr className="my-2 border-black border-dashed" />

        <ul className="space-y-1">
          {ticket.items.map((it, i) => (
            <li key={i} className="text-base">
              <span className="font-extrabold mr-1.5">{it.quantity}×</span>
              <span className="font-semibold">{it.itemName}</span>
              {it.specialInstructions && (
                <div className="pl-4 text-sm italic">↳ {it.specialInstructions}</div>
              )}
            </li>
          ))}
        </ul>

        {ticket.specialInstructions && (
          <>
            <hr className="my-2 border-black border-dashed" />
            <p className="text-sm italic font-semibold">** {ticket.specialInstructions} **</p>
          </>
        )}

        <hr className="my-2 border-black border-dashed" />
        <p className="font-extrabold">TOTAL ITEMS: {ticket.totalItems}</p>
      </div>
    </div>
  )
);
```

> If `w-[72mm]` / `mm` arbitrary values aren't already used in the project, that's the only
> spot to sanity-check with Tailwind's config — everything else is standard utilities.

### Auto-print hook

```ts
function useAutoPrintKot(enabled: boolean) {
  const { ref, ticket, printKot } = useKotPrint();
  const printed = useRef<Set<string>>(new Set()); // dedupe guard

  useEffect(() => {
    if (!enabled) return;
    const onNewOrder = (p: { orderId: string }) => {
      if (printed.current.has(p.orderId)) return;   // don't double-print
      printed.current.add(p.orderId);
      printKot(p.orderId).catch(console.error);
    };
    connection.on("NewOrderReceived", onNewOrder);
    return () => connection.off("NewOrderReceived", onNewOrder);
  }, [enabled]);

  return { ref, ticket }; // render <KitchenTicket ref={ref} ticket={ticket}/> hidden
}
```

## 5. Edge Cases & Error Handling

| Scenario | Expected Behavior |
|----------|-------------------|
| Browser blocks silent printing | First print of a session may show the OS print dialog — unavoidable in a browser. Document for the restaurant; a kiosk/Chrome "kiosk-printing" flag removes the dialog. |
| Same order fires `NewOrderReceived` twice (reconnect/replay) | Dedupe by `orderId` (see `printed` set). |
| SignalR drops then reconnects | `withAutomaticReconnect()` rejoins the group on reconnect; missed orders won't auto-print — the live orders list + manual "Print KOT" is the fallback. |
| KOT fetch 404 | Order not owned by this restaurant or deleted — toast an error, do not print. |
| Auto-print off | Only the manual button prints. Persist the toggle per device (`localStorage`). |

## 6. Testing Plan

- **Backend handler unit tests**: `GetKitchenTicketQueryHandler` — (a) returns ticket for owning restaurant, (b) returns NotFound for a different restaurant, (c) NotFound for missing order, (d) Admin can read any. Maps items + notes; contains no pricing fields.
- **Frontend**: `KitchenTicket` renders qty/name/notes and hides pricing; `useAutoPrintKot` dedupes on repeated `NewOrderReceived`; manual print calls the endpoint.
- **Manual**: real 80mm thermal printer smoke test (layout width, cut).

## 7. Rollout

- [ ] Per-device auto-print toggle (default OFF until validated on real hardware).
- [ ] Metric: KOTs printed / orders received.
- [ ] Rollback: hide the button + disable the SignalR listener; endpoint is read-only and safe to leave.

---

## Implementation Notes (updated during build)

### Files Created/Modified (backend — 2026-07-03)
- `Orders.Application/DTOs/KitchenTicketDto.cs` — new (`KitchenTicketDto` + `KitchenTicketItemDto`)
- `Orders.Application/Queries/GetKitchenTicket/GetKitchenTicketQuery.cs` — new
- `Orders.Application/Queries/GetKitchenTicket/GetKitchenTicketQueryHandler.cs` — new (auth: owning Restaurant or Admin)
- `Orders.Application/Mappings/OrderMappingExtensions.cs` — added `ToKitchenTicket()`
- `Orders.Endpoints/OrderEndpoints.cs` — added `GET /api/orders/{orderId}/kot`

### Decisions Made
- KOT reuses `IOrderRepository.GetByIdAsync` + a new mapping — no new persistence/read model needed.
- Auto-print rides the **existing** `NewOrderReceived` SignalR event (fires on payment) — zero backend change for auto-print.
- No pricing on the ticket by design.
- **80mm thermal** is the launch standard (no A4 fallback for v1).
- **Tailwind-only, no stylesheet** — house rule kept. `react-to-print` isolates the print
  DOM (no visibility hack); the only non-Tailwind bit is the `@page` size, passed via the
  hook's `pageStyle` string.

### Open Questions
- ~~Hub route path~~ → confirmed `/hubs/notifications`.
- ~~Thermal vs A4~~ → **80mm thermal**.
- Do we want a paper "cut" / multi-copy (one per station) later?

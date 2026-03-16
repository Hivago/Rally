# React / TypeScript / Tailwind Rules

> Always-follow rules for Restaurant Dashboard and Admin Panel.

## Project Setup

- Vite + React 18+ + TypeScript (strict mode)
- Tailwind CSS for all styling
- GSAP for animations
- React Query (TanStack Query) for server state
- SignalR client (@microsoft/signalr) for real-time
- React Router for navigation

## Component Rules

- Functional components only, no class components
- One component per file, filename matches component name
- Co-locate related files: `OrderCard/OrderCard.tsx`, `OrderCard/useOrderCard.ts`
- Extract logic into custom hooks when reused or complex
- Use `interface` for component props, not `type`

```typescript
// GOOD
interface OrderCardProps {
  order: Order;
  onAccept: (orderId: string) => void;
}

export function OrderCard({ order, onAccept }: OrderCardProps) {
  // ...
}
```

## TypeScript

- **No `any`** — use `unknown` and narrow, or define proper types
- All API response types defined in `types/api.ts`
- Discriminated unions for state: `type OrderState = { status: 'loading' } | { status: 'success'; data: Order } | { status: 'error'; error: string }`
- Use `as const` for constant arrays and enums
- Zod for runtime validation of API responses (optional but recommended)

## Tailwind

- Tailwind utilities only — no CSS modules, no styled-components, no inline `style={}`
- Use `cn()` helper (clsx + tailwind-merge) for conditional classes
- Design tokens via tailwind.config: colors, spacing, breakpoints
- Mobile-first: default styles for mobile, `md:` `lg:` for larger
- Dark mode via `dark:` variants (if applicable)

## GSAP Animations

- Always use `gsap.context()` for cleanup in `useEffect`
- Create a `useGSAP` custom hook if not using the official one
- Timeline for sequenced animations
- `ScrollTrigger` cleanup in the same context

```typescript
useEffect(() => {
  const ctx = gsap.context(() => {
    gsap.from('.order-card', { opacity: 0, y: 20, stagger: 0.1 });
  }, containerRef);
  
  return () => ctx.revert(); // ALWAYS clean up
}, []);
```

## API Layer

- All API calls go through a centralized `api` client (axios or fetch wrapper)
- Base URL from environment variable: `VITE_API_URL`
- JWT token attached via interceptor
- Token refresh handled automatically on 401
- React Query for all data fetching:

```typescript
// GOOD: React Query hook
export function useOrders(restaurantId: string) {
  return useQuery({
    queryKey: ['orders', restaurantId],
    queryFn: () => api.get<Order[]>(`/api/orders?restaurantId=${restaurantId}`),
    refetchInterval: false, // SignalR handles real-time updates
  });
}

// GOOD: Mutation
export function useAcceptOrder() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (orderId: string) => api.patch(`/api/orders/${orderId}/accept`),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['orders'] }),
  });
}
```

## SignalR Integration

- Single connection manager (singleton pattern)
- Reconnect automatically with exponential backoff
- Type-safe hub method names via shared contract
- Invalidate React Query cache on SignalR events

```typescript
// Connection setup
const connection = new HubConnectionBuilder()
  .withUrl(`${API_URL}/hubs/orders`, {
    accessTokenFactory: () => getAccessToken(),
  })
  .withAutomaticReconnect()
  .build();

// Listen for events and update React Query cache
connection.on('OrderStatusChanged', (data: OrderStatusEvent) => {
  queryClient.invalidateQueries({ queryKey: ['orders'] });
});
```

## State Management

- **Server state**: React Query (orders, menus, users)
- **Client state**: React Context for auth, useState for UI state
- **No Redux** unless complexity absolutely demands it
- URL state for filters, pagination, search (use search params)

## Error Handling

- Error boundaries at route level
- Toast notifications for API errors (non-blocking)
- Loading skeletons for data fetching (not spinners)
- Empty states with clear messaging and action buttons
- Optimistic updates for instant-feel interactions

## File Structure (Restaurant Dashboard)

```
restaurant-dashboard/
├── src/
│   ├── components/
│   │   ├── ui/               # Shared: Button, Input, Card, Modal, Toast
│   │   ├── orders/           # OrderCard, OrderList, OrderDetail
│   │   ├── menu/             # MenuEditor, MenuItem, CategoryList
│   │   └── layout/           # Sidebar, Header, PageContainer
│   ├── hooks/
│   │   ├── useOrders.ts      # Order-related React Query hooks
│   │   ├── useMenu.ts        # Menu management hooks
│   │   ├── useSignalR.ts     # SignalR connection hook
│   │   └── useAuth.ts        # Auth state hook
│   ├── pages/
│   │   ├── DashboardPage.tsx
│   │   ├── OrdersPage.tsx
│   │   ├── MenuPage.tsx
│   │   └── SettingsPage.tsx
│   ├── lib/
│   │   ├── api.ts            # API client
│   │   ├── signalr.ts        # SignalR connection manager
│   │   └── utils.ts          # cn(), formatCurrency(), etc.
│   ├── types/
│   │   ├── api.ts            # API response types
│   │   └── domain.ts         # Domain types (Order, MenuItem, etc.)
│   └── config/
│       └── constants.ts      # API URLs, feature flags
```

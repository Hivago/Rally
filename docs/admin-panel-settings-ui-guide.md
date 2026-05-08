# Admin Panel — Settings Page UI Implementation Guide

> Companion to commit `1120547` on `feat/pickup-orders` (backend endpoints).
> Stack per `CLAUDE.md`: React 18 + TypeScript + Tailwind + GSAP + React Query + SignalR.

## Backend endpoints already shipped

| Method | Route | Auth | Notes |
|---|---|---|---|
| `GET`  | `/api/admin/profile`           | Admin       | Read-only profile for signed-in admin |
| `PUT`  | `/api/admin/profile/password`  | Admin       | Change password (verifies current, confirms new) |
| `GET`  | `/api/admin/users`             | SuperAdmin  | Paginated admin list, optional `role`/`isActive` filters |
| `POST` | `/api/admin/users`             | SuperAdmin  | Create admin (Support or CityAdmin only) |

Roles in `AdminRole` enum: `Support`, `CityAdmin`, `SuperAdmin`. SuperAdmin cannot be created via the API — only the seed user.

---

## 1. API client types (`src/types/api.ts`)

```ts
export type AdminRole = 'Support' | 'CityAdmin' | 'SuperAdmin';

export interface AdminProfile {
  id: string;
  email: string;
  name: string;
  role: AdminRole;
  isActive: boolean;
}

export interface AdminListItem {
  id: string;
  email: string;
  name: string;
  role: AdminRole;
  isActive: boolean;
  createdAt: string;
}

export interface ListAdminsResponse {
  admins: AdminListItem[];
  totalCount: number;
  page: number;
  pageSize: number;
}

export interface ApiErrorResponse {
  code: string;
  message: string;
  fieldErrors?: Record<string, string[]>;
}
```

## 2. React Query hooks (`src/hooks/useAdminSettings.ts`)

```ts
export function useAdminProfile() {
  return useQuery({
    queryKey: ['admin', 'profile'],
    queryFn: () => api.get<AdminProfile>('/api/admin/profile'),
  });
}

export function useChangePassword() {
  return useMutation({
    mutationFn: (body: {
      currentPassword: string;
      newPassword: string;
      confirmNewPassword: string;
    }) => api.put('/api/admin/profile/password', body),
  });
}

export function useAdminUsers(filters: { role?: AdminRole; isActive?: boolean; page: number }) {
  return useQuery({
    queryKey: ['admin', 'users', filters],
    queryFn: () => api.get<ListAdminsResponse>('/api/admin/users', { params: filters }),
  });
}

export function useCreateAdmin() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (body: { name: string; email: string; password: string; role: AdminRole }) =>
      api.post('/api/admin/users', body),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['admin', 'users'] }),
  });
}
```

## 3. Page layout (`pages/SettingsPage.tsx`)

Three tabs. Use URL search params so refresh/share preserves the active tab:

```
/settings?tab=profile   (default)
/settings?tab=security
/settings?tab=users     (only render if profile.role === 'SuperAdmin')
```

```tsx
const tabs = [
  { id: 'profile', label: 'Profile' },
  { id: 'security', label: 'Security' },
  ...(profile?.role === 'SuperAdmin' ? [{ id: 'users', label: 'Users' }] : []),
];
```

## 4. Tab 1 — Profile (read-only)

| Field | Source | Notes |
|---|---|---|
| Name | `profile.name` | disabled input |
| Email | `profile.email` | disabled input |
| Role | `profile.role` | badge: `Support` / `CityAdmin` / `SuperAdmin` |
| Status | `profile.isActive` | green dot if active |

Add a small note: *"Profile editing coming soon. Contact a SuperAdmin to update your details."*

Phone is **not in the API yet** — don't add a phone input. When the backend exposes it, the field appears automatically.

## 5. Tab 2 — Security (change password)

Form state:

```ts
{ currentPassword: '', newPassword: '', confirmNewPassword: '' }
```

**Client-side validation** (mirror the FluentValidation rules so the user gets instant feedback):

- All three required
- `newPassword.length >= 8`
- `newPassword !== currentPassword`
- `confirmNewPassword === newPassword`

Submit → `useChangePassword().mutate(...)`. Map server errors:

| `error.code` contains | UX |
|---|---|
| `Validation` | toast `error.message`, attach `fieldErrors` to form fields |
| `NotFound` | toast "Session expired, sign in again" + redirect to login |

On success: clear form, toast "Password updated". Token remains valid — optionally force re-login for safety.

## 6. Tab 3 — Users (SuperAdmin only)

Two parts in one tab: **list** on top, **create form** below (or right side on desktop).

### List (top)

- Filter row: Role dropdown (`All | Support | CityAdmin`), Status (`All | Active | Inactive`), Page size
- Table columns: Name · Email · Role · Status · Created · *(Actions later — deactivate/reset)*
- Pagination: prev/next using `totalCount`, `page`, `pageSize` from response

### Create form (bottom)

| Field | Type | Validation |
|---|---|---|
| Name | text | required, max 100 |
| Email | email | required, valid email |
| Password | password | required, min 8 — offer "generate" button using `crypto.getRandomValues` |
| Role | select | `Support` or `CityAdmin` only — **don't include SuperAdmin** (backend rejects it) |

Submit → `useCreateAdmin().mutate(...)`. Server error mapping:

| `error.code` | UX |
|---|---|
| `Conflict` (email exists) | inline error under Email field |
| `Forbidden` (somehow not SuperAdmin) | toast + hide tab |
| `Validation` | inline `fieldErrors` |

On success: toast "Admin created", reset form, list auto-refreshes (invalidated in the hook).

## 7. Route guard

Hide the Users tab entirely for non-SuperAdmins:

```tsx
{profile?.role === 'SuperAdmin' && <UsersTab />}
```

Backend enforces this regardless (handler returns 403), but hiding the tab keeps the UX clean.

## 8. GSAP polish (per `react-rules.md`)

Use `gsap.context()` for cleanup. Stagger the user-list rows on first render:

```tsx
useEffect(() => {
  const ctx = gsap.context(() => {
    gsap.from('.admin-row', { opacity: 0, y: 12, stagger: 0.04, duration: 0.3 });
  }, listRef);
  return () => ctx.revert();
}, [admins]);
```

## 9. File structure to create

```
admin-panel/src/
├── pages/
│   └── SettingsPage.tsx
├── components/
│   └── settings/
│       ├── ProfileTab.tsx
│       ├── SecurityTab.tsx
│       ├── UsersTab.tsx
│       ├── AdminTable.tsx
│       └── CreateAdminForm.tsx
├── hooks/
│   └── useAdminSettings.ts   ← all 4 hooks above
└── types/
    └── api.ts                ← types above
```

## 10. Testing checklist

- [ ] Sign in as **Support** → Settings shows Profile + Security only
- [ ] Sign in as **CityAdmin** → same as above
- [ ] Sign in as **SuperAdmin** → all three tabs visible
- [ ] Change password with wrong current → 400 "Current password is incorrect"
- [ ] Change password where new ≠ confirm → blocked client-side
- [ ] Create admin with duplicate email → 409 Conflict shown inline
- [ ] POST `role: "SuperAdmin"` directly via Postman as SuperAdmin → expect 403 (backend guard)
- [ ] Pagination: create 25 admins → page 1 shows 20, page 2 shows 5

---

## Future flow (out of scope today)

When the edit-profile feature comes back on the roadmap, the backend will need:

- Add `PhoneNumber? Phone` to `Admin` entity + migration
- New `UpdateAdminProfileCommand` (name, phone — **not email**, since email is auth identity)
- New endpoint `PUT /api/admin/profile`
- Update `AdminProfileResponse` to include `phone`

Frontend then: enable the disabled inputs in the Profile tab, add a Save button, wire to `useUpdateProfile`.

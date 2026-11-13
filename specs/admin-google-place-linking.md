# Feature Spec: Admin Google Place Linking

> **Status**: Ready for Review
> **Priority**: P2 (Medium)
> **Estimated Effort**: 0.5 day (1 screen, 2 existing endpoints)
> **Module(s)**: Users (Admins) / Catalog
> **Owner**: UI developer (Admin Panel)
> **Date**: 2026-09-07

---

## 1. Problem Statement

Restaurants now support showing their real Google rating and reviews to customers (see `specs/` — GetRestaurantReviews endpoint, backend already shipped), but nothing links a restaurant to its Google Place ID today. Every restaurant onboarded needs an admin to search Google Places for it and save the match — there is currently no UI for this, only raw API calls.

## 2. User Stories

- As an **admin**, I want to search Google Places by restaurant name/address and pick the correct match, so that I don't have to hand-type a Place ID I can't otherwise find.
- As an **admin**, I want to see whether a restaurant already has a Google Place linked, so I know which restaurants still need this done.

## 3. Acceptance Criteria

- [ ] On a restaurant's admin edit screen, there's a "Google Listing" section showing the currently linked place (if any) or "Not linked".
- [ ] A search box lets the admin type a query (name + area) and see up to 5 matching suggestions.
- [ ] Selecting a suggestion saves its Place ID to the restaurant immediately (or stages it for the existing Save button — either is fine).
- [ ] Edge case: query under 2 characters — don't call the API, just disable the search.
- [ ] Edge case: no suggestions returned — show "No matches, try a more specific search."
- [ ] Error case: search or save call fails — show a toast, don't lose the admin's typed query.

## 4. Technical Design

### API Endpoints (already live on `feat/google-reviews-integration`, backend done)

| Method | Endpoint | Auth | Description |
|--------|----------|------|--------------|
| GET | `/api/admins/places/search?query={text}` | Admin | Returns up to 5 Google Place matches |
| PUT | `/api/admins/restaurants/{restaurantId}` | Admin | Partial update — send only `{ "googlePlaceId": "..." }` to link, other fields untouched if omitted |
| GET | `/api/catalog/restaurants/{restaurantId}/reviews` | Public | Already reads whatever Place ID is linked — no frontend change needed here, just confirms the link worked |

**Search response shape:**
```typescript
interface PlaceSuggestion {
  placeId: string;
  description: string;   // full formatted address, e.g. "Kalp - Elevated Cuisine and Cocktails, Mugalsan Road, Airoli, Navi Mumbai, Maharashtra, India"
  mainText: string;       // e.g. "Kalp - Elevated Cuisine and Cocktails"
  secondaryText: string;  // e.g. "Mugalsan Road, Airoli, Navi Mumbai, Maharashtra, India"
}
interface SearchPlacesResponse {
  suggestions: PlaceSuggestion[];
}
```

**Save request:**
```json
PUT /api/admins/restaurants/{restaurantId}
{ "googlePlaceId": "ChIJ2Xqc2Hu55zsRmcJTLxvSJLE" }
```
Returns `204 No Content` on success. This is a partial-update endpoint — every field is optional, so this call only touches `googlePlaceId`.

**Reading the currently-linked place:** the existing "get restaurant details" admin query already returns `googlePlaceId` on the restaurant object (confirm field name on your existing admin restaurant-detail response — it's exposed via `RestaurantDetails.GooglePlaceId` on the backend). If your current admin restaurant-detail screen doesn't yet surface it, that's a one-line addition on your side to display it.

### Frontend Components

| Component | Page | Description |
|-----------|------|--------------|
| `GooglePlaceLinker` | Restaurant edit/detail page (Admin Panel) | Shows current link status + search-and-pick UI |

Suggested shape (React Query, matching existing conventions in `.claude/rules/react-rules.md`):

```typescript
// hooks/useGooglePlaceSearch.ts
export function useGooglePlaceSearch(query: string) {
  return useQuery({
    queryKey: ['google-places-search', query],
    queryFn: () => api.get<SearchPlacesResponse>(`/api/admins/places/search?query=${encodeURIComponent(query)}`),
    enabled: query.trim().length >= 2,
  });
}

// hooks/useLinkGooglePlace.ts
export function useLinkGooglePlace(restaurantId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (placeId: string) =>
      api.put(`/api/admins/restaurants/${restaurantId}`, { googlePlaceId: placeId }),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['restaurant', restaurantId] }),
  });
}
```

Debounce the search input (~400ms) before calling `useGooglePlaceSearch` — this hits the Google Places API server-side and isn't free per call.

## 5. Edge Cases & Error Handling

| Scenario | Expected Behavior |
|----------|--------------------|
| Query < 2 chars | Don't call API; disable/hide suggestions list |
| No suggestions found | Show "No matches, try a more specific search." |
| Search API fails (5xx) | Toast error, keep the admin's typed text in the box |
| Save (PUT) fails | Toast error, keep the selected suggestion highlighted so admin can retry |
| Restaurant already linked | Show current linked name/address with a "Change" action that re-opens the search |

## 6. Testing Plan

- **Frontend tests**: `GooglePlaceLinker` renders empty state, renders suggestions list, calls mutation on select, shows error toast on failure (RTL + MSW mocking both endpoints).

## 7. Rollout

- No feature flag needed — additive, admin-only screen.
- Metrics: not tracked; low-volume admin action.

---

## Implementation Notes (updated during build)

### Files Created/Modified
-

### Decisions Made
-

### Open Questions
-

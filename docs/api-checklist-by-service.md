# Luvora API checklist mapped to services

This is the service-mapped version of the supplied admin and storefront API checklist. Paths below distinguish routes actually implemented in this repository from proposed checklist paths. `Implemented` means a controller route exists; it does not imply that every suggested workflow or external provider is configured.

## Admin APIs

| Checklist area | Owning service | Implemented routes / status |
|---|---|---|
| Authentication and admin account | AuthService | Implemented: `POST /api/admin/auth/login`, `GET /api/admin/auth/me`, `PATCH /api/admin/auth/me`, `PUT /api/admin/auth/me/password`. Missing: admin logout/refresh, reset delivery, MFA, and session management. |
| Dashboard | ReportingService | All eight paths exist under `/api/admin/dashboard`: `summary`, `sales-chart`, `orders-chart`, `recent-orders`, `top-products`, `low-stock`, `pending-tasks`, `revenue-summary`. Aggregates call service APIs; current service data/page limits and some coarse dashboard results mean these need refinement for production metrics. |
| Products | ProductService | Implemented: `GET/POST /api/admin/products`, `PATCH /{productId}/status`; gateway aliases also expose existing product detail/update/delete and `POST /api/admin/products/images`. Missing: variants, related products, bulk/import/export, per-product image reorder/removal. |
| Categories and brands | ProductService | Categories: list/create/read/update/delete/status are implemented under `/api/admin/categories`. Missing: hierarchy, image upload, reorder. Brands are not modeled as a separate entity or API; product brand remains text. |
| Inventory and warehouses | InventoryService | Implemented: summary, stock list/read, adjustments, adjustment list, movement history, low/out-of-stock. Adjustments store before/after, reason, and actor ID; direct quantity PUT is disabled. Missing: variant stock, warehouses, transfers, reservation release, inventory report/export. |
| Orders and returns | OrderService | Implemented: list, detail, items, customer/admin cancellation with audit fields, validated status update, customer return requests for delivered order items, and admin approve/reject review under `/api/admin/orders`. Missing: timeline, notes, fulfillment workflow, and refund processing. |
| Customers | AuthService | Implemented: customer list/detail and suspend/reactivate under `/api/admin/customers`. Missing: permitted profile edits, order/address/activity views, internal notes, reset trigger and authorized export. Sensitive password data is excluded. |
| Payments, transactions, refunds | PaymentService | Implemented: summary, transaction list/detail, order transactions. Missing: retry, refund lifecycle/idempotency, reconciliation and payment reports/exports. |
| Delivery | DeliveryService | Implemented: summary, shipment list/detail and status update using the current delivery model. Existing shipment creation is reached through gateway mapping to `POST /api/deliveries`. Missing: assignment/personnel, tracking events, zones, rates, failed-delivery and reports. |
| Promotions, coupons, campaigns, banners | PromotionService | CRUD/status for promotions and coupons, coupon usage view, campaign list/create/update/status, banner list/create/update/delete/reorder. Banner image route stores a URL, not an uploaded file. Coupon usage is not connected to checkout redemption yet. |
| Reviews and moderation | ReviewService | Implemented: list/detail/summary, approve/hide/reject, report list/resolution, and official reply. |
| Support and complaints | SupportService | Implemented: ticket list/create/read/update/status/assign/messages and complaint list/read/create/status. Internal messages have a separate visibility flag. Customer-facing ticket ownership/integration is not wired. |
| Staff, roles, permissions | AuthService | Implemented: staff list/create/read/status and read/assign existing role. Missing: staff profile editing/reset, custom role CRUD and granular permission catalog/assignment. |
| Reports and exports | ReportingService | Sales, revenue, orders, products, inventory, customers, payments, refunds, delivery and coupons report routes exist. Refund report is not implemented against data. Export request/list/status/download is asynchronous with temporary access-controlled download. |
| Store settings | ConfigurationService | General/currency/tax/shipping/payments/notifications/homepage GET/PUT and email-template list/read/update exist. Payment secrets are rejected/omitted. Test email returns 501 until mail delivery is configured. |
| Admin notifications | NotificationService | Implemented: list, unread count, read one, read all. Admin preferences are missing. |
| Audit and security | AuditService | Implemented: search/detail/security events/staff activity/export history; records are append-only through internal ingestion. Gateway records admin requests. |
| System monitoring | AuditService | Implemented: health, service health, integrations/status test, safe storage summary. Queue and failed-job routes exist but return 501 because no centralized queue/job store is configured. |

### Admin route access

ApiGateway routes `/api/admin/...` to the owning service and enforces the `Admin` role, with `POST /api/admin/auth/login` as the anonymous exception. AuthService login only accepts active admin accounts. Configure matching `JWT_KEY` values in AuthService and ApiGateway; Compose supports optional first-start admin bootstrap through `BOOTSTRAP_ADMIN_EMAIL` and `BOOTSTRAP_ADMIN_PASSWORD`.

## Customer and storefront APIs

| Checklist area | Owning service | Repository mapping / status |
|---|---|---|
| Registration, login, refresh, logout, verification, reset | AuthService | Registration can send a verification link; `POST /api/auth/verify-email` consumes a hashed, single-use 24-hour token, and authenticated resend is available. Configure `SMTP_HOST`, `SMTP_PORT`, `SMTP_USERNAME`, `SMTP_PASSWORD`, `SMTP_FROM`, `SMTP_USE_SSL`, and `EMAIL_VERIFICATION_URL` for delivery. Password reset delivery/consumption remains unconfigured. MFA and social sign-in are not present. |
| Customer profile, preferences, privacy | AuthService | `/api/account/profile` and `/api/users/me` support profile read/update; preference GET/PUT also has `/api/users/me/preferences` and notification preference aliases. Privacy summary and account deactivation after password confirmation exist. Avatar and personal-data export remain missing. |
| Customer addresses | AuthService | List/create/update/delete and set-default exist under `/api/account/addresses` and `/api/users/me/addresses`. Address ownership is checked against the token. |
| Product browsing and search | ProductService | Public product list/detail-by-active-id/slug, search filter and image routes live under `/api/products`; `GET /api/products/{productId}/availability` returns safe availability from InventoryService. Search suggestions, featured/new/best-seller/related/variants/price APIs are not all represented. Reviews use ReviewService routes. |
| Categories and brands | ProductService | Public active category list/detail/category-products routes exist under `/api/categories`. Hierarchy and public brand APIs are missing. |
| Shopping cart | CartService | Authenticated current-user routes: `GET/DELETE /api/cart`, `POST /api/cart/items`, `PUT/PATCH /api/cart/items/{itemId}`, `DELETE /api/cart/items/{itemId}`, clear cart and `POST /api/cart/validate`. Legacy user-ID paths enforce ownership. Prices refresh from ProductService; request prices are ignored. Guest-cart merge remains missing. |
| Wishlist | AuthService | Persistent list/add/remove/clear/check routes exist under `/api/users/me/wishlist`; identity comes from the token. |
| Public coupons/promotions | PromotionService | Public active promotions/banners and authenticated `POST /api/promotions/coupons/validate` exist. Coupon quote uses the authenticated user's cart. Checkout redemption and usage-count updates are not integrated. |
| Checkout | OrderService | Order creation now derives product prices from ProductService and user identity from the token. Persistent checkout sessions, address/shipping selection, coupon application, final revalidation and idempotent placement remain missing. |
| Customer orders | OrderService | Create, detail, list, items and cancel require authentication and enforce ownership; cancellation reasons and actor/time are persisted. Return requests are available for delivered items, with admin approve/reject review. Item prices come from ProductService, not the request. Timeline, fulfillment workflow, refunds, invoice and reorder paths remain incomplete. |
| Customer payment | PaymentService | Create/read/order lookup require authentication and enforce ownership. Amount is loaded from the authenticated customer's OrderService record. The mock processor no longer reports completed; a real provider intent/callback is still missing, so payments remain pending. |
| Delivery tracking | DeliveryService | Shipment detail and tracking routes under `/api/users/me/orders/{orderId}` require authentication and enforce ownership; delivery creation verifies the order owner and requires CONFIRMED or PROCESSING status. Status updates are staff/admin only. Full provider tracking history and delivery options remain missing. |
| Reviews and ratings | ReviewService | Public approved-review reads exist. Create/delete check the authenticated owner; create requires a delivered order containing the reviewed product. Edit, list-my-reviews and report routes exist. New/edited reviews require moderation. Report history for customers remains missing. |
| Customer support | SupportService | Customer ticket create/list/read/messages/close, complaint create/list/read and categories exist; identity is token-derived and internal notes are excluded. Attachments remain missing. |
| Customer notifications | NotificationService | Current-user inbox, unread count, read/read-all and delete aliases exist under `/api/users/me/notifications` with ownership checks. Preferences use AuthService; marketing and transactional preference categories are not separate. |
| Storefront content/search | ConfigurationService / PromotionService / ProductService | Public homepage config and active banner/promotion routes exist. Search suggestions/trending and navigation remain missing. |
| Loyalty/referrals | Not present | Optional; no service/model/routes. |

## .NET service boundaries

| Service | Owns |
|---|---|
| AuthService | Authentication, admin/staff accounts, customer identity/profile, addresses and preferences |
| ProductService | Products, categories and catalog images; brands and variants are future catalog models |
| InventoryService | Stock, adjustments, movement ledger; warehouse/transfer workflows remain future work |
| CartService | User cart and cart items |
| OrderService | Orders, cancellation and future fulfillment/returns |
| PaymentService | Payment records and future provider/refund/reconciliation workflows |
| DeliveryService | Delivery/shipment records and future tracking/personnel/rates |
| PromotionService | Promotions, coupons, campaigns and banners |
| ReviewService | Product reviews and moderation |
| SupportService | Support tickets, messages and complaints |
| NotificationService | User notifications and future admin preferences/templates |
| ReportingService | Cross-service reports, dashboard aggregation and asynchronous exports; calls service APIs instead of reading their databases |
| ConfigurationService | Store configuration and email-template records |
| AuditService | Append-only admin audit/security records and safe service monitoring |

PromotionService, SupportService, ReportingService, ConfigurationService and AuditService each have a separate project, PostgreSQL database and EF migration, and are included in Docker Compose and ApiGateway routing. The current implementation is a mapped starting point: routes listed as missing should be added to the owning service when its domain model/integration is ready, rather than adding them to a single Admin API service.

## Customer API security and integration notes

Cart, Order, Payment, Delivery, Notification, Support and Inventory validate JWTs in the owning service as well as at the gateway. Ownership-sensitive operations derive identity from token claims. Cart/order prices are sourced from ProductService, and PaymentService derives the amount from the authenticated customer's order. Payment provider integration remains absent; payments stay pending. Checkout, coupon redemption, refunds and delivery tracking are incomplete until their full state/provider integrations exist.
